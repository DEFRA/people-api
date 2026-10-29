using System.Security.Cryptography;

namespace Defra_People_API.Services;

public static class ApiKeyGenerator
{
    public static string GenerateApiKeyWithPrefix(string prefix, int length = 32)
    {
        var key = GenerateApiKey(length);
        return $"{prefix}_{key}";
    }

    // Returns an encoded string representing the API key
    private static string GenerateApiKey(int length = 32)
    {
        var bytes = RandomNumberGenerator.GetBytes(length);
        return Convert.ToBase64String(bytes);
    }
}
