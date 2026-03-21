using Application.Common;
using Domain.Entities;
using Infrastructure.Persistence;

namespace Infrastructure.Audit;

/// <summary>Writes audit entries to MS SQL via EF Core.</summary>
public class AuditLogger(AppDbContext db) : IAuditLogger
{
    public async Task LogAsync(Guid orgId, Guid? userId, string action, string? detail = null, CancellationToken ct = default)
    {
        db.AuditLogs.Add(new AuditLog
        {
            OrgId = orgId,
            UserId = userId,
            Action = action,
            Detail = detail is { Length: > 2000 } ? detail[..2000] : detail,
        });
        await db.SaveChangesAsync(ct);
    }
}
