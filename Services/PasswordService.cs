using System.Security.Cryptography;
using System.Text;

namespace HillApp.Services;

public static class PasswordService
{
    private const int Iterations = 100_000;
    private const int SaltSize = 16;
    private const int KeySize = 32;

    public static string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            Iterations,
            HashAlgorithmName.SHA256,
            KeySize);

        return $"PBKDF2${Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    public static bool Verify(string password, string storedValue)
    {
        if (storedValue.StartsWith("PBKDF2$", StringComparison.Ordinal))
        {
            var parts = storedValue.Split('$');
            if (parts.Length != 4 || !int.TryParse(parts[1], out var iterations))
                return false;

            try
            {
                var salt = Convert.FromBase64String(parts[2]);
                var expected = Convert.FromBase64String(parts[3]);
                var actual = Rfc2898DeriveBytes.Pbkdf2(
                    password,
                    salt,
                    iterations,
                    HashAlgorithmName.SHA256,
                    expected.Length);

                return CryptographicOperations.FixedTimeEquals(actual, expected);
            }
            catch (FormatException)
            {
                return false;
            }
        }

        // Compatibilidad con usuarios antiguos si luego migras datos desde SQL Server.
        var enteredDigest = SHA256.HashData(Encoding.UTF8.GetBytes(password));
        var storedDigest = SHA256.HashData(Encoding.UTF8.GetBytes(storedValue));
        return CryptographicOperations.FixedTimeEquals(enteredDigest, storedDigest);
    }

    public static bool NeedsUpgrade(string storedValue) =>
        !storedValue.StartsWith("PBKDF2$", StringComparison.Ordinal);
}
