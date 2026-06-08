using Application.Common;
using Application.Jira;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

/// <summary>
/// Jira ticket workspace. Lets any signed-in user browse/search tickets, view full detail
/// (comments, author, assignee), and act on a ticket — assign a user, post a comment, or move it
/// through its workflow. Jira credentials live server-side (config), so the browser never sees
/// them. Every write is audit-logged.
/// </summary>
[ApiController]
[Authorize]
[Route("api/jira")]
public class JiraController(IJiraService jira, ICurrentUser currentUser, IAuditLogger audit) : ControllerBase
{
    /// <summary>Whether Jira is wired up (so the UI can show/hide the ticket picker).</summary>
    [HttpGet("status")]
    public IActionResult Status() => Ok(jira.GetStatus());

    /// <summary>Search recent issues by key/summary text for the picker.</summary>
    [HttpGet("issues")]
    public async Task<IActionResult> List([FromQuery] string? search, CancellationToken ct)
    {
        if (!jira.IsConfigured) return Ok(Array.Empty<JiraIssueSummary>());
        try
        {
            return Ok(await jira.SearchAsync(search, ct));
        }
        catch (HttpRequestException ex)
        {
            return Problem($"Jira unavailable: {ex.Message}");
        }
    }

    /// <summary>Fetch one issue (with description) to paste into the chat.</summary>
    [HttpGet("issues/{key}")]
    public async Task<IActionResult> Get(string key, CancellationToken ct)
    {
        if (!jira.IsConfigured) return NotFound(new { error = "Jira is not configured." });
        try
        {
            var issue = await jira.GetAsync(key, ct);
            return issue is null ? NotFound(new { error = $"Issue {key} not found." }) : Ok(issue);
        }
        catch (HttpRequestException ex)
        {
            return Problem($"Jira unavailable: {ex.Message}");
        }
    }

    /// <summary>Users that can be assigned to the issue (for the assignee picker).</summary>
    [HttpGet("issues/{key}/assignable")]
    public async Task<IActionResult> Assignable(string key, CancellationToken ct)
    {
        if (!jira.IsConfigured) return Ok(Array.Empty<JiraUser>());
        try { return Ok(await jira.GetAssignableUsersAsync(key, ct)); }
        catch (HttpRequestException ex) { return Problem($"Jira unavailable: {ex.Message}"); }
    }

    /// <summary>Assign the issue to a user.</summary>
    [HttpPut("issues/{key}/assignee")]
    public async Task<IActionResult> Assign(string key, AssignRequest req, CancellationToken ct)
    {
        if (!jira.IsConfigured) return BadRequest(new { error = "Jira is not configured." });
        if (string.IsNullOrWhiteSpace(req.AccountId)) return BadRequest(new { error = "accountId is required." });
        try
        {
            await jira.AssignAsync(key, req.AccountId, ct);
            await audit.LogAsync(currentUser.OrgId, currentUser.UserId, "process.jira.assign",
                $"{key} → {FirstNonEmpty(req.DisplayName, req.AccountId)}", ct);
            return Ok(new { assigned = true });
        }
        catch (HttpRequestException ex) { return Problem($"Jira assign failed: {ex.Message}"); }
    }

    /// <summary>Add a comment to the issue.</summary>
    [HttpPost("issues/{key}/comment")]
    public async Task<IActionResult> Comment(string key, CommentRequest req, CancellationToken ct)
    {
        if (!jira.IsConfigured) return BadRequest(new { error = "Jira is not configured." });
        if (string.IsNullOrWhiteSpace(req.Text)) return BadRequest(new { error = "Comment text is required." });
        try
        {
            await jira.AddCommentAsync(key, req.Text, ct);
            await audit.LogAsync(currentUser.OrgId, currentUser.UserId, "process.jira.comment", key, ct);
            return Ok(new { commented = true });
        }
        catch (HttpRequestException ex) { return Problem($"Jira comment failed: {ex.Message}"); }
    }

    /// <summary>Workflow transitions available for the issue (for the status picker).</summary>
    [HttpGet("issues/{key}/transitions")]
    public async Task<IActionResult> Transitions(string key, CancellationToken ct)
    {
        if (!jira.IsConfigured) return Ok(Array.Empty<JiraTransition>());
        try { return Ok(await jira.GetTransitionsAsync(key, ct)); }
        catch (HttpRequestException ex) { return Problem($"Jira unavailable: {ex.Message}"); }
    }

    /// <summary>Move the issue through a workflow transition.</summary>
    [HttpPost("issues/{key}/transition")]
    public async Task<IActionResult> Transition(string key, TransitionRequest req, CancellationToken ct)
    {
        if (!jira.IsConfigured) return BadRequest(new { error = "Jira is not configured." });
        if (string.IsNullOrWhiteSpace(req.TransitionId)) return BadRequest(new { error = "transitionId is required." });
        try
        {
            await jira.TransitionAsync(key, req.TransitionId, ct);
            await audit.LogAsync(currentUser.OrgId, currentUser.UserId, "process.jira.transition",
                $"{key} → {FirstNonEmpty(req.Name, req.TransitionId)}", ct);
            return Ok(new { transitioned = true });
        }
        catch (HttpRequestException ex) { return Problem($"Jira transition failed: {ex.Message}"); }
    }

    private static string FirstNonEmpty(string? a, string b) => string.IsNullOrWhiteSpace(a) ? b : a!.Trim();
}

public record AssignRequest(string AccountId, string? DisplayName);
public record CommentRequest(string Text);
public record TransitionRequest(string TransitionId, string? Name);
