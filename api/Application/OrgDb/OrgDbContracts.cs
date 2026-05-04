namespace Application.OrgDb;

public record OrgDbConfigDto(
    string DbType,
    string ConnectionString);

public record OrgDbStatusDto(
    bool Connected,
    string? DbType,
    string? SchemaJson,
    DateTime? UpdatedAt);

public interface IOrgDbConfigService
{
    Task<OrgDbStatusDto> GetAsync(Guid orgId, CancellationToken ct = default);
    Task<OrgDbStatusDto> SaveAsync(Guid orgId, OrgDbConfigDto dto, CancellationToken ct = default);
    Task<string> ExploreAsync(Guid orgId, CancellationToken ct = default);
    Task DisconnectAsync(Guid orgId, CancellationToken ct = default);
}
