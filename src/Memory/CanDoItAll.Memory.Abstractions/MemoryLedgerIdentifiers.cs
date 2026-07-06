using System.Text.Json.Serialization;

namespace CanDoItAll.Memory.Abstractions;

internal static class MemoryLedgerGuard
{
    public static Guid EnsureNonEmpty(Guid value, string parameterName, string label)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException($"Memory {label} must not be empty.", parameterName);
        }

        return value;
    }
}

public readonly record struct MemoryOperationRecordId
{
    [JsonConstructor]
    public MemoryOperationRecordId(Guid value)
    {
        Value = MemoryLedgerGuard.EnsureNonEmpty(value, nameof(value), "operation record id");
    }

    public Guid Value { get; }

    public static MemoryOperationRecordId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D");
}

public readonly record struct MemoryContextDeliveryId
{
    [JsonConstructor]
    public MemoryContextDeliveryId(Guid value)
    {
        Value = MemoryLedgerGuard.EnsureNonEmpty(value, nameof(value), "context delivery id");
    }

    public Guid Value { get; }

    public static MemoryContextDeliveryId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D");
}

public readonly record struct MemoryFeedbackRecordId
{
    [JsonConstructor]
    public MemoryFeedbackRecordId(Guid value)
    {
        Value = MemoryLedgerGuard.EnsureNonEmpty(value, nameof(value), "feedback record id");
    }

    public Guid Value { get; }

    public static MemoryFeedbackRecordId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D");
}

public readonly record struct MemoryEventInboxRecordId
{
    [JsonConstructor]
    public MemoryEventInboxRecordId(Guid value)
    {
        Value = MemoryLedgerGuard.EnsureNonEmpty(value, nameof(value), "event inbox record id");
    }

    public Guid Value { get; }

    public static MemoryEventInboxRecordId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D");
}

public readonly record struct MemoryEventOutboxRecordId
{
    [JsonConstructor]
    public MemoryEventOutboxRecordId(Guid value)
    {
        Value = MemoryLedgerGuard.EnsureNonEmpty(value, nameof(value), "event outbox record id");
    }

    public Guid Value { get; }

    public static MemoryEventOutboxRecordId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D");
}

public readonly record struct MemoryEventDedupeKey
{
    [JsonConstructor]
    public MemoryEventDedupeKey(string value)
    {
        Value = MemoryProtocolGuard.EnsureText(value, nameof(value));
    }

    public string Value { get; }

    public static MemoryEventDedupeKey Create(
        MemoryProviderInstanceId providerInstanceId,
        MemoryProviderEventId eventId)
    {
        MemoryProtocolGuard.EnsureText(providerInstanceId.Value, nameof(providerInstanceId));
        MemoryLedgerGuard.EnsureNonEmpty(eventId.Value, nameof(eventId), "provider event id");
        return new MemoryEventDedupeKey($"{providerInstanceId.Value}:{eventId.Value:D}");
    }

    public override string ToString() => Value;
}
