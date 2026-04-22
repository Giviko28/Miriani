using Application.Auth;
using Application.Common;
using Application.Users;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Users;

/// <summary>
/// Admin-only user management against MS SQL, scoped to one organization. Disabling, deleting
/// or resetting a user revokes their refresh tokens so the change takes effect promptly.
/// </summary>
public class UserAdminService(AppDbContext db, IPasswordHasher hasher, IAuditLogger audit) : IUserAdminService
{
    public async Task<IReadOnlyList<UserDto>> ListAsync(Guid orgId, CancellationToken ct = default)
    {
        return await db.Users
            .Where(u => u.OrgId == orgId)
            .OrderBy(u => u.DisplayName)
            .Select(u => Map(u))
            .ToListAsync(ct);
    }

    public async Task<CreateUserResponse> CreateAsync(Guid orgId, CreateUserRequest request, CancellationToken ct = default)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
            throw new InvalidOperationException("A valid email is required.");
        if (string.IsNullOrWhiteSpace(request.DisplayName))
            throw new InvalidOperationException("A display name is required.");

        var exists = await db.Users.AnyAsync(u => u.OrgId == orgId && u.Email == email, ct);
        if (exists)
            throw new DuplicateUserException("A user with this email already exists.");

        var tempPassword = PasswordGenerator.Generate();
        var user = new User
        {
            OrgId = orgId,
            Email = email,
            DisplayName = request.DisplayName.Trim(),
            Role = request.Role,
            PasswordHash = hasher.Hash(tempPassword),
            MustChangePassword = true,
            IsActive = true,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync(ct);
        await audit.LogAsync(orgId, user.Id, "user.create", $"{email} as {user.Role}", ct);

        return new CreateUserResponse(Map(user), tempPassword);
    }

    public async Task<ResetPasswordResponse> ResetPasswordAsync(Guid orgId, Guid targetUserId, CancellationToken ct = default)
    {
        var user = await GetInOrgAsync(orgId, targetUserId, ct);

        var tempPassword = PasswordGenerator.Generate();
        user.PasswordHash = hasher.Hash(tempPassword);
        user.MustChangePassword = true;
        await RevokeTokensAsync(targetUserId, ct);
        await db.SaveChangesAsync(ct);
        await audit.LogAsync(orgId, user.Id, "user.reset_password", user.Email, ct);

        return new ResetPasswordResponse(tempPassword);
    }

    public async Task<UserDto> UpdateAsync(
        Guid orgId, Guid actingUserId, Guid targetUserId, UpdateUserRequest request, CancellationToken ct = default)
    {
        var user = await GetInOrgAsync(orgId, targetUserId, ct);
        var self = actingUserId == targetUserId;

        if (request.Role is { } newRole && newRole != user.Role)
        {
            if (self && user.Role == UserRole.Admin && newRole != UserRole.Admin)
                throw new InvalidOperationException("You cannot demote yourself.");
            if (user.Role == UserRole.Admin && newRole != UserRole.Admin)
                await EnsureNotLastAdminAsync(orgId, targetUserId, ct);
            user.Role = newRole;
        }

        if (request.IsActive is { } active && active != user.IsActive)
        {
            if (self && !active)
                throw new InvalidOperationException("You cannot disable your own account.");
            if (!active && user.Role == UserRole.Admin)
                await EnsureNotLastAdminAsync(orgId, targetUserId, ct);
            user.IsActive = active;
            if (!active) await RevokeTokensAsync(targetUserId, ct);
        }

        await db.SaveChangesAsync(ct);
        await audit.LogAsync(orgId, user.Id, "user.update", $"{user.Email} role={user.Role} active={user.IsActive}", ct);
        return Map(user);
    }

    public async Task DeleteAsync(Guid orgId, Guid actingUserId, Guid targetUserId, CancellationToken ct = default)
    {
        if (actingUserId == targetUserId)
            throw new InvalidOperationException("You cannot delete your own account.");

        var user = await GetInOrgAsync(orgId, targetUserId, ct);
        if (user.Role == UserRole.Admin)
            await EnsureNotLastAdminAsync(orgId, targetUserId, ct);

        await RevokeTokensAsync(targetUserId, ct);
        db.Users.Remove(user);
        await db.SaveChangesAsync(ct);
        await audit.LogAsync(orgId, null, "user.delete", user.Email, ct);
    }

    private async Task<User> GetInOrgAsync(Guid orgId, Guid userId, CancellationToken ct)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId && u.OrgId == orgId, ct);
        return user ?? throw new InvalidOperationException("User not found.");
    }

    private async Task EnsureNotLastAdminAsync(Guid orgId, Guid excludingUserId, CancellationToken ct)
    {
        var otherAdmins = await db.Users.CountAsync(
            u => u.OrgId == orgId && u.Role == UserRole.Admin && u.IsActive && u.Id != excludingUserId, ct);
        if (otherAdmins == 0)
            throw new InvalidOperationException("This is the last active admin; the organization must keep at least one.");
    }

    private async Task RevokeTokensAsync(Guid userId, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var active = await db.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null)
            .ToListAsync(ct);
        foreach (var t in active) t.RevokedAt = now;
    }

    private static UserDto Map(User u) =>
        new(u.Id, u.Email, u.DisplayName, u.Role, u.IsActive, u.MustChangePassword, u.CreatedAt);
}

/// <summary>Thrown when creating a user whose email already exists in the org (maps to HTTP 409).</summary>
public class DuplicateUserException(string message) : Exception(message);
