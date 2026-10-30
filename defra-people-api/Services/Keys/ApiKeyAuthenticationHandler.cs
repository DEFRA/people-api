using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Defra_People_API.Cache;

namespace Defra_People_API.Services;

public class ApiKeyAuthenticationHandler : AuthenticationHandler<ApiKeyAuthenticationOptions>
{
    private const string ApiKeyHeaderName = "X-API-Key";
    private readonly IAuthConfigProvider _authConfigProvider;
    private readonly IApiKeyCacheService _apiKeyCacheService;

    public ApiKeyAuthenticationHandler(IOptionsMonitor<ApiKeyAuthenticationOptions> options, ILoggerFactory logger, UrlEncoder encoder, ISystemClock clock, IAuthConfigProvider authConfigProvider, IApiKeyCacheService apiKeyCacheService) 
        : base(options, logger, encoder, clock)
    {
        _authConfigProvider = authConfigProvider;
        _apiKeyCacheService = apiKeyCacheService;
}

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(ApiKeyHeaderName, out var apiKeyHeaderValues))
        {
            return Task.FromResult(AuthenticateResult.Fail("API Key is missing"));
        }

        var providedApiKey = apiKeyHeaderValues.FirstOrDefault();

        if (string.IsNullOrWhiteSpace(providedApiKey))
        {
            return Task.FromResult(AuthenticateResult.Fail("API Key is missing"));
        }

        // Check if it's the admin key
        var authConfig = _authConfigProvider.GetAuthConfig();
        var adminApiKey = authConfig?.AdminApiKey;

        if (providedApiKey == adminApiKey)
        {
            return CreateSuccessResult("AdminUser", true);
        }
        
        if (_apiKeyCacheService.IsValidKey(providedApiKey))
        {
            return CreateSuccessResult("ApiUser", false);
        }

        return Task.FromResult(AuthenticateResult.Fail("Invalid API Key"));
    }

    private Task<AuthenticateResult> CreateSuccessResult(string username, bool isAdmin)
    {

        var claims = new List<Claim> 
        { 
            new Claim(ClaimTypes.Name, username)
        };
        
        if (isAdmin)
        {
            claims.Add(new Claim(ClaimTypes.Role, "Admin"));
        }
        
        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}

public class ApiKeyAuthenticationOptions : AuthenticationSchemeOptions
{
    public const string DefaultScheme = "ApiKey";
    public string Scheme => DefaultScheme;
    public string AuthenticationType = DefaultScheme;
}

public static class ApiKeyAuthenticationExtensions
{
    public static AuthenticationBuilder AddApiKeyAuthentication(this AuthenticationBuilder builder)
    {
        return builder.AddScheme<ApiKeyAuthenticationOptions, ApiKeyAuthenticationHandler>(
            ApiKeyAuthenticationOptions.DefaultScheme, null);
    }
}