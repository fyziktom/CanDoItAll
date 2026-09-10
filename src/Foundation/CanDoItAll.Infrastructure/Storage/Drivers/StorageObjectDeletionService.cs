using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Infrastructure.Storage;

public sealed record StorageObjectDeletionFacts(
    StorageCatalogPlanningFact Storage,
    StorageCatalogPlanningFact? AuthoritativeBootstrapStorage);

public sealed class StorageObjectDeletionService(
    IDbContextFactory<StorageDbContext> dbContextFactory,
    IStorageDriverRegistry driverRegistry,
    FileSystemStoragePathPolicy fileSystemPathPolicy) {
    public async Task DeleteUnderCallerBindingGateAsync(StorageObjectReference reference,
        Func<StorageObjectDeletionFacts, CancellationToken, Task> validateCurrentReferences,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentNullException.ThrowIfNull(validateCurrentReferences);
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var storages = await dbContext.Set<StorageCatalogRecord>().AsNoTracking().ToArrayAsync(cancellationToken);
        var bootstrap = StorageBootstrapCatalogPolicy.ResolveAuthoritativeFileSystemStorage(
            storages, fileSystemPathPolicy.ResolveWorkspaceRootPath());
        StorageCatalogRecord storage;
        if (reference.StorageId.HasValue) {
            storage = storages.SingleOrDefault(item => item.Id == reference.StorageId.Value)
                ?? throw new InvalidOperationException($"Storage '{reference.StorageId.Value:D}' for managed project media was not found.");
        } else if (reference.ProviderKind == StorageProviderKind.FileSystem) {
            storage = bootstrap ?? throw new InvalidOperationException("Authoritative bootstrap filesystem storage was not found.");
        } else {
            throw new InvalidOperationException($"Managed project media for provider '{reference.ProviderKind}' requires a storage id.");
        }
        if (storage.ProviderKind != reference.ProviderKind) {
            throw new InvalidOperationException($"Storage '{storage.Id:D}' uses provider '{storage.ProviderKind}', but the managed media reference requires '{reference.ProviderKind}'.");
        }
        var facts = new StorageObjectDeletionFacts(
            StorageCatalogPlanningFact.FromCatalogRecord(storage, includeFtpAddressing: true),
            bootstrap is null ? null : StorageCatalogPlanningFact.FromCatalogRecord(bootstrap, includeFtpAddressing: false));
        await validateCurrentReferences(facts, cancellationToken);
        var driver = driverRegistry.Resolve(reference.ProviderKind);
        if (!driver.SupportedCapabilities.HasFlag(StorageCapability.Delete)) {
            throw new InvalidOperationException($"Storage provider '{reference.ProviderKind}' unexpectedly does not support managed project media deletion.");
        }
        if (!storage.IsEnabled) {
            throw new InvalidOperationException($"Storage '{storage.Id:D}' is disabled and cannot delete managed project media.");
        }
        if (storage.IsReadOnly || !storage.CapabilityMask.HasFlag(StorageCapability.Delete)) {
            throw new InvalidOperationException($"Storage '{storage.Id:D}' does not allow managed project media deletion.");
        }
        await driver.DeleteAsync(storage, reference, cancellationToken);
    }
}
