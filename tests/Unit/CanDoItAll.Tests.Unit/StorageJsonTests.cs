using CanDoItAll.Infrastructure.Storage;

namespace CanDoItAll.Tests.Unit;

public sealed class StorageJsonTests
{
    [Fact]
    public void ParseProviderConfiguration_RejectsUnboundedInput()
    {
        string json = "{\"metadataJson\":\"" +
            new string('x', StorageJson.MaximumProviderConfigurationJsonLength) +
            "\"}";

        StorageBrowseException exception = Assert.Throws<StorageBrowseException>(() =>
            StorageJson.ParseProviderConfiguration(json));

        Assert.Equal(StorageBrowseErrorCode.InvalidConfiguration, exception.Error.Code);
    }

    [Fact]
    public void CreateLegacyManagedFileReference_normalizes_the_relative_path_and_route()
    {
        var reference = StorageJson.CreateLegacyManagedFileReference(
            @"\proof\reports\alpha.pdf",
            "application/pdf",
            "alpha.pdf",
            2048);

        Assert.Equal(StorageProviderKind.FileSystem, reference.ProviderKind);
        Assert.Equal(StorageLocatorKind.RelativePath, reference.LocatorKind);
        Assert.Equal("proof/reports/alpha.pdf", reference.Locator);
        Assert.Equal("/managed-files/proof/reports/alpha.pdf", reference.Route);
        Assert.Equal(2048, reference.ContentLength);
    }

    [Fact]
    public void EncodeReferenceToken_round_trips_the_storage_reference()
    {
        var original = new StorageObjectReference(
            Guid.NewGuid(),
            StorageProviderKind.Ipfs,
            StorageLocatorKind.ContentAddress,
            "bafy-test-cid",
            "proof.png",
            "image/png",
            512,
            "https://gateway.example/ipfs/bafy-test-cid",
            "{\"pinned\":true}");

        var token = StorageJson.EncodeReferenceToken(original);
        var success = StorageJson.TryDecodeReferenceToken(token, out var restored);

        Assert.True(success);
        Assert.Equal(original, restored);
    }

    [Fact]
    public void ParseProviderConfiguration_LegacyJson_DefaultsBrowseCacheToDisabled()
    {
        StorageProviderConfiguration configuration = StorageJson.ParseProviderConfiguration(
            """{"gatewayBaseUrl":"https://gateway.example.test"}""");

        Assert.False(configuration.BrowseCache.Enabled);
        Assert.Equal(StorageBrowseCacheMode.Disabled, configuration.BrowseCache.Mode);
        Assert.Equal(TimeSpan.Zero, configuration.BrowseCache.TimeToLive);
    }

    [Fact]
    public void ProviderConfiguration_MemoryCache_RoundTripsTypedSettings()
    {
        var original = new StorageProviderConfiguration
        {
            BrowseCache = new StorageBrowseCacheSettings
            {
                Enabled = true,
                Mode = StorageBrowseCacheMode.Memory,
                TimeToLive = TimeSpan.FromMinutes(2),
                MaximumLifetime = TimeSpan.FromMinutes(10),
                MaximumPageSize = 75,
                MaximumItems = 1_500,
                AllowForceRefresh = true,
                ImmutableVersionPolicy = StorageBrowseImmutableVersionPolicy.RequireProviderVerifiedVersion
            }
        };

        string json = StorageJson.SerializeProviderConfiguration(original);
        StorageProviderConfiguration restored = StorageJson.ParseProviderConfiguration(json);

        Assert.True(restored.BrowseCache.Enabled);
        Assert.Equal(StorageBrowseCacheMode.Memory, restored.BrowseCache.Mode);
        Assert.Equal(TimeSpan.FromMinutes(2), restored.BrowseCache.TimeToLive);
        Assert.Equal(75, restored.BrowseCache.MaximumPageSize);
        Assert.Equal(1_500, restored.BrowseCache.MaximumItems);
        Assert.Equal(
            StorageBrowseImmutableVersionPolicy.RequireProviderVerifiedVersion,
            restored.BrowseCache.ImmutableVersionPolicy);
    }

    [Fact]
    public void ParseProviderConfiguration_HybridCacheWithoutDurableRevision_ThrowsTypedConfigurationError()
    {
        const string json = """
            {
              "browseCache": {
                "enabled": true,
                "mode": "hybrid",
                "timeToLive": "00:01:00",
                "maximumLifetime": "00:05:00"
              }
            }
            """;

        StorageBrowseException exception = Assert.Throws<StorageBrowseException>(() =>
            StorageJson.ParseProviderConfiguration(json));

        Assert.Equal(StorageBrowseErrorCode.InvalidConfiguration, exception.Error.Code);
    }
}
