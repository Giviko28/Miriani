using Application.Auth;
using Application.Common;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Tests;

/// <summary>Shared fakes/builders for the service tests (no real DB or BCrypt needed).</summary>
internal static class TestSupport
{
    public static AppDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"bpa-{Guid.NewGuid()}")
            .Options;
        return new AppDbContext(options);
    }
}

/// <summary>Fast, deterministic password hasher for tests (prefix marker, not secure).</summary>
internal sealed class FakeHasher : IPasswordHasher
{
    public string Hash(string password) => "h:" + password;
    public bool Verify(string password, string hash) => hash == "h:" + password;
}

internal sealed class NullAudit : IAuditLogger
{
    public Task LogAsync(Guid orgId, Guid? userId, string action, string? detail = null, CancellationToken ct = default)
        => Task.CompletedTask;
}
