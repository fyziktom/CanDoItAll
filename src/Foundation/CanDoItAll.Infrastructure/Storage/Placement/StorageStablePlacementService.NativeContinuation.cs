using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Infrastructure.Storage;

public sealed partial class StorageStablePlacementService {
    public async Task<StorageStablePlacementReceipt> ReadCompletedForNativeContinuationAsync(StoragePlacementIntentId intentId,
        StoragePlacementRequest request, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Content);
        if (intentId.Value == Guid.Empty || request.Content.Length > MaximumContentBytes || string.IsNullOrWhiteSpace(request.FileName)) {
            throw new ArgumentException("Native continuation requires the original bounded Storage intent and content.");
        }
        var fingerprint = Fingerprint(request);
        await using var database = await factory.CreateDbContextAsync(cancellationToken);
        var row = await database.Set<StoragePlacementIntentRecord>().AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == intentId.Value, cancellationToken)
            ?? throw new StoragePlacementRecoveryException(StoragePlacementRecoveryFailure.NotFound);
        return await RequireCompletedNativeReceiptAsync(database, row, fingerprint, cancellationToken);
    }

    public async Task RequireCompletedForNativeMutationAsync(StorageStablePlacementReceipt receipt,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(receipt);
        await using var database = await (transactions
            ?? throw new InvalidOperationException("Native Storage binding requires the actual owner transaction coordinator."))
            .CreateEnlistedAsync(contextOptions
                ?? throw new InvalidOperationException("Native Storage binding requires the canonical owner context options."),
                static options => new StorageDbContext(options), cancellationToken);
        IQueryable<StoragePlacementIntentRecord> query;
        if (database.Database.IsNpgsql()) {
            query = database.Set<StoragePlacementIntentRecord>().FromSqlInterpolated($"""
                SELECT * FROM "Storage_PlacementIntents" WHERE "Id" = {receipt.IntentId.Value} FOR SHARE
                """);
        } else if (database.Database.IsInMemory()) {
            query = database.Set<StoragePlacementIntentRecord>().Where(row => row.Id == receipt.IntentId.Value);
        } else {
            throw new NotSupportedException("Native Storage binding requires PostgreSQL or explicit InMemory fixtures.");
        }
        var row = await query.AsNoTracking().SingleOrDefaultAsync(cancellationToken)
            ?? throw new StoragePlacementRecoveryException(StoragePlacementRecoveryFailure.NotFound);
        var current = await RequireCompletedNativeReceiptAsync(database, row, receipt.RequestFingerprint, cancellationToken);
        if (current.IntentId != receipt.IntentId || current.Storage.Id != receipt.Storage.Id ||
                !SameTarget(current.WriteResult.Reference, receipt.WriteResult.Reference)) {
            throw new StorageStablePlacementConflictException(receipt.IntentId);
        }
    }

    private static async Task<StorageStablePlacementReceipt> RequireCompletedNativeReceiptAsync(StorageDbContext database,
        StoragePlacementIntentRecord row, string fingerprint, CancellationToken cancellationToken) {
        if (row.RequestFingerprint != fingerprint) {
            throw new StorageStablePlacementConflictException(new(row.Id));
        }
        if (row.State != StorageStablePlacementState.Completed || row.DeletionRequested) {
            throw new StoragePlacementRecoveryException(StoragePlacementRecoveryFailure.Blocked);
        }
        await RequireOriginalRecoveryTargetAsync(database, row, cancellationToken);
        return Outcome(row).Receipt ?? throw new StoragePlacementRecoveryException(StoragePlacementRecoveryFailure.Blocked);
    }
}
