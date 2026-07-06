using CanDoItAll.Memory.Abstractions;

namespace CanDoItAll.Memory.Application;

public sealed partial class MemoryProviderEventWorker(
    IMemoryProviderProfileStore providerProfileStore,
    IMemoryEventLedgerStore eventLedgerStore,
    IEnumerable<IMemoryProviderEventPollDriver> pollDrivers,
    IEnumerable<IMemoryProviderEventOutboxDriver> outboxDrivers,
    TimeProvider timeProvider,
    MemoryAsyncWorkerOptions options) : IMemoryProviderEventWorker
{
    public async Task<MemoryAsyncWorkerRunResult> PollProviderEventsAsync(CancellationToken cancellationToken = default)
    {
        options.Validate();
        var profiles = await providerProfileStore.ListAsync(cancellationToken);
        var diagnostics = new List<string>();
        var enqueued = 0;
        var duplicates = 0;
        var loopRejected = 0;
        var retried = 0;
        var scanned = 0;

        foreach (var provider in profiles.Where(profile => profile.IsEnabled && profile.Manifest.InteractionSupport.SupportsProviderEvents))
        {
            cancellationToken.ThrowIfCancellationRequested();
            scanned++;
            var driver = pollDrivers.FirstOrDefault(candidate => candidate.DriverKind == provider.DriverKind);
            if (driver is null)
            {
                retried++;
                diagnostics.Add($"No provider event poll driver registered for '{provider.DriverKind}'.");
                continue;
            }

            var poll = await driver.PollEventsAsync(provider, cancellationToken);
            if (poll.Kind != MemoryProviderEventPollResultKind.Events)
            {
                retried++;
                diagnostics.Add(poll.Diagnostic);
                continue;
            }

            foreach (var providerEvent in poll.Events.Take(options.MaxBatchSize))
            {
                var admission = await AdmitProviderEventAsync(
                    provider,
                    providerEvent,
                    MemoryEventLoopContext.ProviderOrigin(provider.InstanceId),
                    cancellationToken);
                enqueued += admission.Status == MemoryEventAdmissionStatus.Accepted ? 1 : 0;
                duplicates += admission.Status == MemoryEventAdmissionStatus.Duplicate ? 1 : 0;
                loopRejected += admission.Status == MemoryEventAdmissionStatus.LoopRejected ? 1 : 0;
                diagnostics.Add(admission.Diagnostic);
            }
        }

        return CreateResult(scanned, 0, retried, 0, enqueued, duplicates, loopRejected, diagnostics);
    }

    public async Task<MemoryEventAdmissionResult> AdmitProviderEventAsync(
        MemoryProviderProfile provider,
        MemoryProviderEvent providerEvent,
        MemoryEventLoopContext loopContext,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(providerEvent);
        ArgumentNullException.ThrowIfNull(loopContext);

        var now = timeProvider.GetUtcNow();
        var record = MemoryEventInboxRecord.Create(
            MemoryEventInboxRecordId.New(),
            provider.InstanceId,
            providerEvent.EventId,
            providerEvent.EventKind,
            providerEvent.CorrelationId,
            providerEvent.CausationId,
            MemoryEventPriority.Normal,
            loopContext,
            MemoryLedgerRetentionPolicy.Expiring(
                now.Add(options.EventRetentionExpiresAfter),
                now.Add(options.EventRetentionForgetsAfter)),
            now);
        if (await eventLedgerStore.ContainsInboxDedupeKeyAsync(record.DedupeKey, cancellationToken))
        {
            return MemoryEventAdmissionResult.Rejected(
                MemoryEventAdmissionStatus.Duplicate,
                $"Memory provider event '{record.ProviderEventId}' was already admitted.");
        }

        var admission = MemoryEventAdmissionRules.EvaluateIncoming(
            record,
            [],
            options.EventLoopGuardPolicy);
        if (!admission.DispatchAllowed)
        {
            return admission;
        }

        await eventLedgerStore.EnqueueInboxAsync(record, cancellationToken);
        return admission;
    }

    public async Task<MemoryAsyncWorkerRunResult> DrainInboxAsync(CancellationToken cancellationToken = default)
    {
        var profiles = await providerProfileStore.ListAsync(cancellationToken);
        var completed = 0;
        var enqueued = 0;
        var diagnostics = new List<string>();
        foreach (var provider in profiles)
        {
            var records = await eventLedgerStore.ListPendingInboxAsync(provider.InstanceId, options.MaxBatchSize, cancellationToken);
            foreach (var record in records)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await eventLedgerStore.TransitionInboxAsync(
                    record.InboxRecordId,
                    MemoryLedgerStatus.Completed,
                    timeProvider.GetUtcNow(),
                    "Provider event admitted to host inbox.",
                    cancellationToken);
                await eventLedgerStore.EnqueueOutboxAsync(CreateAcknowledgement(record), cancellationToken);
                completed++;
                enqueued++;
                diagnostics.Add($"Completed memory provider event inbox '{record.InboxRecordId}'.");
            }
        }

        return CreateResult(completed, completed, 0, 0, enqueued, 0, 0, diagnostics);
    }

    private MemoryEventOutboxRecord CreateAcknowledgement(MemoryEventInboxRecord inbox)
    {
        return MemoryEventOutboxRecord.CreateAcknowledgement(
            MemoryEventOutboxRecordId.New(),
            inbox.ProviderInstanceId,
            inbox.ProviderEventId,
            inbox.InboxRecordId,
            timeProvider.GetUtcNow(),
            MemoryPayload.FromText("accepted"));
    }

    private static MemoryAsyncWorkerRunResult CreateResult(
        int scanned,
        int completed,
        int retried,
        int deadLettered,
        int enqueued,
        int duplicates,
        int loopRejected,
        IReadOnlyList<string> diagnostics) =>
        new(scanned, completed, retried, deadLettered, 0, 0, enqueued, duplicates, loopRejected, 0, diagnostics);
}
