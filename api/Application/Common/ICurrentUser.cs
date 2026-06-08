using Domain.Enums;

namespace Application.Common;

/// <summary>
/// The authenticated caller for the current request, resolved from the JWT. Lets the
/// application/infrastructure layers enforce org scoping and RBAC without touching HTTP.
/// </summary>
public interface ICurrentUser
{
    bool IsAuthenticated { get; }
    Guid UserId { get; }
    Guid OrgId { get; }
    UserRole Role { get; }
    string DisplayName { get; }
}
