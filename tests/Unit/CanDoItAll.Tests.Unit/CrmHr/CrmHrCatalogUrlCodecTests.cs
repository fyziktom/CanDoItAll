using CanDoItAll.AppComponents;
using CanDoItAll.AppComponents.Navigation;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.CrmHr.Pages;

namespace CanDoItAll.Tests.Unit.CrmHr;

// The committed query every routed CRM/HR catalog shares: bounded search text, normalized tags, the filter token and
// the one-based page, each omitted at its default.
public sealed class CrmHrCatalogUrlCodecTests {
    private static readonly CrmHrCatalogUrlCodec<PartyRecordScope> Catalog = DirectoryUrlCodec.Catalog;

    [Theory]
    [InlineData(PagedRecordQueryChangeKind.Search, UrlHistoryMode.Replace)]
    [InlineData(PagedRecordQueryChangeKind.Correction, UrlHistoryMode.Replace)]
    [InlineData(PagedRecordQueryChangeKind.Tags, UrlHistoryMode.Push)]
    [InlineData(PagedRecordQueryChangeKind.Filter, UrlHistoryMode.Push)]
    [InlineData(PagedRecordQueryChangeKind.Reset, UrlHistoryMode.Push)]
    [InlineData(PagedRecordQueryChangeKind.Page, UrlHistoryMode.Push)]
    public void Live_search_and_page_corrections_replace_while_explicit_changes_push(PagedRecordQueryChangeKind kind, UrlHistoryMode expected) {
        Assert.Equal(expected, CrmHrCatalogUrlCodec<PartyRecordScope>.HistoryMode(kind));
    }

    [Fact]
    public void The_default_catalog_writes_nothing_and_its_keys_follow_the_catalog_shape() {
        Assert.Equal("", Format(Catalog, Catalog.Default));
        Assert.Equal(new[] { "q", "tags", "scope", "page" }, Catalog.Keys);
        Assert.Equal(new[] { "q", "tags", "page" }, CrmUrlCodec.Catalog.Keys);
        Assert.Equal(new[] { "q", "kind", "page" }, WorkforceUrlCodec.Catalog.Keys);
        Assert.Equal(new[] { "q", "page" }, CrmHrCatalogUrlCodec<PartyRecordScope>.Fixed(PartyRecordScope.People, 50).Keys);
    }

    [Fact]
    public void Search_text_is_trimmed_and_bounded_by_the_source_limit() {
        var longest = new string('a', PartyRecordQueryLimits.MaximumSearchLength);
        var tooLong = longest + "a";

        Assert.Equal("acme corp", Read(Catalog, "?q=%20acme%20corp%20").Search);
        Assert.Equal("acme corp", Read(Catalog, "?q=acme+corp").Search);
        Assert.Equal(longest, Read(Catalog, "?q=" + longest).Search);
        Assert.Null(Read(Catalog, "?q=" + tooLong).Search);
        Assert.Null(Read(Catalog, "?q=%20%20").Search);
        Assert.Equal("?q=acme%20corp", Format(Catalog, Catalog.Default with { Search = " acme corp " }));
        Assert.Equal("", Format(Catalog, Catalog.Default with { Search = tooLong }));
        Assert.Equal("", Format(Catalog, Catalog.Default with { Search = "   " }));
    }

    [Fact]
    public void Tags_are_trimmed_deduplicated_and_bounded_or_dropped_as_a_whole() {
        var longestTag = new string('t', CrmPartyCommandLimits.MaximumTagLength);
        var allowed = Enumerable.Range(1, PartyRecordQueryLimits.MaximumTagCount).Select(index => $"tag{index}").ToArray();

        Assert.Equal(new[] { "vip", "partner" }, Read(Catalog, "?tags=%20vip%20,,VIP,partner").Tags);
        Assert.Equal(new[] { "vip", "partner" }, Read(Catalog, "?tags=vip%2Cpartner").Tags);
        Assert.Equal(new[] { longestTag }, Read(Catalog, "?tags=" + longestTag).Tags);
        Assert.Equal(new[] { "vip" }, Read(Catalog, "?tags=vip," + longestTag + "t").Tags);
        Assert.Equal(allowed, Read(Catalog, "?tags=" + string.Join(',', allowed)).Tags);
        Assert.Empty(Read(Catalog, "?tags=" + string.Join(',', allowed) + ",tag21").Tags);
        Assert.Empty(Read(Catalog, "?tags=%20,%20").Tags);
        Assert.Equal("?tags=vip%2Cpartner", Format(Catalog, Catalog.Default with { Tags = [" vip ", "VIP", "partner"] }));
        Assert.Equal("", Format(Catalog, Catalog.Default with { Tags = [.. allowed, "tag21"] }));
    }

