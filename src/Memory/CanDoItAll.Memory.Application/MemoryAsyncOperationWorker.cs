using CanDoItAll.Memory.Abstractions;

namespace CanDoItAll.Memory.Application;

public sealed class MemoryAsyncOperationWorker(
    IMemoryProviderProfileStore providerProfileStore,
    IMemoryOperationLedgerStore operationLedgerStore,
    IEnumerable<IMemoryProviderOperationStatusDriver> statusDrivers,
    TimeProvider timeProvider,
    MemoryAsyncWorkerOptions options) : IMemoryAsyncOperationWorker
{
    public async Task<MemoryAsyncWorkerRunResult> PollOperationsAsync(CancellationToken cancellationToken = default)
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
        var diagnostics = new List<string>();
        var completed = 0;
        var retried = 0;
        var deadLettered = 0;
        var timedOut = 0;
        var cancelled = 0;

        foreach (var operation in operations)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!profiles.TryGetValue(operation.ProviderInstanceId, out var provider))
            {
                var retryResult = await RetryOrFailAsync(operation, now, "Memory provider profile is no longer registered.", cancellationToken);
                retried += retryResult.Retried;
                deadLettered += retryResult.DeadLettered;
                diagnostics.Add(retryResult.Diagnostic);
                continue;
            }

            if (IsTimedOut(operation, provider, now))
            {
                await operationLedgerStore.TransitionAsync(operation.OperationId, MemoryLedgerStatus.TimedOut, now, "Memory operation timed out.", cancellationToken);
                timedOut++;
                diagnostics.Add($"Timed out memory operation '{operation.OperationId}'.");
                continue;
            }

            var driver = statusDrivers.FirstOrDefault(candidate => candidate.DriverKind == provider.DriverKind);
            if (driver is null)
            {
                var retryResult = await RetryOrFailAsync(operation, now, $"No status driver registered for '{provider.DriverKind}'.", cancellationToken);
                retried += retryResult.Retried;
                deadLettered += retryResult.DeadLettered;
                diagnostics.Add(retryResult.Diagnostic);
                continue;
            }

            var pollResult = await driver.PollOperationAsync(provider, operation, cancellationToken);
            var applied = await ApplyPollResultAsync(operation, pollResult, now, cancellationToken);
            completed += applied.Completed;
            retried += applied.Retried;
            deadLettered += applied.DeadLettered;
            timedOut += applied.TimedOut;
            cancelled += applied.Cancelled;
            diagnostics.Add(applied.Diagnostic);
        }

        return new MemoryAsyncWorkerRunResult(
            operations.Count,
            completed,
            retried,
            deadLettered,
            timedOut,
            cancelled,
            0,
            0,
            0,
            0,
            diagnostics);
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
            throw new InvalidOperationException($"Memory operation '{operationId}' cannot be cancelled from '{operation.Status}'.");
        }

        return await operationLedgerStore.TransitionAsync(
            operationId,
            MemoryLedgerStatus.Cancelled,
            timeProvider.GetUtcNow(),
            reason,
            cancellationToken);
    }

    private async Task<WorkerOutcome> ApplyPollResultAsync(
        MemoryOperationRecord operation,
        MemoryProviderOperationPollResult result,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        return result.Kind switch
        {
            MemoryProviderOperationPollResultKind.OperationResult when result.OperationResult is { } operationResult =>
                await ApplyOperationResultAsync(operation, operationResult, result.Diagnostic, now, cancellationToken),
            MemoryProviderOperationPollResultKind.StillRunning =>
                await KeepRunningAsync(operation, now, result.Diagnostic, cancellationToken),
            MemoryProviderOperationPollResultKind.RetryableFailure =>
                await RetryOrFailAsync(operation, now, result.Diagnostic, cancellationToken),
            MemoryProviderOperationPollResultKind.TerminalFailure or MemoryProviderOperationPollResultKind.UnsupportedCapability =>
                await FailAsync(operation, now, result.Diagnostic, cancellationToken),
            _ => await FailAsync(operation, now, "Malformed memory provider operation poll result.", cancellationToken)
        };
    }

    private async Task<WorkerOutcome> ApplyOperationResultAsync(
        MemoryOperationRecord operation,
        MemoryOperationResult result,
        string diagnostic,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var nextStatus = result.Status switch
        {
            MemoryOperationStatus.Succeeded => MemoryLedgerStatus.Completed,
            MemoryOperationStatus.Failed => MemoryLedgerStatus.Failed,
            MemoryOperationStatus.Canceled => MemoryLedgerStatus.Cancelled,
            MemoryOperationStatus.TimedOut => MemoryLedgerStatus.TimedOut,
            MemoryOperationStatus.Accepted or MemoryOperationStatus.Running => MemoryLedgerStatus.Running,
            _ => MemoryLedgerStatus.Failed
        };

        if (nextStatus == MemoryLedgerStatus.Running)
        {
            return await KeepRunningAsync(operation, now, diagnostic, cancellationToken);
        }

        await operationLedgerStore.TransitionAsync(operation.OperationId, nextStatus, now, diagnostic, cancellationToken);
        return nextStatus switch
        {
            MemoryLedgerStatus.Completed => WorkerOutcome.ForCompleted(diagnostic),
            MemoryLedgerStatus.TimedOut => WorkerOutcome.ForTimedOut(diagnostic),
            MemoryLedgerStatus.Cancelled => WorkerOutcome.ForCancelled(diagnostic),
            _ => WorkerOutcome.ForDeadLettered(diagnostic)
        };
    }

    private async Task<WorkerOutcome> KeepRunningAsync(
        MemoryOperationRecord operation,
        DateTimeOffset now,
        string diagnostic,
        CancellationToken cancellationToken)
    {
        if (operation.Status != MemoryLedgerStatus.Running)
        {
            await operationLedgerStore.TransitionAsync(operation.OperationId, MemoryLedgerStatus.Running, now, diagnostic, cancellationToken);
            return WorkerOutcome.ForRetried(diagnostic);
        }

        await operationLedgerStore.DeferAsync(operation.OperationId, now, diagnostic, incrementRetry: false, cancellationToken);
        return WorkerOutcome.ForRetried(diagnostic);
    }

    private async Task<WorkerOutcome> RetryOrFailAsync(
        MemoryOperationRecord operation,
        DateTimeOffset now,
        string diagnostic,
        CancellationToken cancellationToken)
    {
        if (operation.RetryCount + 1 >= options.MaxRetryAttempts)
        {
            return await FailAsync(operation, now, diagnostic, cancellationToken);
        }

        await operationLedgerStore.DeferAsync(operation.OperationId, now, diagnostic, incrementRetry: true, cancellationToken);
        return WorkerOutcome.ForRetried(diagnostic);
    }

    private async Task<WorkerOutcome> FailAsync(
        MemoryOperationRecord operation,
        DateTimeOffset now,
        string diagnostic,
        CancellationToken cancellationToken)
    {
        await operationLedgerStore.TransitionAsync(operation.OperationId, MemoryLedgerStatus.Failed, now, diagnostic, cancellationToken);
        return WorkerOutcome.ForDeadLettered(diagnostic);
    }

    private static bool IsTimedOut(
        MemoryOperationRecord operation,
        MemoryProviderProfile provider,
        DateTimeOffset now) =>
        operation.CreatedAtUtc.Add(provider.Manifest.Limits.OperationTimeout) <= now;

    private sealed record WorkerOutcome(
        int Completed,
        int Retried,
        int DeadLettered,
        int TimedOut,
        int Cancelled,
        string Diagnostic)
    {
        public static WorkerOutcome ForCompleted(string diagnostic) => new(1, 0, 0, 0, 0, diagnostic);
        public static WorkerOutcome ForRetried(string diagnostic) => new(0, 1, 0, 0, 0, diagnostic);
        public static WorkerOutcome ForDeadLettered(string diagnostic) => new(0, 0, 1, 0, 0, diagnostic);
        public static WorkerOutcome ForTimedOut(string diagnostic) => new(0, 0, 0, 1, 0, diagnostic);
        public static WorkerOutcome ForCancelled(string diagnostic) => new(0, 0, 0, 0, 1, diagnostic);
    }
}
