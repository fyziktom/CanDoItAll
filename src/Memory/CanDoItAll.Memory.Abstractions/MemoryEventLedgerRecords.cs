namespace CanDoItAll.Memory.Abstractions;

public sealed record MemoryEventLoopContext(
    MemoryEventOrigin Origin,
    int HopCount,
    IReadOnlyList<MemoryProviderInstanceId> ProviderHops,
    string? LastAgentId)
{
    public static MemoryEventLoopContext ProviderOrigin(MemoryProviderInstanceId providerInstanceId) =>
        new(
            MemoryEventOrigin.MemoryProvider,
            HopCount: 1,
            ProviderHops: [providerInstanceId],
            LastAgentId: null);
}

public sealed record MemoryEventInboxRecord(
    MemoryEventInboxRecordId InboxRecordId,
    MemoryProviderInstanceId ProviderInstanceId,
    MemoryProviderEventId ProviderEventId,
    MemoryProviderEventKind EventKind,
    MemoryCorrelationId CorrelationId,
    MemoryCausationId CausationId,
    MemoryEventDedupeKey DedupeKey,
    MemoryEventPriority Priority,
    MemoryEventLoopContext LoopContext,
    MemoryLedgerRetentionPolicy Retention,
    MemoryLedgerStatus Status,
    int RetryCount,
    DateTimeOffset ReceivedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    string StatusReason,
    MemoryExtensionData Extensions)
{
    public static MemoryEventInboxRecord Create(
        MemoryEventInboxRecordId inboxRecordId,
        MemoryProviderInstanceId providerInstanceId,
        MemoryProviderEventId providerEventId,
        MemoryProviderEventKind eventKind,
        MemoryCorrelationId correlationId,
        MemoryCausationId causationId,
        MemoryEventPriority priority,
        MemoryEventLoopContext loopContext,
        MemoryLedgerRetentionPolicy retention,
        DateTimeOffset receivedAtUtc,
        MemoryExtensionData? extensions = null)
    {
        MemoryLedgerGuard.EnsureNonEmpty(inboxRecordId.Value, nameof(inboxRecordId), "event inbox record id");
        MemoryProtocolGuard.EnsureText(providerInstanceId.Value, nameof(providerInstanceId));
        MemoryLedgerGuard.EnsureNonEmpty(providerEventId.Value, nameof(providerEventId), "provider event id");
        MemoryLedgerGuard.EnsureNonEmpty(correlationId.Value, nameof(correlationId), "correlation id");
        MemoryLedgerGuard.EnsureNonEmpty(causationId.Value, nameof(causationId), "causation id");
        ArgumentNullException.ThrowIfNull(loopContext);
        ArgumentNullException.ThrowIfNull(retention);

        return new MemoryEventInboxRecord(
            inboxRecordId,
            providerInstanceId,
            providerEventId,
            eventKind,
            correlationId,
            causationId,
            MemoryEventDedupeKey.Create(providerInstanceId, providerEventId),
            priority,
            loopContext,
            retention,
            MemoryLedgerStatus.Pending,
            RetryCount: 0,
            receivedAtUtc,
            UpdatedAtUtc: receivedAtUtc,
            StatusReason: MemoryLedgerStatusReasons.Received,
            extensions ?? MemoryExtensionData.Empty);
    }
}

public sealed record MemoryEventOutboxRecord(
    MemoryEventOutboxRecordId OutboxRecordId,
    MemoryProviderInstanceId ProviderInstanceId,
    MemoryProviderEventId ProviderEventId,
    MemoryEventInboxRecordId? InboxRecordId,
    MemoryEventDedupeKey DedupeKey,
    MemoryLedgerStatus Status,
    int RetryCount,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    string PayloadKind,
    MemoryPayload Payload)
{
    public static MemoryEventOutboxRecord CreateAcknowledgement(
        MemoryEventOutboxRecordId outboxRecordId,
        MemoryProviderInstanceId providerInstanceId,
        MemoryProviderEventId providerEventId,
        MemoryEventInboxRecordId? inboxRecordId,
        DateTimeOffset createdAtUtc,
        MemoryPayload payload)
    {
        MemoryLedgerGuard.EnsureNonEmpty(outboxRecordId.Value, nameof(outboxRecordId), "event outbox record id");
        MemoryProtocolGuard.EnsureText(providerInstanceId.Value, nameof(providerInstanceId));
        MemoryLedgerGuard.EnsureNonEmpty(providerEventId.Value, nameof(providerEventId), "provider event id");
        ArgumentNullException.ThrowIfNull(payload);

        return new MemoryEventOutboxRecord(
            outboxRecordId,
            providerInstanceId,
            providerEventId,
            inboxRecordId,
            MemoryEventDedupeKey.Create(providerInstanceId, providerEventId),
            MemoryLedgerStatus.Pending,
            RetryCount: 0,
            createdAtUtc,
            UpdatedAtUtc: createdAtUtc,
            PayloadKind: MemoryEventOutboxPayloadKinds.Acknowledgement,
            payload);
    }
}
