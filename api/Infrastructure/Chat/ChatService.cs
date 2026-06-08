using System.Text.Json;
using Application.Ai;
using Application.Chat;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Chat;

/// <summary>
/// Persists conversations in MS SQL and orchestrates each turn: store the user message, send
/// the recent history to the AI service for memory, then store the assistant reply.
/// </summary>
public class ChatService(AppDbContext db, IAiService ai) : IChatService
{
    private const int HistoryWindow = 10; // recent messages sent to the AI as context

    public async Task<IReadOnlyList<ChatSessionSummary>> ListSessionsAsync(
        Guid orgId, Guid userId, CancellationToken ct = default)
    {
        return await db.ChatSessions
            .Where(s => s.OrgId == orgId && s.UserId == userId)
            .OrderByDescending(s => s.UpdatedAt)
            .Select(s => new ChatSessionSummary(s.Id, s.Title, s.UpdatedAt))
            .ToListAsync(ct);
    }

    public async Task<ChatThread?> GetThreadAsync(
        Guid orgId, Guid userId, Guid sessionId, CancellationToken ct = default)
    {
        var session = await db.ChatSessions
            .FirstOrDefaultAsync(s => s.Id == sessionId && s.OrgId == orgId && s.UserId == userId, ct);
        if (session is null) return null;

        var messages = await db.ChatMessages
            .Where(m => m.SessionId == sessionId)
            .OrderBy(m => m.CreatedAt)
            .Select(m => new ChatMessageDto(
                m.Id, m.Sender, m.Content, m.Route, m.UsedContext, m.Sources, m.Structured, m.CreatedAt))
            .ToListAsync(ct);

        return new ChatThread(session.Id, session.Title, messages);
    }

    public async Task<SendMessageResult> SendAsync(
        Guid orgId, Guid userId, UserRole role, string displayName, SendMessageRequest req, CancellationToken ct = default)
    {
        var query = req.Query.Trim();

        var session = req.SessionId is { } sid
            ? await db.ChatSessions.FirstOrDefaultAsync(
                  s => s.Id == sid && s.OrgId == orgId && s.UserId == userId, ct)
              ?? throw new KeyNotFoundException("Chat session not found.")
            : null;

        if (session is null)
        {
            session = new ChatSession
            {
                OrgId = orgId,
                UserId = userId,
                Title = Truncate(query, 60),
            };
            db.ChatSessions.Add(session);
        }

        // History is the prior turns, captured before we add the new user message.
        var history = await db.ChatMessages
            .Where(m => m.SessionId == session.Id)
            .OrderByDescending(m => m.CreatedAt)
            .Take(HistoryWindow)
            .ToListAsync(ct);
        history.Reverse();
        var turns = history.Select(m => new AiTurn(m.Sender, m.Content)).ToList();

        db.ChatMessages.Add(new ChatMessage
        {
            SessionId = session.Id,
            Sender = "user",
            Content = query,
        });

        var answer = await ai.RunAgentAsync(
            orgId, role, query, turns,
            attachmentText: req.AttachmentText, attachmentName: req.AttachmentName,
            userName: displayName, ct: ct);

        var sourcesJson = answer.Sources.Count > 0 ? JsonSerializer.Serialize(answer.Sources) : null;
        var structuredJson = answer.Structured is not null ? JsonSerializer.Serialize(answer.Structured) : null;

        db.ChatMessages.Add(new ChatMessage
        {
            SessionId = session.Id,
            Sender = "assistant",
            Content = answer.Answer,
            Route = answer.Route,
            UsedContext = answer.UsedContext,
            Sources = sourcesJson,
            Structured = structuredJson,
        });

        session.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        return new SendMessageResult(
            session.Id, session.Title, answer.Answer, answer.Route, answer.UsedContext,
            sourcesJson, structuredJson);
    }

    public async Task<bool> DeleteAsync(Guid orgId, Guid userId, Guid sessionId, CancellationToken ct = default)
    {
        var session = await db.ChatSessions
            .FirstOrDefaultAsync(s => s.Id == sessionId && s.OrgId == orgId && s.UserId == userId, ct);
        if (session is null) return false;

        db.ChatSessions.Remove(session); // messages cascade-delete
        await db.SaveChangesAsync(ct);
        return true;
    }

    private static string Truncate(string s, int n)
    {
        s = s.Trim();
        if (string.IsNullOrEmpty(s)) return "New chat";
        return s.Length <= n ? s : s[..n].TrimEnd() + "…";
    }
}
