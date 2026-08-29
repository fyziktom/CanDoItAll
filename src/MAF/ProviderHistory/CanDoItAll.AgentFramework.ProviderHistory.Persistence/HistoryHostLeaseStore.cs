using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.AgentFramework.ProviderHistory.Persistence;

public sealed class HistoryHostLeaseStore(IDbContextFactory<AppDbContext> factory, TimeProvider clock) : IDisposable {
    private readonly Guid hostId = Guid.NewGuid();
    private readonly SemaphoreSlim gate = new(1, 1);
    private DateTimeOffset refreshAfter;
    private HistoryPartition? activePartition;

    public async Task<Guid> EnsureAsync(HistoryPartition partition, CancellationToken cancellationToken) {
        await gate.WaitAsync(cancellationToken);
        try {
            if (activePartition is { } active && active != partition) {
                throw new ProviderHistoryException(HistoryFailure.StaleContext, "The history host lease belongs to another runtime partition.");
            }
            if (activePartition.HasValue && clock.GetUtcNow() < refreshAfter) {
                return hostId;
            }
            await using var db = await factory.CreateDbContextAsync(cancellationToken);
            await HistoryPartitionStore.RequireAsync(db, partition, cancellationToken);
            var expires = clock.GetUtcNow().AddSeconds(90);
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "ProviderHistory_HostLeases" ("Id", "PartitionId", "ExpiresAtUtc")
                VALUES ({hostId}, {partition.StorageLineageId}, {expires})
                ON CONFLICT ("Id") DO UPDATE SET "ExpiresAtUtc" = EXCLUDED."ExpiresAtUtc"
                """, cancellationToken);
            activePartition = partition;
            refreshAfter = clock.GetUtcNow().AddSeconds(20);
            return hostId;
        } finally {
            gate.Release();
        }
    }

    public async Task HeartbeatAsync(HistoryPartition partition, CancellationToken cancellationToken) {
        if (activePartition.HasValue) {
            await EnsureAsync(partition, cancellationToken);
        }
    }

    public void Dispose() => gate.Dispose();
}
