using System.Text.Json.Serialization;

namespace CanDoItAll.Memory.Abstractions;

public readonly record struct MemoryOperationId
{
    [JsonConstructor]
    public MemoryOperationId(Guid value)
    {
        Value = MemoryProtocolGuard.EnsureNonEmpty(value, nameof(value));
    }

    public Guid Value { get; }

    public static MemoryOperationId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D");
}

public readonly record struct MemoryContextPackId
{
    [JsonConstructor]
    public MemoryContextPackId(Guid value)
    {
        Value = MemoryProtocolGuard.EnsureNonEmpty(value, nameof(value));
    }

    public Guid Value { get; }

    public static MemoryContextPackId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D");
}

public readonly record struct MemoryCorrelationId
{
    [JsonConstructor]
    public MemoryCorrelationId(Guid value)
    {
        Value = MemoryProtocolGuard.EnsureNonEmpty(value, nameof(value));
    }

    public Guid Value { get; }

    public static MemoryCorrelationId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D");
}

public readonly record struct MemoryCausationId
{
    [JsonConstructor]
    public MemoryCausationId(Guid value)
    {
        Value = MemoryProtocolGuard.EnsureNonEmpty(value, nameof(value));
    }

    public Guid Value { get; }

    public static MemoryCausationId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D");
}
