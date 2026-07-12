using CanDoItAll.Memory.Abstractions;

namespace CanDoItAll.Memory.Application;

public sealed class MemoryAsyncOperationWorker : IMemoryAsyncOperationWorker
{
    private readonly IMemoryProviderProfileStore providerProfileStore;
    private readonly IMemoryOperationLedgerStore operationLedgerStore;
    private readonly TimeProvider timeProvider;
    private readonly MemoryAsyncWorkerOptions options;
    private readonly MemoryAsyncOperationProcessor processor;

    public MemoryAsyncOperationWorker(
        IMemoryProviderProfileStore providerProfileStore,
        IMemoryOperationLedgerStore operationLedgerStore,
        IEnumerable<IMemoryProviderOperationStatusDriver> statusDrivers,
        TimeProvider timeProvider,
        MemoryAsyncWorkerOptions options)
    {
        this.providerProfileStore = providerProfileStore;
        this.operationLedgerStore = operationLedgerStore;
        this.timeProvider = timeProvider;
        this.options = options;
        processor = new MemoryAsyncOperationProcessor(operationLedgerStore, statusDrivers, options);
    }

    public async Task<MemoryAsyncWorkerRunResult> PollOperationsAsync(
        CancellationToken cancellationToken = default)
    {
        options.Validate();
        var now = timeProvider.GetUtcNow();
        var operations = await operationLedgerStore.ListDueForPollingAsync(
            now,
            options.PollingStaleAfter,
            options.MaxBatchSize,
            cancellationToken);
        var profiles = (await providerProfileStore.ListAsync(cancellationToken))
            .ToDictionary(profile => profile.InstanceId);
        var outcomes = new List<MemoryOperationWorkerOutcome>(operations.Count);
        foreach (var operation in operations)
        {
            cancellationToken.ThrowIfCancellationRequested();
            profiles.TryGetValue(operation.ProviderInstanceId, out var provider);
            outcomes.Add(await processor.ProcessAsync(operation, provider, now, cancellationToken));
        }

        return new MemoryAsyncWorkerRunResult(
            operations.Count,
            outcomes.Sum(outcome => outcome.Completed),
            outcomes.Sum(outcome => outcome.Retried),
            outcomes.Sum(outcome => outcome.DeadLettered),
            outcomes.Sum(outcome => outcome.TimedOut),
            outcomes.Sum(outcome => outcome.Cancelled),
            Enqueued: 0,
            Duplicates: 0,
            LoopRejected: 0,
            IpfsUnpinRequests: 0,
            outcomes.Select(outcome => outcome.Diagnostic).ToArray());
    }

    public async Task<MemoryOperationRecord> CancelOperationAsync(
        MemoryOperationId operationId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        var operation = await operationLedgerStore.GetAsync(operationId, cancellationToken)
            ?? throw new InvalidOperationException($"Memory operation '{operationId}' was not found.");
        if (operation.Status is not (MemoryLedgerStatus.Pending or MemoryLedgerStatus.Accepted or MemoryLedgerStatus.Running))
        {
            throw new InvalidOperationException(
                $"Memory operation '{operationId}' cannot be cancelled from '{operation.Status}'.");
        }

        return await operationLedgerStore.TransitionAsync(
            operationId,
            MemoryLedgerStatus.Cancelled,
            timeProvider.GetUtcNow(),
            reason,
            cancellationToken);
    }
}
