namespace Defra_People_API.Cache;

public interface IApiKeyBlobStore
{
    IReadOnlyList<string> ListKeyBlobNames();
    string DownloadText(string blobName);
    Task UploadTextAsync(string blobName, string content);
}
