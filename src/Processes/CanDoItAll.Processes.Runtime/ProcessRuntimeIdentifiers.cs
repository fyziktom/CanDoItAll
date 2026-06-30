using CanDoItAll.Processes.Abstractions;

namespace CanDoItAll.Processes.Runtime;

public readonly record struct RuntimeCommandId
{
    public RuntimeCommandId(Guid value)
    {
        Value = RuntimeIdentifierValidation.RequireGuid(value, nameof(value));
    }

    public Guid Value { get; }

    public static RuntimeCommandId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D");
}

public readonly record struct DispatchClaimToken
{
    public DispatchClaimToken(Guid value)
    {
        Value = RuntimeIdentifierValidation.RequireGuid(value, nameof(value));
    }

    public Guid Value { get; }

    public static DispatchClaimToken New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D");
}

public readonly record struct DispatcherOwnerId
{
    public DispatcherOwnerId(string value)
    {
        Value = RuntimeIdentifierValidation.RequireToken(value, nameof(value));
    }

    public string Value { get; }

    public override string ToString() => Value;
}

public readonly record struct StrategyResultIdempotencyKey
{
    public StrategyResultIdempotencyKey(Guid value)
    {
        Value = RuntimeIdentifierValidation.RequireGuid(value, nameof(value));
    }

    public Guid Value { get; }

    public static StrategyResultIdempotencyKey New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D");
}

public readonly record struct RuntimeOutboxMessageId
{
    public RuntimeOutboxMessageId(Guid value)
    {
        Value = RuntimeIdentifierValidation.RequireGuid(value, nameof(value));
    }

    public Guid Value { get; }

    public static RuntimeOutboxMessageId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D");
}

public readonly record struct ArtifactLedgerEventId
{
    public ArtifactLedgerEventId(Guid value)
    {
        Value = RuntimeIdentifierValidation.RequireGuid(value, nameof(value));
    }

    public Guid Value { get; }

    public static ArtifactLedgerEventId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D");
}

internal static class RuntimeIdentifierValidation
{
    public static Guid RequireGuid(Guid value, string parameterName)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("Runtime identifier cannot be empty.", parameterName);
        }

        return value;
    }

    public static string RequireToken(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Runtime token cannot be empty.", parameterName);
        }

        return value.Trim();
    }
}
