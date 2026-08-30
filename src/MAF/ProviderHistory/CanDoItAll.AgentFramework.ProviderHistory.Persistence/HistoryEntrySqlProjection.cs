using System.Linq.Expressions;

namespace CanDoItAll.AgentFramework.ProviderHistory.Persistence;

public static class HistoryEntrySqlProjection {
    public static Expression<Func<HistoryEntryRow, HistoryEntry>> For(HistoryPartition partition) => row => new HistoryEntry(
        new HistoryEntryId(row.Id), partition,
        row.RequestId.HasValue ? new ProviderRequestId(row.RequestId.Value) : null,
        row.AttemptId.HasValue ? new ProviderAttemptId(row.AttemptId.Value) : null,
        row.Granularity, row.SortAtUtc, row.TimeBasis, row.StartedAtUtc, row.FinishedAtUtc,
        new HistoryProvider(row.ProviderId.HasValue ? new ProviderIdentity(row.ProviderId.Value) : null,
            row.ProviderName, row.ProviderKind,
            row.RequestedModel != null ? new ProviderModelIdentity(row.RequestedModel) : null,
            row.ResolvedModel != null ? new ProviderModelIdentity(row.ResolvedModel) : null),
        row.Operation, row.Workload, row.Outcome,
        new HistoryCaller(row.AuthenticationKind,
            row.CredentialId.HasValue ? new ManagedCredentialId(row.CredentialId.Value) : null,
            row.Issuer, row.Subject, row.CallerName),
        new HistoryUsage(row.UsageState, row.InputTokens, row.OutputTokens, row.CachedInputTokens,
            row.CacheWriteTokens, row.ReasoningTokens, row.ImageCount),
        new HistoryPrice(row.PriceState, row.Amount, row.Currency, row.PriceHash, row.PriceVersion) { SourceRevision = row.PriceSourceRevision },
        row.MetadataAuthority, row.RetentionAuthority, row.DetailState) {
            CorrelationId = row.CorrelationId,
            ExternalReference = row.ExternalReferenceValue != null
                ? new HistoryExternalReference(row.ExternalReferenceValue, row.ExternalReferenceType)
                : null,
            RemoteRequest = row.RemoteSourceId.HasValue && row.RemoteRequestId != null
                ? new RemoteRequestReference(row.RemoteSourceId.Value, row.RemoteRequestId) : null,
            ExpiresAtUtc = row.ExpiresAtUtc,
            Version = row.Version
        };
}
