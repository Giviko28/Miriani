using Domain.Enums;

namespace Application.Chat;

public record ChatSessionSummary(Guid Id, string Title, DateTime UpdatedAt);

public record ChatMessageDto(
    Guid Id, string Sender, string Content, string? Route, bool UsedContext,
    string? Sources, string? Structured, DateTime CreatedAt);

public record ChatThread(Guid Id, string Title, IReadOnlyList<ChatMessageDto> Messages);

public record SendMessageRequest(
    Guid? SessionId, string Query, string? AttachmentText = null, string? AttachmentName = null);

public record SendMessageResult(
    Guid SessionId, string Title, string Answer, string Route, bool UsedContext,
    string? Sources, string? Structured);

/// <summary>
/// Manages a user's saved conversations with the AI assistant. All operations are scoped to
/// the caller's org and user id; users only ever see their own sessions.
/// </summary>
public interface IChatService
{
    Task<IReadOnlyList<ChatSessionSummary>> ListSessionsAsync(Guid orgId, Guid userId, CancellationToken ct = default);
    Task<ChatThread?> GetThreadAsync(Guid orgId, Guid userId, Guid sessionId, CancellationToken ct = default);
    Task<SendMessageResult> SendAsync(Guid orgId, Guid userId, UserRole role, SendMessageRequest req, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid orgId, Guid userId, Guid sessionId, CancellationToken ct = default);
}
