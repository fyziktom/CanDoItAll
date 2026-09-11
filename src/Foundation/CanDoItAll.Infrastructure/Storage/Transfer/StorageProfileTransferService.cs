using System.Security.Cryptography;
using System.Text.Json;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Infrastructure.Storage;

public sealed record StorageProfileTransferSession(Guid SessionId, Guid ProfileId, bool IsTarget);

public sealed record StorageTransferPlacedObject(Guid StorageId, StorageProviderKind ProviderKind,
    StorageWriteResult WriteResult, string RelativePath, string Route);

public sealed class StorageProfileTransferService(DatabaseTransferOperationRunner operations,
    IStorageDriverRegistry storageDrivers, IStorageTransferProvenancePolicy provenancePolicy,
    FileSystemStoragePathPolicy fileSystemPathPolicy, IClock clock, ILogger<StoragePlacementService> placementLogger) {
    private readonly AsyncLocal<Frame?> current = new();

    public async Task<TResult> WithSourceAsync<TResult>(DatabaseTransferProfileSession database, ResolvedDatabaseProfile profile,
        Func<StorageProfileTransferSession, CancellationToken, Task<TResult>> operation, CancellationToken cancellationToken = default) {
        if (database.ProfileId != profile.Profile.Id) {
            throw new InvalidOperationException("The source Storage snapshot does not match the database transfer profile.");
        }
        await using var context = await operations.CreateOwnerAsync<StorageDbContext>(database, static options => new StorageDbContext(options), cancellationToken);
        var storages = await context.Set<StorageCatalogRecord>().AsNoTracking().ToListAsync(cancellationToken);
        return await WithPlanAsync(profile, new(storages, [], [], null, null, string.Empty), false, operation, cancellationToken);
    }

    public async Task<TResult> WithTargetAsync<TResult>(ResolvedDatabaseProfile profile,
        Func<StorageProfileTransferSession, CancellationToken, Task<TResult>> operation, CancellationToken cancellationToken = default) {
        var snapshot = await operations.RunIndependentAsync(profile, async (database, token) => {
            await using var context = await operations.CreateOwnerAsync<StorageDbContext>(database, static options => new StorageDbContext(options), token);
            return await LoadCatalogAsync(context, token);
        }, cancellationToken);
        var plan = BuildPlan(profile, snapshot.Storages, snapshot.Rules);
        return await WithPlanAsync(profile, plan, true, operation, cancellationToken);
    }

    private async Task<TResult> WithPlanAsync<TResult>(ResolvedDatabaseProfile profile, TargetStoragePlan plan, bool isTarget,
        Func<StorageProfileTransferSession, CancellationToken, Task<TResult>> operation, CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(operation);
        if (current.Value is not null) {
            throw new InvalidOperationException("A Storage package plan is already active on this execution path.");
        }
        var request = new StorageProfileTransferSession(Guid.NewGuid(), profile.Profile.Id, isTarget);
        var frame = new Frame(request, profile, plan);
        current.Value = frame;
        try {
            return await operation(request, cancellationToken);
        } finally {
            frame.Retired = true;
            current.Value = null;
        }
    }

    public async Task<Stream> OpenSourceReadAsync(StorageProfileTransferSession session, StorageObjectReference reference,
        Guid bindingId, CancellationToken cancellationToken = default) {
        var frame = Require(session, false);
        var storage = ResolveSourceStorage(reference, frame.StoragesById, frame.BootstrapStorage.Value, bindingId);
        ValidateReadableStorage(storage, reference, bindingId);
        provenancePolicy.ValidateExport(reference,
            StorageCatalogPlanningFact.FromCatalogRecord(storage, reference.ProviderKind == StorageProviderKind.Ftp), bindingId);
        var stream = await ResolveReadableDriver(reference.ProviderKind).OpenReadAsync(storage.ToDriverInput(), reference, cancellationToken);
        Require(session, false);
        return stream;
    }

    public async Task<StorageTransferPlacedObject> PlaceAsync(StorageProfileTransferSession session, StoragePlacementRequest request,
        CancellationToken cancellationToken = default) {
        var plan = Require(session, true).Plan;
        var catalog = new StorageProfileTransferCatalogSnapshot(plan.PlacementStorages, plan.Rules);
        var placement = await new StoragePlacementService(catalog, new DefaultStorageRoutingService(catalog, catalog.ListRoutingRuleRecordsAsync), storageDrivers, placementLogger)
            .PlaceAsync(request, cancellationToken);
        Require(session, true);
        return new(placement.Storage.Id, placement.Storage.ProviderKind, placement.WriteResult, placement.RelativePath, placement.Route);
    }

    public StorageObjectReference AdoptImmutable(StorageProfileTransferSession session, StorageObjectReference source,
        long contentLength, Guid sourceProfileId, Guid packageId) {
        var storage = Require(session, true).Plan.Storages.Where(row => row.ProviderKind == StorageProviderKind.Ipfs && row.IsEnabled &&
            row.CapabilityMask.HasFlag(StorageCapability.Read) && row.HealthStatus != StorageHealthStatus.Unavailable)
            .OrderByDescending(row => row.IsSystemDefault).ThenBy(row => row.DisplayOrder).ThenBy(row => row.Id).FirstOrDefault()
            ?? throw new InvalidDataException("The package contains immutable IPFS assets, but the target profile has no readable IPFS storage catalog entry. Configure the target provider before importing.");
        _ = ResolveReadableDriver(StorageProviderKind.Ipfs);
        return StoragePackageReferenceAdoption.AdoptImmutable(source, storage.Id, contentLength, sourceProfileId, packageId);
    }

    public Task<Stream> OpenTargetReadAsync(StorageProfileTransferSession session, StorageObjectReference reference,
        CancellationToken cancellationToken = default) {
        var storage = ResolveTarget(session, reference);
        return ResolveReadableDriver(reference.ProviderKind).OpenReadAsync(storage.ToDriverInput(), reference, cancellationToken);
    }

    public StorageObjectReference Stamp(StorageProfileTransferSession session, StorageObjectReference reference, string requestedPath)
        => provenancePolicy.StampImport(reference, requestedPath,
            StorageCatalogPlanningFact.FromCatalogRecord(ResolveTarget(session, reference), reference.ProviderKind == StorageProviderKind.Ftp));

    public bool CanDelete(StorageProfileTransferSession session, Guid storageId, StorageProviderKind providerKind, StorageObjectReference reference) {
        _ = ResolveSelectedTarget(session, storageId, providerKind);
        return storageDrivers.TryResolve(reference.ProviderKind, out var driver) && driver.SupportedCapabilities.HasFlag(StorageCapability.Delete);
    }

    public Task DeleteStagedAsync(StorageProfileTransferSession session, Guid storageId, StorageProviderKind providerKind,
        StorageObjectReference reference, CancellationToken cancellationToken)
        => storageDrivers.Resolve(reference.ProviderKind).DeleteAsync(ResolveSelectedTarget(session, storageId, providerKind).ToDriverInput(), reference, cancellationToken);

    public async Task ValidateAndPersistTargetAsync(DatabaseTransferProfileSession database, StorageProfileTransferSession session,
        CancellationToken cancellationToken = default) {
        var frame = Require(session, true);
        if (database.ProfileId != session.ProfileId || database.Mode != DatabaseTransferProfileMode.Serializable) {
            throw new InvalidOperationException("Storage catalog staging requires the exact Serializable target transfer session.");
        }
        await using var context = await operations.CreateOwnerAsync<StorageDbContext>(database, static options => new StorageDbContext(options), cancellationToken);
        var current = await LoadCatalogAsync(context, cancellationToken);
        if (!string.Equals(ComputeStorageCatalogFingerprint(current.Storages, current.Rules), frame.Plan.CatalogFingerprint, StringComparison.Ordinal)) {
            throw new InvalidOperationException("The inactive target storage catalog changed while project assets were being staged. Import was stopped before project data changed.");
        }
        if (frame.Plan.PendingStorage is not null) {
            context.Add(frame.Plan.PendingStorage);
            if (frame.Plan.PendingRule is not null) {
                context.Add(frame.Plan.PendingRule);
            }
            await context.SaveChangesAsync(cancellationToken);
        }
    }

    private static async Task<(List<StorageCatalogRecord> Storages, List<StorageRoutingRule> Rules)> LoadCatalogAsync(StorageDbContext context,
        CancellationToken cancellationToken) => (
            await context.Set<StorageCatalogRecord>().AsNoTracking().OrderBy(row => row.DisplayOrder).ThenBy(row => row.Id).ToListAsync(cancellationToken),
            await context.Set<StorageRoutingRule>().AsNoTracking().Where(row => row.IsEnabled).OrderBy(row => row.Priority).ThenBy(row => row.Id).ToListAsync(cancellationToken));

    private StorageCatalogRecord ResolveTarget(StorageProfileTransferSession session, StorageObjectReference reference) {
        var storage = Require(session, true).Plan.Storages.SingleOrDefault(row => row.Id == reference.StorageId && row.ProviderKind == reference.ProviderKind);
        return storage ?? throw new InvalidDataException("The staged object does not belong to the captured target Storage plan.");
    }

    private StorageCatalogRecord ResolveSelectedTarget(StorageProfileTransferSession session, Guid storageId, StorageProviderKind providerKind)
        => Require(session, true).Plan.Storages.SingleOrDefault(row => row.Id == storageId && row.ProviderKind == providerKind)
            ?? throw new InvalidDataException("The staged storage identity does not belong to the captured target plan.");

    private IStorageDriver ResolveReadableDriver(StorageProviderKind kind) {
        if (!storageDrivers.TryResolve(kind, out var driver) || driver.ProviderKind != kind || !driver.SupportedCapabilities.HasFlag(StorageCapability.Read)) {
            throw new InvalidDataException($"No readable storage driver is registered for provider '{kind}'.");
        }
        return driver;
    }

    private Frame Require(StorageProfileTransferSession request, bool isTarget) {
        var frame = current.Value;
        if (frame is null || frame.Retired || frame.Request != request || request.IsTarget != isTarget) {
            throw new InvalidOperationException("The Storage package plan is absent, mismatched or disposed.");
        }
        return frame;
    }

    private TargetStoragePlan BuildPlan(ResolvedDatabaseProfile targetProfile, List<StorageCatalogRecord> storages,
        List<StorageRoutingRule> rules) {
        var catalogFingerprint = ComputeStorageCatalogFingerprint(storages, rules);
        StorageCatalogRecord? pendingStorage = null;
        StorageRoutingRule? pendingRule = null;
        if (storages.Count == 0) {
            if (string.IsNullOrWhiteSpace(targetProfile.Profile.Storage.WorkspaceRoot)) {
                throw new InvalidDataException(
                    "The inactive target profile has no storage catalog and no workspace root for a bootstrap storage.");
            }

            var now = clock.GetUtcNow();
            var workspaceRoot = Path.GetFullPath(
                targetProfile.Profile.Storage.WorkspaceRoot);
            pendingStorage = new StorageCatalogRecord {
                Id = Guid.NewGuid(),
                Name = "Workspace file system",
                ProviderKind = StorageProviderKind.FileSystem,
                IsEnabled = true,
                IsSystemDefault = true,
                ConnectionMode = StorageConnectionMode.Local,
                EndpointOrRoot = workspaceRoot,
                CapabilityMask =
                    StorageCapability.Read |
                    StorageCapability.Write |
                    StorageCapability.Delete |
                    StorageCapability.Download |
                    StorageCapability.InlinePreview |
                    StorageCapability.OpenLocally |
                    StorageCapability.MutableUpdate |
                    StorageCapability.BatchFolderUpload |
                    StorageCapability.BatchTransfer |
                    StorageCapability.ConnectionTest,
                HealthStatus = StorageHealthStatus.Healthy,
                LastHealthMessage = "Bootstrap workspace storage created by project package import",
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };
            StorageCatalogHostBindingPolicy.BindCurrent(
                pendingStorage,
                workspaceRoot,
                now);
            pendingRule = new StorageRoutingRule {
                Id = Guid.NewGuid(),
                Name = "Workspace editable fallback",
                IsEnabled = true,
                Priority = 1000,
                ScopeKind = StorageRoutingScopeKind.Workspace,
                UsagePurpose = StorageUsagePurpose.Unknown,
                ContentKind = StorageContentKind.Unknown,
                RequiredCapabilities = StorageCapability.Write,
                PreferredStorageId = pendingStorage.Id,
                Reason = "Bootstrap filesystem fallback for imported project assets.",
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };
            storages.Add(pendingStorage);
            rules.Add(pendingRule);
        }

        foreach (var storage in storages.Where(storage =>
                     storage.ProviderKind == StorageProviderKind.FileSystem &&
                     storage.IsEnabled &&
                     !storage.IsReadOnly)) {
            if (string.IsNullOrWhiteSpace(storage.EndpointOrRoot)) {
                throw new InvalidDataException(
                    $"Target filesystem storage '{storage.Name}' does not have an explicit root for inactive-profile import.");
            }

            _ = fileSystemPathPolicy.ResolveReparseSafeFullPath(
                Path.GetFullPath(storage.EndpointOrRoot));
        }

        var placementStorages = storages
            .Where(IsUsablePlacementStorage)
            .ToList();
        if (placementStorages.Count == 0) {
            throw new InvalidDataException(
                "The inactive target profile has no enabled storage that can write and verify imported project assets.");
        }

        return new TargetStoragePlan(
            storages,
            placementStorages,
            rules,
            pendingStorage,
            pendingRule,
            catalogFingerprint);
    }

    private bool IsUsablePlacementStorage(StorageCatalogRecord storage) {
        const StorageCapability required =
            StorageCapability.Read |
            StorageCapability.Write |
            StorageCapability.Delete;
        return storage.ProviderKind is StorageProviderKind.FileSystem or StorageProviderKind.Ftp &&
               storage.IsEnabled &&
               !storage.IsReadOnly &&
               storage.HealthStatus != StorageHealthStatus.Unavailable &&
               (storage.CapabilityMask & required) == required &&
               storageDrivers.TryResolve(storage.ProviderKind, out var driver) &&
               (driver.SupportedCapabilities & required) == required;
    }

    private static string ComputeStorageCatalogFingerprint(
        IReadOnlyList<StorageCatalogRecord> storages,
        IReadOnlyList<StorageRoutingRule> rules) {
        var snapshot = new {
            Storages = storages
                .OrderBy(storage => storage.Id)
                .Select(storage => new {
                    storage.Id,
                    storage.Name,
                    storage.ProviderKind,
                    storage.IsEnabled,
                    storage.IsSystemDefault,
                    storage.IsReadOnly,
                    storage.DisplayOrder,
                    storage.EndpointOrRoot,
                    storage.ConfigJson,
                    storage.CapabilityMask,
                    storage.HealthStatus,
                    storage.CredentialSecretId
                }),
            Rules = rules
                .OrderBy(rule => rule.Id)
                .Select(rule => new {
                    rule.Id,
                    rule.IsEnabled,
                    rule.Priority,
                    rule.ScopeKind,
                    rule.ProjectId,
                    rule.NodeKey,
                    rule.UsagePurpose,
                    rule.ContentKind,
                    rule.MimePattern,
                    rule.MinimumContentLength,
                    rule.MaximumContentLength,
                    rule.EditIntent,
                    rule.PreviewRequired,
                    rule.PublishIntent,
                    rule.RequiredCapabilities,
                    rule.PreferredStorageId,
                    rule.AlternativeStorageIdsJson
                })
        };
        var json = JsonSerializer.Serialize(snapshot);
        return Convert.ToHexStringLower(SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(json)));
    }

    private static StorageCatalogRecord ResolveSourceStorage(
        StorageObjectReference reference,
        IReadOnlyDictionary<Guid, StorageCatalogRecord> storagesById,
        StorageCatalogRecord? bootstrapStorage,
        Guid bindingId) {
        StorageCatalogRecord? storage = null;
        if (reference.StorageId.HasValue) {
            storagesById.TryGetValue(reference.StorageId.Value, out storage);
        } else if (reference.ProviderKind == StorageProviderKind.FileSystem) {
            storage = bootstrapStorage;
        }

        if (storage is null) {
            throw new InvalidDataException(
                $"Project media binding '{bindingId:D}' points to a mutable storage catalog entry that does not exist.");
        }

        if (storage.ProviderKind != reference.ProviderKind) {
            throw new InvalidDataException(
                $"Project media binding '{bindingId:D}' storage provider does not match its catalog entry.");
        }

        return storage;
    }

    private static void ValidateReadableStorage(
        StorageCatalogRecord storage,
        StorageObjectReference reference,
        Guid bindingId) {
        if (!storage.IsEnabled ||
            storage.HealthStatus == StorageHealthStatus.Unavailable ||
            !storage.CapabilityMask.HasFlag(StorageCapability.Read)) {
            throw new InvalidDataException(
                $"Project media binding '{bindingId:D}' storage is not available for package reads.");
        }

        if (reference.ProviderKind == StorageProviderKind.FileSystem &&
            string.IsNullOrWhiteSpace(storage.EndpointOrRoot)) {
            throw new InvalidDataException(
                $"Project media binding '{bindingId:D}' filesystem storage has no authoritative root.");
        }
    }

    private sealed record TargetStoragePlan(IReadOnlyList<StorageCatalogRecord> Storages, IReadOnlyList<StorageCatalogRecord> PlacementStorages,
        IReadOnlyList<StorageRoutingRule> Rules, StorageCatalogRecord? PendingStorage, StorageRoutingRule? PendingRule, string CatalogFingerprint);

    private sealed class Frame(StorageProfileTransferSession request, ResolvedDatabaseProfile profile, TargetStoragePlan plan) {
        public StorageProfileTransferSession Request { get; } = request;
        public ResolvedDatabaseProfile Profile { get; } = profile;
        public TargetStoragePlan Plan { get; } = plan;
        public IReadOnlyDictionary<Guid, StorageCatalogRecord> StoragesById { get; } = plan.Storages.ToDictionary(row => row.Id);
        public Lazy<StorageCatalogRecord?> BootstrapStorage { get; } = new(() => request.IsTarget || string.IsNullOrWhiteSpace(profile.Profile.Storage.WorkspaceRoot)
            ? null : StorageBootstrapCatalogPolicy.ResolveAuthoritativeFileSystemStorage(plan.Storages, profile.Profile.Storage.WorkspaceRoot));
        public bool Retired { get; set; }
    }
}
