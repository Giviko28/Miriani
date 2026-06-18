using Application.Auth;
using Application.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(IAuthService auth, ICurrentUser currentUser) : ControllerBase
{
    /// <summary>Authenticate and return an access token + refresh token.</summary>
    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken ct)
    {
        var result = await auth.LoginAsync(request, ct);
        return result is null
            ? Unauthorized(new { error = "Invalid email or password." })
            : Ok(result);
    }

    /// <summary>Exchange a valid refresh token for a fresh token pair (rotates the refresh token).</summary>
    [AllowAnonymous]
    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh(RefreshRequest request, CancellationToken ct)
    {
        var result = await auth.RefreshAsync(request.RefreshToken, ct);
        return result is null
            ? Unauthorized(new { error = "Invalid or expired refresh token." })
            : Ok(result);
    }

    /// <summary>Revoke a refresh token (sign out the session).</summary>
    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout(LogoutRequest request, CancellationToken ct)
    {
        await auth.LogoutAsync(request.RefreshToken, ct);
        return NoContent();
    }

    /// <summary>Change the signed-in user's own password.</summary>
    [Authorize]
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 8)
            return BadRequest(new { error = "New password must be at least 8 characters." });

        var ok = await auth.ChangePasswordAsync(currentUser.UserId, request.CurrentPassword, request.NewPassword, ct);
        return ok ? NoContent() : BadRequest(new { error = "Current password is incorrect." });
    }
}
