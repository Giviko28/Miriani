namespace Domain.Entities;

/// <summary>
/// Append-only record of notable actions (logins, uploads, AI requests). Supports the
/// accountability the proposal calls for when AI touches business operations.
/// </summary>
public class AuditLog
{
    public long Id { get; set; }
    public Guid OrgId { get; set; }
    public Guid? UserId { get; set; }

    public string Action { get; set; } = string.Empty;
    public string? Detail { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
