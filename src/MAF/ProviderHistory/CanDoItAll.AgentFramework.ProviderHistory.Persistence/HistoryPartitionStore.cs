using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.AgentFramework.ProviderHistory.Persistence;

public sealed class HistoryPartitionStore(IDbContextFactory<AppDbContext> factory) : IProviderHistoryPartition {
    public async Task<HistoryPartition> GetAsync(CancellationToken cancellationToken) {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var existing = await ReadAsync(db, cancellationToken);
        if (existing is not null) {
            return ToPartition(existing);
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var partition = await GetForWriteAsync(db, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return partition;
    }

    public static async Task<HistoryPartition> GetForWriteAsync(AppDbContext ownerContext, CancellationToken cancellationToken) {
        if (ownerContext.Database.IsRelational() && ownerContext.Database.CurrentTransaction is null) {
            throw new InvalidOperationException("Canonical history staging requires the owner's active transaction.");
        }
        var active = ownerContext.Set<HistoryStorageIdentity>().Local.SingleOrDefault(row => row.Id == HistoryStorageIdentity.SingletonId);
        var staged = active is null ? null : ownerContext.Set<HistoryPartitionRow>().Local.SingleOrDefault(row => row.Id == active.PartitionId);
        var existing = staged ?? await ReadAsync(ownerContext, cancellationToken);
        if (existing is not null) {
            return ToPartition(existing);
        }
        if (ownerContext.Database.IsRelational()) {
            await ownerContext.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(724091824013)", cancellationToken);
            existing = await ReadAsync(ownerContext, cancellationToken);
            if (existing is not null) {
                return ToPartition(existing);
            }
        }
        var partition = new HistoryPartitionRow();
        ownerContext.Add(partition);
        ownerContext.Add(new HistoryStorageIdentity { PartitionId = partition.Id });
        ownerContext.Add(new HistoryPolicyRow { PartitionId = partition.Id });
        foreach (var source in Enum.GetValues<HistorySourceKind>()) {
            ownerContext.Add(new HistoryCheckpointRow { PartitionId = partition.Id, SourceKind = source });
        }
        return ToPartition(partition);
    }

    internal static HistoryPartition ToPartition(HistoryPartitionRow row)
        => new(row.OriginInstanceId, row.Id, row.SecurityPartition);

    public static async Task RequireAsync(AppDbContext db, HistoryPartition partition, CancellationToken cancellationToken) {
        var matches = await db.Set<HistoryPartitionRow>().AnyAsync(row =>
            row.Id == partition.StorageLineageId && row.OriginInstanceId == partition.OriginInstanceId &&
            row.SecurityPartition == partition.SecurityPartition, cancellationToken);
        if (!matches) {
            throw new ProviderHistoryException(HistoryFailure.StaleContext, "The history storage partition changed.");
        }
    }

    private static Task<HistoryPartitionRow?> ReadAsync(AppDbContext db, CancellationToken cancellationToken)
        => (from identity in db.Set<HistoryStorageIdentity>().AsNoTracking()
            join partition in db.Set<HistoryPartitionRow>().AsNoTracking() on identity.PartitionId equals partition.Id
            where identity.Id == HistoryStorageIdentity.SingletonId
            select partition).SingleOrDefaultAsync(cancellationToken);
}
