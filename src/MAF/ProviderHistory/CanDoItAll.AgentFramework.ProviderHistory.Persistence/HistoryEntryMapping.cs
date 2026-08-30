namespace CanDoItAll.AgentFramework.ProviderHistory.Persistence;

internal static class HistoryEntryMapping {
    internal static HistoryEntryRow From(HistoryEntry entry) => new() {
        Id = entry.Id.Value,
        PartitionId = entry.Partition.StorageLineageId,
        RequestId = entry.RequestId?.Value,
        AttemptId = entry.AttemptId?.Value,
        Granularity = entry.Granularity,
        SortAtUtc = HistoryStorageTimestamp.Normalize(entry.SortAtUtc),
        TimeBasis = entry.TimeBasis,
        StartedAtUtc = HistoryStorageTimestamp.Normalize(entry.StartedAtUtc),
        FinishedAtUtc = HistoryStorageTimestamp.Normalize(entry.FinishedAtUtc),
        ProviderId = entry.Provider.Id?.Value,
        ProviderName = entry.Provider.Name,
        ProviderKind = entry.Provider.Kind,
        RequestedModel = entry.Provider.RequestedModel?.Value,
        ResolvedModel = entry.Provider.ResolvedModel?.Value,
        Operation = entry.Operation,
        Workload = entry.Workload,
        Outcome = entry.Outcome,
        AuthenticationKind = entry.Caller.Kind,
        CredentialId = entry.Caller.CredentialId?.Value,
        Issuer = entry.Caller.Issuer,
        Subject = entry.Caller.Subject,
        CallerName = entry.Caller.DisplayName,
        CorrelationId = entry.CorrelationId,
        ExternalReferenceType = entry.ExternalReference?.Type,
        ExternalReferenceValue = entry.ExternalReference?.Value,
        UsageState = entry.Usage.State,
        InputTokens = entry.Usage.InputTokens,
        OutputTokens = entry.Usage.OutputTokens,
        CachedInputTokens = entry.Usage.CachedInputTokens,
        CacheWriteTokens = entry.Usage.CacheWriteTokens,
        ReasoningTokens = entry.Usage.ReasoningTokens,
        ImageCount = entry.Usage.ImageCount,
        PriceState = entry.Price.State,
        Amount = entry.Price.Amount,
        Currency = entry.Price.Currency,
        PriceHash = entry.Price.ProfileHash,
        PriceVersion = entry.Price.Version,
        PriceSourceRevision = entry.Price.SourceRevision,
        MetadataAuthority = entry.MetadataAuthority,
        RetentionAuthority = entry.RetentionAuthority,
        DetailState = entry.DetailState,
        ExpiresAtUtc = HistoryStorageTimestamp.Normalize(entry.ExpiresAtUtc),
        RemoteSourceId = entry.RemoteRequest?.ConfiguredSourceId,
        RemoteRequestId = entry.RemoteRequest?.PublisherRequestId,
        Version = entry.Version
    };

    internal static HistoryEntry ToEntry(HistoryEntryRow row, HistoryPartition partition) => new(
        new(row.Id), partition,
        row.RequestId is { } request ? new ProviderRequestId(request) : null,
        row.AttemptId is { } attempt ? new ProviderAttemptId(attempt) : null,
        row.Granularity, row.SortAtUtc, row.TimeBasis, row.StartedAtUtc, row.FinishedAtUtc,
        new(row.ProviderId is { } provider ? new ProviderIdentity(provider) : null,
            row.ProviderName, row.ProviderKind,
            row.RequestedModel is { } requested ? new ProviderModelIdentity(requested) : null,
            row.ResolvedModel is { } resolved ? new ProviderModelIdentity(resolved) : null),
        row.Operation, row.Workload, row.Outcome,
        new(row.AuthenticationKind, row.CredentialId is { } credential ? new ManagedCredentialId(credential) : null,
            row.Issuer, row.Subject, row.CallerName),
        new(row.UsageState, row.InputTokens, row.OutputTokens, row.CachedInputTokens,
            row.CacheWriteTokens, row.ReasoningTokens, row.ImageCount),
        new(row.PriceState, row.Amount, row.Currency, row.PriceHash, row.PriceVersion) { SourceRevision = row.PriceSourceRevision },
        row.MetadataAuthority, row.RetentionAuthority, row.DetailState) {
        CorrelationId = row.CorrelationId,
        ExternalReference = row.ExternalReferenceValue is { } externalReference
            ? new(externalReference, row.ExternalReferenceType)
            : null,
        RemoteRequest = row.RemoteSourceId is { } remote && row.RemoteRequestId is { } remoteRequest
            ? new(remote, remoteRequest) : null,
        ExpiresAtUtc = row.ExpiresAtUtc,
        Version = row.Version
    };

    internal static HistoryEntryRow Started(HistoryAttemptStart start) => From(new(
        start.EntryId, start.Partition, start.RequestId, start.AttemptId,
        HistoryGranularity.ProviderCallAttempt, start.StartedAtUtc, HistoryTimeBasis.AttemptStarted,
        start.StartedAtUtc, null, start.Provider, start.Operation, start.Workload, HistoryOutcome.Started,
        start.Caller, new(HistoryUsageState.Unavailable), new(HistoryPriceState.Unpriced),
        HistoryMetadataAuthority.Standalone, HistoryRetentionAuthority.HistoryPolicy,
        start.ContentOwner is null ? HistoryDetailState.NotCaptured : HistoryDetailState.PendingCanonical) {
        CorrelationId = start.CorrelationId,
        ExpiresAtUtc = start.StartedAtUtc.AddDays(start.Policy.Policy.MetadataRetentionDays)
    });

    internal static void Complete(HistoryEntryRow row, HistoryAttemptCompletion completion) {
        row.Outcome = completion.Outcome;
        row.FinishedAtUtc = HistoryStorageTimestamp.Normalize(completion.FinishedAtUtc);
        row.UsageState = completion.Usage.State;
        row.InputTokens = completion.Usage.InputTokens;
        row.OutputTokens = completion.Usage.OutputTokens;
        row.CachedInputTokens = completion.Usage.CachedInputTokens;
        row.CacheWriteTokens = completion.Usage.CacheWriteTokens;
        row.ReasoningTokens = completion.Usage.ReasoningTokens;
        row.ImageCount = completion.Usage.ImageCount;
        row.PriceState = completion.Price.State;
        row.Amount = completion.Price.Amount;
        row.Currency = completion.Price.Currency;
        row.PriceHash = completion.Price.ProfileHash;
        row.PriceVersion = completion.Price.Version;
        row.PriceSourceRevision = completion.Price.SourceRevision;
        row.RemoteSourceId = completion.RemoteRequest?.ConfiguredSourceId;
        row.RemoteRequestId = completion.RemoteRequest?.PublisherRequestId;
        row.Version++;
    }
}
