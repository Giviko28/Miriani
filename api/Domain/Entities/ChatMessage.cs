namespace Domain.Entities;

/// <summary>
/// One turn in a <see cref="ChatSession"/>. User messages carry just text; assistant
/// messages also record the agent route, whether the answer was grounded, and the source
/// citations / structured output as JSON strings (rendered by the frontend).
/// </summary>
public class ChatMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid SessionId { get; set; }
    public ChatSession? Session { get; set; }

    /// <summary>"user" or "assistant".</summary>
    public string Sender { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;

    // Assistant-only metadata.
    public string? Route { get; set; }
    public bool UsedContext { get; set; }
    public string? Sources { get; set; }     // JSON array
    public string? Structured { get; set; }  // JSON object

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
