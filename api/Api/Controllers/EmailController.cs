using Application.Common;
using Application.Email;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

/// <summary>
/// Outbound email, human-in-the-loop. An agent drafts the email in chat; the user reviews it and
/// posts it here to actually send. Every send is written to the audit trail.
/// </summary>
[ApiController]
[Authorize]
[Route("api/email")]
public class EmailController(IEmailService email, IAuditLogger audit, ICurrentUser currentUser)
    : ControllerBase
{
    /// <summary>Whether outbound email is wired up (so the UI can show/hide the Send button).</summary>
    [HttpGet("status")]
    public IActionResult Status() => Ok(email.GetStatus());

    /// <summary>Send a reviewed, agent-drafted email.</summary>
    [HttpPost("send")]
    public async Task<IActionResult> Send(SendEmailRequest req, CancellationToken ct)
    {
        if (!email.IsConfigured)
            return BadRequest(new { error = "Outbound email is not configured." });
        if (string.IsNullOrWhiteSpace(req.To) || !req.To.Contains('@'))
            return BadRequest(new { error = "A valid recipient address is required." });
        if (string.IsNullOrWhiteSpace(req.Subject) && string.IsNullOrWhiteSpace(req.Body))
            return BadRequest(new { error = "The email is empty." });

        try
        {
            await email.SendAsync(req, ct);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            return Problem($"Failed to send email: {ex.Message}");
        }

        await audit.LogAsync(
            currentUser.OrgId, currentUser.UserId, "email.send",
            $"To {req.To.Trim()} — {req.Subject}", ct);

        return Ok(new { sent = true });
    }
}
