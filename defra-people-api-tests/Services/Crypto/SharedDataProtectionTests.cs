using System.Collections.Generic;
using Azure.Identity;
using Azure.Storage.Blobs;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;
using Defra_People_API.Services;

namespace defra.people_api_tests.Services.Crypto;

public class SharedDataProtectionTests
{
    private static Dictionary<string, string?> ValidSettings() => new()
    {
        ["DataProtection:Enabled"] = "true",
        ["DataProtection:BlobUri"] = "https://offlinetest.blob.core.windows.net/data-protection/keys.xml",
        ["DataProtection:KeyIdentifier"] = "https://offline-test.vault.azure.net/keys/data-protection",
        ["DataProtection:ApplicationName"] = "People.Api.Production",
        ["DataProtection:ManagedIdentityClientId"] = "33333333-3333-3333-3333-333333333333"
    };

    private static IConfiguration Configuration(Dictionary<string, string?> settings) =>
        new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

    [Theory]
    [InlineData("Enabled", "not-a-bool")]
    [InlineData("BlobUri", "")]
    [InlineData("BlobUri", "not-a-uri")]
    [InlineData("BlobUri", "http://offlinetest.blob.core.windows.net/data-protection/keys.xml")]
    [InlineData("BlobUri", "https://offlinetest.blob.core.windows.net/data-protection")]
    [InlineData("KeyIdentifier", "")]
    [InlineData("KeyIdentifier", "not-a-uri")]
    [InlineData("KeyIdentifier", "https://offline-test.vault.azure.net/secrets/data-protection")]
    [InlineData("ApplicationName", "")]
    [InlineData("ApplicationName", "   ")]
    [InlineData("ManagedIdentityClientId", "")]
    [InlineData("ManagedIdentityClientId", "00000000-0000-0000-0000-000000000000")]
    public void InvalidOrIncompleteEnablement_Throws(string key, string value)
    {
        var settings = ValidSettings();
        settings["DataProtection:" + key] = value;

        Assert.Throws<InvalidOperationException>(() =>
            new ServiceCollection().AddPeopleApiDataProtection(Configuration(settings)));
    }

    [Fact]
    public void DisabledConfiguration_FallsBackToDefaultDataProtection()
    {
        var settings = ValidSettings();
        settings["DataProtection:Enabled"] = "false";

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddPeopleApiDataProtection(Configuration(settings));
        using var provider = services.BuildServiceProvider();

        Assert.Null(provider.GetService<ManagedIdentityCredential>());
        Assert.Null(provider.GetService<BlobClient>());
        Assert.NotNull(provider.GetService<IDataProtectionProvider>());
    }

    [Fact]
    public void EnabledConfiguration_RegistersBlobClientAndManagedIdentityCredential()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddPeopleApiDataProtection(Configuration(ValidSettings()));
        using var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetRequiredService<ManagedIdentityCredential>());
        Assert.Equal(new Uri(ValidSettings()["DataProtection:BlobUri"]!), provider.GetRequiredService<BlobClient>().Uri);
        Assert.Equal(ValidSettings()["DataProtection:ApplicationName"],
            provider.GetRequiredService<IOptions<DataProtectionOptions>>().Value.ApplicationDiscriminator);

        var options = provider.GetRequiredService<IOptions<KeyManagementOptions>>().Value;
        Assert.NotNull(options.XmlRepository);
        Assert.NotNull(options.XmlEncryptor);
    }
}
