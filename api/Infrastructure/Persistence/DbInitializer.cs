using Domain.Entities;
using Microsoft.EntityFrameworkCore;
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

    public static async Task InitializeAsync(IServiceProvider services, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await db.Database.MigrateAsync(ct);

        var org = await db.Organizations.FirstOrDefaultAsync(ct);
        if (org is null) return; // No org yet (no users registered) — processes seed on first login flow.

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
    }
}
