using Domain.Enums;

namespace Application.Ai;

public record AiSource(string DocId, string FileName, int ChunkIndex, double Distance, string Text);

/// <summary>One prior conversation turn supplied as context for a follow-up request.</summary>
public record AiTurn(string Sender, string Content);

public record AiAnswer(string Answer, bool UsedContext, IReadOnlyList<AiSource> Sources);

/// <summary>Result of the agent graph: which agent handled it, plus optional structured output.</summary>
public record AiAgentAnswer(
    string Route,
    string Answer,
    bool UsedContext,
    IReadOnlyList<AiSource> Sources,
    IReadOnlyDictionary<string, object>? Structured);

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
    /// give the agent conversational memory for follow-up questions.
    /// </summary>
    Task<AiAgentAnswer> RunAgentAsync(
        Guid orgId, UserRole role, string query,
        IReadOnlyList<AiTurn>? history = null, CancellationToken ct = default);

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
