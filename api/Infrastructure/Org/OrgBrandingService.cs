using Application.Org;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Org;

/// <summary>
/// Reads/updates a tenant's company branding on the <c>Organizations</c> row. The logo is stored
/// inline as bytes (logos are small) and served through the gateway, so the browser never needs a
/// public file URL.
/// </summary>
public class OrgBrandingService(AppDbContext db) : IOrgBrandingService
{
    public async Task<OrgBrandingDto> GetAsync(Guid orgId, CancellationToken ct = default)
    {
        var org = await db.Organizations.FirstOrDefaultAsync(o => o.Id == orgId, ct)
                  ?? throw new KeyNotFoundException("Organization not found.");
        return new OrgBrandingDto(
            org.Name, org.DisplayName, org.Tagline, org.AccentColor, org.LogoData is { Length: > 0 });
    }

    public async Task UpdateAsync(
        Guid orgId, string? displayName, string? tagline, string? accentColor, CancellationToken ct = default)
    {
        var org = await Require(orgId, ct);
        org.DisplayName = Trim(displayName);
        org.Tagline = Trim(tagline);
        org.AccentColor = Trim(accentColor);
        await db.SaveChangesAsync(ct);
    }

    public async Task SetLogoAsync(Guid orgId, string contentType, byte[] data, CancellationToken ct = default)
    {
        var org = await Require(orgId, ct);
        org.LogoData = data;
        org.LogoContentType = contentType;
        await db.SaveChangesAsync(ct);
    }

    public async Task<OrgLogo?> GetLogoAsync(Guid orgId, CancellationToken ct = default)
    {
        var org = await db.Organizations.FirstOrDefaultAsync(o => o.Id == orgId, ct);
        if (org?.LogoData is not { Length: > 0 }) return null;
        return new OrgLogo(org.LogoData, org.LogoContentType ?? "image/png");
    }

    public async Task RemoveLogoAsync(Guid orgId, CancellationToken ct = default)
    {
        var org = await Require(orgId, ct);
        org.LogoData = null;
        org.LogoContentType = null;
        await db.SaveChangesAsync(ct);
    }

    private async Task<Domain.Entities.Organization> Require(Guid orgId, CancellationToken ct) =>
        await db.Organizations.FirstOrDefaultAsync(o => o.Id == orgId, ct)
        ?? throw new KeyNotFoundException("Organization not found.");

    private static string? Trim(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
