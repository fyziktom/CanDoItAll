using CanDoItAll.Memory.Abstractions;

namespace CanDoItAll.Memory.Application;

internal sealed class MemoryAsyncOperationProcessor
{
    private readonly MemoryProviderDriverCatalog<IMemoryProviderOperationStatusDriver> driverCatalog;
    private readonly MemoryOperationPollResultApplier resultApplier;

    public MemoryAsyncOperationProcessor(
        IMemoryOperationLedgerStore operationLedgerStore,
        IEnumerable<IMemoryProviderOperationStatusDriver> statusDrivers,
        MemoryAsyncWorkerOptions options)
    {
        driverCatalog = new(statusDrivers, static driver => driver.DriverKind);
        resultApplier = new MemoryOperationPollResultApplier(operationLedgerStore, options);
    }

    public async Task<MemoryOperationWorkerOutcome> ProcessAsync(
        MemoryOperationRecord operation,
        MemoryProviderProfile? provider,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        try
        {
            return await ProcessCoreAsync(operation, provider, now, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return MemoryOperationWorkerOutcome.ForRetried(
                MemoryWorkerExceptionDiagnostic.Create(
                    $"Memory operation '{operation.OperationId}' processing",
                    exception));
        }
    }

    private async Task<MemoryOperationWorkerOutcome> ProcessCoreAsync(
        MemoryOperationRecord operation,
        MemoryProviderProfile? provider,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (provider is null)
        {
            return await resultApplier.RetryOrFailAsync(
                operation,
                now,
                "Memory provider profile is no longer registered.",
                cancellationToken);
        }

        if (await EvaluateScheduleAsync(operation, provider, now, cancellationToken) is { } scheduledOutcome)
        {
            return scheduledOutcome;
        }

        var driver = driverCatalog.ResolveUnique(provider.DriverKind, out var driverFailure);
        if (driver is null)
        {
            return await resultApplier.RetryOrFailAsync(
                operation,
                now,
                driverFailure,
                cancellationToken);
        }

        MemoryProviderOperationPollResult pollResult;
        try
        {
            pollResult = await driver.PollOperationAsync(provider, operation, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return await resultApplier.RetryOrFailAsync(
                operation,
                now,
                MemoryWorkerExceptionDiagnostic.Create(
                    $"Memory operation '{operation.OperationId}' provider poll",
                    exception),
                cancellationToken);
        }

        return await resultApplier.ApplyAsync(
            operation,
            pollResult,
            provider,
            now,
            cancellationToken);
    }

    private async Task<MemoryOperationWorkerOutcome?> EvaluateScheduleAsync(
        MemoryOperationRecord operation,
        MemoryProviderProfile provider,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var accepted = operation.Extensions.GetAcceptedOperation();
        if (accepted is not null)
        {
            if (accepted.OperationId != operation.OperationId || accepted.PollAfter <= TimeSpan.Zero)
            {
                return await resultApplier.FailAsync(
                    operation,
                    now,
                    "Persisted memory operation acceptance metadata is invalid.",
                    cancellationToken);
            }

            if (accepted.ExpiresAtUtc <= now)
            {
                return await resultApplier.TimeOutAsync(
                    operation,
                    now,
                    "Memory provider operation acceptance expired.",
                    cancellationToken);
            }

            if (now - operation.UpdatedAtUtc < accepted.PollAfter)
            {
                return MemoryOperationWorkerOutcome.ForRetried(
                    $"Memory operation '{operation.OperationId}' is not due for provider polling yet.");
            }
        }

        return operation.CreatedAtUtc.Add(provider.Manifest.Limits.OperationTimeout) <= now
            ? await resultApplier.TimeOutAsync(
                operation,
                now,
                "Memory operation timed out.",
                cancellationToken)
            : null;
    }
}
