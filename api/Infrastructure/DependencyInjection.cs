using Application.Ai;
using Application.Auth;
using Application.Common;
using Application.Documents;
using Application.Faqs;
using Application.Users;
using Infrastructure.Ai;
using Infrastructure.Audit;
using Infrastructure.Auth;
using Infrastructure.Documents;
using Infrastructure.Faqs;
using Infrastructure.Persistence;
using Infrastructure.Storage;
using Infrastructure.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure;

/// <summary>Registers persistence, auth, storage, and the application services.</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(config.GetConnectionString("Default")));

        services.Configure<JwtOptions>(config.GetSection(JwtOptions.SectionName));

        services.AddScoped<IPasswordHasher, BcryptPasswordHasher>();
        services.AddScoped<ITokenService, JwtTokenService>();
        services.AddScoped<IAuthService, AuthService>();

        services.AddScoped<IAuditLogger, AuditLogger>();
        services.AddScoped<IFileStorage, LocalFileStorage>();
        services.AddScoped<IDocumentService, DocumentService>();
        services.AddScoped<IUserAdminService, UserAdminService>();
        services.AddScoped<IFaqService, FaqService>();

        // Typed client for the Python AI service.
        var aiBaseUrl = config["AiService:BaseUrl"] ?? "http://localhost:8001";
        services.AddHttpClient<IAiService, AiServiceClient>(client =>
        {
            client.BaseAddress = new Uri(aiBaseUrl);
            client.Timeout = TimeSpan.FromSeconds(180); // ingestion embeds every chunk
        });

        return services;
    }
}
