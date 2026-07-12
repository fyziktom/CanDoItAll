namespace CanDoItAll.Memory.Abstractions;

public sealed record MemoryOperationRecord(
    MemoryOperationRecordId RecordId,
    MemoryOperationId OperationId,
    MemoryProviderInstanceId ProviderInstanceId,
    MemoryCapabilityId RequestedCapability,
    MemoryOperationKind OperationKind,
    MemoryLedgerRequester Requester,
    MemoryCorrelationId CorrelationId,
    MemoryCausationId CausationId,
    IReadOnlyList<MemorySourceSnapshotId> SourceSnapshotIds,
    MemoryLedgerRetentionPolicy Retention,
    MemoryLedgerStatus Status,
    int RetryCount,
    int TransitionCount,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    string StatusReason,
    MemoryIpfsSnapshotMetadata? IpfsSnapshot,
    MemoryExtensionData Extensions)
{
    public static MemoryOperationRecord Create(
        MemoryOperationRecordId recordId,
        MemoryOperationId operationId,
        MemoryProviderInstanceId providerInstanceId,
        MemoryCapabilityId requestedCapability,
        MemoryOperationKind operationKind,
        MemoryLedgerRequester requester,
        MemoryCorrelationId correlationId,
        MemoryCausationId causationId,
        IReadOnlyList<MemorySourceSnapshotId> sourceSnapshotIds,
        MemoryLedgerRetentionPolicy retention,
        DateTimeOffset createdAtUtc,
        MemoryIpfsSnapshotMetadata? ipfsSnapshot = null,
        MemoryExtensionData? extensions = null)
    {
        EnsureRecordIds(recordId, operationId, providerInstanceId, correlationId, causationId);
        ArgumentNullException.ThrowIfNull(requester);
        ArgumentNullException.ThrowIfNull(retention);

        return new MemoryOperationRecord(
            recordId,
            operationId,
            providerInstanceId,
            requestedCapability,
            operationKind,
            requester,
            correlationId,
            causationId,
            sourceSnapshotIds.ToArray(),
            retention,
            MemoryLedgerStatus.Pending,
            RetryCount: 0,
            TransitionCount: 0,
            createdAtUtc,
            UpdatedAtUtc: createdAtUtc,
            CompletedAtUtc: null,
            StatusReason: MemoryLedgerStatusReasons.Created,
            ipfsSnapshot,
            extensions ?? MemoryExtensionData.Empty);
    }

    private static void EnsureRecordIds(
        MemoryOperationRecordId recordId,
        MemoryOperationId operationId,
        MemoryProviderInstanceId providerInstanceId,
        MemoryCorrelationId correlationId,
        MemoryCausationId causationId)
    {
        MemoryLedgerGuard.EnsureNonEmpty(recordId.Value, nameof(recordId), "operation record id");
        MemoryLedgerGuard.EnsureNonEmpty(operationId.Value, nameof(operationId), "operation id");
        MemoryProtocolGuard.EnsureText(providerInstanceId.Value, nameof(providerInstanceId));
        MemoryLedgerGuard.EnsureNonEmpty(correlationId.Value, nameof(correlationId), "correlation id");
        MemoryLedgerGuard.EnsureNonEmpty(causationId.Value, nameof(causationId), "causation id");
    }
}

public sealed record MemoryContextDeliveryRecord(
    MemoryContextDeliveryId ContextDeliveryId,
    MemoryOperationId OperationId,
    MemoryContextPackId ContextPackId,
    MemoryProviderInstanceId ProviderInstanceId,
    MemoryLedgerRequester Requester,
    IReadOnlyList<MemorySourceSnapshotId> SourceSnapshotIds,
    MemoryLedgerRetentionPolicy Retention,
    DateTimeOffset DeliveredAtUtc,
    MemoryIpfsSnapshotMetadata? IpfsSnapshot)
{
    public static MemoryContextDeliveryRecord Create(
        MemoryContextDeliveryId contextDeliveryId,
        MemoryOperationId operationId,
        MemoryContextPackId contextPackId,
        MemoryProviderInstanceId providerInstanceId,
        MemoryLedgerRequester requester,
        IReadOnlyList<MemorySourceSnapshotId> sourceSnapshotIds,
        MemoryLedgerRetentionPolicy retention,
        DateTimeOffset deliveredAtUtc,
        MemoryIpfsSnapshotMetadata? ipfsSnapshot = null)
    {
        MemoryLedgerGuard.EnsureNonEmpty(contextDeliveryId.Value, nameof(contextDeliveryId), "context delivery id");
        MemoryLedgerGuard.EnsureNonEmpty(operationId.Value, nameof(operationId), "operation id");
        MemoryLedgerGuard.EnsureNonEmpty(contextPackId.Value, nameof(contextPackId), "context pack id");
        MemoryProtocolGuard.EnsureText(providerInstanceId.Value, nameof(providerInstanceId));
        ArgumentNullException.ThrowIfNull(requester);
        ArgumentNullException.ThrowIfNull(retention);

        return new MemoryContextDeliveryRecord(
            contextDeliveryId,
            operationId,
            contextPackId,
            providerInstanceId,
            requester,
            sourceSnapshotIds.ToArray(),
            retention,
            deliveredAtUtc,
            ipfsSnapshot);
    }
}
