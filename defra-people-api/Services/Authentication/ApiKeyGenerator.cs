using System.Security.Cryptography;

namespace Defra_People_API.Services;

public static class ApiKeyGenerator
{
    public static string GenerateApiKeyWithPrefix(string consumer)
    {
        var prefix = new string(consumer.Where(char.IsLetterOrDigit).Take(4).ToArray()).ToUpperInvariant();
        if (prefix.Length == 0) prefix = "KEY";

        var randomPart = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');

        return $"{prefix}_{randomPart}";
    }
}
