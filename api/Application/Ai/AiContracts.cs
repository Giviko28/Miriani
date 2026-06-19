using Domain.Enums;

namespace Application.Ai;

public record AiSource(string DocId, string FileName, int ChunkIndex, double Distance, string Text);

/// <summary>One prior conversation turn supplied as context for a follow-up request.</summary>
public record AiTurn(string Sender, string Content);

/// <summary>Result of extracting text from a temporary chat attachment.</summary>
public record AiExtractResult(string FileName, string Text, int Chars, bool Truncated);

public record AiAnswer(string Answer, bool UsedContext, IReadOnlyList<AiSource> Sources);

/// <summary>Result of the agent graph: which agent handled it, plus optional structured output.</summary>
public record AiAgentAnswer(
    string Route,
    string Answer,
    bool UsedContext,
    IReadOnlyList<AiSource> Sources,
    IReadOnlyDictionary<string, object>? Structured);

/// <summary>One comment on a ticket, passed to the AI as drafting context.</summary>
public record AiJiraComment(string Author, string Body);

/// <summary>An existing Jira ticket's content, passed to the AI to draft an action over it.</summary>
public record AiJiraTicket(
    string Key, string Summary, string Status, string IssueType,
    string? Priority, string Description, IReadOnlyList<AiJiraComment> Comments);

/// <summary>
/// Gateway to the Python AI service. The .NET layer always supplies org and role from the
/// authenticated caller — the AI service never decides access on its own.
/// </summary>
public interface IAiService
{
    /// <summary>Index a document's bytes into the vector store under the given access role.</summary>
    Task<int> IngestAsync(Guid orgId, Guid docId, string fileName, UserRole accessRole, byte[] data, CancellationToken ct = default);

    /// <summary>Retrieve role-scoped context and generate a grounded answer.</summary>
    Task<AiAnswer> QueryAsync(Guid orgId, UserRole role, string query, CancellationToken ct = default);

    /// <summary>
    /// Route the request through the agent graph to a specialized agent. Optional prior turns
    /// give the agent conversational memory for follow-up questions. An optional ephemeral
    /// attachment (extracted text for this message only) is passed as one-shot context and is
    /// never stored or embedded.
    /// </summary>
    Task<AiAgentAnswer> RunAgentAsync(
        Guid orgId, UserRole role, string query,
        IReadOnlyList<AiTurn>? history = null,
        string? attachmentText = null, string? attachmentName = null,
        string? userName = null,
        CancellationToken ct = default);

    /// <summary>
    /// Extract plain text from an uploaded file WITHOUT storing it (for temporary chat
    /// attachments). Returns the extracted text; nothing is embedded or persisted.
    /// </summary>
    Task<AiExtractResult> ExtractAttachmentAsync(string fileName, byte[] data, CancellationToken ct = default);

    /// <summary>
    /// Draft an AI action (Slack alert / manager email / report) over an existing Jira ticket,
    /// grounded in the ticket text plus role-scoped company knowledge. The draft is reviewed by the
    /// human before the gateway performs the real side-effect; the ticket is never stored/embedded.
    /// </summary>
    Task<IReadOnlyDictionary<string, object>?> DraftJiraActionAsync(
        Guid orgId, UserRole role, string action, AiJiraTicket ticket,
        string? managerName = null, CancellationToken ct = default);

    /// <summary>Connect the org's external DB: introspect schema and cache it in the AI service.</summary>
    Task<string> ConnectDbAsync(Guid orgId, string connectionString, CancellationToken ct = default);

    /// <summary>Sample every table and save a natural-language description as permanent context.</summary>
    Task<string> ExploreDbAsync(Guid orgId, CancellationToken ct = default);

    /// <summary>Remove the org's cached DB connection from the AI service.</summary>
    Task DisconnectDbAsync(Guid orgId, CancellationToken ct = default);

    /// <summary>Delete all vector chunks belonging to a document from the AI service.</summary>
    Task DeleteDocumentAsync(Guid docId, CancellationToken ct = default);

    /// <summary>
    /// Purge orphaned vectors: tell the AI service the authoritative set of document ids for an
    /// org so it can delete any chunks left behind by documents no longer in the system of record.
    /// </summary>
    Task<IReadOnlyList<string>> ReconcileDocumentsAsync(
        Guid orgId, IReadOnlyCollection<Guid> validDocIds, CancellationToken ct = default);
}
