namespace Defra_People_API.Entities;

public class GenerateApiKeyRequest
{
    public string Consumer { get; set; } = null!;
    public string Issuer { get; set; } = null!;
    public string Description { get; set; } = null!;
    public string Owner { get; set; } = null!;
}
