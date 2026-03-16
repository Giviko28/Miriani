namespace Domain.Entities;

/// <summary>
/// A business process the system can automate (e.g. policy Q&amp;A, invoice generation).
/// AgentKey links the process to the specialized agent that handles it in the AI service
/// (wired up in Milestone 5). Seeded with the demo process set.
/// </summary>
public class BusinessProcess
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrgId { get; set; }
    public Organization? Org { get; set; }

    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    /// <summary>Stable identifier the AI service uses to route to the right agent.</summary>
    public string AgentKey { get; set; } = string.Empty;

    public bool IsEnabled { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
