namespace Defra_People_API.Entities;

public class AccessKey
{
    public AccessKey()
    {
        Created = DateTime.UtcNow;
        Expires = DateTime.UtcNow.AddYears(1);
    }

    // only stored in memory
    [System.Text.Json.Serialization.JsonIgnore]
    public string Key { get; set; } = null!;
    public string EncryptedKey { get; set; } = null!;
    public string EncryptionKey { get; set; } = null!;
    public string Consumer { get; set; } = null!;
    public string Issuer { get; set; } = null!;
    public string Description { get; set; } = null!;
    public string Owner { get; set; } = null!;
    public DateTime Created { get; set; }
    public DateTime Expires { get; set; }
    public bool Active { get; set; }
}
