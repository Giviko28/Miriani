using Domain.Enums;

namespace Domain.Entities;

/// <summary>
/// An authenticated person within an organization. The Role drives RBAC, and is later
/// propagated to the AI service so RAG retrieval only returns context the user may see.
/// </summary>
public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrgId { get; set; }
    public Organization? Org { get; set; }

    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public UserRole Role { get; set; } = UserRole.Employee;

    /// <summary>Disabled users cannot log in or refresh; their tokens are revoked.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Set for admin-created users; forces a password change on first login.</summary>
    public bool MustChangePassword { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
