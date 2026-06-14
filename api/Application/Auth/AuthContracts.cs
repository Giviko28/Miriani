using Domain.Enums;

namespace Application.Auth;

public record RegisterRequest(string Email, string Password, string DisplayName, UserRole Role);

public record LoginRequest(string Email, string Password);

public record AuthResponse(string Token, string Email, string DisplayName, UserRole Role, DateTime ExpiresAt);

/// <summary>Registers and authenticates users, issuing JWTs on success.</summary>
public interface IAuthService
{
    Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct = default);
    Task<AuthResponse?> LoginAsync(LoginRequest request, CancellationToken ct = default);
}

/// <summary>Issues signed JWTs carrying the user's identity, org, and role claims.</summary>
public interface ITokenService
{
    (string Token, DateTime ExpiresAt) CreateToken(Guid userId, Guid orgId, string email, UserRole role);
}

/// <summary>Hashes and verifies passwords. Implementation chooses the algorithm.</summary>
public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string hash);
}
