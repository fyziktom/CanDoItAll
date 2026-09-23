using CanDoItAll.Infrastructure.Persistence;

namespace CanDoItAll.AgentFramework.ProviderHistory.Persistence;

public sealed class HistoryMaintenanceContext(
    HistoryPartition partition,
    DatabaseRuntimeSnapshot runtime,
    IDatabaseRuntimeWriteFence fence) {
    public HistoryPartition Partition { get; } = partition;

    public Task<T> DatabaseAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken)
        => fence.ExecuteAsync(runtime, operation, cancellationToken);

    public Task DatabaseAsync(Func<CancellationToken, Task> operation, CancellationToken cancellationToken)
        => fence.ExecuteAsync(runtime, async token => {
            await operation(token);
            return true;
        }, cancellationToken);
}
