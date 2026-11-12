using CanDoItAll.AppComponents;
using CanDoItAll.AppComponents.Navigation;

namespace CanDoItAll.Modules.CrmHr.Pages;

public static class CrmHrCatalogUrlKeys {
    public const string Search = "q";
    public const string Tags = "tags";
    public const string Page = "page";
}

// The committed query of a routed catalog: the search, the tags, the filter and the one-based page. What the operator
// is still typing never reaches it; the page writes it when the catalog commits a change.
public sealed record CrmHrCatalogUrlState<TFilter>(
    string? Search,
    IReadOnlyList<string> Tags,
    TFilter Filter,
    int Page)
    where TFilter : struct, Enum {
    public PagedRecordQueryState<TFilter> ToQuery() => new(Search ?? string.Empty, Tags, Filter, Page - 1);

    public bool Equals(CrmHrCatalogUrlState<TFilter>? other)
        => other is not null &&
           string.Equals(Search, other.Search, StringComparison.Ordinal) &&
           Tags.SequenceEqual(other.Tags, StringComparer.Ordinal) &&
           EqualityComparer<TFilter>.Default.Equals(Filter, other.Filter) &&
           Page == other.Page;

    public override int GetHashCode() => HashCode.Combine(Search, Tags.Count, Filter, Page);
}

// The catalog keys of one routed page. A catalog whose filter cannot change has no filter key and one without a tag
// filter has no tags key. Search text and tags are the operator's own query input, bounded like the source accepts.
public sealed class CrmHrCatalogUrlCodec<TFilter>
    where TFilter : struct, Enum {
    private readonly string? filterKey;
    private readonly UrlTokenMap<TFilter>? filters;
    private readonly int maximumSearchLength;
    private readonly int maximumTags;

    private CrmHrCatalogUrlCodec(
        string? filterKey,
        UrlTokenMap<TFilter>? filters,
        TFilter defaultFilter,
        int maximumSearchLength,
        int maximumTags) {
        ArgumentOutOfRangeException.ThrowIfLessThan(maximumSearchLength, 1);
        ArgumentOutOfRangeException.ThrowIfNegative(maximumTags);
        if (filters is not null && !filters.Contains(defaultFilter)) {
            throw new ArgumentException($"The default filter {defaultFilter} has no URL token.", nameof(defaultFilter));
        }

        this.filterKey = filterKey;
        this.filters = filters;
        this.maximumSearchLength = maximumSearchLength;
        this.maximumTags = maximumTags;
        Default = new(null, [], defaultFilter, 1);
        List<string> keys = [CrmHrCatalogUrlKeys.Search];
        if (maximumTags > 0) {
            keys.Add(CrmHrCatalogUrlKeys.Tags);
        }

        if (filterKey is not null) {
            keys.Add(filterKey);
        }

        keys.Add(CrmHrCatalogUrlKeys.Page);
        Keys = keys;
    }

    public IReadOnlyList<string> Keys { get; }

    public CrmHrCatalogUrlState<TFilter> Default { get; }

    public static CrmHrCatalogUrlCodec<TFilter> Filtered(
        string filterKey,
        UrlTokenMap<TFilter> filters,
        TFilter defaultFilter,
        int maximumSearchLength,
        int maximumTags = 0) {
        ArgumentException.ThrowIfNullOrWhiteSpace(filterKey);
        ArgumentNullException.ThrowIfNull(filters);
        return new(filterKey, filters, defaultFilter, maximumSearchLength, maximumTags);
    }

    public static CrmHrCatalogUrlCodec<TFilter> Fixed(TFilter filter, int maximumSearchLength, int maximumTags = 0)
        => new(null, null, filter, maximumSearchLength, maximumTags);

    // A live search commit and the correction of a page that no longer exists refine the entry; every other change is
    // an explicit operator action with its own history entry.
    public static UrlHistoryMode HistoryMode(PagedRecordQueryChangeKind kind)
        => kind is PagedRecordQueryChangeKind.Search or PagedRecordQueryChangeKind.Correction
            ? UrlHistoryMode.Replace
            : UrlHistoryMode.Push;

    public CrmHrCatalogUrlState<TFilter> FromQuery(PagedRecordQueryState<TFilter> query) {
        ArgumentNullException.ThrowIfNull(query);
        return new(query.SearchText, query.Tags, query.Filter, query.PageIndex + 1);
    }

    public CrmHrCatalogUrlState<TFilter> Read(UrlQuery query) {
        ArgumentNullException.ThrowIfNull(query);
        return new(
            query.GetText(CrmHrCatalogUrlKeys.Search, maximumSearchLength),
            maximumTags == 0 ? [] : ReadTags(query.Get(CrmHrCatalogUrlKeys.Tags)),
            filters is null ? Default.Filter : query.GetToken(filterKey!, filters, Default.Filter),
            query.GetPage(CrmHrCatalogUrlKeys.Page));
    }

    public void Write(CrmHrCatalogUrlState<TFilter> state, UrlQueryWriter query) {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(query);
        var search = state.Search?.Trim();
        query.Text(CrmHrCatalogUrlKeys.Search, search?.Length <= maximumSearchLength ? search : null);
        if (maximumTags > 0) {
            var tags = NormalizeTags(state.Tags);
            query.Text(CrmHrCatalogUrlKeys.Tags, tags.Count <= maximumTags ? string.Join(',', tags) : null);
        }

        if (filters is not null) {
            query.Token(filterKey!, state.Filter, filters, Default.Filter);
        }

        query.Page(CrmHrCatalogUrlKeys.Page, state.Page);
    }

    // One value of comma separated tags; more tags than the source accepts make the whole value invalid.
    private IReadOnlyList<string> ReadTags(string? value) {
        if (string.IsNullOrWhiteSpace(value)) {
            return [];
        }

        var tags = NormalizeTags(value.Split(','));
        return tags.Count <= maximumTags ? tags : [];
    }

    private static IReadOnlyList<string> NormalizeTags(IEnumerable<string> tags)
        => tags
            .Select(tag => tag.Trim())
            .Where(tag => tag.Length is > 0 and <= CrmPartyCommandLimits.MaximumTagLength && !tag.Contains(','))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
}
