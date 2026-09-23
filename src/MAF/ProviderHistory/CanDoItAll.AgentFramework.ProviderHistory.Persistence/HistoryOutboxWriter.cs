using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.AgentFramework.ProviderHistory.Persistence;

public sealed class HistoryOutboxWriter(
    DbContextOptions<ProviderHistoryDbContext> options,
    CoordinatedDatabaseTransaction transactions,
    TimeProvider clock) {
    public async Task StageAsync(HistorySourceMutation mutation, CancellationToken cancellationToken) {
        HistorySourceIdentity.Validate(mutation);
        await using var db = await transactions.CreateEnlistedAsync(options, static value => new ProviderHistoryDbContext(value), cancellationToken);
        db.Add(new HistoryOutboxRow {
            PartitionId = mutation.Source.Partition.StorageLineageId,
            CreatedAtUtc = clock.GetUtcNow(),
            RetryAfterUtc = clock.GetUtcNow(),
            Mutation = mutation
        });
        await db.SaveChangesAsync(cancellationToken);
    }
}
