using CanDoItAll.Memory.Abstractions;

namespace CanDoItAll.Memory.Application;

public sealed class MemoryProviderEventOutboxProcessor(
    IMemoryProviderProfileStore providerProfileStore,
    IMemoryEventLedgerStore eventLedgerStore,
    IEnumerable<IMemoryProviderEventOutboxDriver> outboxDrivers,
    TimeProvider timeProvider,
    MemoryAsyncWorkerOptions options)
{
    private readonly MemoryProviderDriverCatalog<IMemoryProviderEventOutboxDriver> driverCatalog =
        new(outboxDrivers, static driver => driver.DriverKind);

    public async Task<MemoryAsyncWorkerRunResult> DrainAsync(CancellationToken cancellationToken)
    {
        options.Validate();
        var profiles = await providerProfileStore.ListAsync(cancellationToken);
        var completed = 0;
        var retried = 0;
        var deadLettered = 0;
        var scanned = 0;
        var diagnostics = new List<string>();

        foreach (var provider in profiles)
        {
            var records = await eventLedgerStore.ListPendingOutboxAsync(
                provider.InstanceId,
                options.MaxBatchSize,
                cancellationToken);
            foreach (var record in records)
            {
                cancellationToken.ThrowIfCancellationRequested();
                scanned++;
                var outcome = await DispatchSafelyAsync(provider, record, cancellationToken);
                completed += outcome.Completed;
                retried += outcome.Retried;
                deadLettered += outcome.DeadLettered;
                diagnostics.Add(outcome.Diagnostic);
            }
        }

        return new MemoryAsyncWorkerRunResult(
            scanned,
            completed,
            retried,
            deadLettered,
            TimedOut: 0,
            Cancelled: 0,
            Enqueued: 0,
            Duplicates: 0,
            LoopRejected: 0,
            IpfsUnpinRequests: 0,
            diagnostics);
    }

    private async Task<OutboxOutcome> DispatchSafelyAsync(
        MemoryProviderProfile provider,
        MemoryEventOutboxRecord record,
        CancellationToken cancellationToken)
    {
        try
        {
            return await DispatchAsync(provider, record, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return OutboxOutcome.ForRetried(MemoryWorkerExceptionDiagnostic.Create(
                $"Memory event outbox '{record.OutboxRecordId}' processing",
                exception));
        }
    }

    private async Task<OutboxOutcome> DispatchAsync(
        MemoryProviderProfile provider,
        MemoryEventOutboxRecord record,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var driver = driverCatalog.ResolveUnique(provider.DriverKind, out var driverFailure);
        if (driver is null)
        {
            return await RetryOrFailAsync(
                record,
                now,
                driverFailure,
                cancellationToken);
        }

        var running = record.Status == MemoryLedgerStatus.Running
            ? record
            : await eventLedgerStore.TransitionOutboxAsync(
                record.OutboxRecordId,
                MemoryLedgerStatus.Running,
                now,
                cancellationToken);
        MemoryProviderQueueDispatchResult dispatch;
        try
        {
            dispatch = await driver.DeliverOutboxAsync(provider, running, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return await RetryOrFailAsync(
                running,
                now,
                MemoryWorkerExceptionDiagnostic.Create(
                    $"Memory event outbox '{record.OutboxRecordId}' provider delivery",
                    exception),
                cancellationToken);
        }

        return dispatch.Kind switch
        {
            MemoryProviderQueueDispatchResultKind.Succeeded =>
                await CompleteAsync(running, now, dispatch.Diagnostic, cancellationToken),
            MemoryProviderQueueDispatchResultKind.RetryableFailure =>
                await RetryOrFailAsync(running, now, dispatch.Diagnostic, cancellationToken),
            MemoryProviderQueueDispatchResultKind.TerminalFailure or
                MemoryProviderQueueDispatchResultKind.UnsupportedCapability =>
                await FailAsync(running, now, dispatch.Diagnostic, cancellationToken),
            _ => await FailAsync(
                running,
                now,
                "Malformed memory event outbox dispatch result.",
                cancellationToken)
        };
    }

    private async Task<OutboxOutcome> RetryOrFailAsync(
        MemoryEventOutboxRecord record,
        DateTimeOffset now,
        string diagnostic,
        CancellationToken cancellationToken)
    {
        if (record.RetryCount + 1 >= options.MaxRetryAttempts)
        {
            return await FailAsync(record, now, diagnostic, cancellationToken);
        }

        await eventLedgerStore.DeferOutboxAsync(
            record.OutboxRecordId,
            now,
            incrementRetry: true,
            cancellationToken);
        return OutboxOutcome.ForRetried(diagnostic);
    }

    private async Task<OutboxOutcome> CompleteAsync(
        MemoryEventOutboxRecord record,
        DateTimeOffset now,
        string diagnostic,
        CancellationToken cancellationToken)
    {
        await eventLedgerStore.TransitionOutboxAsync(
            record.OutboxRecordId,
            MemoryLedgerStatus.Completed,
            now,
            cancellationToken);
        return OutboxOutcome.ForCompleted(diagnostic);
    }

    private async Task<OutboxOutcome> FailAsync(
        MemoryEventOutboxRecord record,
        DateTimeOffset now,
        string diagnostic,
        CancellationToken cancellationToken)
    {
        await eventLedgerStore.TransitionOutboxAsync(
            record.OutboxRecordId,
            MemoryLedgerStatus.Failed,
            now,
            cancellationToken);
        return OutboxOutcome.ForDeadLettered(diagnostic);
    }

    private sealed record OutboxOutcome(
        int Completed,
        int Retried,
        int DeadLettered,
        string Diagnostic)
    {
        public static OutboxOutcome ForCompleted(string diagnostic) => new(1, 0, 0, diagnostic);

        public static OutboxOutcome ForRetried(string diagnostic) => new(0, 1, 0, diagnostic);

        public static OutboxOutcome ForDeadLettered(string diagnostic) => new(0, 0, 1, diagnostic);
    }
}
