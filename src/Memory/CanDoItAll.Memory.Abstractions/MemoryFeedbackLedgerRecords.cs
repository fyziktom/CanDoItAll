namespace CanDoItAll.Memory.Abstractions;

public sealed record MemoryFeedbackRecord(
    MemoryFeedbackRecordId FeedbackRecordId,
    MemoryContextDeliveryId? ContextDeliveryId,
    MemoryOperationId? OperationId,
    MemoryProviderInstanceId ProviderInstanceId,
    MemoryFeedbackStage Stage,
    MemoryFeedbackOutcome Outcome,
    MemoryFeedbackMatchState MatchState,
    MemoryLedgerRequester Requester,
    MemoryEconomicImpact? EconomicImpact,
    string? UnmatchedReason,
    MemoryLedgerRetentionPolicy Retention,
    MemoryLedgerStatus Status,
    int RetryCount,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    MemoryIpfsSnapshotMetadata? IpfsSnapshot)
{
    public static MemoryFeedbackRecord CreateMatched(
        MemoryFeedbackRecordId feedbackRecordId,
        MemoryContextDeliveryId contextDeliveryId,
        MemoryOperationId operationId,
        MemoryProviderInstanceId providerInstanceId,
        MemoryFeedbackStage stage,
        MemoryFeedbackOutcome outcome,
        MemoryLedgerRequester requester,
        MemoryEconomicImpact? economicImpact,
        MemoryLedgerRetentionPolicy retention,
        DateTimeOffset createdAtUtc,
        MemoryIpfsSnapshotMetadata? ipfsSnapshot = null)
    {
        MemoryLedgerGuard.EnsureNonEmpty(contextDeliveryId.Value, nameof(contextDeliveryId), "context delivery id");
        MemoryLedgerGuard.EnsureNonEmpty(operationId.Value, nameof(operationId), "operation id");
        return Create(
            feedbackRecordId,
            contextDeliveryId,
            operationId,
            providerInstanceId,
            stage,
            outcome,
            MemoryFeedbackMatchState.Matched,
            requester,
            economicImpact,
            unmatchedReason: null,
            retention,
            createdAtUtc,
            ipfsSnapshot);
    }

    public static MemoryFeedbackRecord CreateUnmatched(
        MemoryFeedbackRecordId feedbackRecordId,
        MemoryProviderInstanceId providerInstanceId,
        MemoryFeedbackStage stage,
        MemoryFeedbackOutcome outcome,
        MemoryLedgerRequester requester,
        string unmatchedReason,
        MemoryLedgerRetentionPolicy retention,
        DateTimeOffset createdAtUtc,
        MemoryEconomicImpact? economicImpact = null,
        MemoryIpfsSnapshotMetadata? ipfsSnapshot = null)
    {
        return Create(
            feedbackRecordId,
            contextDeliveryId: null,
            operationId: null,
            providerInstanceId,
            stage,
            outcome,
            MemoryFeedbackMatchState.Unmatched,
            requester,
            economicImpact,
            MemoryProtocolGuard.EnsureText(unmatchedReason, nameof(unmatchedReason)),
            retention,
            createdAtUtc,
            ipfsSnapshot);
    }

    private static MemoryFeedbackRecord Create(
        MemoryFeedbackRecordId feedbackRecordId,
        MemoryContextDeliveryId? contextDeliveryId,
        MemoryOperationId? operationId,
        MemoryProviderInstanceId providerInstanceId,
        MemoryFeedbackStage stage,
        MemoryFeedbackOutcome outcome,
        MemoryFeedbackMatchState matchState,
        MemoryLedgerRequester requester,
        MemoryEconomicImpact? economicImpact,
        string? unmatchedReason,
        MemoryLedgerRetentionPolicy retention,
        DateTimeOffset createdAtUtc,
        MemoryIpfsSnapshotMetadata? ipfsSnapshot)
    {
        MemoryLedgerGuard.EnsureNonEmpty(feedbackRecordId.Value, nameof(feedbackRecordId), "feedback record id");
        MemoryProtocolGuard.EnsureText(providerInstanceId.Value, nameof(providerInstanceId));
        ArgumentNullException.ThrowIfNull(requester);
        ArgumentNullException.ThrowIfNull(retention);

        return new MemoryFeedbackRecord(
            feedbackRecordId,
            contextDeliveryId,
            operationId,
            providerInstanceId,
            stage,
            outcome,
            matchState,
            requester,
            economicImpact,
            unmatchedReason,
            retention,
            MemoryLedgerStatus.Pending,
            RetryCount: 0,
            createdAtUtc,
            UpdatedAtUtc: createdAtUtc,
            ipfsSnapshot);
    }
}
