using CanDoItAll.Infrastructure.Storage;

namespace CanDoItAll.Tests.Unit;

public sealed class StorageJsonTests
{
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
}
