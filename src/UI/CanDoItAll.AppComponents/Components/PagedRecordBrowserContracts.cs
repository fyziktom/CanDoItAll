namespace CanDoItAll.AppComponents;

public sealed record PagedRecordRequest<TFilter>(
    string SearchText,
    IReadOnlyList<string> Tags,
    TFilter Filter,
    int PageIndex,
    int PageSize)
    where TFilter : notnull;

public sealed record PagedRecordPage<TKey>(
    IReadOnlyList<PagedRecordOption<TKey>> Items,
    int PageIndex,
    int PageSize,
    int TotalCount)
    where TKey : notnull
{
    public int TotalPages => TotalCount == 0
        ? 0
        : (int)Math.Ceiling(TotalCount / (double)PageSize);
}

public sealed record PagedRecordOption<TKey>(
    TKey Key,
    string Title,
    string KindLabel)
    where TKey : notnull
{
    public string Subtitle { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public string Meta { get; init; } = string.Empty;

    public string Icon { get; init; } = "category";

    public IReadOnlyList<string> Tags { get; init; } = [];

    public bool IsDisabled { get; init; }

    public string DisabledReason { get; init; } = string.Empty;

    public string TestId { get; init; } = string.Empty;
}

public sealed record PagedRecordFilterOption<TFilter>(
    TFilter Value,
    string Label,
    string TestId)
    where TFilter : notnull;

public sealed record PagedRecordSelection<TKey>(TKey Key)
    where TKey : notnull;

public delegate Task<PagedRecordPage<TKey>> PagedRecordLoader<TKey, TFilter>(
    PagedRecordRequest<TFilter> request,
    CancellationToken cancellationToken)
    where TKey : notnull
    where TFilter : notnull;
