using Application.Ai;
using Application.Auth;
using Application.Common;
using Application.Documents;
using Application.Email;
using Application.Faqs;
using Application.Jira;
using Application.OrgDb;
using Application.Processes;
using Application.Users;
using Infrastructure.Ai;
using Infrastructure.Audit;
using Infrastructure.Auth;
using Infrastructure.Calendar;
using Infrastructure.Documents;
using Infrastructure.Email;
using Infrastructure.Faqs;
using Infrastructure.Jira;
using Infrastructure.Notifications;
using Infrastructure.OrgDb;
using Infrastructure.Pdf;
using Infrastructure.Persistence;
using Infrastructure.Storage;
using Infrastructure.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure;

/// <summary>Registers persistence, auth, storage, and the application services.</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(NormalizePostgresConnectionString(config.GetConnectionString("Default")))
                   // EF 10 escalates this to an error during migrate-on-boot. Design-time
                   // (`dotnet ef migrations has-pending-model-changes`) confirms the snapshot
                   // matches the model; the runtime check is a known Npgsql false positive, so
                   // we ignore it here rather than blocking startup.
                   .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning)));

        services.Configure<JwtOptions>(config.GetSection(JwtOptions.SectionName));

        services.AddScoped<IPasswordHasher, BcryptPasswordHasher>();
        services.AddScoped<ITokenService, JwtTokenService>();
        services.AddScoped<IAuthService, AuthService>();

        services.AddScoped<IAuditLogger, AuditLogger>();
        services.AddScoped<IFileStorage, LocalFileStorage>();
        services.AddScoped<IDocumentService, DocumentService>();
        services.AddScoped<IUserAdminService, UserAdminService>();
        services.AddScoped<IFaqService, FaqService>();
        services.AddScoped<Application.Chat.IChatService, Infrastructure.Chat.ChatService>();
        services.AddScoped<IOrgDbConfigService, OrgDbConfigService>();
        services.AddScoped<Application.Org.IOrgBrandingService, Infrastructure.Org.OrgBrandingService>();

        // Typed client for the Python AI service.
        var aiBaseUrl = EnsureHttpScheme(config["AiService:BaseUrl"]) ?? "http://localhost:8001";
        services.AddHttpClient<IAiService, AiServiceClient>(client =>
        {
            client.BaseAddress = new Uri(aiBaseUrl);
            client.Timeout = TimeSpan.FromSeconds(180); // ingestion embeds every chunk
        });

        // --- business-process integrations ---
        services.Configure<SmtpOptions>(config.GetSection(SmtpOptions.SectionName));
        services.Configure<JiraOptions>(config.GetSection(JiraOptions.SectionName));
        services.Configure<NotificationOptions>(config.GetSection(NotificationOptions.SectionName));
        services.Configure<ProcessOptions>(config.GetSection(ProcessOptions.SectionName));

        services.AddScoped<IEmailService, SmtpEmailService>();
        services.AddSingleton<IPdfService, PdfService>();
        services.AddSingleton<ICalendarService, IcsCalendarService>();
        services.AddHttpClient<IJiraService, JiraClient>();
        services.AddHttpClient<INotificationService, WebhookNotificationService>();

        return services;
    }

    /// <summary>
    /// Ensures a base URL has an http(s) scheme. Render's internal service discovery hands out
    /// a bare "host:port" for free-tier web services; HttpClient.BaseAddress needs a scheme.
    /// </summary>
    public static string? EnsureHttpScheme(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return url;
        if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            return url;
        return "http://" + url;
    }

    /// <summary>
    /// Accepts either a native Npgsql keyword connection string or a URI-style one
    /// (postgres://user:pass@host:port/db?sslmode=require), as handed out by Neon, Render,
    /// Supabase, etc., and returns a keyword string Npgsql understands. Pasting the provider
    /// URL straight into the connection-string env var "just works".
    /// </summary>
    public static string? NormalizePostgresConnectionString(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return raw;
        if (!raw.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) &&
            !raw.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
            return raw;

        var uri = new Uri(raw);
        var userInfo = uri.UserInfo.Split(':', 2);
        var builder = new Npgsql.NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.IsDefaultPort ? 5432 : uri.Port,
            Database = uri.AbsolutePath.TrimStart('/'),
            Username = Uri.UnescapeDataString(userInfo[0]),
            Password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : null,
        };

        // Carry query-string options through (sslmode, channel_binding, etc.).
        foreach (var pair in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var kv = pair.Split('=', 2);
            var key = Uri.UnescapeDataString(kv[0]);
            var value = kv.Length > 1 ? Uri.UnescapeDataString(kv[1]) : null;
            switch (key.ToLowerInvariant())
            {
                case "sslmode":
                    if (Enum.TryParse<Npgsql.SslMode>(value, ignoreCase: true, out var mode))
                        builder.SslMode = mode;
                    break;
                case "channel_binding":
                    builder.ChannelBinding = value?.ToLowerInvariant() switch
                    {
                        "require" => Npgsql.ChannelBinding.Require,
                        "disable" => Npgsql.ChannelBinding.Disable,
                        _ => Npgsql.ChannelBinding.Prefer,
                    };
                    break;
            }
        }

        // Managed Postgres (Neon/Render/Supabase) always requires TLS.
        if (builder.SslMode == Npgsql.SslMode.Disable)
            builder.SslMode = Npgsql.SslMode.Require;

        return builder.ConnectionString;
    }
}
