using CanDoItAll.Memory.Abstractions;

namespace CanDoItAll.Memory.Application;

public sealed class MemoryFeedbackWorker(
    IMemoryProviderProfileStore providerProfileStore,
    IMemoryFeedbackLedgerStore feedbackLedgerStore,
    IEnumerable<IMemoryProviderFeedbackDeliveryDriver> feedbackDrivers,
    TimeProvider timeProvider,
    MemoryAsyncWorkerOptions options) : IMemoryFeedbackWorker
{
    public async Task<MemoryAsyncWorkerRunResult> DeliverPendingFeedbackAsync(CancellationToken cancellationToken = default)
    {
        options.Validate();
        var now = timeProvider.GetUtcNow();
        var feedback = await feedbackLedgerStore.ListDueForDeliveryAsync(
            now,
            options.PollingStaleAfter,
            options.MaxBatchSize,
            cancellationToken);
        var profiles = (await providerProfileStore.ListAsync(cancellationToken))
            .ToDictionary(profile => profile.InstanceId);
        var completed = 0;
        var retried = 0;
        var deadLettered = 0;
        var diagnostics = new List<string>();

        foreach (var record in feedback)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!profiles.TryGetValue(record.ProviderInstanceId, out var provider))
            {
                var retry = await RetryOrFailAsync(record, now, "Memory feedback provider profile is no longer registered.", cancellationToken);
                retried += retry.Retried;
                deadLettered += retry.DeadLettered;
                diagnostics.Add(retry.Diagnostic);
                continue;
            }

            var driver = feedbackDrivers.FirstOrDefault(candidate => candidate.DriverKind == provider.DriverKind);
            if (driver is null)
            {
                var retry = await RetryOrFailAsync(record, now, $"No feedback delivery driver registered for '{provider.DriverKind}'.", cancellationToken);
                retried += retry.Retried;
                deadLettered += retry.DeadLettered;
                diagnostics.Add(retry.Diagnostic);
                continue;
            }

            var running = record.Status == MemoryLedgerStatus.Running
                ? record
                : await feedbackLedgerStore.TransitionAsync(record.FeedbackRecordId, MemoryLedgerStatus.Running, now, "Delivering feedback to memory provider.", cancellationToken);
            var dispatch = await driver.DeliverFeedbackAsync(provider, running, cancellationToken);
            var applied = await ApplyDispatchResultAsync(running, dispatch, now, cancellationToken);
            completed += applied.Completed;
            retried += applied.Retried;
            deadLettered += applied.DeadLettered;
            diagnostics.Add(applied.Diagnostic);
        }

        return new MemoryAsyncWorkerRunResult(
            feedback.Count,
            completed,
            retried,
            deadLettered,
            0,
            0,
            0,
            0,
            0,
            0,
            diagnostics);
    }

    private async Task<FeedbackOutcome> ApplyDispatchResultAsync(
        MemoryFeedbackRecord feedback,
        MemoryProviderQueueDispatchResult dispatch,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        return dispatch.Kind switch
        {
            MemoryProviderQueueDispatchResultKind.Succeeded =>
                await CompleteAsync(feedback, now, dispatch.Diagnostic, cancellationToken),
            MemoryProviderQueueDispatchResultKind.RetryableFailure =>
                await RetryOrFailAsync(feedback, now, dispatch.Diagnostic, cancellationToken),
            MemoryProviderQueueDispatchResultKind.TerminalFailure or MemoryProviderQueueDispatchResultKind.UnsupportedCapability =>
                await FailAsync(feedback, now, dispatch.Diagnostic, cancellationToken),
            _ => await FailAsync(feedback, now, "Malformed memory feedback dispatch result.", cancellationToken)
        };
    }

    private async Task<FeedbackOutcome> CompleteAsync(
        MemoryFeedbackRecord feedback,
        DateTimeOffset now,
        string diagnostic,
        CancellationToken cancellationToken)
    {
        await feedbackLedgerStore.TransitionAsync(
            feedback.FeedbackRecordId,
            MemoryLedgerStatus.Completed,
            now,
            diagnostic,
            cancellationToken);
        return FeedbackOutcome.ForCompleted(diagnostic);
    }

    private async Task<FeedbackOutcome> RetryOrFailAsync(
        MemoryFeedbackRecord feedback,
        DateTimeOffset now,
        string diagnostic,
        CancellationToken cancellationToken)
    {
        if (feedback.RetryCount + 1 >= options.MaxRetryAttempts)
        {
            return await FailAsync(feedback, now, diagnostic, cancellationToken);
        }

        await feedbackLedgerStore.DeferAsync(feedback.FeedbackRecordId, now, incrementRetry: true, cancellationToken);
        return FeedbackOutcome.ForRetried(diagnostic);
    }

    private async Task<FeedbackOutcome> FailAsync(
        MemoryFeedbackRecord feedback,
        DateTimeOffset now,
        string diagnostic,
        CancellationToken cancellationToken)
    {
        await feedbackLedgerStore.TransitionAsync(
            feedback.FeedbackRecordId,
            MemoryLedgerStatus.Failed,
            now,
            diagnostic,
            cancellationToken);
        return FeedbackOutcome.ForDeadLettered(diagnostic);
    }

    private sealed record FeedbackOutcome(
        int Completed,
        int Retried,
        int DeadLettered,
        string Diagnostic)
    {
        public static FeedbackOutcome ForCompleted(string diagnostic) => new(1, 0, 0, diagnostic);
        public static FeedbackOutcome ForRetried(string diagnostic) => new(0, 1, 0, diagnostic);
        public static FeedbackOutcome ForDeadLettered(string diagnostic) => new(0, 0, 1, diagnostic);
    }
}
