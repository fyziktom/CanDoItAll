using CanDoItAll.Infrastructure.Storage;

namespace CanDoItAll.Tests.Unit.Projects;

public sealed class ProjectPackageStorageCatalogSnapshotTests
{
    [Fact]
    public async Task Snapshot_exposes_detached_exact_metadata_and_retains_owner_configuration()
    {
        var storage = CreateStorage(isSystemDefault: false);
        var rule = new StorageRoutingRule
        {
            Id = Guid.NewGuid(),
            PreferredStorageId = storage.Id
        };
        var snapshot = new StorageProfileTransferCatalogSnapshot(
            [storage],
            [rule]);

        var listedStorages = await snapshot.ListAsync();
        var listedRules = await snapshot.ListRulesAsync();
        var resolved = await snapshot.GetAsync(storage.Id);

        Assert.Equal(storage.ToSnapshot(), Assert.Single(listedStorages));
        Assert.Equal(rule.ToSnapshot(), Assert.Single(listedRules));
        Assert.Equal(storage.ToSnapshot(), resolved);
        Assert.Equal(storage.ConfigJson, (await snapshot.GetDriverAsync(storage.Id))!.OriginalConfigurationJson);
        Assert.Same(rule, Assert.Single(await snapshot.ListRoutingRuleRecordsAsync()));
        Assert.Null(await snapshot.GetAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task Snapshot_resolves_only_the_system_default_file_system_bootstrap()
    {
        var nonDefaultFileSystem = CreateStorage(isSystemDefault: false);
        var systemDefaultFileSystem = CreateStorage(isSystemDefault: true);
        var snapshot = new StorageProfileTransferCatalogSnapshot(
            [nonDefaultFileSystem, systemDefaultFileSystem],
            []);

        var resolved = await snapshot.EnsureBootstrapFileSystemStorageAsync();

        Assert.Equal(systemDefaultFileSystem.ToDriverInput(), resolved);
    }

    [Fact]
    public async Task Snapshot_rejects_every_mutation()
    {
        var storage = CreateStorage(isSystemDefault: true);
        var rule = new StorageRoutingRule
        {
            Id = Guid.NewGuid(),
            PreferredStorageId = storage.Id
        };
        var snapshot = new StorageProfileTransferCatalogSnapshot(
            [storage],
            [rule]);

        var saveStorage = await Assert.ThrowsAsync<NotSupportedException>(
            () => snapshot.SaveAsync(StorageCatalogSaveRequest.FromSnapshot(storage.ToSnapshot())));
        var deleteStorage = await Assert.ThrowsAsync<NotSupportedException>(
            () => snapshot.DeleteAsync(storage.Id));
        var saveRule = await Assert.ThrowsAsync<NotSupportedException>(
            () => snapshot.SaveRuleAsync(StorageRoutingRuleSaveRequest.FromSnapshot(rule.ToSnapshot())));

        Assert.Contains("read-only", saveStorage.Message, StringComparison.Ordinal);
        Assert.Equal(saveStorage.Message, deleteStorage.Message);
        Assert.Equal(saveStorage.Message, saveRule.Message);
    }

    private static StorageCatalogRecord CreateStorage(bool isSystemDefault)
        => new()
        {
            Id = Guid.NewGuid(),
            Name = "Package target storage",
            ProviderKind = StorageProviderKind.FileSystem,
            IsEnabled = true,
            IsSystemDefault = isSystemDefault,
            EndpointOrRoot = "package-target",
            CapabilityMask = StorageCapability.Read | StorageCapability.Write,
            HealthStatus = StorageHealthStatus.Healthy,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };
}
