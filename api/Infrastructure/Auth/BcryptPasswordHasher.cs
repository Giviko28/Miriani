using Application.Auth;

namespace Infrastructure.Auth;

/// <summary>Password hashing backed by BCrypt (per-hash salt, adaptive work factor).</summary>
public class BcryptPasswordHasher : IPasswordHasher
{
    public string Hash(string password) => BCrypt.Net.BCrypt.HashPassword(password);

    public bool Verify(string password, string hash) => BCrypt.Net.BCrypt.Verify(password, hash);
}
