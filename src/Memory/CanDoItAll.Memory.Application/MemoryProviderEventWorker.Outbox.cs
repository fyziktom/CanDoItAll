using CanDoItAll.Memory.Abstractions;

namespace CanDoItAll.Memory.Application;

public sealed partial class MemoryProviderEventWorker
{
    public async Task<MemoryAsyncWorkerRunResult> DrainOutboxAsync(CancellationToken cancellationToken = default)
    {
        options.Validate();
        var profiles = (await providerProfileStore.ListAsync(cancellationToken))
            .ToDictionary(profile => profile.InstanceId);
        var completed = 0;
        var retried = 0;
        var deadLettered = 0;
        var scanned = 0;
        var diagnostics = new List<string>();

        foreach (var provider in profiles.Values)
        {
            var records = await eventLedgerStore.ListPendingOutboxAsync(
                provider.InstanceId,
                options.MaxBatchSize,
                cancellationToken);
            foreach (var record in records)
            {
                cancellationToken.ThrowIfCancellationRequested();
                scanned++;
                var outcome = await DispatchOutboxAsync(provider, record, cancellationToken);
                completed += outcome.Completed;
                retried += outcome.Retried;
                deadLettered += outcome.DeadLettered;
                diagnostics.Add(outcome.Diagnostic);
            }
        }

        return CreateResult(scanned, completed, retried, deadLettered, 0, 0, 0, diagnostics);
    }

    private async Task<OutboxOutcome> DispatchOutboxAsync(
        MemoryProviderProfile provider,
        MemoryEventOutboxRecord record,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var driver = outboxDrivers.FirstOrDefault(candidate => candidate.DriverKind == provider.DriverKind);
        if (driver is null)
        {
            return await RetryOrFailOutboxAsync(
                record,
                now,
                $"No event outbox driver registered for '{provider.DriverKind}'.",
                cancellationToken);
        }

        var running = record.Status == MemoryLedgerStatus.Running
            ? record
            : await eventLedgerStore.TransitionOutboxAsync(record.OutboxRecordId, MemoryLedgerStatus.Running, now, cancellationToken);
        var dispatch = await driver.DeliverOutboxAsync(provider, running, cancellationToken);
        return dispatch.Kind switch
        {
            MemoryProviderQueueDispatchResultKind.Succeeded =>
                await CompleteOutboxAsync(running, now, dispatch.Diagnostic, cancellationToken),
            MemoryProviderQueueDispatchResultKind.RetryableFailure =>
                await RetryOrFailOutboxAsync(running, now, dispatch.Diagnostic, cancellationToken),
            MemoryProviderQueueDispatchResultKind.TerminalFailure or MemoryProviderQueueDispatchResultKind.UnsupportedCapability =>
                await FailOutboxAsync(running, now, dispatch.Diagnostic, cancellationToken),
            _ => await FailOutboxAsync(running, now, "Malformed memory event outbox dispatch result.", cancellationToken)
        };
    }

    private async Task<OutboxOutcome> RetryOrFailOutboxAsync(
        MemoryEventOutboxRecord record,
        DateTimeOffset now,
        string diagnostic,
        CancellationToken cancellationToken)
    {
        if (record.RetryCount + 1 >= options.MaxRetryAttempts)
        {
            return await FailOutboxAsync(record, now, diagnostic, cancellationToken);
        }

        await eventLedgerStore.DeferOutboxAsync(
            record.OutboxRecordId,
            now,
            incrementRetry: true,
            cancellationToken);
        return OutboxOutcome.ForRetried(diagnostic);
    }

    private async Task<OutboxOutcome> CompleteOutboxAsync(
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

    private async Task<OutboxOutcome> FailOutboxAsync(
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
