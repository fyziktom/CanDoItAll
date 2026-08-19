using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.Workbench;

namespace CanDoItAll.Tests.Unit.Projects;

public sealed class ProjectPackageStorageCatalogSnapshotTests
{
    [Fact]
    public async Task Snapshot_exposes_exact_storage_and_rule_instances()
    {
        var storage = CreateStorage(isSystemDefault: false);
        var rule = new StorageRoutingRule
        {
            Id = Guid.NewGuid(),
            PreferredStorageId = storage.Id
        };
        var snapshot = new ProjectPackageStorageCatalogSnapshot(
            [storage],
            [rule]);

        var listedStorages = await snapshot.ListAsync();
        var listedRules = await snapshot.ListRulesAsync();
        var resolved = await snapshot.GetAsync(storage.Id);

        Assert.Same(storage, Assert.Single(listedStorages));
        Assert.Same(rule, Assert.Single(listedRules));
        Assert.Same(storage, resolved);
        Assert.Null(await snapshot.GetAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task Snapshot_resolves_only_the_system_default_file_system_bootstrap()
    {
        var nonDefaultFileSystem = CreateStorage(isSystemDefault: false);
        var systemDefaultFileSystem = CreateStorage(isSystemDefault: true);
        var snapshot = new ProjectPackageStorageCatalogSnapshot(
            [nonDefaultFileSystem, systemDefaultFileSystem],
            []);

        var resolved = await snapshot.EnsureBootstrapFileSystemStorageAsync();

        Assert.Same(systemDefaultFileSystem, resolved);
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
        var snapshot = new ProjectPackageStorageCatalogSnapshot(
            [storage],
            [rule]);

        var saveStorage = await Assert.ThrowsAsync<NotSupportedException>(
            () => snapshot.SaveAsync(storage));
        var deleteStorage = await Assert.ThrowsAsync<NotSupportedException>(
            () => snapshot.DeleteAsync(storage.Id));
        var saveRule = await Assert.ThrowsAsync<NotSupportedException>(
            () => snapshot.SaveRuleAsync(rule));

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
