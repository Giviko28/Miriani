using Application.Auth;
using Application.Common;
using Domain.Entities;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Auth;

/// <summary>
/// Register/login against MS SQL. For the single-org MVP every user is attached to one
/// default organization, created on first registration. Multi-org onboarding is future work.
/// </summary>
public class AuthService(
    AppDbContext db,
    IPasswordHasher hasher,
    ITokenService tokens,
    IAuditLogger audit) : IAuthService
{
    private const string DefaultOrgName = "Demo Company";

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var org = await GetOrCreateDefaultOrgAsync(ct);

        var exists = await db.Users.AnyAsync(u => u.OrgId == org.Id && u.Email == email, ct);
        if (exists)
            throw new InvalidOperationException("A user with this email already exists.");

        var user = new User
        {
            OrgId = org.Id,
            Email = email,
            DisplayName = request.DisplayName.Trim(),
            PasswordHash = hasher.Hash(request.Password),
            Role = request.Role,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync(ct);

        await audit.LogAsync(org.Id, user.Id, "user.register", $"{email} as {user.Role}", ct);
        return BuildResponse(user);
    }

    public async Task<AuthResponse?> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email, ct);
        if (user is null || !hasher.Verify(request.Password, user.PasswordHash))
            return null;

        await audit.LogAsync(user.OrgId, user.Id, "user.login", email, ct);
        return BuildResponse(user);
    }

    private AuthResponse BuildResponse(User user)
    {
        var (token, expiresAt) = tokens.CreateToken(user.Id, user.OrgId, user.Email, user.Role);
        return new AuthResponse(token, user.Email, user.DisplayName, user.Role, expiresAt);
    }

    private async Task<Organization> GetOrCreateDefaultOrgAsync(CancellationToken ct)
    {
        var org = await db.Organizations.FirstOrDefaultAsync(ct);
        if (org is not null) return org;

        org = new Organization { Name = DefaultOrgName };
        db.Organizations.Add(org);
        await db.SaveChangesAsync(ct);
        return org;
    }
}
