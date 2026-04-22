using Application.Auth;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Auth;
using Microsoft.Extensions.Options;
using Xunit;

namespace Tests;

public class AuthServiceTests
{
    private sealed class FakeTokens : ITokenService
    {
        public (string Token, DateTime ExpiresAt) CreateToken(Guid userId, Guid orgId, string email, UserRole role)
            => ("access-" + Guid.NewGuid(), DateTime.UtcNow.AddMinutes(15));
    }

    private static AuthService NewService(Infrastructure.Persistence.AppDbContext db)
    {
        var opts = Options.Create(new JwtOptions { RefreshDays = 30, ExpiryMinutes = 15 });
        return new AuthService(db, new FakeHasher(), new FakeTokens(), new NullAudit(), opts);
    }

    private static User SeedUser(Infrastructure.Persistence.AppDbContext db, bool active = true)
    {
        var user = new User
        {
            OrgId = Guid.NewGuid(),
            Email = "u@bpa.local",
            DisplayName = "U",
            PasswordHash = "h:pw",
            Role = UserRole.Employee,
            IsActive = active,
        };
        db.Users.Add(user);
        db.SaveChanges();
        return user;
    }

    [Fact]
    public async Task Login_returns_access_and_refresh_for_valid_active_user()
    {
        using var db = TestSupport.NewDb();
        SeedUser(db);
        var svc = NewService(db);

        var result = await svc.LoginAsync(new LoginRequest("u@bpa.local", "pw"));

        Assert.NotNull(result);
        Assert.False(string.IsNullOrEmpty(result!.Token));
        Assert.False(string.IsNullOrEmpty(result.RefreshToken));
    }

    [Fact]
    public async Task Login_rejects_disabled_user()
    {
        using var db = TestSupport.NewDb();
        SeedUser(db, active: false);
        var svc = NewService(db);

        var result = await svc.LoginAsync(new LoginRequest("u@bpa.local", "pw"));

        Assert.Null(result);
    }

    [Fact]
    public async Task Refresh_rotates_and_rejects_the_old_token()
    {
        using var db = TestSupport.NewDb();
        SeedUser(db);
        var svc = NewService(db);
        var login = await svc.LoginAsync(new LoginRequest("u@bpa.local", "pw"));

        var rotated = await svc.RefreshAsync(login!.RefreshToken);
        Assert.NotNull(rotated);
        Assert.NotEqual(login.RefreshToken, rotated!.RefreshToken);

        // The original token was revoked by rotation.
        var reuse = await svc.RefreshAsync(login.RefreshToken);
        Assert.Null(reuse);
    }

    [Fact]
    public async Task Refresh_fails_after_user_disabled()
    {
        using var db = TestSupport.NewDb();
        var user = SeedUser(db);
        var svc = NewService(db);
        var login = await svc.LoginAsync(new LoginRequest("u@bpa.local", "pw"));

        user.IsActive = false;
        db.SaveChanges();

        var result = await svc.RefreshAsync(login!.RefreshToken);
        Assert.Null(result);
    }

    [Fact]
    public async Task ChangePassword_clears_flag_and_revokes_existing_tokens()
    {
        using var db = TestSupport.NewDb();
        var user = SeedUser(db);
        user.MustChangePassword = true;
        db.SaveChanges();
        var svc = NewService(db);
        var login = await svc.LoginAsync(new LoginRequest("u@bpa.local", "pw"));

        var ok = await svc.ChangePasswordAsync(user.Id, "pw", "newpassword");
        Assert.True(ok);
        Assert.False(user.MustChangePassword);

        // Old refresh token revoked.
        Assert.Null(await svc.RefreshAsync(login!.RefreshToken));
    }

    [Fact]
    public async Task ChangePassword_rejects_wrong_current_password()
    {
        using var db = TestSupport.NewDb();
        var user = SeedUser(db);
        var svc = NewService(db);

        var ok = await svc.ChangePasswordAsync(user.Id, "wrong", "newpassword");
        Assert.False(ok);
    }
}
