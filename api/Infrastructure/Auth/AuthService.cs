using Application.Auth;
using Application.Common;
using Domain.Entities;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Infrastructure.Auth;

/// <summary>
/// Authenticates users against MS SQL and manages access/refresh tokens. Users are created
/// by admins (see <c>IUserAdminService</c>); there is no self-registration. Refresh tokens
/// are stored hashed and rotated on every refresh.
/// </summary>
public class AuthService(
    AppDbContext db,
    IPasswordHasher hasher,
    ITokenService tokens,
    IAuditLogger audit,
    IOptions<JwtOptions> jwtOptions) : IAuthService
{
    private readonly JwtOptions _jwt = jwtOptions.Value;

    public async Task<AuthResponse?> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email, ct);
        if (user is null || !user.IsActive || !hasher.Verify(request.Password, user.PasswordHash))
            return null;

        var (token, expiresAt) = tokens.CreateToken(user.Id, user.OrgId, user.Email, user.Role, user.DisplayName);
        var raw = await IssueRefreshTokenAsync(user.Id, ct);

        await audit.LogAsync(user.OrgId, user.Id, "user.login", email, ct);
        return new AuthResponse(
            token, raw, user.Email, user.DisplayName, user.Role, user.MustChangePassword, expiresAt);
    }

    public async Task<TokenPair?> RefreshAsync(string rawRefreshToken, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(rawRefreshToken)) return null;

        var hash = RefreshTokens.Hash(rawRefreshToken);
        var existing = await db.RefreshTokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.TokenHash == hash, ct);

        var now = DateTime.UtcNow;
        if (existing is null || !existing.IsActive(now) || existing.User is null || !existing.User.IsActive)
            return null;

        // Rotate: revoke the presented token, issue a fresh one.
        existing.RevokedAt = now;
        var user = existing.User;
        var (token, expiresAt) = tokens.CreateToken(user.Id, user.OrgId, user.Email, user.Role, user.DisplayName);
        var raw = await IssueRefreshTokenAsync(user.Id, ct);
        return new TokenPair(token, raw, expiresAt);
    }

    public async Task LogoutAsync(string rawRefreshToken, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(rawRefreshToken)) return;

        var hash = RefreshTokens.Hash(rawRefreshToken);
        var existing = await db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, ct);
        if (existing is not null && existing.RevokedAt is null)
        {
            existing.RevokedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
        }
    }

    public async Task<bool> ChangePasswordAsync(
        Guid userId, string currentPassword, string newPassword, CancellationToken ct = default)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null || !hasher.Verify(currentPassword, user.PasswordHash))
            return false;

        user.PasswordHash = hasher.Hash(newPassword);
        user.MustChangePassword = false;
        // Force other sessions to re-authenticate after a password change.
        await RevokeAllForUserAsync(userId, ct);
        await db.SaveChangesAsync(ct);
        await audit.LogAsync(user.OrgId, user.Id, "user.change_password", user.Email, ct);
        return true;
    }

    private async Task<string> IssueRefreshTokenAsync(Guid userId, CancellationToken ct)
    {
        var raw = RefreshTokens.NewRawToken();
        db.RefreshTokens.Add(new RefreshToken
        {
            UserId = userId,
            TokenHash = RefreshTokens.Hash(raw),
            ExpiresAt = DateTime.UtcNow.AddDays(_jwt.RefreshDays),
        });
        await db.SaveChangesAsync(ct);
        return raw;
    }

    /// <summary>Revokes every active refresh token for a user. Caller saves changes.</summary>
    private async Task RevokeAllForUserAsync(Guid userId, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var active = await db.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null)
            .ToListAsync(ct);
        foreach (var t in active) t.RevokedAt = now;
    }
}
