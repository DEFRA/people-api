using System.Security.Claims;
using System.Text.Encodings.Web;
using Defra_People_API.Cache;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Defra_People_API.Services;

public class ApiKeyAuthenticationOptions : AuthenticationSchemeOptions
{
    public const string DefaultScheme = "ApiKey";
    public const string HeaderName = "X-API-Key";
}

public class ApiKeyAuthenticationHandler : AuthenticationHandler<ApiKeyAuthenticationOptions>
{
    private readonly IApiKeyCacheService _apiKeyCacheService;
    private readonly IAuthConfigProvider _authConfigProvider;

    public ApiKeyAuthenticationHandler(
        IOptionsMonitor<ApiKeyAuthenticationOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IApiKeyCacheService apiKeyCacheService,
        IAuthConfigProvider authConfigProvider)
        : base(options, logger, encoder)
    {
        _apiKeyCacheService = apiKeyCacheService;
        _authConfigProvider = authConfigProvider;
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(ApiKeyAuthenticationOptions.HeaderName, out var values))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var providedKey = values.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(providedKey))
        {
            return Task.FromResult(AuthenticateResult.Fail("Missing API key"));
        }

        var adminKey = _authConfigProvider.GetAuthConfig().AdminApiKey;
        var isAdmin = !string.IsNullOrEmpty(adminKey) && providedKey == adminKey;

        if (!isAdmin && !_apiKeyCacheService.IsValidKey(providedKey))
        {
            return Task.FromResult(AuthenticateResult.Fail("Invalid API key"));
        }

        var claims = new[] { new Claim(ClaimTypes.Name, isAdmin ? "Admin" : "ApiKeyClient") };
        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}

public static class ApiKeyAuthenticationExtensions
{
    public static AuthenticationBuilder AddApiKeyAuthentication(this AuthenticationBuilder builder)
    {
        return builder.AddScheme<ApiKeyAuthenticationOptions, ApiKeyAuthenticationHandler>(
            ApiKeyAuthenticationOptions.DefaultScheme, _ => { });
    }
}
