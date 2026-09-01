using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.AgentFramework.ProviderHistory.Persistence;

public sealed class HistoryOutboxProcessor(
    IDbContextFactory<AppDbContext> factory, TimeProvider clock, ILogger<HistoryOutboxProcessor> logger) {
    public async Task<int> ProcessAsync(HistoryPartition partition, int maximumItems, CancellationToken cancellationToken) {
        if (maximumItems is < 1 or > 1000) {
            throw new ArgumentOutOfRangeException(nameof(maximumItems));
        }
        Guid? failedId = null;
        try {
            await using var db = await factory.CreateDbContextAsync(cancellationToken);
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            await HistoryPartitionStore.RequireAsync(db, partition, cancellationToken);
            var lineage = partition.StorageLineageId;
            var lockKey = System.Buffers.Binary.BinaryPrimitives.ReadInt64LittleEndian(lineage.ToByteArray());
            var acquired = await db.Database.SqlQuery<bool>(
                $"""SELECT pg_try_advisory_xact_lock({lockKey}) AS "Value" """).SingleAsync(cancellationToken);
            if (!acquired) {
                return 0;
            }
            var now = clock.GetUtcNow();
            var batch = await db.Set<HistoryOutboxRow>().Where(row => row.PartitionId == lineage && row.RetryAfterUtc <= now)
                .OrderBy(row => row.CreatedAtUtc).ThenBy(row => row.Id).Take(maximumItems).ToListAsync(cancellationToken);
            var processed = 0;
            var startedAt = clock.GetTimestamp();
            foreach (var item in batch) {
                failedId = item.Id;
                await HistorySourceProjection.ApplyAsync(db, item.Mutation, cancellationToken);
                db.Remove(item);
                await db.SaveChangesAsync(cancellationToken);
                processed++;
                if (clock.GetElapsedTime(startedAt) >= TimeSpan.FromSeconds(2)) {
                    break;
                }
            }
            await transaction.CommitAsync(cancellationToken);
            return processed;
        } catch (Exception exception) when (failedId.HasValue && exception is not OperationCanceledException) {
            await RecordFailureAsync(failedId.Value, exception, cancellationToken);
            throw new ProviderHistoryException(HistoryFailure.Unavailable,
                "History projection could not commit; the source mutation remains queued for retry.");
        }
    }

    private async Task RecordFailureAsync(Guid outboxId, Exception failure, CancellationToken cancellationToken) {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var row = await db.Set<HistoryOutboxRow>().SingleAsync(item => item.Id == outboxId, cancellationToken);
        row.Attempts = checked(row.Attempts + 1);
        row.RetryAfterUtc = clock.GetUtcNow().AddSeconds(Math.Min(300, 5 * row.Attempts));
        row.FailureCode = failure is ProviderHistoryException history ? history.Failure.ToString() : failure.GetType().Name;
        var checkpoint = await db.Set<HistoryCheckpointRow>().SingleAsync(item =>
            item.PartitionId == row.PartitionId && item.SourceKind == row.Mutation.Source.Kind, cancellationToken);
        checkpoint.Coverage = HistoryCoverageState.Failed;
        checkpoint.FailureCode = row.FailureCode;
        await db.SaveChangesAsync(cancellationToken);
        logger.LogError("History projection failed for outbox {OutboxId}, source kind {SourceKind}, attempt {Attempt}, code {FailureCode}.",
            row.Id, row.Mutation.Source.Kind, row.Attempts, row.FailureCode);
    }
}
