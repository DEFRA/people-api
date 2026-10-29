namespace Defra_People_API.Services.Keys;

public interface IRandomGenerator
{
    byte[] GetRandomBytes(int length);
}
