using Application.Jira;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

/// <summary>
/// Read-only Jira intake. Lets any signed-in user browse/search tickets and pull one into chat
/// so the assistant can help solve it with the company's grounded knowledge. Jira credentials
/// live server-side (config), so the browser never sees them.
/// </summary>
[ApiController]
[Authorize]
[Route("api/jira")]
public class JiraController(IJiraService jira) : ControllerBase
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
}
