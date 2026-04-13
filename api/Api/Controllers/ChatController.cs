using Application.Chat;
using Application.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Authorize]
[Route("api/chat")]
public class ChatController(IChatService chat, ICurrentUser currentUser) : ControllerBase
{
    /// <summary>List the caller's chat sessions, newest first.</summary>
    [HttpGet("sessions")]
    public async Task<IActionResult> ListSessions(CancellationToken ct)
        => Ok(await chat.ListSessionsAsync(currentUser.OrgId, currentUser.UserId, ct));

    /// <summary>Get one conversation (the caller's) with its full message thread.</summary>
    [HttpGet("sessions/{id:guid}")]
    public async Task<IActionResult> GetSession(Guid id, CancellationToken ct)
    {
        var thread = await chat.GetThreadAsync(currentUser.OrgId, currentUser.UserId, id, ct);
        return thread is null ? NotFound() : Ok(thread);
    }

    /// <summary>
    /// Send a message. Creates a new session when no sessionId is given. The assistant reply
    /// is grounded and remembers recent turns in the same conversation.
    /// </summary>
    [HttpPost("message")]
    public async Task<IActionResult> Send(SendMessageRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Query))
            return BadRequest(new { error = "Query is required." });

        try
        {
            return Ok(await chat.SendAsync(currentUser.OrgId, currentUser.UserId, currentUser.Role, req, ct));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (HttpRequestException ex)
        {
            return Problem($"AI service unavailable: {ex.Message}");
        }
    }

    /// <summary>Delete one of the caller's conversations.</summary>
    [HttpDelete("sessions/{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var deleted = await chat.DeleteAsync(currentUser.OrgId, currentUser.UserId, id, ct);
        return deleted ? NoContent() : NotFound();
    }
}
