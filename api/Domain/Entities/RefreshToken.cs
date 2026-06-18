namespace Domain.Entities;

/// <summary>
/// A long-lived token that lets a client obtain fresh access tokens without re-entering
/// credentials. The raw token is never stored — only its SHA-256 hash — and each token is
/// single-use: refreshing rotates it (revokes the old, issues a new one).
/// </summary>
public class RefreshToken
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UserId { get; set; }
    public User? User { get; set; }

    public string TokenHash { get; set; } = string.Empty;

    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? RevokedAt { get; set; }

    public bool IsActive(DateTime now) => RevokedAt is null && ExpiresAt > now;
}