    [Fact]
    public void A_tag_the_comma_separated_value_cannot_carry_is_not_navigation_state() {
        Assert.Equal("?tags=vip", Format(Catalog, Catalog.Default with { Tags = ["vip", "north,south"] }));
    }

    [Fact]
    public void A_catalog_without_a_tag_filter_ignores_tags() {
        var catalog = WorkforceUrlCodec.Catalog;

        Assert.Empty(Read(catalog, "?tags=vip").Tags);
        Assert.Equal("", Format(catalog, catalog.Default with { Tags = ["vip"] }));
    }

    [Fact]
    public void A_fixed_catalog_has_no_filter_in_the_url() {
        var catalog = CrmUrlCodec.Catalog;

        Assert.Equal(PartyRecordScope.Organizations, catalog.Default.Filter);
        Assert.Equal(PartyRecordScope.Organizations, Read(catalog, "?scope=people").Filter);
        Assert.Equal("", Format(catalog, catalog.Default with { Filter = PartyRecordScope.People }));
    }

    [Theory]
    [InlineData("", 1)]
    [InlineData("?page=1", 1)]
    [InlineData("?page=2", 2)]
    [InlineData("?page=0", 1)]
    [InlineData("?page=-3", 1)]
    [InlineData("?page=two", 1)]
    [InlineData("?page=100000", 100000)]
    [InlineData("?page=100001", 1)]
    public void The_page_is_one_based_and_bounded(string query, int expected) {
        Assert.Equal(expected, Read(Catalog, query).Page);
    }

    [Fact]
    public void The_first_page_is_omitted_and_later_pages_are_written() {
        Assert.Equal("", Format(Catalog, Catalog.Default with { Page = 1 }));
        Assert.Equal("?page=7", Format(Catalog, Catalog.Default with { Page = 7 }));
    }

    [Fact]
    public void The_catalog_query_of_the_browser_is_zero_based_and_converts_both_ways() {
        var state = new CrmHrCatalogUrlState<PartyRecordScope>("acme", ["vip"], PartyRecordScope.People, 3);
        var query = state.ToQuery();

        Assert.Equal(new PagedRecordQueryState<PartyRecordScope>("acme", ["vip"], PartyRecordScope.People, 2), query);
        Assert.Equal(state, Catalog.FromQuery(query));
        Assert.Equal(new PagedRecordQueryState<PartyRecordScope>("", [], PartyRecordScope.All, 0), Catalog.Default.ToQuery());
    }

    [Fact]
    public void Catalog_states_compare_their_tags_by_value_and_order() {
        var left = Catalog.Default with { Tags = ["vip", "partner"] };
        var right = Catalog.Default with { Tags = new List<string> { "vip", "partner" } };

        Assert.Equal(left, right);
        Assert.Equal(left.GetHashCode(), right.GetHashCode());
        Assert.NotEqual(left, Catalog.Default with { Tags = ["partner", "vip"] });
    }

    [Fact]
    public void A_catalog_needs_a_mapped_default_filter_and_valid_bounds() {
        var partial = new UrlTokenMap<PartyRecordScope>((PartyRecordScope.People, "people"));

        Assert.Throws<ArgumentException>(() => CrmHrCatalogUrlCodec<PartyRecordScope>.Filtered("scope", partial, PartyRecordScope.All, 200));
        Assert.Throws<ArgumentException>(() => CrmHrCatalogUrlCodec<PartyRecordScope>.Filtered(" ", DirectoryUrlCodec.Scopes, PartyRecordScope.All, 200));
        Assert.Throws<ArgumentOutOfRangeException>(() => CrmHrCatalogUrlCodec<PartyRecordScope>.Fixed(PartyRecordScope.All, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => CrmHrCatalogUrlCodec<PartyRecordScope>.Fixed(PartyRecordScope.All, 200, -1));
    }

    private static string Format<TFilter>(CrmHrCatalogUrlCodec<TFilter> catalog, CrmHrCatalogUrlState<TFilter> state)
        where TFilter : struct, Enum {
        var writer = new UrlQueryWriter(catalog.Keys);
        catalog.Write(state, writer);
        return UrlLocation.Format(writer.Pairs);
    }

    private static CrmHrCatalogUrlState<TFilter> Read<TFilter>(CrmHrCatalogUrlCodec<TFilter> catalog, string query)
        where TFilter : struct, Enum
        => catalog.Read(UrlQuery.Parse(query));
}
