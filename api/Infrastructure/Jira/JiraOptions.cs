namespace Infrastructure.Jira;

/// <summary>
/// Jira Cloud connection settings, bound from the "Jira" config section. Auth is Basic with
/// the account email + an API token (id.atlassian.com → Security → API tokens). Leave BaseUrl
/// empty to disable the feature; the UI hides the ticket picker when it isn't configured.
/// </summary>
public class JiraOptions
{
    public const string SectionName = "Jira";

    /// <summary>e.g. https://your-company.atlassian.net</summary>
    public string BaseUrl { get; set; } = "";

    public string Email { get; set; } = "";

    public string ApiToken { get; set; } = "";

    /// <summary>Project key new issues are created under (e.g. "OPS").</summary>
    public string ProjectKey { get; set; } = "OPS";

    /// <summary>
    /// JQL used when no search text is supplied (the default ticket list). Jira Cloud's
    /// /search/jql endpoint rejects unbounded queries, so this must include a restriction.
    /// </summary>
    public string DefaultJql { get; set; } = "updated >= -90d order by updated DESC";

    /// <summary>How many issues to return in the picker.</summary>
    public int MaxResults { get; set; } = 25;
}
