using System.Globalization;
using System.Text.Json;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Operations;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Persistence.Entities;
using CanDoItAll.AgentFramework.ProviderHistory;
using CanDoItAll.AgentFramework.ProviderHistory.Persistence;
using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.AgentFramework.Llm.SimpleChats.Persistence;

public sealed class LlmChatHistoryProjection(HistoryOutboxWriter outbox) {
    public async Task StageAsync(AppDbContext ownerContext, LlmChatInvocationRecord record, CancellationToken cancellationToken) {
        var partition = await HistoryPartitionStore.GetForWriteAsync(ownerContext, cancellationToken);
        var mutation = Create(record, partition);
        var source = mutation.Source;
        outbox.Stage(ownerContext, mutation);
        if (record.HistoryAttempts.Count == 0) {
            return;
        }
        var prior = await ownerContext.Set<LlmChatInvocationRecordRow>().AsNoTracking()
            .Where(row => row.OperationId == record.OperationId.Value && row.Ordinal < record.Ordinal)
            .OrderBy(row => row.Ordinal).Select(row => row.HistoryAttemptsJson)
            .Take(HistoryAttemptCollection.MaximumAttempts + 1).ToArrayAsync(cancellationToken);
        if (prior.Length > HistoryAttemptCollection.MaximumAttempts) {
            throw new ProviderHistoryException(HistoryFailure.Conflict, "The chat operation exceeds its history evidence bound.");
        }
        var attempts = prior.SelectMany(ParseAttempts).Concat(record.HistoryAttempts).ToArray();
        outbox.Stage(ownerContext, new(source with { Evidence = new(source.Owner.Value) },
            new(record.Ordinal), HistorySourceMutationKind.Upsert, null, []) { Attempts = attempts });
    }

    internal static HistorySourceMutation Create(LlmChatInvocationRecord record, HistoryPartition partition) {
        var source = new CanonicalEvidenceReference(partition, HistorySourceKind.SimpleChat,
            new(record.OperationId.Value.ToString("N")), new(record.Ordinal.ToString(CultureInfo.InvariantCulture)));
        return new(source, new(1), HistorySourceMutationKind.Upsert, LegacyEntry(record, source), []) {
            Attempts = record.HistoryAttempts
        };
    }

    internal static HistoryEntry[] ParseAttempts(string json) =>
        JsonSerializer.Deserialize<HistoryEntry[]>(json)
            ?? throw new InvalidDataException("Stored chat history attempt evidence is null.");

    internal static HistoryEntry LegacyEntry(LlmChatInvocationRecord record, CanonicalEvidenceReference source) {
        var price = record.PricingStatus switch {
            LlmChatInvocationPricingEvidenceStatus.ProviderReported =>
                new HistoryPrice(HistoryPriceState.ProviderReported, record.ProviderCostUsd, "USD",
                    record.PricingProfileHash, record.PricingVersion),
            LlmChatInvocationPricingEvidenceStatus.CalculatedAtExecution =>
                new HistoryPrice(HistoryPriceState.CalculatedAtExecution, record.CalculatedCostUsd, "USD",
                    record.PricingProfileHash, record.PricingVersion),
            _ => new HistoryPrice(HistoryPriceState.Unpriced)
        };
        var knownUsage = record.UsageStatus is LlmChatInvocationUsageEvidenceStatus.Observed
            or LlmChatInvocationUsageEvidenceStatus.LegacyKnownTokens;
        return new(HistoryEntryId.ForCanonical(new(source.Kind, source.Owner, source.Evidence)), source.Partition,
            null, null, HistoryGranularity.LegacyAggregate,
            HistoryStorageTimestamp.Normalize(record.StartedAtUtc), HistoryTimeBasis.CanonicalRecorded,
            null, HistoryStorageTimestamp.Normalize(record.CompletedAtUtc),
            new(new(record.ProviderProfileId), record.ProviderName, record.ProviderKind.ToString(), new(record.Model), new(record.Model)),
            HistoryOperation.CompleteChat, HistoryWorkload.SimpleChat, record.Outcome switch {
                LlmChatInvocationOutcome.Succeeded => HistoryOutcome.Succeeded,
                LlmChatInvocationOutcome.Cancelled => HistoryOutcome.Cancelled,
                _ => HistoryOutcome.Failed
            }, new(HistoryAuthenticationKind.Unknown),
            new(knownUsage ? HistoryUsageState.Complete : HistoryUsageState.Unavailable,
                knownUsage ? record.Usage.InputTokens : null, knownUsage ? record.Usage.OutputTokens : null,
                knownUsage ? record.Usage.CachedInputTokens : null), price,
            HistoryMetadataAuthority.CanonicalProjection, HistoryRetentionAuthority.CanonicalOwner, HistoryDetailState.Canonical) {
                CorrelationId = record.CorrelationId,
                Version = 1
            };
    }
}
