namespace Application.Org;

/// <summary>The org's branding/company profile, as read by the client. Logo bytes are served
/// separately (GET branding/logo); here we only flag whether one exists.</summary>
public record OrgBrandingDto(
    string CompanyName, string? DisplayName, string? Tagline, string? AccentColor, bool HasLogo);

/// <summary>The logo bytes + content type, or null when no logo is set.</summary>
public record OrgLogo(byte[] Data, string ContentType);

/// <summary>Reads and updates a tenant's company branding (display name, tagline, accent, logo).</summary>
public interface IOrgBrandingService
{
    Task<OrgBrandingDto> GetAsync(Guid orgId, CancellationToken ct = default);
    Task UpdateAsync(Guid orgId, string? displayName, string? tagline, string? accentColor, CancellationToken ct = default);
    Task SetLogoAsync(Guid orgId, string contentType, byte[] data, CancellationToken ct = default);
    Task<OrgLogo?> GetLogoAsync(Guid orgId, CancellationToken ct = default);
    Task RemoveLogoAsync(Guid orgId, CancellationToken ct = default);
}
