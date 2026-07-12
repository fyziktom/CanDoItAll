using System.Text.Json.Serialization;

namespace CanDoItAll.Memory.Abstractions;

public readonly record struct MemorySourceSnapshotId
{
    [JsonConstructor]
    public MemorySourceSnapshotId(string value)
    {
        Value = MemoryProtocolGuard.EnsureText(value, nameof(value));
    }

    public string Value { get; }

    public static MemorySourceSnapshotId Parse(string value) => new(value);

    public override string ToString() => Value;
}

public readonly record struct MemorySourceRequestId
{
    [JsonConstructor]
    public MemorySourceRequestId(string value)
    {
        Value = MemoryProtocolGuard.EnsureText(value, nameof(value));
    }

    public string Value { get; }

    public static MemorySourceRequestId Parse(string value) => new(value);

    public override string ToString() => Value;
}

public readonly record struct MemoryProviderEventId
{
    [JsonConstructor]
    public MemoryProviderEventId(Guid value)
    {
        Value = MemoryProtocolGuard.EnsureNonEmpty(value, nameof(value));
    }

    public Guid Value { get; }

    public static MemoryProviderEventId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D");
}

public readonly record struct MemoryFeedbackHandle
{
    [JsonConstructor]
    public MemoryFeedbackHandle(string value)
    {
        Value = MemoryProtocolGuard.EnsureIdentifier(value, nameof(value));
    }

    public string Value { get; }

    public static MemoryFeedbackHandle Parse(string value) => new(value);

    public override string ToString() => Value;
}
