using CanDoItAll.Memory.Abstractions;

namespace CanDoItAll.Memory.Application;

internal sealed class MemoryOperationPollResultApplier(
    IMemoryOperationLedgerStore operationLedgerStore,
    MemoryAsyncWorkerOptions options)
{
    public Task<MemoryOperationWorkerOutcome> ApplyAsync(
        MemoryOperationRecord operation,
        MemoryProviderOperationPollResult result,
        MemoryProviderProfile provider,
        DateTimeOffset now,
        CancellationToken cancellationToken) =>
        result.Kind switch
        {
            MemoryProviderOperationPollResultKind.OperationResult when result.OperationResult is { } operationResult =>
                ApplyOperationResultAsync(operation, operationResult, provider, result.Diagnostic, now, cancellationToken),
            MemoryProviderOperationPollResultKind.StillRunning =>
                KeepRunningAsync(operation, now, result.Diagnostic, cancellationToken),
            MemoryProviderOperationPollResultKind.RetryableFailure =>
                RetryOrFailAsync(operation, now, result.Diagnostic, cancellationToken),
            MemoryProviderOperationPollResultKind.TerminalFailure or
                MemoryProviderOperationPollResultKind.UnsupportedCapability =>
                FailAsync(operation, now, result.Diagnostic, cancellationToken),
            _ => FailAsync(operation, now, "Malformed memory provider operation poll result.", cancellationToken)
        };

    public async Task<MemoryOperationWorkerOutcome> RetryOrFailAsync(
        MemoryOperationRecord operation,
        DateTimeOffset now,
        string diagnostic,
        CancellationToken cancellationToken)
    {
        if (operation.RetryCount + 1 >= options.MaxRetryAttempts)
        {
            return await FailAsync(operation, now, diagnostic, cancellationToken);
        }

        await operationLedgerStore.DeferAsync(
            operation.OperationId,
            now,
            diagnostic,
            incrementRetry: true,
            cancellationToken);
        return MemoryOperationWorkerOutcome.ForRetried(diagnostic);
    }

    public async Task<MemoryOperationWorkerOutcome> FailAsync(
        MemoryOperationRecord operation,
        DateTimeOffset now,
        string diagnostic,
        CancellationToken cancellationToken)
    {
        await operationLedgerStore.TransitionAsync(
            operation.OperationId,
            MemoryLedgerStatus.Failed,
            now,
            diagnostic,
            cancellationToken);
        return MemoryOperationWorkerOutcome.ForDeadLettered(diagnostic);
    }

    public async Task<MemoryOperationWorkerOutcome> TimeOutAsync(
        MemoryOperationRecord operation,
        DateTimeOffset now,
        string diagnostic,
        CancellationToken cancellationToken)
    {
        await operationLedgerStore.TransitionAsync(
            operation.OperationId,
            MemoryLedgerStatus.TimedOut,
            now,
            diagnostic,
            cancellationToken);
        return MemoryOperationWorkerOutcome.ForTimedOut(diagnostic);
    }

    private async Task<MemoryOperationWorkerOutcome> ApplyOperationResultAsync(
        MemoryOperationRecord operation,
        MemoryOperationResult result,
        MemoryProviderProfile provider,
        string diagnostic,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (result.OperationId != operation.OperationId)
        {
            return await FailAsync(
                operation,
                now,
                "Memory provider returned a result for a different operation id.",
                cancellationToken);
        }

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

        if (MemoryOperationResultValidator.GetFailure(operation, result, provider) is { } resultFailure)
        {
            return await FailAsync(operation, now, resultFailure, cancellationToken);
        }

        var extensions = operation.Extensions.WithFinalOperationResult(
            operation.OperationId,
            operation.ProviderInstanceId,
            result);
        await operationLedgerStore.TransitionAsync(
            operation.OperationId,
            nextStatus,
            now,
            diagnostic,
            extensions,
            cancellationToken);
        return nextStatus switch
        {
            MemoryLedgerStatus.Completed => MemoryOperationWorkerOutcome.ForCompleted(diagnostic),
            MemoryLedgerStatus.TimedOut => MemoryOperationWorkerOutcome.ForTimedOut(diagnostic),
            MemoryLedgerStatus.Cancelled => MemoryOperationWorkerOutcome.ForCancelled(diagnostic),
            _ => MemoryOperationWorkerOutcome.ForDeadLettered(diagnostic)
        };
    }

    private async Task<MemoryOperationWorkerOutcome> KeepRunningAsync(
        MemoryOperationRecord operation,
        DateTimeOffset now,
        string diagnostic,
        CancellationToken cancellationToken)
    {
        if (operation.Status != MemoryLedgerStatus.Running)
        {
            await operationLedgerStore.TransitionAsync(
                operation.OperationId,
                MemoryLedgerStatus.Running,
                now,
                diagnostic,
                cancellationToken);
        }
        else
        {
            await operationLedgerStore.DeferAsync(
                operation.OperationId,
                now,
                diagnostic,
                incrementRetry: false,
                cancellationToken);
        }

        return MemoryOperationWorkerOutcome.ForRetried(diagnostic);
    }
}
