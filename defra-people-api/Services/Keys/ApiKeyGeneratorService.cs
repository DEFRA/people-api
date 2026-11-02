namespace Defra_People_API.Services.Keys;

public interface IApiKeyGeneratorService
{
    string GenerateApiKeyWithPrefix(string prefix, int length = 32);
}

public class ApiKeyGeneratorService : IApiKeyGeneratorService
{
    private readonly IRandomGenerator _randomGenerator;

    public ApiKeyGeneratorService(IRandomGenerator randomGenerator)
    {
        _randomGenerator = randomGenerator ?? throw new ArgumentNullException(nameof(randomGenerator));
    }

    public string GenerateApiKeyWithPrefix(string prefix, int length = 32)
    {
        var key = GenerateApiKey(length);
        return $"{prefix}_{key}";
    }

    private string GenerateApiKey(int length = 32)
    {
        var bytes = _randomGenerator.GetRandomBytes(length);
        return Convert.ToBase64String(bytes);
    }
}
