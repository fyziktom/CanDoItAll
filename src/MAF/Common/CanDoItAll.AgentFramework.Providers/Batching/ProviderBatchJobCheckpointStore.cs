using System.Collections.Concurrent;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Providers;

public interface IProviderBatchJobCheckpointStore
{
    Task<IReadOnlyList<ProviderBatchItemCheckpoint>> GetItemCheckpointsAsync(
        Guid jobId,
        CancellationToken cancellationToken = default);

    Task UpsertItemCheckpointAsync(
        ProviderBatchItemCheckpoint checkpoint,
        CancellationToken cancellationToken = default);
}

public sealed class InMemoryProviderBatchJobCheckpointStore : IProviderBatchJobCheckpointStore
{
    private readonly ConcurrentDictionary<ProviderBatchCheckpointKey, ProviderBatchItemCheckpoint> checkpoints = [];

    public Task<IReadOnlyList<ProviderBatchItemCheckpoint>> GetItemCheckpointsAsync(
        Guid jobId,
        CancellationToken cancellationToken = default)
    {
        var result = checkpoints
            .Where(pair => pair.Key.JobId == jobId)
            .Select(pair => pair.Value)
            .OrderBy(checkpoint => checkpoint.Sequence)
            .ToList();

        return Task.FromResult<IReadOnlyList<ProviderBatchItemCheckpoint>>(result);
    }

    public Task UpsertItemCheckpointAsync(
        ProviderBatchItemCheckpoint checkpoint,
        CancellationToken cancellationToken = default)
    {
        checkpoints[new ProviderBatchCheckpointKey(checkpoint.JobId, checkpoint.InputId)] = checkpoint;
        return Task.CompletedTask;
    }

    private sealed record ProviderBatchCheckpointKey(
        Guid JobId,
        Guid InputId);
}
