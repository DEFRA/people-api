using Defra_People_API.Entities;

namespace Defra_People_API.Services;

public interface IAuthConfigProvider
{
    AuthenticationConfig GetAuthConfig();
}