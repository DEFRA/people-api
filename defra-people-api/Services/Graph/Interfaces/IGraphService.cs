using Microsoft.Graph.Models;

namespace Defra_People_API.Services.Graph;

public interface IGraphService
{
    Task<IEnumerable<User>> GetUserBySID(string id, bool getManager);
    Task<IEnumerable<User>> GetUserBySAMAccount(string id, bool getManager);
    Task<IEnumerable<User>> GetUserByMailNickname(string id, bool getManager);
}
