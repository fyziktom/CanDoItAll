using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.ProviderHistory;
using CanDoItAll.AgentFramework.ProviderHistory.Persistence;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.SharedProviders.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.AgentFramework.ProviderManagement;

public sealed class SharedProviderHistoryProjection(HistoryOutboxWriter outbox) {
    public async Task<DateTimeOffset> ResolveRetentionAsync(AppDbContext ownerContext, DateTimeOffset startedAtUtc,
        DateTimeOffset? requestedDeadline, CancellationToken cancellationToken) {
        if (requestedDeadline is { } deadline) {
            return HistoryStorageTimestamp.Normalize(deadline);
        }
        var partition = await HistoryPartitionStore.GetForWriteAsync(ownerContext, cancellationToken);
        var policy = ownerContext.Set<HistoryPolicyRow>().Local.SingleOrDefault(row => row.PartitionId == partition.StorageLineageId)
            ?? await ownerContext.Set<HistoryPolicyRow>().AsNoTracking()
                .SingleAsync(row => row.PartitionId == partition.StorageLineageId, cancellationToken);
        return startedAtUtc.AddDays(policy.MetadataRetentionDays);
    }

    public async Task StageAsync(AppDbContext ownerContext, SharedProviderInvocationRecord record, CancellationToken cancellationToken) {
        var partition = await HistoryPartitionStore.GetForWriteAsync(ownerContext, cancellationToken);
        outbox.Stage(ownerContext, Create(record, partition));
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
