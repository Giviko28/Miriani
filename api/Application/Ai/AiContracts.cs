using Domain.Enums;

namespace Application.Ai;

public record AiSource(string DocId, string FileName, int ChunkIndex, double Distance, string Text);

public record AiAnswer(string Answer, bool UsedContext, IReadOnlyList<AiSource> Sources);

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
}
