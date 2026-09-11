using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Infrastructure.Storage;

public sealed partial class StorageStablePlacementService {
    internal async Task<IReadOnlyList<StoragePlacementRecoverySnapshot>> FindCompletedRecoveryAsync(
        IReadOnlyCollection<StoragePlacementIntentId> intentIds, Guid? storageId,
        CancellationToken cancellationToken) {
        if (intentIds.Count > 128 || intentIds.Any(id => id.Value == Guid.Empty)) {
            throw new ArgumentException("Continuation recovery observes at most 128 exact placement intents.", nameof(intentIds));
        }
        if (intentIds.Count == 0) {
            return [];
        }
        var ids = intentIds.Select(item => item.Value).Distinct().ToArray();
        await using var database = await factory.CreateDbContextAsync(cancellationToken);
        var query = database.Set<StoragePlacementIntentRecord>().AsNoTracking().Where(row => ids.Contains(row.Id) &&
            (row.State == StorageStablePlacementState.Completed || row.State == StorageStablePlacementState.Deleted));
        if (storageId is { } selectedStorage) {
            query = query.Where(row => row.StorageId == selectedStorage);
        }
        var rows = await query.OrderBy(row => row.Id).ToArrayAsync(cancellationToken);
        var storageIds = rows.Select(row => row.StorageId).Distinct().ToArray();
        var storages = await database.Set<StorageCatalogRecord>().AsNoTracking().Where(row => storageIds.Contains(row.Id))
            .ToDictionaryAsync(row => row.Id, cancellationToken);
        return rows.Select(row => RecoverySnapshot(row, storages.GetValueOrDefault(row.StorageId))).ToArray();
    }
}
