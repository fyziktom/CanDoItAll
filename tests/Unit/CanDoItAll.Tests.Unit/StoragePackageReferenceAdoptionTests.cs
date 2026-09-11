using CanDoItAll.Infrastructure.Storage;

namespace CanDoItAll.Tests.Unit;

public sealed class StoragePackageReferenceAdoptionTests {
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Adoption_preserves_source_evidence_and_creates_an_independent_target_reference(int sourceVersion) {
        var source = Source() with { FormatVersion = sourceVersion, PlacementIntentId = sourceVersion == 3 ? Guid.NewGuid() : null };
        var sourceProfile = Guid.NewGuid();
        var package = Guid.NewGuid();
        var targetStorage = Guid.NewGuid();
        var adopted = StoragePackageReferenceAdoption.AdoptImmutable(source, targetStorage, 17, sourceProfile, package);
        Assert.Equal(targetStorage, adopted.StorageId);
        Assert.Equal(source.Locator, adopted.Locator);
        Assert.Null(adopted.PlacementIntentId);
        Assert.Equal(StorageObjectReference.CurrentFormatVersion, adopted.FormatVersion);
        Assert.Equal(string.Empty, adopted.Route);
        Assert.Equal("{}", adopted.MetadataJson);
        Assert.Same(source, adopted.ImportedHistory!.SourceReference);
        Assert.Equal(sourceProfile, adopted.ImportedHistory.Origin.SourceProfileId);
        Assert.Equal(package, adopted.ImportedHistory.Origin.TransferId);
        Assert.Equal(adopted, StorageJson.ParseReference(StorageJson.SerializeReference(adopted)));
    }

    [Fact]
    public void Manifest_length_replaces_old_metadata_while_history_keeps_the_original_reference() {
        var source = Source() with { ContentLength = 99 };
        var adopted = StoragePackageReferenceAdoption.AdoptImmutable(source, Guid.NewGuid(), 17, Guid.NewGuid(), Guid.NewGuid());
        Assert.Equal(17, adopted.ContentLength);
        Assert.Equal(99, adopted.ImportedHistory!.SourceReference.ContentLength);
    }

    [Fact]
    public void Repeated_import_keeps_each_source_reference_and_profile_without_restamping_old_intents() {
        var original = Source();
        var firstProfile = Guid.NewGuid();
        var first = StoragePackageReferenceAdoption.AdoptImmutable(original, Guid.NewGuid(), 17, firstProfile, Guid.NewGuid());
        var secondProfile = Guid.NewGuid();
        var second = StoragePackageReferenceAdoption.AdoptImmutable(first, Guid.NewGuid(), 17, secondProfile, Guid.NewGuid());
        var restored = Assert.IsType<StorageObjectReference>(StorageJson.ParseReference(StorageJson.SerializeReference(second)));
        Assert.Null(restored.PlacementIntentId);
        Assert.Equal(secondProfile, restored.ImportedHistory!.Origin.SourceProfileId);
        Assert.Equal(firstProfile, restored.ImportedHistory.SourceReference.ImportedHistory!.Origin.SourceProfileId);
        Assert.Equal(original, restored.ImportedHistory.SourceReference.ImportedHistory.SourceReference);
    }

    [Theory]
    [InlineData(InvalidEvidence.Provider)]
    [InlineData(InvalidEvidence.LocatorKind)]
    [InlineData(InvalidEvidence.TargetStorage)]
    [InlineData(InvalidEvidence.Length)]
    [InlineData(InvalidEvidence.MissingIntent)]
    [InlineData(InvalidEvidence.EmptyIntent)]
    [InlineData(InvalidEvidence.Version)]
    public void Invalid_source_or_target_identity_is_not_silently_repaired(InvalidEvidence evidence) {
        var source = evidence switch {
            InvalidEvidence.Provider => Source() with { ProviderKind = StorageProviderKind.Ftp },
            InvalidEvidence.LocatorKind => Source() with { LocatorKind = StorageLocatorKind.RemotePath },
            InvalidEvidence.MissingIntent => Source() with { PlacementIntentId = null },
            InvalidEvidence.EmptyIntent => Source() with { PlacementIntentId = Guid.Empty },
            InvalidEvidence.Version => Source() with { FormatVersion = StorageObjectReference.MaximumSupportedFormatVersion + 1 },
            _ => Source()
        };
        Assert.Throws<InvalidDataException>(() => StoragePackageReferenceAdoption.AdoptImmutable(source,
            evidence == InvalidEvidence.TargetStorage ? Guid.Empty : Guid.NewGuid(),
            evidence == InvalidEvidence.Length ? -1 : 17, Guid.NewGuid(), Guid.NewGuid()));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Source_profile_and_package_identity_are_required(bool missingProfile) {
        Assert.Throws<ArgumentException>(() => StoragePackageReferenceAdoption.AdoptImmutable(Source(), Guid.NewGuid(), 17,
            missingProfile ? Guid.Empty : Guid.NewGuid(), missingProfile ? Guid.NewGuid() : Guid.Empty));
    }

    private static StorageObjectReference Source() => new(Guid.NewGuid(), StorageProviderKind.Ipfs,
        StorageLocatorKind.ContentAddress, "bafy-retained-source", "original.txt", "text/plain", 17,
        "https://source.example.test/ipfs/bafy-retained-source", "{\"source\":\"original metadata\"}") {
        FormatVersion = StorageObjectReference.StablePlacementFormatVersion, PlacementIntentId = Guid.NewGuid()
    };

    public enum InvalidEvidence { Provider, LocatorKind, TargetStorage, Length, MissingIntent, EmptyIntent, Version }
}
