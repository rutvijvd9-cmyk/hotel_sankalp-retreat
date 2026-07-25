namespace HotelAutomation.Core.Security;

using System.Security.Cryptography;
using System.Text;

public sealed class PasswordHasher
{
    private const int SaltSize = 16; // 128 bit
    private const int KeySize = 32;  // 256 bit
    private const int Iterations = 100000;
    private static readonly HashAlgorithmName HashAlgorithm = HashAlgorithmName.SHA256;

    /// <summary>
    /// Hashes the password using PBKDF2 with SHA256.
    /// Format: {iterations}.{salt_base64}.{hash_base64}
    /// </summary>
    public string HashPassword(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var key = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            Iterations,
            HashAlgorithm,
            KeySize);

        return $"{Iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(key)}";
    }

    /// <summary>
    /// Verifies a password against a hash.
    /// </summary>
    public bool VerifyPassword(string password, string hashedPassword)
    {
        var parts = hashedPassword.Split('.', 3);
        if (parts.Length != 3)
        {
            return false;
        }

        if (!int.TryParse(parts[0], out var iterations))
        {
            return false;
        }

        var salt = Convert.FromBase64String(parts[1]);
        var key = Convert.FromBase64String(parts[2]);

        var keyToCheck = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            iterations,
            HashAlgorithm,
            KeySize);

        return CryptographicOperations.FixedTimeEquals(key, keyToCheck);
    }
}
