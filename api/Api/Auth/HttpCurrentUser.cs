using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Application.Common;
using Domain.Enums;
using Infrastructure.Auth;

namespace Api.Auth;

/// <summary>Resolves <see cref="ICurrentUser"/> from the JWT claims on the current request.</summary>
public class HttpCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private ClaimsPrincipal? Principal => accessor.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated ?? false;

    public Guid UserId => GetGuid(JwtRegisteredClaimNames.Sub) ?? GetGuid(ClaimTypes.NameIdentifier) ?? Guid.Empty;

    public Guid OrgId => GetGuid(JwtTokenService.OrgClaim) ?? Guid.Empty;

    public UserRole Role =>
        Enum.TryParse<UserRole>(Principal?.FindFirstValue(ClaimTypes.Role), out var r) ? r : UserRole.Employee;

    public string DisplayName =>
        Principal?.FindFirstValue(JwtRegisteredClaimNames.Name) ?? 
        Principal?.FindFirstValue(ClaimTypes.Name) ?? "";

    private Guid? GetGuid(string claimType) =>
        Guid.TryParse(Principal?.FindFirstValue(claimType), out var g) ? g : null;
}
