using Domain.Enums;

namespace Application.Auth;

public record LoginRequest(string Email, string Password);

public record RefreshRequest(string RefreshToken);

public record LogoutRequest(string RefreshToken);

public record ChangePasswordRequest(string CurrentPassword, string NewPassword);

public record AuthResponse(
    string Token,
    string RefreshToken,
    string Email,
    string DisplayName,
    UserRole Role,
    bool MustChangePassword,
    DateTime ExpiresAt);

/// <summary>A freshly rotated token pair (access + refresh) and the access-token expiry.</summary>
public record TokenPair(string Token, string RefreshToken, DateTime ExpiresAt);

/// <summary>Authenticates users and manages access/refresh tokens.</summary>
public interface IAuthService
{
    Task<AuthResponse?> LoginAsync(LoginRequest request, CancellationToken ct = default);
    Task<TokenPair?> RefreshAsync(string rawRefreshToken, CancellationToken ct = default);
    Task LogoutAsync(string rawRefreshToken, CancellationToken ct = default);

    /// <summary>Returns false when the current password is wrong.</summary>
    Task<bool> ChangePasswordAsync(Guid userId, string currentPassword, string newPassword, CancellationToken ct = default);
}

/// <summary>Issues signed JWTs carrying the user's identity, org, and role claims.</summary>
public interface ITokenService
{
    (string Token, DateTime ExpiresAt) CreateToken(Guid userId, Guid orgId, string email, UserRole role, string displayName);
}

/// <summary>Hashes and verifies passwords. Implementation chooses the algorithm.</summary>
public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string hash);
}
