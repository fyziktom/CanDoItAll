using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.ProviderHistory;
using CanDoItAll.AgentFramework.ProviderHistory.Persistence;
using CanDoItAll.SharedProviders.Abstractions;

namespace CanDoItAll.Modules.AgentFramework.ProviderManagement;

public sealed class SharedProviderHistoryProjection(
    HistoryPartitionStore partitions,
    HistoryRetentionStore retention,
    HistoryOutboxWriter outbox) {
    public Task<DateTimeOffset> ResolveRetentionAsync(DateTimeOffset startedAtUtc,
        DateTimeOffset? requestedDeadline, CancellationToken cancellationToken)
        => retention.ResolveMetadataRetentionForWriteAsync(startedAtUtc, requestedDeadline, cancellationToken);

    public async Task StageAsync(SharedProviderInvocationRecord record, CancellationToken cancellationToken) {
        var partition = await partitions.GetForWriteAsync(cancellationToken);
        await outbox.StageAsync(Create(record, partition), cancellationToken);
    }

    public static HistorySourceMutation Create(SharedProviderInvocationRecord record, HistoryPartition partition) {
        var id = record.Id.ToString("N");
        var started = HistoryStorageTimestamp.Normalize(record.StartedAtUtc);
        var entry = new HistoryEntry(new(record.Id), partition, new(record.Id), new(record.Id),
            HistoryGranularity.ProviderCallAttempt, started, HistoryTimeBasis.AttemptStarted,
            started, HistoryStorageTimestamp.Normalize(record.CompletedAtUtc),
            new(new(record.ProviderProfileId), record.ProviderNameSnapshot, record.ProviderKindSnapshot?.ToString() ?? "",
                new(record.PublicModelId.Value), new(record.UpstreamModelId)),
            record.Operation == SharedProviderRelayOperation.ImageGenerations ? HistoryOperation.GenerateImage : HistoryOperation.CompleteChat,
            HistoryWorkload.SharedRelay, record.Outcome switch {
                SharedProviderInvocationOutcome.InProgress => HistoryOutcome.Started,
                SharedProviderInvocationOutcome.Succeeded => HistoryOutcome.Succeeded,
                SharedProviderInvocationOutcome.Cancelled => HistoryOutcome.Cancelled,
                _ => record.FinalizationRecovered ? HistoryOutcome.Interrupted : HistoryOutcome.Failed
            },
            SharedProviderCallerHistoryMapper.Map(record.CallerIdentity, record.AuthenticatedSubject),
            new(record.UsageCompleteness switch {
                SharedProviderMetadataCompleteness.Complete => HistoryUsageState.Complete,
                SharedProviderMetadataCompleteness.Partial => HistoryUsageState.Partial,
                _ => HistoryUsageState.Unavailable
            }, record.InputTokenCount, record.OutputTokenCount, record.CachedInputTokenCount,
                record.CacheWriteTokenCount, record.ReasoningTokenCount, record.ImageCount),
            record.PriceEvidence is { } price ? ProviderHistoryPriceMapping.From(price) :
                new(HistoryPriceState.Unpriced, record.Price),
            HistoryMetadataAuthority.CanonicalProjection, HistoryRetentionAuthority.HistoryPolicy, HistoryDetailState.NotCaptured) {
            CorrelationId = record.CorrelationId,
            ExternalReference = record.AccessContextReference is { } externalReference
                ? new(externalReference.Value, record.AccessContextReferenceType?.Value)
                : null,
            ExpiresAtUtc = HistoryStorageTimestamp.Normalize(record.DeleteAfterUtc),
            Version = record.HistoryVersion
        };
        return new(new(partition, HistorySourceKind.SharedRelay, new(id), new(id)),
            new(record.HistoryVersion), HistorySourceMutationKind.Upsert, entry, []) { Role = HistoryOwnerRole.PrimaryEvidence };
    }
}
