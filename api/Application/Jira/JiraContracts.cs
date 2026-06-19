namespace Application.Jira;

/// <summary>A compact Jira issue used to populate the chat ticket picker.</summary>
public record JiraIssueSummary(
    string Key, string Summary, string Status, string IssueType,
    string? Assignee = null, string? Priority = null);

/// <summary>A Jira user (assignee/reporter/comment author).</summary>
public record JiraUser(string AccountId, string DisplayName, string? Email, string? AvatarUrl);

/// <summary>A single comment on an issue, body flattened to plain text.</summary>
public record JiraComment(string Author, string Body, string Created);

/// <summary>A workflow transition available for an issue (e.g. "In Progress").</summary>
public record JiraTransition(string Id, string Name, string ToStatus);

/// <summary>A single Jira issue with its description flattened to plain text and full detail.</summary>
public record JiraIssueDetail(
    string Key, string Summary, string Status, string IssueType, string Description,
    JiraUser? Assignee = null, JiraUser? Reporter = null, string? Priority = null,
    string? Created = null, string? Updated = null,
    IReadOnlyList<string>? Labels = null, IReadOnlyList<JiraComment>? Comments = null);

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

    /// <summary>Users that can be assigned to the given issue.</summary>
    Task<IReadOnlyList<JiraUser>> GetAssignableUsersAsync(string key, CancellationToken ct = default);

    /// <summary>Assign the issue to a user (by Atlassian accountId).</summary>
    Task AssignAsync(string key, string accountId, CancellationToken ct = default);

    /// <summary>Add a plain-text comment to the issue.</summary>
    Task AddCommentAsync(string key, string text, CancellationToken ct = default);

    /// <summary>List the workflow transitions currently available for the issue.</summary>
    Task<IReadOnlyList<JiraTransition>> GetTransitionsAsync(string key, CancellationToken ct = default);

    /// <summary>Move the issue through a workflow transition (by transition id).</summary>
    Task TransitionAsync(string key, string transitionId, CancellationToken ct = default);
}
