namespace Application.Email;

/// <summary>A request to send one email. The agent drafts it; the user reviews and approves.</summary>
public record SendEmailRequest(string To, string Subject, string Body);

/// <summary>
/// One file attached to an outbound email (e.g. a generated PDF invoice or an .ics calendar
/// hold). Content is the raw bytes; MediaType is the MIME type (e.g. "application/pdf").
/// </summary>
public record EmailAttachment(string FileName, string MediaType, byte[] Content);

/// <summary>Whether outbound email is wired up, and (if so) the From address we send as.</summary>
public record EmailStatus(bool Configured, string? From);

/// <summary>
/// Outbound email. Kept human-in-the-loop: an agent produces a draft, the user approves, and
/// only then does the gateway actually send. Sends are recorded in the audit trail.
/// </summary>
public interface IEmailService
{
    bool IsConfigured { get; }

    EmailStatus GetStatus();

    Task SendAsync(SendEmailRequest request, CancellationToken ct = default);

    /// <summary>Send an email with optional attachments (used by the business-process flows).</summary>
    Task SendAsync(SendEmailRequest request, IReadOnlyList<EmailAttachment> attachments, CancellationToken ct = default);
}
