using Application.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Authorize]
[Route("api/me")]
public class MeController(ICurrentUser currentUser) : ControllerBase
{
    /// <summary>Returns the identity/role resolved from the caller's JWT.</summary>
    [HttpGet]
    public IActionResult Get() => Ok(new
    {
        userId = currentUser.UserId,
        orgId = currentUser.OrgId,
        role = currentUser.Role.ToString(),
    });

    /// <summary>Smoke endpoint that only Admins may call — proves role enforcement.</summary>
    [HttpGet("admin-only")]
    [Authorize(Roles = "Admin")]
    public IActionResult AdminOnly() => Ok(new { message = "You are an Admin." });
}
