namespace CanDoItAll.Memory.Abstractions;

public readonly record struct MemoryProviderResponseSizeLimit
{
    public const long DefaultMaximumBytes = 4 * 1024 * 1024;
    public const long AbsoluteMaximumBytes = 8 * 1024 * 1024;

    private const long JsonEnvelopeOverheadBytes = 64 * 1024;
    private const int MaximumJsonEscapeExpansion = 6;

    public static MemoryProviderResponseSizeLimit Default { get; } = new(DefaultMaximumBytes);

    public MemoryProviderResponseSizeLimit(long maximumBytes)
    {
        if (maximumBytes is <= 0 or > AbsoluteMaximumBytes)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumBytes),
                $"Response size limit must be between 1 and {AbsoluteMaximumBytes} bytes.");
        }

        MaximumBytes = maximumBytes;
    }

    public long MaximumBytes { get; }

    public MemoryProviderResponseSizeLimit ConstrainToJsonEnvelope(MemoryBudget budget)
    {
        ArgumentNullException.ThrowIfNull(budget);
        EnsureValid();

        var expandableSourceBytes = Math.Min(
            budget.MaxSourceBytes,
            (AbsoluteMaximumBytes - JsonEnvelopeOverheadBytes) / MaximumJsonEscapeExpansion);
        var budgetAwareMaximum = JsonEnvelopeOverheadBytes +
            (expandableSourceBytes * MaximumJsonEscapeExpansion);
        return new MemoryProviderResponseSizeLimit(
            Math.Min(MaximumBytes, budgetAwareMaximum));
    }

    public void EnsureValid()
    {
        if (MaximumBytes is <= 0 or > AbsoluteMaximumBytes)
        {
            throw new InvalidOperationException(
                $"Response size limit must be between 1 and {AbsoluteMaximumBytes} bytes.");
        }
    }
}
