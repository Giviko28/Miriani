namespace Infrastructure.Email;

/// <summary>
/// SMTP settings, bound from the "Smtp" config section. Leave Host empty to disable outbound
/// email; the UI hides the Send button when it isn't configured. Secrets (Password) belong in
/// user-secrets, not appsettings.
/// </summary>
public class SmtpOptions
{
    public const string SectionName = "Smtp";

    public string Host { get; set; } = "";

    public int Port { get; set; } = 587;

    /// <summary>SMTP auth user (often the same as FromAddress). Optional for open relays.</summary>
    public string User { get; set; } = "";

    public string Password { get; set; } = "";

    public string FromAddress { get; set; } = "";

    public string FromName { get; set; } = "BPA Assistant";

    /// <summary>Use STARTTLS (true, port 587) vs implicit SSL (false, port 465).</summary>
    public bool UseStartTls { get; set; } = true;

    /// <summary>
    /// Transport security: "Auto" (default — STARTTLS or SSL per UseStartTls, for real
    /// providers), "StartTls", "Ssl", or "None" (plain, for a local Mailpit sink in demos).
    /// </summary>
    public string Security { get; set; } = "Auto";
}
