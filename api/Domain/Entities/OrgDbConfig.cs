namespace Domain.Entities;

/// <summary>
/// Stores the external database connection for an organization so Mirian can query it.
/// One record per org — upserted when the admin saves the connection details.
/// </summary>
public class OrgDbConfig
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrgId { get; set; }
    public string DbType { get; set; } = string.Empty;       // sqlite | postgresql | mysql | mssql
    public string ConnectionString { get; set; } = string.Empty;
    public string? SchemaJson { get; set; }                  // cached from AI service after connect
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Organization Org { get; set; } = null!;
}
