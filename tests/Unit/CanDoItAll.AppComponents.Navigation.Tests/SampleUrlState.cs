using CanDoItAll.AppComponents.Navigation;

namespace CanDoItAll.Tests.Unit.Navigation;

internal enum SampleTab {
    Overview,
    Items,
    History
}

internal enum SampleSection {
    General,
    Advanced
}

internal sealed record SampleUrlState(
    SampleTab Tab = SampleTab.Overview,
    Guid? ItemId = null,
    SampleSection? Section = null,
    string? Search = null,
    int Page = 1);

internal sealed class SampleUrlCodec : IUrlStateCodec<SampleUrlState> {
    public const string TabKey = "tab";
    public const string ItemKey = "itemId";
    public const string SectionKey = "section";
    public const string SearchKey = "q";
    public const string PageKey = "page";

    public static SampleUrlCodec Instance { get; } = new();

    public static UrlTokenMap<SampleTab> Tabs { get; } = new UrlTokenMap<SampleTab>(
        (SampleTab.Overview, "overview"),
        (SampleTab.Items, "items"),
        (SampleTab.History, "history")).WithAliases(("list", SampleTab.Items));

    public static UrlTokenMap<SampleSection> Sections { get; } = new(
        (SampleSection.General, "general"),
        (SampleSection.Advanced, "advanced"));

    public IReadOnlyList<string> Keys { get; } = [TabKey, ItemKey, SectionKey, SearchKey, PageKey];

    public SampleUrlState Read(UrlQuery query) {
        var tab = query.GetToken(TabKey, Tabs, SampleTab.Overview);
        var itemId = tab == SampleTab.Items ? query.GetId(ItemKey) : null;
        return new(
            tab,
            itemId,
            itemId is null ? null : query.GetToken(SectionKey, Sections),
            tab == SampleTab.Items ? query.GetText(SearchKey, 64) : null,
            tab == SampleTab.Items ? query.GetPage(PageKey) : 1);
    }

    public void Write(SampleUrlState state, UrlQueryWriter query) {
        query.Token(TabKey, state.Tab, Tabs, SampleTab.Overview);
        if (state.Tab != SampleTab.Items) {
            return;
        }
        query.Id(ItemKey, state.ItemId);
        if (state.ItemId is not null) {
            query.Token(SectionKey, state.Section, Sections);
        }
        query.Text(SearchKey, state.Search);
        query.Page(PageKey, state.Page);
    }
}
