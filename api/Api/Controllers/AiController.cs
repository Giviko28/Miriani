using Application.Ai;
using Application.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Authorize]
[Route("api/ai")]
public class AiController(IAiService ai, ICurrentUser currentUser, IAuditLogger audit) : ControllerBase
{
    public record QueryRequest(string Query);

    /// <summary>
    /// Ask a question answered from the org's knowledge base. Org and role are taken from
    /// the caller's JWT, so retrieval is filtered to what this user is allowed to see — the
    /// client cannot widen its own access.
    /// </summary>
    [HttpPost("query")]
    public async Task<IActionResult> Query(QueryRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Query))
            return BadRequest(new { error = "Query is required." });

        try
        {
            var answer = await ai.QueryAsync(currentUser.OrgId, currentUser.Role, req.Query, ct);
            await audit.LogAsync(currentUser.OrgId, currentUser.UserId, "ai.query", req.Query, ct);
            return Ok(answer);
        }
        catch (HttpRequestException ex)
        {
            return Problem($"AI service unavailable: {ex.Message}");
        }
    }

    /// <summary>
    /// Route a request through the agent system. The router picks the right specialized
    /// agent (policy Q&amp;A, summarization, email/report drafting, invoice generation).
    /// Org and role come from the JWT, so retrieval stays role-scoped.
    /// </summary>
    [HttpPost("agent")]
    public async Task<IActionResult> Agent(QueryRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Query))
            return BadRequest(new { error = "Query is required." });

        try
        {
            var result = await ai.RunAgentAsync(currentUser.OrgId, currentUser.Role, req.Query, history: null, ct: ct);
            await audit.LogAsync(currentUser.OrgId, currentUser.UserId, "ai.agent",
                $"[{result.Route}] {req.Query}", ct);
            return Ok(result);
        }
        catch (HttpRequestException ex)
        {
            return Problem($"AI service unavailable: {ex.Message}");
        }
    }
}
