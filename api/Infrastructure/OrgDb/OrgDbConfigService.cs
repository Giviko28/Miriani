using Application.Ai;
using Application.OrgDb;
using Domain.Entities;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.OrgDb;

public class OrgDbConfigService(AppDbContext db, IAiService ai) : IOrgDbConfigService
{
    public async Task<OrgDbStatusDto> GetAsync(Guid orgId, CancellationToken ct = default)
    {
        var config = await db.OrgDbConfigs.FirstOrDefaultAsync(x => x.OrgId == orgId, ct);
        if (config is null)
            return new OrgDbStatusDto(false, null, null, null);
        return new OrgDbStatusDto(true, config.DbType, config.SchemaJson, config.UpdatedAt);
    }

    public async Task<OrgDbStatusDto> SaveAsync(Guid orgId, OrgDbConfigDto dto, CancellationToken ct = default)
    {
        // Forward to AI service — introspects schema and caches it there.
        var schemaJson = await ai.ConnectDbAsync(orgId, dto.ConnectionString, ct);

        var config = await db.OrgDbConfigs.FirstOrDefaultAsync(x => x.OrgId == orgId, ct);
        if (config is null)
        {
            config = new OrgDbConfig { OrgId = orgId };
            db.OrgDbConfigs.Add(config);
        }

        config.DbType = dto.DbType;
        config.ConnectionString = dto.ConnectionString;
        config.SchemaJson = schemaJson;
        config.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
        return new OrgDbStatusDto(true, config.DbType, config.SchemaJson, config.UpdatedAt);
    }

    public async Task<string> ExploreAsync(Guid orgId, CancellationToken ct = default)
    {
        var summary = await ai.ExploreDbAsync(orgId, ct);
        var config = await db.OrgDbConfigs.FirstOrDefaultAsync(x => x.OrgId == orgId, ct);
        if (config is not null)
        {
            config.SchemaJson = summary;
            config.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
        }
        return summary;
    }

    public async Task DisconnectAsync(Guid orgId, CancellationToken ct = default)
    {
        var config = await db.OrgDbConfigs.FirstOrDefaultAsync(x => x.OrgId == orgId, ct);
        if (config is not null)
        {
            db.OrgDbConfigs.Remove(config);
            await db.SaveChangesAsync(ct);
        }
        await ai.DisconnectDbAsync(orgId, ct);
    }
}
