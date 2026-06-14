using System.Security.Cryptography;

namespace Infrastructure.Users;

/// <summary>
/// Generates strong temporary passwords for admin-created users. Guarantees at least one
/// lower, upper, digit, and symbol so the result satisfies common policies, then shuffles.
/// </summary>
public static class PasswordGenerator
{
    private const string Lower = "abcdefghijkmnopqrstuvwxyz";   // no l
    private const string Upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";    // no I, O
    private const string Digits = "23456789";                  // no 0, 1
    private const string Symbols = "!@#$%*?";
    private const string All = Lower + Upper + Digits + Symbols;

    public static string Generate(int length = 14)
    {
        if (length < 8) length = 8;
        var chars = new char[length];
        chars[0] = Pick(Lower);
        chars[1] = Pick(Upper);
        chars[2] = Pick(Digits);
        chars[3] = Pick(Symbols);
        for (var i = 4; i < length; i++) chars[i] = Pick(All);

        // Fisher-Yates shuffle so the guaranteed classes aren't always in front.
        for (var i = chars.Length - 1; i > 0; i--)
        {
            var j = RandomNumberGenerator.GetInt32(i + 1);
            (chars[i], chars[j]) = (chars[j], chars[i]);
        }
        return new string(chars);
    }

    private static char Pick(string set) => set[RandomNumberGenerator.GetInt32(set.Length)];
}
