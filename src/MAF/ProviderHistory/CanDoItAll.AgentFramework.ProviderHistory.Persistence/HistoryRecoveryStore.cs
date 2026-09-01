using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.AgentFramework.ProviderHistory.Persistence;

public sealed class HistoryRecoveryStore(IDbContextFactory<AppDbContext> factory, TimeProvider clock) {
    public async Task<int> InterruptAbandonedAsync(HistoryPartition partition, int maximumItems, CancellationToken cancellationToken) {
        if (maximumItems is < 1 or > 1000) {
            throw new ArgumentOutOfRangeException(nameof(maximumItems));
        }
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await HistoryPartitionStore.RequireAsync(db, partition, cancellationToken);
        await HistoryWriteLock.AttemptAsync(db, partition.StorageLineageId, cancellationToken);
        var now = clock.GetUtcNow();
        var entries = await (from entry in db.Set<HistoryEntryRow>()
            join lease in db.Set<HistoryHostLeaseRow>() on entry.CaptureHostId equals lease.Id
            where entry.PartitionId == partition.StorageLineageId && entry.Outcome == HistoryOutcome.Started &&
                lease.ExpiresAtUtc <= now
            orderby entry.SortAtUtc, entry.Id
            select entry).Take(maximumItems).ToListAsync(cancellationToken);
        foreach (var entry in entries) {
            entry.Outcome = HistoryOutcome.Interrupted;
            entry.FinishedAtUtc = now;
            entry.Version++;
        }
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return entries.Count;
    }
}
