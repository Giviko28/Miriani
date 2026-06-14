using Application.Users;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Users;
using Xunit;

namespace Tests;

public class UserAdminServiceTests
{
    private static UserAdminService NewService(Infrastructure.Persistence.AppDbContext db)
        => new(db, new FakeHasher(), new NullAudit());

    private static (Guid orgId, User admin) SeedOrgWithAdmin(Infrastructure.Persistence.AppDbContext db)
    {
        var orgId = Guid.NewGuid();
        var admin = new User { OrgId = orgId, Email = "admin@bpa.local", DisplayName = "A", PasswordHash = "h:x", Role = UserRole.Admin };
        db.Users.Add(admin);
        db.SaveChanges();
        return (orgId, admin);
    }

    [Fact]
    public async Task Create_returns_temp_password_and_sets_must_change()
    {
        using var db = TestSupport.NewDb();
        var (orgId, _) = SeedOrgWithAdmin(db);
        var svc = NewService(db);

        var result = await svc.CreateAsync(orgId, new CreateUserRequest("jane@bpa.local", "Jane", UserRole.Manager));

        Assert.False(string.IsNullOrWhiteSpace(result.TempPassword));
        Assert.True(result.User.MustChangePassword);
        Assert.Equal(UserRole.Manager, result.User.Role);
    }

    [Fact]
    public async Task Create_duplicate_email_throws_DuplicateUserException()
    {
        using var db = TestSupport.NewDb();
        var (orgId, _) = SeedOrgWithAdmin(db);
        var svc = NewService(db);
        await svc.CreateAsync(orgId, new CreateUserRequest("jane@bpa.local", "Jane", UserRole.Employee));

        await Assert.ThrowsAsync<DuplicateUserException>(
            () => svc.CreateAsync(orgId, new CreateUserRequest("jane@bpa.local", "Jane2", UserRole.Employee)));
    }

    [Fact]
    public async Task Cannot_demote_the_last_admin()
    {
        using var db = TestSupport.NewDb();
        var (orgId, admin) = SeedOrgWithAdmin(db);
        var svc = NewService(db);

        // Use a different acting admin id so the self-guard isn't what trips first.
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.UpdateAsync(orgId, Guid.NewGuid(), admin.Id, new UpdateUserRequest(UserRole.Employee, null)));
    }

    [Fact]
    public async Task Cannot_delete_own_account()
    {
        using var db = TestSupport.NewDb();
        var (orgId, admin) = SeedOrgWithAdmin(db);
        var svc = NewService(db);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.DeleteAsync(orgId, admin.Id, admin.Id));
    }

    [Fact]
    public async Task Disabling_user_revokes_their_refresh_tokens()
    {
        using var db = TestSupport.NewDb();
        var (orgId, admin) = SeedOrgWithAdmin(db);
        var target = new User { OrgId = orgId, Email = "bob@bpa.local", DisplayName = "Bob", PasswordHash = "h:x", Role = UserRole.Employee };
        db.Users.Add(target);
        db.RefreshTokens.Add(new RefreshToken { UserId = target.Id, TokenHash = "abc", ExpiresAt = DateTime.UtcNow.AddDays(1) });
        db.SaveChanges();
        var svc = NewService(db);

        await svc.UpdateAsync(orgId, admin.Id, target.Id, new UpdateUserRequest(null, false));

        Assert.All(db.RefreshTokens.Where(t => t.UserId == target.Id), t => Assert.NotNull(t.RevokedAt));
    }
}
