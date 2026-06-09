using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Infrastructure.Persistence;

/// <summary>
/// Design-time factory used by the EF Core CLI (migrations) so it can construct the
/// context without booting the API host. The connection string here is only used for
/// generating migrations, not at runtime.
/// </summary>
public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Port=5432;Database=BpaDb;Username=postgres;Password=postgres")
            .Options;
        return new AppDbContext(options);
    }
}
