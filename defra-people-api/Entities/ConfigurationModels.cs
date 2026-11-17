namespace Defra_People_API.Entities;

public class GraphApiConfig
{
    public string? ClientId { get; set; } = default!;
    public string ClientSecret { get; set; } = default!;
    public string TenantId { get; set; } = default!;
    public string[] Scopes { get; set; } = default!;
}
