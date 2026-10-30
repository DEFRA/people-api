using System.Security.Cryptography;

namespace Defra_People_API.Services.Keys;

public class CryptoRandomGenerator : IRandomGenerator
{
    public byte[] GetRandomBytes(int length)
    {
        return RandomNumberGenerator.GetBytes(length);
    }
}