using Application.Ai;
using Application.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Authorize]
[Route("api/ai")]
public class AiController(IAiService ai, ICurrentUser currentUser) : ControllerBase
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
            return Ok(answer);
        }
        catch (HttpRequestException ex)
        {
            return Problem($"AI service unavailable: {ex.Message}");
        }
    }
}
