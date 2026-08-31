namespace CanDoItAll.AgentFramework.ProviderHistory;

public static class HistoryAttemptEvidence {
    public static HistoryEntry Create(HistoryAttemptStart start, HistoryAttemptCompletion? completion = null) =>
        new(start.EntryId, start.Partition, start.RequestId, start.AttemptId,
            HistoryGranularity.ProviderCallAttempt, Normalize(start.StartedAtUtc), HistoryTimeBasis.AttemptStarted,
            Normalize(start.StartedAtUtc), completion is null ? null : Normalize(completion.FinishedAtUtc),
            start.Provider, start.Operation, start.Workload, completion?.Outcome ?? HistoryOutcome.Started,
            start.Caller, completion?.Usage ?? new(HistoryUsageState.Unavailable),
            completion?.Price ?? new(HistoryPriceState.Unpriced), HistoryMetadataAuthority.Standalone,
            HistoryRetentionAuthority.HistoryPolicy,
            start.ContentOwner is null ? HistoryDetailState.NotCaptured : HistoryDetailState.PendingCanonical) {
            CorrelationId = start.CorrelationId,
            ExternalReference = start.ExternalReference,
            RemoteRequest = completion?.RemoteRequest,
            ExpiresAtUtc = Normalize(start.StartedAtUtc.AddDays(start.Policy.Policy.MetadataRetentionDays)),
            Version = completion is null ? 0 : 1
        };

    private static DateTimeOffset Normalize(DateTimeOffset value) =>
        new(value.UtcTicks - value.UtcTicks % 10, TimeSpan.Zero);
}
