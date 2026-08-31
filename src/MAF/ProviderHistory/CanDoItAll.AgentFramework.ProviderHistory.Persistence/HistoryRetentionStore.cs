using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.AgentFramework.ProviderHistory.Persistence;

public sealed class HistoryRetentionStore(IDbContextFactory<AppDbContext> factory, TimeProvider clock) {
    public async Task<int> PurgeExpiredDetailAsync(HistoryPartition partition, int maximumItems, CancellationToken cancellationToken) {
        ValidateBatch(maximumItems);
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await HistoryPartitionStore.RequireAsync(db, partition, cancellationToken);
        var policy = await HistoryPolicyStore.LockAsync(db, partition.StorageLineageId, cancellationToken);
        var now = clock.GetUtcNow();
        var rows = await db.Set<HistoryDetailRow>().Where(row =>
            row.PartitionId == partition.StorageLineageId && row.ExpiresAtUtc <= now && row.StoredBytes > 0)
            .OrderBy(row => row.ExpiresAtUtc).ThenBy(row => row.Id).Take(maximumItems).ToListAsync(cancellationToken);
        var released = rows.Sum(row => (long)row.StoredBytes);
        if (released > policy.UsedDetailBytes) {
            throw new ProviderHistoryException(HistoryFailure.Conflict, "The history detail quota counter is inconsistent.");
        }
        foreach (var row in rows) {
            HistoryDetailStore.Omit(row, HistoryDetailState.Expired);
        }
        policy.UsedDetailBytes -= released;
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return rows.Count;
    }

    public async Task<int> PurgeExpiredMetadataAsync(HistoryPartition partition, int maximumItems, CancellationToken cancellationToken) {
        ValidateBatch(maximumItems);
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await HistoryPartitionStore.RequireAsync(db, partition, cancellationToken);
        await HistoryPolicyStore.LockAsync(db, partition.StorageLineageId, cancellationToken);
        var now = clock.GetUtcNow();
        var rows = await db.Set<HistoryEntryRow>().Where(row =>
            row.PartitionId == partition.StorageLineageId &&
            (row.MetadataAuthority == HistoryMetadataAuthority.Standalone || !row.IsVisible) &&
            row.RetentionAuthority == HistoryRetentionAuthority.HistoryPolicy && row.Outcome != HistoryOutcome.Started &&
            row.ExpiresAtUtc <= now &&
            !db.Set<HistoryDetailRow>().Any(detail => detail.EntryId == row.Id && detail.StoredBytes > 0) &&
            !db.Set<HistoryDetailRow>().Any(detail => detail.Id == row.InputDetailId && detail.StoredBytes > 0))
            .OrderBy(row => row.SortAtUtc).ThenBy(row => row.Id).Take(maximumItems).ToListAsync(cancellationToken);
        var ids = rows.Select(row => row.Id).ToArray();
        await db.Set<HistoryOwnerRow>().Where(row => ids.Contains(row.EntryId)).ExecuteDeleteAsync(cancellationToken);
        await db.Set<HistoryDetailRow>().Where(row => row.Part == HistoryDetailPart.Response && ids.Contains(row.EntryId!.Value))
            .ExecuteDeleteAsync(cancellationToken);
        db.RemoveRange(rows);
        await db.SaveChangesAsync(cancellationToken);
        var remaining = maximumItems - rows.Count;
        var removedInputs = remaining == 0 ? 0 : await db.Set<HistoryDetailRow>()
            .Where(detail => detail.PartitionId == partition.StorageLineageId &&
                detail.Part == HistoryDetailPart.Input && detail.EntryId == null &&
                detail.ExpiresAtUtc <= now && detail.StoredBytes == 0 &&
                !db.Set<HistoryEntryRow>().Any(entry => entry.InputDetailId == detail.Id))
            .OrderBy(detail => detail.ExpiresAtUtc).ThenBy(detail => detail.Id).Take(remaining)
            .ExecuteDeleteAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return rows.Count + removedInputs;
    }

    private static void ValidateBatch(int maximumItems) {
        if (maximumItems is < 1 or > 1000) {
            throw new ArgumentOutOfRangeException(nameof(maximumItems));
        }
    }
}
