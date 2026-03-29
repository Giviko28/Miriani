using Application.Auth;
using Domain.Entities;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.Persistence;

/// <summary>
/// Applies pending migrations and seeds the demo organization's business processes (the
/// 6 processes from the proposal). Idempotent — safe to run on every startup.
/// </summary>
public static class DbInitializer
{
    private static readonly (string Name, string AgentKey, string Description)[] DemoProcesses =
    [
        ("Internal Policy Q&A", "policy_qa", "Answer employee questions from company policies and handbooks."),
        ("Document Summarization", "doc_summary", "Summarize an uploaded PDF, Word, or Excel document."),
        ("Email Drafting", "email_draft", "Draft business emails grounded in company context."),
        ("Report Drafting", "report_draft", "Draft business reports from a prompt and company data."),
        ("Request Routing", "router", "Classify an incoming request and route it to the right agent."),
        ("Invoice Generation", "invoice_gen", "Generate a structured invoice from provided details."),
    ];

    private static readonly string[] StarterFaqs =
    [
        "What is our remote work policy?",
        "How do I submit an expense report?",
        "How many vacation days do I get?",
    ];

    public static async Task InitializeAsync(IServiceProvider services, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();

        await db.Database.MigrateAsync(ct);

        // Ensure the single demo organization exists so processes can be seeded up front
        // (registration attaches users to this same org).
        var org = await db.Organizations.FirstOrDefaultAsync(ct);
        if (org is null)
        {
            org = new Organization { Name = "Demo Company" };
            db.Organizations.Add(org);
            await db.SaveChangesAsync(ct);
        }

        foreach (var (name, agentKey, description) in DemoProcesses)
        {
            var exists = await db.Processes.AnyAsync(p => p.OrgId == org.Id && p.AgentKey == agentKey, ct);
            if (!exists)
            {
                db.Processes.Add(new BusinessProcess
                {
                    OrgId = org.Id,
                    Name = name,
                    AgentKey = agentKey,
                    Description = description,
                });
            }
        }
        await db.SaveChangesAsync(ct);

        await SeedRootAdminAsync(db, hasher, config, org.Id, ct);
        await SeedFaqsAsync(db, org.Id, ct);
    }

    /// <summary>Create a root admin if the org has no admin yet, so the system is usable on first run.</summary>
    private static async Task SeedRootAdminAsync(
        AppDbContext db, IPasswordHasher hasher, IConfiguration config, Guid orgId, CancellationToken ct)
    {
        var hasAdmin = await db.Users.AnyAsync(u => u.OrgId == orgId && u.Role == UserRole.Admin, ct);
        if (hasAdmin) return;

        const string email = "admin@bpa.local";
        var password = config["Seed:RootAdminPassword"];
        if (string.IsNullOrWhiteSpace(password)) password = "ChangeMe!123";

        db.Users.Add(new User
        {
            OrgId = orgId,
            Email = email,
            DisplayName = "Root Admin",
            Role = UserRole.Admin,
            PasswordHash = hasher.Hash(password),
            MustChangePassword = false,
            IsActive = true,
        });
        await db.SaveChangesAsync(ct);

        Console.WriteLine("========================================================");
        Console.WriteLine($"  Seeded root admin: {email} / {password}");
        Console.WriteLine("  Change this password after first sign-in.");
        Console.WriteLine("========================================================");
    }

    private static async Task SeedFaqsAsync(AppDbContext db, Guid orgId, CancellationToken ct)
    {
        if (await db.Faqs.AnyAsync(f => f.OrgId == orgId, ct)) return;

        for (var i = 0; i < StarterFaqs.Length; i++)
            db.Faqs.Add(new Faq { OrgId = orgId, Question = StarterFaqs[i], SortOrder = i + 1 });
        await db.SaveChangesAsync(ct);
    }
}
