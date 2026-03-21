using Application.Common;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/audit")]
public class AuditController(AppDbContext db, ICurrentUser currentUser) : ControllerBase
{
    /// <summary>Most recent audit entries for the caller's organization (Admin only).</summary>
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct, int take = 100)
    {
        var orgId = currentUser.OrgId;
        var entries = await db.AuditLogs
            .Where(a => a.OrgId == orgId)
            .OrderByDescending(a => a.Id)
            .Take(Math.Clamp(take, 1, 500))
            .Select(a => new { a.Id, a.UserId, a.Action, a.Detail, a.CreatedAt })
            .ToListAsync(ct);
        return Ok(entries);
    }
}
