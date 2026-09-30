using System.Security.Cryptography;

namespace Rota.Api.Auth;

/// <summary>
/// Password hashes stored as "pbkdf2-sha256$iterations$salt$hash" (base64).
/// <c>dotnet Rota.Api.dll hash-password</c> prints one for the bootstrap admin (Admin__PasswordHash).
/// </summary>
public static class Passwords
{
    private const int Iterations = 210_000;
    private const int SaltSize = 16;
    private const int HashSize = 32;
    private const string Prefix = "pbkdf2-sha256";

    public static string Hash(string password)
    {
        byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);
        byte[] hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, HashSize);
        return $"{Prefix}${Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    public const int MinLength = 10;

    // No 0/O/1/l/I so temporary passwords are easy to read out and type.
    private const string TempAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghjkmnpqrstuvwxyz23456789";

    public static string NewTemporary() => new(RandomNumberGenerator.GetItems<char>(TempAlphabet, 14));

    /// <summary>A random URL-safe token and the SHA-256 (hex) to store for it.</summary>
    public static (string Token, string Hash) NewResetToken()
    {
        string token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        return (token, HashToken(token));
    }

    public static string HashToken(string token) => Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token)));

    public static bool Verify(string password, string? stored)
    {
        if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(stored)) return false;

        var parts = stored.Split('$');
        if (parts.Length != 4 || parts[0] != Prefix || !int.TryParse(parts[1], out int iterations)) return false;

        try
        {
            byte[] salt = Convert.FromBase64String(parts[2]);
            byte[] expected = Convert.FromBase64String(parts[3]);
            byte[] actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expected.Length);
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
