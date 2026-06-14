namespace Domain.Entities;

/// <summary>
/// An admin-curated frequently-asked question. Shown to users as clickable suggestion chips
/// on the chat screen; clicking one runs it through the AI assistant for a live answer.
/// </summary>
public class Faq
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid OrgId { get; set; }
    public Organization? Org { get; set; }

    public string Question { get; set; } = string.Empty;
    public int SortOrder { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
