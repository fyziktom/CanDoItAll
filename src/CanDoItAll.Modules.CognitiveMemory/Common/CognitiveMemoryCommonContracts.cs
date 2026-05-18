using System.Text.Json.Serialization;

namespace CanDoItAll.Modules.CognitiveMemory;

public readonly record struct CognitiveMemoryOperationId
{
    [JsonConstructor]
    public CognitiveMemoryOperationId(Guid value)
    {
        Value = CognitiveMemoryGuard.EnsureNonEmpty(value, nameof(value));
    }

    public Guid Value { get; }

    public static CognitiveMemoryOperationId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D");
}

public readonly record struct CognitiveMemoryStageId
{
    [JsonConstructor]
    public CognitiveMemoryStageId(string value)
    {
        Value = CognitiveMemoryGuard.EnsureText(value, nameof(value));
    }

    public string Value { get; }

    public override string ToString() => Value;
}

public readonly record struct CognitiveMemorySectionId
{
    [JsonConstructor]
    public CognitiveMemorySectionId(string value)
    {
        Value = CognitiveMemoryGuard.EnsureText(value, nameof(value));
    }

    public string Value { get; }

    public override string ToString() => Value;
}

public readonly record struct CognitiveMemoryEvaluatorProfileId
{
    [JsonConstructor]
    public CognitiveMemoryEvaluatorProfileId(string value)
    {
        Value = CognitiveMemoryGuard.EnsureText(value, nameof(value));
    }

    public string Value { get; }

    public override string ToString() => Value;
}

public readonly record struct CognitiveMemoryProjectionProfileId
{
    [JsonConstructor]
    public CognitiveMemoryProjectionProfileId(string value)
    {
        Value = CognitiveMemoryGuard.EnsureText(value, nameof(value));
    }

    public string Value { get; }

    public override string ToString() => Value;
}

public readonly record struct CognitiveMemoryEmbeddingProfileId
{
    [JsonConstructor]
    public CognitiveMemoryEmbeddingProfileId(string value)
    {
        Value = CognitiveMemoryGuard.EnsureText(value, nameof(value));
    }

    public string Value { get; }

    public override string ToString() => Value;
}

public readonly record struct CognitiveMemoryPayloadSchemaVersion
{
    [JsonConstructor]
    public CognitiveMemoryPayloadSchemaVersion(string value)
    {
        Value = CognitiveMemoryGuard.EnsureText(value, nameof(value));
    }

    public string Value { get; }

    public override string ToString() => Value;
}

public readonly record struct CognitiveMemoryPageCursor
{
    [JsonConstructor]
    public CognitiveMemoryPageCursor(string value)
    {
        Value = CognitiveMemoryGuard.EnsureText(value, nameof(value));
    }

    public string Value { get; }

    public override string ToString() => Value;
}

public enum CognitiveMemoryEvidenceKind
{
    SourceSnapshot = 0,
    SourceItem = 1,
    HumanReview = 2,
    UserCorrection = 3,
    ProbeAnswer = 4,
    RegressionReplay = 5,
    WorkflowOutcome = 6,
    ProcessOutcome = 7,
    ProviderTrace = 8
}

public enum CognitiveMemoryBudgetLimit
{
    ItemCount = 0,
    ByteCount = 1,
    Timeout = 2,
    Cancellation = 3,
    TokenCount = 4,
    SectionCount = 5,
    DetailCount = 6
}

public enum CognitiveMemoryDurablePayloadKind
{
    SourceProvenance = 0,
    RecallTrace = 1,
    ReviewReason = 2,
    ProjectionPayload = 3,
    ProviderTrace = 4,
    TestFixture = 5
}

public sealed record CognitiveMemoryPageRequest
{
    public const int DefaultTake = 100;
    public const int MaxTake = 1000;

    public CognitiveMemoryPageRequest(CognitiveMemoryPageCursor? cursor = null, int take = DefaultTake)
    {
        if (take is < 1 or > MaxTake)
        {
            throw new ArgumentOutOfRangeException(
                nameof(take),
                $"Page size must be between 1 and {MaxTake}; callers must not rely on silent truncation.");
        }

        Cursor = cursor;
        Take = take;
    }

    public CognitiveMemoryPageCursor? Cursor { get; }

    public int Take { get; }
}

public sealed record CognitiveMemoryPage<T>(
    IReadOnlyList<T> Items,
    CognitiveMemoryPageCursor? NextCursor,
    bool HasMore,
    CognitiveMemoryBudgetLimit? LimitingBudget);

public sealed record CognitiveMemoryProcessingBudget
{
    public CognitiveMemoryProcessingBudget(
        int maxItemCount,
        long maxByteCount,
        TimeSpan timeout)
    {
        if (maxItemCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxItemCount), "Item budget must be positive.");
        }

        if (maxByteCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxByteCount), "Byte budget must be positive.");
        }

        if (timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout), "Timeout budget must be positive.");
        }

        MaxItemCount = maxItemCount;
        MaxByteCount = maxByteCount;
        Timeout = timeout;
    }

    public int MaxItemCount { get; }

    public long MaxByteCount { get; }

    public TimeSpan Timeout { get; }
}

public sealed record CognitiveMemoryBudgetDecision(
    bool Accepted,
    CognitiveMemoryBudgetLimit? Limit,
    int AcceptedItemCount,
    long AcceptedByteCount);

public sealed class CognitiveMemoryBudgetTracker
{
    private readonly CognitiveMemoryProcessingBudget budget;
    private readonly DateTimeOffset startedAtUtc;
    private int acceptedItemCount;
    private long acceptedByteCount;

    public CognitiveMemoryBudgetTracker(
        CognitiveMemoryProcessingBudget budget,
        DateTimeOffset startedAtUtc)
    {
        this.budget = budget;
        this.startedAtUtc = startedAtUtc;
    }

    public CognitiveMemoryBudgetDecision TryAccept(
        int itemByteCount,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return new CognitiveMemoryBudgetDecision(false, CognitiveMemoryBudgetLimit.Cancellation, acceptedItemCount, acceptedByteCount);
        }

        if (itemByteCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(itemByteCount), "Item byte count must not be negative.");
        }

        if (nowUtc - startedAtUtc > budget.Timeout)
        {
            return new CognitiveMemoryBudgetDecision(false, CognitiveMemoryBudgetLimit.Timeout, acceptedItemCount, acceptedByteCount);
        }

        if (acceptedItemCount + 1 > budget.MaxItemCount)
        {
            return new CognitiveMemoryBudgetDecision(false, CognitiveMemoryBudgetLimit.ItemCount, acceptedItemCount, acceptedByteCount);
        }

        if (acceptedByteCount + itemByteCount > budget.MaxByteCount)
        {
            return new CognitiveMemoryBudgetDecision(false, CognitiveMemoryBudgetLimit.ByteCount, acceptedItemCount, acceptedByteCount);
        }

        acceptedItemCount++;
        acceptedByteCount += itemByteCount;
        return new CognitiveMemoryBudgetDecision(true, null, acceptedItemCount, acceptedByteCount);
    }
}

public readonly record struct CognitiveMemoryVector
{
    public CognitiveMemoryVector(ReadOnlyMemory<float> values)
    {
        if (values.IsEmpty)
        {
            throw new ArgumentException("Vectors must contain at least one value.", nameof(values));
        }

        Values = values;
    }

    public ReadOnlyMemory<float> Values { get; }

    public int Length => Values.Length;

    public float[] ToArrayForAdapterBoundary() => Values.ToArray();
}
