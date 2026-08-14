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
            @"proof\reports\alpha.pdf",
            "application/pdf",
            "alpha.pdf",
            2048);

        Assert.Equal(StorageProviderKind.FileSystem, reference.ProviderKind);
        Assert.Equal(StorageLocatorKind.RelativePath, reference.LocatorKind);
        Assert.Equal("proof/reports/alpha.pdf", reference.Locator);
        Assert.Equal("/managed-files/proof/reports/alpha.pdf", reference.Route);
        Assert.Equal(2048, reference.ContentLength);
        Assert.Equal(StorageObjectReference.CurrentFormatVersion, reference.FormatVersion);
    }

    [Fact]
    public void CreateLegacyManagedFileReference_rejects_a_leading_separator()
    {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            StorageJson.CreateLegacyManagedFileReference(
                @"\proof\reports\alpha.pdf",
                "application/pdf",
                "alpha.pdf"));

        Assert.Contains("relative", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ParseReference_migrates_legacy_separators_to_a_versioned_logical_locator()
    {
        const string legacyJson = """
            {
              "storageId": null,
              "providerKind": "fileSystem",
              "locatorKind": "relativePath",
              "locator": "managed-files\\reports\\alpha.pdf",
              "displayName": "alpha.pdf"
            }
            """;

        StorageObjectReference migrated = Assert.IsType<StorageObjectReference>(
            StorageJson.ParseReference(legacyJson));
        string persisted = StorageJson.SerializeReference(migrated);

        Assert.Equal("managed-files/reports/alpha.pdf", migrated.Locator);
        Assert.Equal(StorageObjectReference.CurrentFormatVersion, migrated.FormatVersion);
        Assert.Contains("\"formatVersion\":2", persisted, StringComparison.Ordinal);
        Assert.DoesNotContain("managed-files\\\\reports", persisted, StringComparison.Ordinal);
    }

    [Fact]
    public void ParseReference_rejects_traversal_in_a_legacy_logical_locator()
    {
        const string legacyJson = """
            {
              "providerKind": "fileSystem",
              "locatorKind": "relativePath",
              "locator": "managed-files\\..\\outside.txt"
            }
            """;

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            StorageJson.ParseReference(legacyJson));

        Assert.Contains("non-traversing", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [MemberData(nameof(RootedOrPhysicalLogicalLocatorCases))]
    public void SerializeReference_rejects_rooted_or_physical_logical_locators(
        StorageLocatorKind locatorKind,
        string locator)
    {
        var reference = new StorageObjectReference(
            null,
            locatorKind == StorageLocatorKind.RelativePath
                ? StorageProviderKind.FileSystem
                : StorageProviderKind.Ftp,
            locatorKind,
            locator);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            StorageJson.SerializeReference(reference));

        Assert.Contains("relative", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [MemberData(nameof(RootedOrPhysicalLogicalLocatorCases))]
    public void ParseReference_rejects_rooted_or_physical_legacy_logical_locators(
        StorageLocatorKind locatorKind,
        string locator)
    {
        string json = System.Text.Json.JsonSerializer.Serialize(new
        {
            formatVersion = 1,
            providerKind = locatorKind == StorageLocatorKind.RelativePath
                ? StorageProviderKind.FileSystem
                : StorageProviderKind.Ftp,
            locatorKind,
            locator
        });

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            StorageJson.ParseReference(json));

        Assert.Contains("relative", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("folder//file.txt")]
    [InlineData("folder/./file.txt")]
    [InlineData("folder/../file.txt")]
    public void SerializeReference_rejects_empty_dot_or_traversal_segments(string locator)
    {
        var reference = new StorageObjectReference(
            null,
            StorageProviderKind.FileSystem,
            StorageLocatorKind.RelativePath,
            locator);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            StorageJson.SerializeReference(reference));

        Assert.Contains("non-traversing", exception.Message, StringComparison.OrdinalIgnoreCase);
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

    public static IEnumerable<object[]> RootedOrPhysicalLogicalLocatorCases()
    {
        string[] locators =
        [
            "/etc/passwd",
            @"\Windows\System32",
            @"C:\Windows\System32",
            "C:relative",
            @"\\server\share\file.txt",
            "//server/share/file.txt",
            "https://files.example.test/object"
        ];
        foreach (StorageLocatorKind locatorKind in new[]
                 {
                     StorageLocatorKind.RelativePath,
                     StorageLocatorKind.RemotePath
                 })
        {
            foreach (string locator in locators)
            {
                yield return [locatorKind, locator];
            }
        }
    }
}
