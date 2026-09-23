using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Infrastructure.Storage;

internal sealed record StorageRecoveryMutationGuard(
    Func<CancellationToken, Task> BeforeProviderFinalizationAsync,
    Func<StorageDbContext, StoragePlacementIntentRecord, CancellationToken, Task> BeforeSaveAsync);

internal sealed record StoragePlacementRecoverySnapshot(StoragePlacementRecoveryIdentity Identity,
    StorageStablePlacementState State, DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc,
    bool ReceiptPresent, bool WriteAcknowledged, bool ExternalDispatchConfirmedStopped, bool DeletionRequested,
    StoragePlacementRecoveryBlock StorageBlock);

public sealed partial class StorageStablePlacementService {
    internal async Task<StoragePlacementRecoverySnapshot?> FindRecoveryAsync(StoragePlacementIntentId intentId,
        CancellationToken cancellationToken) {
        await using var database = await factory.CreateDbContextAsync(cancellationToken);
        var row = await database.Set<StoragePlacementIntentRecord>().AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == intentId.Value, cancellationToken);
        if (row is null) {
            return null;
        }
        var storage = await database.Set<StorageCatalogRecord>().AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == row.StorageId, cancellationToken);
        return RecoverySnapshot(row, storage);
    }

    internal async Task<IReadOnlyList<StoragePlacementRecoverySnapshot>> ListRecoveryAsync(StoragePlacementRecoveryQuery request,
        CancellationToken cancellationToken) {
        await using var database = await factory.CreateDbContextAsync(cancellationToken);
        var query = database.Set<StoragePlacementIntentRecord>().AsNoTracking().Where(row =>
            row.State == StorageStablePlacementState.Prepared || row.State == StorageStablePlacementState.Dispatching ||
            row.State == StorageStablePlacementState.Uncertain || row.State == StorageStablePlacementState.Conflict);
        if (request.ProjectId is { } projectId) {
            query = query.Where(row => row.ProjectId == projectId);
        }
        if (request.StorageId is { } storageId) {
            query = query.Where(row => row.StorageId == storageId);
        }
        var rows = await query.OrderBy(row => row.CreatedAtUtc).ThenBy(row => row.Id)
            .Skip(request.Offset).Take(request.Take + 1).ToArrayAsync(cancellationToken);
        var storageIds = rows.Select(row => row.StorageId).Distinct().ToArray();
        var storages = await database.Set<StorageCatalogRecord>().AsNoTracking().Where(row => storageIds.Contains(row.Id))
            .ToDictionaryAsync(row => row.Id, cancellationToken);
        return rows.Select(row => RecoverySnapshot(row, storages.GetValueOrDefault(row.StorageId))).ToArray();
    }

    internal Task<StorageStablePlacementOutcome> ReconcileForRecoveryAsync(StoragePlacementIntentId intentId,
        StorageRecoveryMutationGuard guard, CancellationToken cancellationToken)
        => ReconcileCoreAsync(intentId, guard, cancellationToken);

    internal Task<StorageStablePlacementOutcome> VerifyTerminationForRecoveryAsync(StoragePlacementIntentId intentId,
        StorageRecoveryMutationGuard guard, CancellationToken cancellationToken)
        => RecordExternalTerminationCoreAsync(intentId, guard, cancellationToken);

    internal static async Task<StoragePlacementRecoveryIdentity> RequireOriginalRecoveryTargetAsync(StorageDbContext database,
        StoragePlacementIntentRecord row, CancellationToken cancellationToken) {
        IQueryable<StorageCatalogRecord> query;
        if (database.Database.IsNpgsql()) {
            query = database.Set<StorageCatalogRecord>().FromSqlInterpolated($"""
                SELECT * FROM "Storage_Catalog" WHERE "Id" = {row.StorageId} FOR SHARE
                """);
        } else if (database.Database.IsInMemory()) {
            query = database.Set<StorageCatalogRecord>().Where(item => item.Id == row.StorageId);
        } else {
            throw new StoragePlacementRecoveryException(StoragePlacementRecoveryFailure.Unavailable);
        }
        var storage = await query.AsNoTracking().SingleOrDefaultAsync(cancellationToken);
        var snapshot = RecoverySnapshot(row, storage);
        if (snapshot.StorageBlock != StoragePlacementRecoveryBlock.None || row.DeletionRequested) {
            throw new StoragePlacementRecoveryException(StoragePlacementRecoveryFailure.Blocked);
        }
        return snapshot.Identity;
    }

    private static StoragePlacementRecoverySnapshot RecoverySnapshot(StoragePlacementIntentRecord row, StorageCatalogRecord? storage) {
        var plan = ReadPlan(row);
        var block = storage is null ? StoragePlacementRecoveryBlock.OriginalStorageUnavailable
            : TargetFingerprint(storage.ToDriverInput()) != plan.TargetFingerprint ? StoragePlacementRecoveryBlock.OriginalTargetConflict
            : StoragePlacementRecoveryBlock.None;
        if (block == StoragePlacementRecoveryBlock.None) {
            try {
                ValidateCurrentCapabilities(storage!.ToSnapshot(), plan.PreviewRequired);
            } catch (InvalidOperationException) {
                block = StoragePlacementRecoveryBlock.OriginalStorageUnavailable;
            }
        }
        return new(new(new(row.Id), row.ProjectId, row.StorageId, plan.Reference.ProviderKind), row.State,
            row.CreatedAtUtc, row.UpdatedAtUtc, row.ReceiptJson.Length > 0, row.WriteAcknowledged,
            row.ExternalDispatchConfirmedStopped, row.DeletionRequested, block);
    }
}
