using Domain.Enums;

namespace Application.Users;

public record UserDto(
    Guid Id,
    string Email,
    string DisplayName,
    UserRole Role,
    bool IsActive,
    bool MustChangePassword,
    DateTime CreatedAt);

public record CreateUserRequest(string Email, string DisplayName, UserRole Role);

public record CreateUserResponse(UserDto User, string TempPassword);

public record UpdateUserRequest(UserRole? Role, bool? IsActive);

public record ResetPasswordResponse(string TempPassword);

/// <summary>
/// Admin-only user lifecycle within an organization. All methods are scoped to the caller's
/// org. Guardrails prevent an admin from locking the org out of its last admin or itself.
/// </summary>
public interface IUserAdminService
{
    Task<IReadOnlyList<UserDto>> ListAsync(Guid orgId, CancellationToken ct = default);
    Task<CreateUserResponse> CreateAsync(Guid orgId, CreateUserRequest request, CancellationToken ct = default);
    Task<ResetPasswordResponse> ResetPasswordAsync(Guid orgId, Guid targetUserId, CancellationToken ct = default);
    Task<UserDto> UpdateAsync(Guid orgId, Guid actingUserId, Guid targetUserId, UpdateUserRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid orgId, Guid actingUserId, Guid targetUserId, CancellationToken ct = default);
}
