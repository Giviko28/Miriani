using System.Net.Http.Json;
using Application.Ai;
using Domain.Enums;

namespace Infrastructure.Ai;

/// <summary>
/// Typed HTTP client for the Python AI service. Maps the authenticated caller's role to the
/// numeric role level the AI service uses for retrieval filtering.
/// </summary>
public class AiServiceClient(HttpClient http) : IAiService
{
    public async Task<int> IngestAsync(
        Guid orgId, Guid docId, string fileName, UserRole accessRole, byte[] data, CancellationToken ct = default)
    {
        using var form = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(data);
        form.Add(fileContent, "file", fileName);
        form.Add(new StringContent(orgId.ToString()), "org_id");
        form.Add(new StringContent(docId.ToString()), "doc_id");
        form.Add(new StringContent(((int)accessRole).ToString()), "access_role");

        var resp = await http.PostAsync("/ingest", form, ct);
        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadFromJsonAsync<IngestBody>(ct);
        return body?.Chunks ?? 0;
    }

    public async Task<AiAnswer> QueryAsync(Guid orgId, UserRole role, string query, CancellationToken ct = default)
    {
        var payload = new
        {
            org_id = orgId.ToString(),
            role_level = (int)role,
            query,
        };

        var resp = await http.PostAsJsonAsync("/rag/query", payload, ct);
        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadFromJsonAsync<QueryBody>(ct)
                   ?? new QueryBody("", false, []);

        var sources = body.Sources
            .Select(s => new AiSource(s.Doc_Id, s.File_Name, s.Chunk_Index, s.Distance, s.Text))
            .ToList();
        return new AiAnswer(body.Answer, body.Used_Context, sources);
    }

    public async Task<AiAgentAnswer> RunAgentAsync(
        Guid orgId, UserRole role, string query,
        IReadOnlyList<AiTurn>? history = null, CancellationToken ct = default)
    {
        var payload = new
        {
            org_id = orgId.ToString(),
            role_level = (int)role,
            query,
            history = history?.Select(t => new { sender = t.Sender, content = t.Content }).ToList(),
        };

        var resp = await http.PostAsJsonAsync("/agent/run", payload, ct);
        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadFromJsonAsync<AgentBody>(ct)
                   ?? new AgentBody("", "", false, [], null);

        var sources = body.Sources
            .Select(s => new AiSource(s.Doc_Id, s.File_Name, s.Chunk_Index, s.Distance, s.Text))
            .ToList();
        return new AiAgentAnswer(body.Route, body.Answer, body.Used_Context, sources, body.Structured);
    }

    public async Task<string> ConnectDbAsync(Guid orgId, string connectionString, CancellationToken ct = default)
    {
        var payload = new { org_id = orgId.ToString(), connection_string = connectionString };
        var resp = await http.PostAsJsonAsync("/db/connect", payload, ct);
        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadFromJsonAsync<DbSchemaBody>(ct);
        return System.Text.Json.JsonSerializer.Serialize(body);
    }

    public async Task<string> ExploreDbAsync(Guid orgId, CancellationToken ct = default)
    {
        var resp = await http.PostAsync($"/db/explore/{orgId}", null, ct);
        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadFromJsonAsync<DbExploreBody>(ct);
        return body?.Summary ?? string.Empty;
    }

    public async Task DisconnectDbAsync(Guid orgId, CancellationToken ct = default)
    {
        var resp = await http.DeleteAsync($"/db/disconnect/{orgId}", ct);
        resp.EnsureSuccessStatusCode();
    }

    public async Task DeleteDocumentAsync(Guid docId, CancellationToken ct = default)
    {
        var resp = await http.DeleteAsync($"/ingest/{docId}", ct);
        resp.EnsureSuccessStatusCode();
    }

    public async Task<IReadOnlyList<string>> ReconcileDocumentsAsync(
        Guid orgId, IReadOnlyCollection<Guid> validDocIds, CancellationToken ct = default)
    {
        var payload = new
        {
            org_id = orgId.ToString(),
            valid_doc_ids = validDocIds.Select(d => d.ToString()).ToList(),
        };
        var resp = await http.PostAsJsonAsync("/ingest/reconcile", payload, ct);
        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadFromJsonAsync<ReconcileBody>(ct);
        return body?.Removed ?? [];
    }

    private record ReconcileBody(List<string> Removed);
    private record IngestBody(string Doc_Id, int Chunks);
    private record DbSchemaBody(string Org_Id, List<object> Tables);
    private record DbExploreBody(string Org_Id, string Summary, int Tables_Explored);
    private record QueryBody(string Answer, bool Used_Context, List<SourceBody> Sources);
    private record AgentBody(
        string Route, string Answer, bool Used_Context, List<SourceBody> Sources,
        Dictionary<string, object>? Structured);
    private record SourceBody(string Doc_Id, string File_Name, int Chunk_Index, double Distance, string Text);
}
