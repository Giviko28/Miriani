namespace Domain.Entities;

/// <summary>
/// A saved conversation between a user and the AI assistant. Owned by the user who created
/// it; messages are ordered by <see cref="ChatMessage.CreatedAt"/>.
/// </summary>
public class ChatSession
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid OrgId { get; set; }
    public Guid UserId { get; set; }

    public string Title { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public List<ChatMessage> Messages { get; set; } = [];
}
