using Azure.Storage.Blobs;

namespace Defra_People_API.Cache;

// Backs ApiKeyCacheService with Azure Blob Storage instead of local disk, so issued API keys
// survive restarts and are shared across replicas (containers have no durable local filesystem).
public class ApiKeyBlobStore : IApiKeyBlobStore
{
    private const string KeyBlobSuffix = ".key";
    private readonly BlobContainerClient _containerClient;

    public ApiKeyBlobStore(BlobContainerClient containerClient)
    {
        _containerClient = containerClient;
    }

    public IReadOnlyList<string> ListKeyBlobNames()
    {
        _containerClient.CreateIfNotExists();

        return _containerClient.GetBlobs()
            .Where(b => b.Name.EndsWith(KeyBlobSuffix, StringComparison.OrdinalIgnoreCase))
            .Select(b => b.Name)
            .ToList();
    }

    public string DownloadText(string blobName) =>
        _containerClient.GetBlobClient(blobName).DownloadContent().Value.Content.ToString();

    public async Task UploadTextAsync(string blobName, string content)
    {
        await _containerClient.CreateIfNotExistsAsync();
        await _containerClient.GetBlobClient(blobName).UploadAsync(BinaryData.FromString(content), overwrite: false);
    }
}
