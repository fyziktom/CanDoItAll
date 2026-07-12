using CanDoItAll.Memory.Abstractions;

namespace CanDoItAll.Memory.Application;

public sealed class MemoryProviderEventInboxProcessor(
    IMemoryEventLedgerStore eventLedgerStore,
    TimeProvider timeProvider,
    MemoryAsyncWorkerOptions options)
{
    internal async Task<MemoryEventInboxOutcome> ProcessAsync(
        MemoryEventInboxRecord record,
        CancellationToken cancellationToken)
    {
        var active = record;
        try
        {
            if (active.Status != MemoryLedgerStatus.Running)
            {
                active = await eventLedgerStore.TransitionInboxAsync(
                    active.InboxRecordId,
                    MemoryLedgerStatus.Running,
                    timeProvider.GetUtcNow(),
                    "Processing provider event inbox record.",
                    cancellationToken);
            }

            await eventLedgerStore.EnqueueOutboxAsync(CreateAcknowledgement(active), cancellationToken);
            await eventLedgerStore.TransitionInboxAsync(
                active.InboxRecordId,
                MemoryLedgerStatus.Completed,
                timeProvider.GetUtcNow(),
                "Provider event admitted to host inbox.",
                cancellationToken);
            return MemoryEventInboxOutcome.ForCompleted(
                $"Completed memory provider event inbox '{active.InboxRecordId}'.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var diagnostic = MemoryWorkerExceptionDiagnostic.Create(
                $"Memory provider event inbox '{record.InboxRecordId}' processing",
                exception);
            return await RetryOrFailAsync(active, diagnostic, cancellationToken);
        }
    }

    private async Task<MemoryEventInboxOutcome> RetryOrFailAsync(
        MemoryEventInboxRecord record,
        string diagnostic,
        CancellationToken cancellationToken)
    {
        if (record.RetryCount + 1 >= options.MaxRetryAttempts)
        {
            await eventLedgerStore.TransitionInboxAsync(
                record.InboxRecordId,
                MemoryLedgerStatus.Failed,
                timeProvider.GetUtcNow(),
                diagnostic,
                cancellationToken);
            return MemoryEventInboxOutcome.ForDeadLettered(diagnostic);
        }

        await eventLedgerStore.DeferInboxAsync(
            record.InboxRecordId,
            timeProvider.GetUtcNow(),
            diagnostic,
            incrementRetry: true,
            cancellationToken);
        return MemoryEventInboxOutcome.ForRetried(diagnostic);
    }

    private MemoryEventOutboxRecord CreateAcknowledgement(MemoryEventInboxRecord inbox) =>
        MemoryEventOutboxRecord.CreateAcknowledgement(
            MemoryEventOutboxRecordId.New(),
            inbox.ProviderInstanceId,
            inbox.ProviderEventId,
            inbox.InboxRecordId,
            timeProvider.GetUtcNow(),
            MemoryPayload.FromText("accepted"));
}

internal sealed record MemoryEventInboxOutcome(
    int Completed,
    int Retried,
    int DeadLettered,
    int Enqueued,
    string Diagnostic)
{
    public static MemoryEventInboxOutcome ForCompleted(string diagnostic) => new(1, 0, 0, 1, diagnostic);
    public static MemoryEventInboxOutcome ForRetried(string diagnostic) => new(0, 1, 0, 0, diagnostic);
    public static MemoryEventInboxOutcome ForDeadLettered(string diagnostic) => new(0, 0, 1, 0, diagnostic);
}
