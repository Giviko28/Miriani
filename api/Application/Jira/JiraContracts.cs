namespace Application.Jira;

/// <summary>A compact Jira issue used to populate the chat ticket picker.</summary>
public record JiraIssueSummary(string Key, string Summary, string Status, string IssueType);

/// <summary>A single Jira issue with its description flattened to plain text.</summary>
public record JiraIssueDetail(
    string Key, string Summary, string Status, string IssueType, string Description);

/// <summary>Whether Jira is wired up, and (if so) which site we point at.</summary>
public record JiraStatus(bool Configured, string? Site);

/// <summary>A new issue to create in Jira.</summary>
public record JiraNewIssue(string Summary, string Description, string IssueType = "Task", string? Priority = null);

/// <summary>The result of creating an issue. Simulated=true means Jira wasn't configured and a
/// local placeholder key was minted so demos still work offline.</summary>
public record JiraCreatedIssue(string Key, string Url, bool Simulated);

/// <summary>
/// Read-only access to a Jira Cloud instance. The gateway exposes this so users can pull a
/// ticket's title/summary into the chat and have the assistant help solve it with the
/// company's grounded knowledge. No write-back to Jira — this is intake only.
/// </summary>
public interface IJiraService
{
    bool IsConfigured { get; }

    JiraStatus GetStatus();

    /// <summary>Search recent issues. <paramref name="search"/> matches key/summary text.</summary>
    Task<IReadOnlyList<JiraIssueSummary>> SearchAsync(string? search, CancellationToken ct = default);

    /// <summary>Fetch one issue with its description, or null if it doesn't exist.</summary>
    Task<JiraIssueDetail?> GetAsync(string key, CancellationToken ct = default);

    /// <summary>
    /// Create an issue. When Jira is configured this writes to the real instance; otherwise it
    /// mints a local placeholder key (Simulated=true) so the helpdesk/onboarding demos still run.
    /// </summary>
    Task<JiraCreatedIssue> CreateIssueAsync(JiraNewIssue issue, CancellationToken ct = default);
}
