namespace Domain.Entities;

/// <summary>
/// A tenant company. The MVP runs as a single organization, but every row that belongs
/// to a company carries its OrgId so true multi-tenancy is a later step, not a rewrite.
/// </summary>
public class Organization
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<User> Users { get; set; } = new List<User>();
    public ICollection<Document> Documents { get; set; } = new List<Document>();
    public ICollection<BusinessProcess> Processes { get; set; } = new List<BusinessProcess>();
}
