namespace Domain.Entities;

/// <summary>
/// A tenant company. The MVP runs as a single organization, but every row that belongs
/// to a company carries its OrgId so true multi-tenancy is a later step, not a rewrite.
/// </summary>
public class Organization
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // --- branding / company profile (admin-customizable) ---

    /// <summary>Friendly company name shown in the app header (falls back to <see cref="Name"/>).</summary>
    public string? DisplayName { get; set; }

    /// <summary>Short subtitle shown under the company name in the header.</summary>
    public string? Tagline { get; set; }

    /// <summary>Brand accent color as a hex string (e.g. "#2563eb"), applied via a CSS variable.</summary>
    public string? AccentColor { get; set; }

    /// <summary>MIME type of the uploaded logo (null when no logo is set).</summary>
    public string? LogoContentType { get; set; }

    /// <summary>Raw logo bytes, stored in the DB (logos are small; served via the gateway).</summary>
    public byte[]? LogoData { get; set; }

    public ICollection<User> Users { get; set; } = new List<User>();
    public ICollection<Document> Documents { get; set; } = new List<Document>();
    public ICollection<BusinessProcess> Processes { get; set; } = new List<BusinessProcess>();
}
