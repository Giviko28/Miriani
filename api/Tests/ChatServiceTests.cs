using Application.Ai;
using Application.Chat;
using Domain.Enums;
using Infrastructure.Chat;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Tests;

public class ChatServiceTests
{
    /// <summary>Stub AI service: returns a fixed answer and records the history it received.</summary>
    private sealed class StubAi : IAiService
    {
        public IReadOnlyList<AiTurn>? LastHistory;

        public Task<int> IngestAsync(Guid orgId, Guid docId, string fileName, UserRole accessRole, byte[] data, CancellationToken ct = default)
            => Task.FromResult(0);

        public Task<AiAnswer> QueryAsync(Guid orgId, UserRole role, string query, CancellationToken ct = default)
            => Task.FromResult(new AiAnswer("a", true, []));

        public Task<AiAgentAnswer> RunAgentAsync(Guid orgId, UserRole role, string query, IReadOnlyList<AiTurn>? history = null, string? attachmentText = null, string? attachmentName = null, CancellationToken ct = default)
        {
            LastHistory = history;
            return Task.FromResult(new AiAgentAnswer("policy_qa", "Grounded reply.", true, [], null));
        }

        public Task<AiExtractResult> ExtractAttachmentAsync(string fileName, byte[] data, CancellationToken ct = default)
            => Task.FromResult(new AiExtractResult(fileName, "", 0, false));

        public Task<string> ConnectDbAsync(Guid orgId, string connectionString, CancellationToken ct = default)
            => Task.FromResult("{}");

        public Task<string> ExploreDbAsync(Guid orgId, CancellationToken ct = default)
            => Task.FromResult("");

        public Task DisconnectDbAsync(Guid orgId, CancellationToken ct = default) => Task.CompletedTask;

        public Task DeleteDocumentAsync(Guid docId, CancellationToken ct = default) => Task.CompletedTask;

        public Task<IReadOnlyList<string>> ReconcileDocumentsAsync(
            Guid orgId, IReadOnlyCollection<Guid> validDocIds, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<string>>([]);
    }

    private static (ChatService svc, StubAi ai) NewService(Infrastructure.Persistence.AppDbContext db)
    {
        var ai = new StubAi();
        return (new ChatService(db, ai), ai);
    }

    [Fact]
    public async Task Send_with_no_session_creates_session_and_two_messages()
    {
        using var db = TestSupport.NewDb();
        var (svc, _) = NewService(db);
        var orgId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var result = await svc.SendAsync(orgId, userId, UserRole.Employee,
            new SendMessageRequest(null, "What is the remote work policy?"));

        Assert.Equal("What is the remote work policy?", result.Title);
        Assert.Single(db.ChatSessions);
        Assert.Equal(2, db.ChatMessages.Count());
        Assert.Equal("policy_qa", result.Route);
    }

    [Fact]
    public async Task Second_send_passes_prior_turns_as_history()
    {
        using var db = TestSupport.NewDb();
        var (svc, ai) = NewService(db);
        var orgId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var first = await svc.SendAsync(orgId, userId, UserRole.Employee,
            new SendMessageRequest(null, "What is the remote work policy?"));
        await svc.SendAsync(orgId, userId, UserRole.Employee,
            new SendMessageRequest(first.SessionId, "What about for managers?"));

        Assert.NotNull(ai.LastHistory);
        Assert.Equal(2, ai.LastHistory!.Count); // the first user + assistant turns
        Assert.Equal("user", ai.LastHistory[0].Sender);
        Assert.Equal("What is the remote work policy?", ai.LastHistory[0].Content);
        Assert.Equal("assistant", ai.LastHistory[1].Sender);
    }

    [Fact]
    public async Task GetThread_returns_null_for_another_users_session()
    {
        using var db = TestSupport.NewDb();
        var (svc, _) = NewService(db);
        var orgId = Guid.NewGuid();
        var owner = Guid.NewGuid();
        var other = Guid.NewGuid();

        var created = await svc.SendAsync(orgId, owner, UserRole.Employee,
            new SendMessageRequest(null, "hello"));

        Assert.NotNull(await svc.GetThreadAsync(orgId, owner, created.SessionId));
        Assert.Null(await svc.GetThreadAsync(orgId, other, created.SessionId));
    }

    [Fact]
    public async Task Delete_removes_session_and_messages_only_for_owner()
    {
        using var db = TestSupport.NewDb();
        var (svc, _) = NewService(db);
        var orgId = Guid.NewGuid();
        var owner = Guid.NewGuid();
        var other = Guid.NewGuid();
        var created = await svc.SendAsync(orgId, owner, UserRole.Employee,
            new SendMessageRequest(null, "hello"));

        Assert.False(await svc.DeleteAsync(orgId, other, created.SessionId)); // not the owner
        Assert.True(await svc.DeleteAsync(orgId, owner, created.SessionId));
        Assert.Empty(db.ChatSessions);
        Assert.Empty(db.ChatMessages);
    }
}
