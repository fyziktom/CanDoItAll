using System.Text.Json;
using CanDoItAll.AgentFramework.ProviderHistory;
using CanDoItAll.AgentFramework.ProviderHistory.Persistence;
using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.AgentFramework.ProviderManagement;

public sealed class SharedProviderHistorySource(
    IDbContextFactory<AppDbContext> factory,
    HistoryOutboxWriter outbox,
    TimeProvider clock) : IProviderHistorySource, IHistorySourceMaintenance {
    public HistorySourceKind Kind => HistorySourceKind.SharedRelay;

    public Task<HistorySourceProgress> ProcessAsync(HistoryMaintenanceContext context, string? cursor,
        int maximumItems, CancellationToken cancellationToken) => context.DatabaseAsync(
            token => ProcessCoreAsync(context.Partition, cursor, maximumItems, token), cancellationToken);

    private async Task<HistorySourceProgress> ProcessCoreAsync(HistoryPartition partition, string? cursor,
        int maximumItems, CancellationToken cancellationToken) {
        if (maximumItems is < 1 or > 1000) {
            throw new ArgumentOutOfRangeException(nameof(maximumItems));
        }
        await PurgeExpiredAsync(partition, maximumItems, cancellationToken);
        var position = cursor is null ? new Position(Guid.Empty, false) : JsonSerializer.Deserialize<Position>(cursor) ?? throw new InvalidDataException("Invalid history source cursor.");
        if (position.Complete) {
            return new(cursor, true);
        }
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await HistoryPartitionStore.RequireAsync(db, partition, cancellationToken);
        var rows = await db.Set<SharedProviderInvocationRecord>().AsNoTracking()
            .Where(row => row.Id.CompareTo(position.Id) > 0).OrderBy(row => row.Id).Take(maximumItems).ToArrayAsync(cancellationToken);
        foreach (var row in rows) {
            outbox.Stage(db, SharedProviderHistoryProjection.Create(row, partition));
        }
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        var next = new Position(rows.LastOrDefault()?.Id ?? position.Id, rows.Length < maximumItems);
        return new(JsonSerializer.Serialize(next), next.Complete);
    }

    private async Task PurgeExpiredAsync(HistoryPartition partition, int maximumItems, CancellationToken cancellationToken) {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await HistoryPartitionStore.RequireAsync(db, partition, cancellationToken);
        var now = clock.GetUtcNow();
        var expired = await db.Set<SharedProviderInvocationRecord>()
            .Where(row => row.Outcome != SharedProviderInvocationOutcome.InProgress &&
                (row.DeleteAfterUtc <= now || db.Set<HistoryEntryRow>().Any(entry =>
                    entry.PartitionId == partition.StorageLineageId && entry.Id == row.Id &&
                    entry.RetentionAuthority == HistoryRetentionAuthority.HistoryPolicy && entry.ExpiresAtUtc <= now)))
            .OrderBy(row => row.DeleteAfterUtc).ThenBy(row => row.Id).Take(maximumItems).ToArrayAsync(cancellationToken);
        foreach (var row in expired) {
            var id = row.Id.ToString("N");
            outbox.Stage(db, new(new(partition, Kind, new(id), new(id)),
                new(checked(row.HistoryVersion + 1)), HistorySourceMutationKind.Delete, null, []));
        }
        db.RemoveRange(expired);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<HistorySourceMutation?> ReadAsync(CanonicalEvidenceReference source, CancellationToken cancellationToken) {
        if (source.Kind != Kind || source.Owner.Value != source.Evidence.Value ||
            !Guid.TryParseExact(source.Owner.Value, "N", out var id)) {
            return null;
        }
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        await HistoryPartitionStore.RequireAsync(db, source.Partition, cancellationToken);
        var row = await db.Set<SharedProviderInvocationRecord>().AsNoTracking().SingleOrDefaultAsync(row => row.Id == id, cancellationToken);
        return row is null ? null : SharedProviderHistoryProjection.Create(row, source.Partition);
    }

    public Task<HistoryDetail> ReadDetailAsync(CanonicalEvidenceReference source, HistoryEntryId entryId,
        CancellationToken cancellationToken) => Task.FromResult(new HistoryDetail(entryId, HistoryDetailState.NotCaptured));

    private sealed record Position(Guid Id, bool Complete);
}
