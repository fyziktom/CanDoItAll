using CanDoItAll.Infrastructure.Persistence;

namespace CanDoItAll.AgentFramework.ProviderHistory.Persistence;

public sealed class HistoryOutboxWriter(TimeProvider clock) {
    public void Stage(AppDbContext ownerContext, HistorySourceMutation mutation) {
        ArgumentNullException.ThrowIfNull(ownerContext);
        HistorySourceIdentity.Validate(mutation);
        ownerContext.Add(new HistoryOutboxRow {
            PartitionId = mutation.Source.Partition.StorageLineageId,
            CreatedAtUtc = clock.GetUtcNow(),
            RetryAfterUtc = clock.GetUtcNow(),
            Mutation = mutation
        });
    }
}
