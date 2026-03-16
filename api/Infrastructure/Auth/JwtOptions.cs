namespace Infrastructure.Auth;

/// <summary>JWT signing/validation settings, bound from the "Jwt" configuration section.</summary>
public class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "bpa-api";
    public string Audience { get; set; } = "bpa-clients";
    public string Secret { get; set; } = string.Empty;
    public int ExpiryMinutes { get; set; } = 120;
}
