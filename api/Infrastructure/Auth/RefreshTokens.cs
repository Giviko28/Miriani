using System.Security.Cryptography;

namespace Infrastructure.Auth;

/// <summary>
/// Helpers for refresh tokens: generate a cryptographically random raw token (handed to the
/// client) and hash it with SHA-256 (what we persist). We never store the raw token.
/// </summary>
public static class RefreshTokens
{
    public static string NewRawToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes)
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');
    }

    public static string Hash(string rawToken)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(rawToken);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }
}
