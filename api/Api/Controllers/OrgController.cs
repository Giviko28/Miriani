using Application.Common;
using Application.Org;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

/// <summary>
/// Company profile / branding for the caller's organization. Any signed-in user can read the
/// branding (so the app header can render it); only Admins can change it. The logo is stored in the
/// DB and served through this gateway, org-scoped from the caller's JWT.
/// </summary>
[ApiController]
[Authorize]
[Route("api/org")]
public class OrgController(IOrgBrandingService branding, ICurrentUser currentUser, IAuditLogger audit) : ControllerBase
{
    private const long MaxLogoBytes = 1 * 1024 * 1024; // 1 MB

    /// <summary>The org's branding (display name, tagline, accent, whether a logo exists).</summary>
    [HttpGet("branding")]
    public async Task<IActionResult> GetBranding(CancellationToken ct)
        => Ok(await branding.GetAsync(currentUser.OrgId, ct));

    /// <summary>The org's logo bytes (or 404 when none is set). Used as an image source.</summary>
    [HttpGet("branding/logo")]
    public async Task<IActionResult> GetLogo(CancellationToken ct)
    {
        var logo = await branding.GetLogoAsync(currentUser.OrgId, ct);
        return logo is null ? NotFound() : File(logo.Data, logo.ContentType);
    }

    /// <summary>Update the text branding fields (Admin only).</summary>
    [HttpPut("branding")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateBranding(UpdateBrandingRequest req, CancellationToken ct)
    {
        await branding.UpdateAsync(currentUser.OrgId, req.DisplayName, req.Tagline, req.AccentColor, ct);
        await audit.LogAsync(currentUser.OrgId, currentUser.UserId, "org.branding.update",
            req.DisplayName, ct);
        return Ok(await branding.GetAsync(currentUser.OrgId, ct));
    }

    /// <summary>Upload/replace the company logo (Admin only). Image types, ≤1 MB.</summary>
    [HttpPost("branding/logo")]
    [Authorize(Roles = "Admin")]
    [RequestSizeLimit(MaxLogoBytes)]
    public async Task<IActionResult> UploadLogo(IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { error = "No file provided." });
        if (file.Length > MaxLogoBytes)
            return BadRequest(new { error = "Logo is too large (max 1 MB)." });
        if (string.IsNullOrWhiteSpace(file.ContentType) || !file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { error = "Only image files are allowed." });

        using var ms = new MemoryStream();
        await file.CopyToAsync(ms, ct);
        await branding.SetLogoAsync(currentUser.OrgId, file.ContentType, ms.ToArray(), ct);
        await audit.LogAsync(currentUser.OrgId, currentUser.UserId, "org.branding.logo", file.FileName, ct);
        return Ok(new { uploaded = true });
    }

    /// <summary>Remove the company logo (Admin only).</summary>
    [HttpDelete("branding/logo")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> RemoveLogo(CancellationToken ct)
    {
        await branding.RemoveLogoAsync(currentUser.OrgId, ct);
        await audit.LogAsync(currentUser.OrgId, currentUser.UserId, "org.branding.logo.remove", null, ct);
        return NoContent();
    }
}

public record UpdateBrandingRequest(string? DisplayName, string? Tagline, string? AccentColor);
