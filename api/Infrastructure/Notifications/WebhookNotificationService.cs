using System.Text;
using System.Text.Json;
using Application.Processes;
using Microsoft.Extensions.Options;

namespace Infrastructure.Notifications;

/// <summary>
/// Posts alerts to a chat channel via a generic incoming webhook. Slack and Microsoft Teams both
/// accept a JSON body with a "text" field, so one shape covers both. When no webhook URL is
/// configured this is a no-op that reports false, and the caller surfaces a graceful message.
/// </summary>
public class WebhookNotificationService(HttpClient http, IOptions<NotificationOptions> options)
    : INotificationService
{
    private readonly NotificationOptions _opt = options.Value;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_opt.WebhookUrl);

    public async Task<bool> NotifyAsync(string title, string text, CancellationToken ct = default)
    {
        if (!IsConfigured) return false;

        // Slack/Teams-compatible payload. Channel is advisory (Slack honours it for legacy hooks).
        var payload = JsonSerializer.Serialize(new
        {
            channel = _opt.Channel,
            text = $"*{title}*\n{text}",
        });

        using var content = new StringContent(payload, Encoding.UTF8, "application/json");
        using var resp = await http.PostAsync(_opt.WebhookUrl, content, ct);
        resp.EnsureSuccessStatusCode();
        return true;
    }
}

/// <summary>Webhook/notification settings, bound from the "Notifications" config section.</summary>
public class NotificationOptions
{
    public const string SectionName = "Notifications";

    /// <summary>Slack/Teams/generic incoming webhook URL. Empty disables broadcasting.</summary>
    public string WebhookUrl { get; set; } = "";

    public string Channel { get; set; } = "#ops-alerts";
}
