namespace CanDoItAll.AppComponents;

public enum PagedRecordResultsScrollMode
{
    Page,
    Bounded
}

public enum PagedRecordGridMode
{
    Fluid,
    BoundedCatalog
}

public enum PagedRecordBadgeTone
{
    Neutral,
    Info,
    Accent,
    Success,
    Warning,
    Teal
}

public enum PagedRecordStatusTone
{
    Neutral,
    Info,
    Success,
    Warning,
    Danger
}

public sealed record PagedRecordCornerStatus(
    string Text,
    PagedRecordStatusTone Tone);

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

    public string SubtitleTooltip { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public string Meta { get; init; } = string.Empty;

    public string Icon { get; init; } = "category";

    public IReadOnlyList<string> Tags { get; init; } = [];

    public PagedRecordBadgeTone KindTone { get; init; } = PagedRecordBadgeTone.Neutral;

    public PagedRecordBadgeTone TagTone { get; init; } = PagedRecordBadgeTone.Neutral;

    public PagedRecordCornerStatus? CornerStatus { get; init; }

    public bool PinKindToUpperLeft { get; init; }

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

// The committed query of a browser whose owner keeps it, for example in the page URL: the trimmed search text, the
// normalized tags, the filter and the zero-based page. Tags compare in order.
public sealed record PagedRecordQueryState<TFilter>(
    string SearchText,
    IReadOnlyList<string> Tags,
    TFilter Filter,
    int PageIndex)
    where TFilter : notnull
{
    public bool Equals(PagedRecordQueryState<TFilter>? other)
        => other is not null &&
           string.Equals(SearchText, other.SearchText, StringComparison.Ordinal) &&
           Tags.SequenceEqual(other.Tags, StringComparer.Ordinal) &&
           EqualityComparer<TFilter>.Default.Equals(Filter, other.Filter) &&
           PageIndex == other.PageIndex;

    public override int GetHashCode()
        => HashCode.Combine(SearchText, Tags.Count, Filter, PageIndex);
}

// What committed a query change. A debounced search commit and a correction of a page that does not exist are
// refinements of the current view; tag, filter, reset and page changes are explicit operator actions.
public enum PagedRecordQueryChangeKind
{
    Search,
    Tags,
    Filter,
    Reset,
    Page,
    Correction
}

public sealed record PagedRecordQueryChange<TFilter>(
    PagedRecordQueryState<TFilter> Query,
    PagedRecordQueryChangeKind Kind)
    where TFilter : notnull;

/// <param name="IsInteractive">
/// False while the browser is prerendered or statically rendered; templated select controls must stay disabled until
/// it is true, otherwise a user's early click is dropped without feedback.
/// </param>
public sealed record PagedRecordItemTemplateContext<TKey>(
    PagedRecordOption<TKey> Option,
    bool IsSelected,
    Func<Task> SelectAsync,
    bool IsInteractive = true)
    where TKey : notnull;

public delegate Task<PagedRecordPage<TKey>> PagedRecordLoader<TKey, TFilter>(
    PagedRecordRequest<TFilter> request,
    CancellationToken cancellationToken)
    where TKey : notnull
    where TFilter : notnull;
