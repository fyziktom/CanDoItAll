using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.AgentFramework.ProviderHistory.Persistence;

public sealed class HistorySourceMaintenanceRunner(
    IDbContextFactory<AppDbContext> factory,
    TimeProvider clock) {
    public async Task<bool> ProcessAsync(IHistorySourceMaintenance source, HistoryMaintenanceContext context,
        int maximumItems, CancellationToken cancellationToken) {
        if (maximumItems is < 1 or > 1000) {
            throw new ArgumentOutOfRangeException(nameof(maximumItems));
        }
        var partition = context.Partition;
        var lease = await context.DatabaseAsync(token => AcquireAsync(partition, source.Kind, token), cancellationToken);
        if (lease is null) {
            return false;
        }
        try {
            var progress = await source.ProcessAsync(context, lease.Cursor, maximumItems, cancellationToken);
            if (progress.Cursor?.Length > 4096) {
                throw new InvalidDataException("The source maintenance cursor exceeds its storage bound.");
            }
            await context.DatabaseAsync(token => CompleteAsync(partition, source.Kind, lease.Id, progress, null, token), cancellationToken);
            return true;
        } catch (DatabaseRuntimeProfileChangedException) {
            throw;
        } catch (Exception exception) {
            using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            try {
                await context.DatabaseAsync(token => CompleteAsync(partition, source.Kind, lease.Id, null,
                    exception.GetType().Name, token), cleanup.Token);
            } catch (DatabaseRuntimeProfileChangedException) {
                throw;
            } catch (Exception cleanupFailure) {
                throw new AggregateException("Source maintenance and checkpoint failure recording both failed.", exception, cleanupFailure);
            }
            throw;
        }
    }

    private async Task<Lease?> AcquireAsync(HistoryPartition partition, HistorySourceKind kind, CancellationToken token) {
        await using var db = await factory.CreateDbContextAsync(token);
        await using var transaction = await db.Database.BeginTransactionAsync(token);
        await HistoryPartitionStore.RequireAsync(db, partition, token);
        await HistoryWriteLock.AttemptAsync(db, partition.StorageLineageId, token);
        var row = await db.Set<HistoryCheckpointRow>().SingleAsync(
            value => value.PartitionId == partition.StorageLineageId && value.SourceKind == kind, token);
        if (row.LeaseUntilUtc > clock.GetUtcNow()) {
            return null;
        }
        var lease = new Lease(Guid.NewGuid(), row.Cursor);
        row.LeaseOwner = lease.Id;
        row.LeaseUntilUtc = clock.GetUtcNow().AddSeconds(30);
        row.Coverage = HistoryCoverageState.Partial;
        await db.SaveChangesAsync(token);
        await transaction.CommitAsync(token);
        return lease;
    }

    private async Task CompleteAsync(HistoryPartition partition, HistorySourceKind kind, Guid lease,
        HistorySourceProgress? progress, string? failure, CancellationToken token) {
        await using var db = await factory.CreateDbContextAsync(token);
        await using var transaction = await db.Database.BeginTransactionAsync(token);
        await HistoryPartitionStore.RequireAsync(db, partition, token);
        await HistoryWriteLock.AttemptAsync(db, partition.StorageLineageId, token);
        var row = await db.Set<HistoryCheckpointRow>().SingleAsync(
            value => value.PartitionId == partition.StorageLineageId && value.SourceKind == kind, token);
        if (row.LeaseOwner != lease) {
            throw new ProviderHistoryException(HistoryFailure.Conflict, "The source maintenance lease was replaced.");
        }
        var pending = await db.Set<HistoryOutboxRow>().AnyAsync(value => value.PartitionId == partition.StorageLineageId, token);
        row.LeaseOwner = null;
        row.LeaseUntilUtc = null;
        row.FailureCode = failure;
        row.Coverage = failure is not null ? HistoryCoverageState.Failed
            : progress!.BackfillComplete && !pending ? HistoryCoverageState.Current : HistoryCoverageState.Partial;
        if (progress is not null) {
            row.Cursor = progress.Cursor;
            if (row.Coverage == HistoryCoverageState.Current) {
                row.IndexedThroughUtc = clock.GetUtcNow();
            }
        }
        await db.SaveChangesAsync(token);
        await transaction.CommitAsync(token);
    }

    private sealed record Lease(Guid Id, string? Cursor);
}
