using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Application.Jira;
using Microsoft.Extensions.Options;

namespace Infrastructure.Jira;

/// <summary>
/// Typed HTTP client over the Jira Cloud REST API (v3). Read-only: it lists/searches issues and
/// fetches a single issue's description so the user can drop a ticket into chat. Auth is HTTP
/// Basic (email + API token). Descriptions come back as Atlassian Document Format (ADF) JSON,
/// which we flatten to plain text for the assistant and the chat box.
/// </summary>
public class JiraClient(HttpClient http, IOptions<JiraOptions> options) : IJiraService
{
    private readonly JiraOptions _opt = options.Value;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_opt.BaseUrl)
        && !string.IsNullOrWhiteSpace(_opt.Email)
        && !string.IsNullOrWhiteSpace(_opt.ApiToken);

    public JiraStatus GetStatus() => new(IsConfigured, IsConfigured ? _opt.BaseUrl : null);

    public async Task<IReadOnlyList<JiraIssueSummary>> SearchAsync(string? search, CancellationToken ct = default)
    {
        EnsureConfigured();

        var jql = JiraText.BuildJql(search, _opt.DefaultJql);
        var url = $"{Root()}/rest/api/3/search/jql"
                  + $"?jql={Uri.EscapeDataString(jql)}"
                  + "&fields=summary,status,issuetype,assignee,priority"
                  + $"&maxResults={_opt.MaxResults}";

        using var resp = await http.SendAsync(Authorized(HttpMethod.Get, url), ct);
        resp.EnsureSuccessStatusCode();

        await using var stream = await resp.Content.ReadAsStreamAsync(ct);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

        var results = new List<JiraIssueSummary>();
        if (doc.RootElement.TryGetProperty("issues", out var issues))
        {
            foreach (var issue in issues.EnumerateArray())
            {
                var fields = issue.GetProperty("fields");
                results.Add(new JiraIssueSummary(
                    issue.GetProperty("key").GetString() ?? "",
                    fields.TryGetProperty("summary", out var s) ? s.GetString() ?? "" : "",
                    Named(fields, "status"),
                    Named(fields, "issuetype"),
                    Assignee: ParseUser(fields, "assignee")?.DisplayName,
                    Priority: Named(fields, "priority") is { Length: > 0 } pr ? pr : null));
            }
        }
        return results;
    }

    public async Task<JiraIssueDetail?> GetAsync(string key, CancellationToken ct = default)
    {
        EnsureConfigured();

        var url = $"{Root()}/rest/api/3/issue/{Uri.EscapeDataString(key)}"
                  + "?fields=summary,status,issuetype,description,assignee,reporter,priority,created,updated,labels,comment";

        using var resp = await http.SendAsync(Authorized(HttpMethod.Get, url), ct);
        if (resp.StatusCode == HttpStatusCode.NotFound) return null;
        resp.EnsureSuccessStatusCode();

        await using var stream = await resp.Content.ReadAsStreamAsync(ct);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

        var fields = doc.RootElement.GetProperty("fields");
        var description = fields.TryGetProperty("description", out var d) ? JiraText.FlattenAdf(d) : "";

        return new JiraIssueDetail(
            doc.RootElement.GetProperty("key").GetString() ?? key,
            fields.TryGetProperty("summary", out var s) ? s.GetString() ?? "" : "",
            Named(fields, "status"),
            Named(fields, "issuetype"),
            description.Trim(),
            Assignee: ParseUser(fields, "assignee"),
            Reporter: ParseUser(fields, "reporter"),
            Priority: Named(fields, "priority") is { Length: > 0 } p ? p : null,
            Created: GetStringField(fields, "created"),
            Updated: GetStringField(fields, "updated"),
            Labels: ParseLabels(fields),
            Comments: ParseComments(fields));
    }

    public async Task<IReadOnlyList<JiraUser>> GetAssignableUsersAsync(string key, CancellationToken ct = default)
    {
        EnsureConfigured();
        var url = $"{Root()}/rest/api/3/user/assignable/search"
                  + $"?issueKey={Uri.EscapeDataString(key)}&maxResults=50";

        using var resp = await http.SendAsync(Authorized(HttpMethod.Get, url), ct);
        resp.EnsureSuccessStatusCode();

        await using var stream = await resp.Content.ReadAsStreamAsync(ct);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

        var users = new List<JiraUser>();
        if (doc.RootElement.ValueKind == JsonValueKind.Array)
            foreach (var u in doc.RootElement.EnumerateArray())
                if (ReadUser(u) is { } user) users.Add(user);
        return users;
    }

    public async Task AssignAsync(string key, string accountId, CancellationToken ct = default)
    {
        EnsureConfigured();
        var req = Authorized(HttpMethod.Put, $"{Root()}/rest/api/3/issue/{Uri.EscapeDataString(key)}/assignee");
        req.Content = new StringContent(JsonSerializer.Serialize(new { accountId }), Encoding.UTF8, "application/json");
        using var resp = await http.SendAsync(req, ct);
        await EnsureWriteSucceeded(resp, $"assign {key}", ct);
    }

    public async Task AddCommentAsync(string key, string text, CancellationToken ct = default)
    {
        EnsureConfigured();
        var payload = new { body = JiraText.ToAdf(text) };
        var req = Authorized(HttpMethod.Post, $"{Root()}/rest/api/3/issue/{Uri.EscapeDataString(key)}/comment");
        req.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        using var resp = await http.SendAsync(req, ct);
        await EnsureWriteSucceeded(resp, $"comment on {key}", ct);
    }

    public async Task<IReadOnlyList<JiraTransition>> GetTransitionsAsync(string key, CancellationToken ct = default)
    {
        EnsureConfigured();
        var url = $"{Root()}/rest/api/3/issue/{Uri.EscapeDataString(key)}/transitions";

        using var resp = await http.SendAsync(Authorized(HttpMethod.Get, url), ct);
        resp.EnsureSuccessStatusCode();

        await using var stream = await resp.Content.ReadAsStreamAsync(ct);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

        var transitions = new List<JiraTransition>();
        if (doc.RootElement.TryGetProperty("transitions", out var arr))
            foreach (var t in arr.EnumerateArray())
            {
                var toStatus = t.TryGetProperty("to", out var to) && to.TryGetProperty("name", out var tn)
                    ? tn.GetString() ?? "" : "";
                transitions.Add(new JiraTransition(
                    t.GetProperty("id").GetString() ?? "",
                    t.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "",
                    toStatus));
            }
        return transitions;
    }

    public async Task TransitionAsync(string key, string transitionId, CancellationToken ct = default)
    {
        EnsureConfigured();
        var payload = new { transition = new { id = transitionId } };
        var req = Authorized(HttpMethod.Post, $"{Root()}/rest/api/3/issue/{Uri.EscapeDataString(key)}/transitions");
        req.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        using var resp = await http.SendAsync(req, ct);
        await EnsureWriteSucceeded(resp, $"transition {key}", ct);
    }

    public async Task<JiraCreatedIssue> CreateIssueAsync(JiraNewIssue issue, CancellationToken ct = default)
    {
        // Offline/demo fallback: no Jira configured → mint a local placeholder key so the
        // helpdesk and onboarding flows still produce a visible, referenceable ticket.
        if (!IsConfigured)
        {
            var n = Interlocked.Increment(ref _localCounter);
            return new JiraCreatedIssue($"{_opt.ProjectKey}-{1000 + n}", "#", Simulated: true);
        }

        var requested = string.IsNullOrWhiteSpace(issue.IssueType) ? "Task" : issue.IssueType;

        // Try the requested issue type; if the project doesn't define it (e.g. "Incident" or
        // "Service Request" on a default Kanban project), fall back to "Task" so the demo
        // doesn't break on a configuration mismatch.
        var (resp, body) = await PostIssueAsync(requested, issue, ct);
        if (!resp.IsSuccessStatusCode && !requested.Equals("Task", StringComparison.OrdinalIgnoreCase))
        {
            resp.Dispose();
            (resp, body) = await PostIssueAsync("Task", issue, ct);
        }

        using (resp)
        {
            if (!resp.IsSuccessStatusCode)
                throw new HttpRequestException($"Jira create failed ({(int)resp.StatusCode}): {body}");

            using var doc = JsonDocument.Parse(body);
            var key = doc.RootElement.GetProperty("key").GetString() ?? "";
            return new JiraCreatedIssue(key, $"{Root()}/browse/{key}", Simulated: false);
        }
    }

    private async Task<(HttpResponseMessage Resp, string Body)> PostIssueAsync(
        string issueType, JiraNewIssue issue, CancellationToken ct)
    {
        var payload = new
        {
            fields = new Dictionary<string, object?>
            {
                ["project"] = new { key = _opt.ProjectKey },
                ["summary"] = issue.Summary,
                ["issuetype"] = new { name = issueType },
                ["description"] = JiraText.ToAdf(issue.Description),
            },
        };
        var req = Authorized(HttpMethod.Post, $"{Root()}/rest/api/3/issue");
        req.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        var resp = await http.SendAsync(req, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);
        return (resp, body);
    }

    // --- helpers ---

    private static int _localCounter;

    private string Root() => _opt.BaseUrl.TrimEnd('/');

    private void EnsureConfigured()
    {
        if (!IsConfigured)
            throw new InvalidOperationException("Jira is not configured.");
    }

    private HttpRequestMessage Authorized(HttpMethod method, string url)
    {
        var req = new HttpRequestMessage(method, url);
        var basic = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_opt.Email}:{_opt.ApiToken}"));
        req.Headers.Authorization = new AuthenticationHeaderValue("Basic", basic);
        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return req;
    }

    /// <summary>Read a nested {"name": ...} field (status, issuetype) safely.</summary>
    private static string Named(JsonElement fields, string property) =>
        fields.TryGetProperty(property, out var obj)
        && obj.ValueKind == JsonValueKind.Object
        && obj.TryGetProperty("name", out var name)
            ? name.GetString() ?? ""
            : "";

    private static string? GetStringField(JsonElement fields, string property) =>
        fields.TryGetProperty(property, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString() : null;

    /// <summary>Read a {"displayName","accountId",...} object at <paramref name="property"/>.</summary>
    private static JiraUser? ParseUser(JsonElement fields, string property) =>
        fields.TryGetProperty(property, out var obj) ? ReadUser(obj) : null;

    private static JiraUser? ReadUser(JsonElement obj)
    {
        if (obj.ValueKind != JsonValueKind.Object) return null;
        var accountId = obj.TryGetProperty("accountId", out var a) ? a.GetString() ?? "" : "";
        var display = obj.TryGetProperty("displayName", out var d) ? d.GetString() ?? "" : "";
        if (string.IsNullOrEmpty(accountId) && string.IsNullOrEmpty(display)) return null;
        var email = obj.TryGetProperty("emailAddress", out var e) ? e.GetString() : null;
        var avatar = obj.TryGetProperty("avatarUrls", out var av) && av.ValueKind == JsonValueKind.Object
            && av.TryGetProperty("48x48", out var url) ? url.GetString() : null;
        return new JiraUser(accountId, display, email, avatar);
    }

    private static IReadOnlyList<string> ParseLabels(JsonElement fields)
    {
        var labels = new List<string>();
        if (fields.TryGetProperty("labels", out var arr) && arr.ValueKind == JsonValueKind.Array)
            foreach (var l in arr.EnumerateArray())
                if (l.GetString() is { Length: > 0 } s) labels.Add(s);
        return labels;
    }

    private static IReadOnlyList<JiraComment> ParseComments(JsonElement fields)
    {
        var comments = new List<JiraComment>();
        if (fields.TryGetProperty("comment", out var c)
            && c.ValueKind == JsonValueKind.Object
            && c.TryGetProperty("comments", out var arr)
            && arr.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in arr.EnumerateArray())
            {
                var author = item.TryGetProperty("author", out var au) && au.TryGetProperty("displayName", out var an)
                    ? an.GetString() ?? "Unknown" : "Unknown";
                var body = item.TryGetProperty("body", out var b) ? JiraText.FlattenAdf(b) : "";
                var created = item.TryGetProperty("created", out var cr) ? cr.GetString() ?? "" : "";
                comments.Add(new JiraComment(author, body.Trim(), created));
            }
        }
        return comments;
    }

    /// <summary>Throw a clean error if a write (assign/comment/transition) didn't succeed.</summary>
    private static async Task EnsureWriteSucceeded(HttpResponseMessage resp, string what, CancellationToken ct)
    {
        if (resp.IsSuccessStatusCode) return;
        var body = await resp.Content.ReadAsStringAsync(ct);
        throw new HttpRequestException($"Jira {what} failed ({(int)resp.StatusCode}): {body}");
    }
}
