using Application.Email;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Infrastructure.Email;

/// <summary>
/// Sends email over SMTP via MailKit. Used only after a human approves an agent-drafted email,
/// so this just does the transport — validation and audit logging live in the controller.
/// </summary>
public class SmtpEmailService(IOptions<SmtpOptions> options) : IEmailService
{
    private readonly SmtpOptions _opt = options.Value;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_opt.Host) && !string.IsNullOrWhiteSpace(_opt.FromAddress);

    public EmailStatus GetStatus() => new(IsConfigured, IsConfigured ? _opt.FromAddress : null);

    public Task SendAsync(SendEmailRequest request, CancellationToken ct = default) =>
        SendAsync(request, Array.Empty<EmailAttachment>(), ct);

    public async Task SendAsync(
        SendEmailRequest request, IReadOnlyList<EmailAttachment> attachments, CancellationToken ct = default)
    {
        if (!IsConfigured)
            throw new InvalidOperationException("Outbound email is not configured.");

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_opt.FromName, _opt.FromAddress));
        message.To.Add(MailboxAddress.Parse(request.To.Trim()));
        message.Subject = request.Subject;

        var body = new BodyBuilder { TextBody = request.Body };
        foreach (var att in attachments ?? Array.Empty<EmailAttachment>())
            body.Attachments.Add(att.FileName, att.Content, ContentType.Parse(att.MediaType));
        message.Body = body.ToMessageBody();

        using var client = new SmtpClient();
        await client.ConnectAsync(_opt.Host, _opt.Port, ResolveSecurity(), ct);
        if (!string.IsNullOrWhiteSpace(_opt.User))
            await client.AuthenticateAsync(_opt.User, _opt.Password, ct);
        await client.SendAsync(message, ct);
        await client.DisconnectAsync(true, ct);
    }

    /// <summary>
    /// Map the configured security mode to a socket option. "None" lets a local Mailpit sink
    /// work in demos; "Auto" preserves the real-provider behaviour (STARTTLS or implicit SSL).
    /// </summary>
    private SecureSocketOptions ResolveSecurity() => _opt.Security?.Trim().ToLowerInvariant() switch
    {
        "none" => SecureSocketOptions.None,
        "starttls" => SecureSocketOptions.StartTlsWhenAvailable,
        "ssl" => SecureSocketOptions.SslOnConnect,
        _ => _opt.UseStartTls ? SecureSocketOptions.StartTlsWhenAvailable : SecureSocketOptions.SslOnConnect,
    };
}
