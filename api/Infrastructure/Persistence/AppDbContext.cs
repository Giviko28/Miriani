using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence;

/// <summary>
/// EF Core context for the structured (MS SQL) side of the system. The vector side lives
/// in ChromaDB and is owned by the AI service.
/// </summary>
public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<BusinessProcess> Processes => Set<BusinessProcess>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Faq> Faqs => Set<Faq>();
    public DbSet<ChatSession> ChatSessions => Set<ChatSession>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();
    public DbSet<OrgDbConfig> OrgDbConfigs => Set<OrgDbConfig>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Organization>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.DisplayName).HasMaxLength(200);
            e.Property(x => x.Tagline).HasMaxLength(280);
            e.Property(x => x.AccentColor).HasMaxLength(16);
            e.Property(x => x.LogoContentType).HasMaxLength(128);
        });

        b.Entity<User>(e =>
        {
            e.Property(x => x.Email).HasMaxLength(256).IsRequired();
            e.Property(x => x.DisplayName).HasMaxLength(200).IsRequired();
            e.Property(x => x.PasswordHash).IsRequired();
            e.Property(x => x.Role).HasConversion<string>().HasMaxLength(32);
            // Email is unique per organization.
            e.HasIndex(x => new { x.OrgId, x.Email }).IsUnique();
            e.HasOne(x => x.Org).WithMany(o => o.Users).HasForeignKey(x => x.OrgId);
        });

        b.Entity<Document>(e =>
        {
            e.Property(x => x.FileName).HasMaxLength(512).IsRequired();
            e.Property(x => x.ContentType).HasMaxLength(128);
            e.Property(x => x.StoragePath).HasMaxLength(1024).IsRequired();
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
            e.Property(x => x.AccessRole).HasConversion<string>().HasMaxLength(32);
            e.HasOne(x => x.Org).WithMany(o => o.Documents).HasForeignKey(x => x.OrgId);
            e.HasIndex(x => x.OrgId);
        });

        b.Entity<BusinessProcess>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.Description).HasMaxLength(1000);
            e.Property(x => x.AgentKey).HasMaxLength(64).IsRequired();
            e.HasOne(x => x.Org).WithMany(o => o.Processes).HasForeignKey(x => x.OrgId);
            e.HasIndex(x => new { x.OrgId, x.AgentKey }).IsUnique();
        });

        b.Entity<AuditLog>(e =>
        {
            e.Property(x => x.Action).HasMaxLength(128).IsRequired();
            e.Property(x => x.Detail).HasMaxLength(2000);
            e.HasIndex(x => new { x.OrgId, x.CreatedAt });
        });

        b.Entity<RefreshToken>(e =>
        {
            e.Property(x => x.TokenHash).HasMaxLength(128).IsRequired();
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId);
            e.HasIndex(x => x.UserId);
            e.HasIndex(x => x.TokenHash);
        });

        b.Entity<Faq>(e =>
        {
            e.Property(x => x.Question).HasMaxLength(500).IsRequired();
            e.HasOne(x => x.Org).WithMany().HasForeignKey(x => x.OrgId);
            e.HasIndex(x => new { x.OrgId, x.SortOrder });
        });

        b.Entity<ChatSession>(e =>
        {
            e.Property(x => x.Title).HasMaxLength(200).IsRequired();
            e.HasIndex(x => new { x.UserId, x.UpdatedAt });
            e.HasMany(x => x.Messages).WithOne(m => m.Session)
                .HasForeignKey(m => m.SessionId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<ChatMessage>(e =>
        {
            e.Property(x => x.Sender).HasMaxLength(16).IsRequired();
            e.Property(x => x.Content).IsRequired();
            e.Property(x => x.Route).HasMaxLength(32);
            e.HasIndex(x => new { x.SessionId, x.CreatedAt });
        });

        b.Entity<OrgDbConfig>(e =>
        {
            e.Property(x => x.DbType).HasMaxLength(32).IsRequired();
            e.Property(x => x.ConnectionString).HasMaxLength(2000).IsRequired();
            e.HasOne(x => x.Org).WithMany().HasForeignKey(x => x.OrgId);
            e.HasIndex(x => x.OrgId).IsUnique();
        });
    }
}
