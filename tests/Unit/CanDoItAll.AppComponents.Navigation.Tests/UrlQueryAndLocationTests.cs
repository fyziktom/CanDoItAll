using CanDoItAll.AppComponents.Navigation;

namespace CanDoItAll.Tests.Unit.Navigation;

public sealed class UrlQueryAndLocationTests {
    [Fact]
    public void Parse_decodes_values_matches_keys_ignoring_case_and_keeps_repeated_keys() {
        var query = UrlQuery.Parse("?Tab=items&q=a+b%20c&flag&tag=x&tag=y&=orphan&&");

        Assert.Equal("items", query.Get("tab"));
        Assert.Equal("a b c", query.Get("Q"));
        Assert.Equal(string.Empty, query.Get("flag"));
        Assert.True(query.Contains("FLAG"));
        Assert.Equal(["x", "y"], query.GetAll("tag"));
        Assert.Null(query.Get("missing"));
        Assert.Equal(5, query.Pairs.Count);
    }

    [Fact]
    public void FromUri_ignores_path_and_fragment() {
        var query = UrlQuery.FromUri("http://localhost/agents?tab=items#section-2");

        Assert.Equal("items", query.Get("tab"));
        Assert.Single(query.Pairs);
    }

    [Fact]
    public void Compose_replaces_owned_keys_and_preserves_foreign_text_and_fragment() {
        var composed = UrlLocation.Compose(
            "http://localhost/items?utm_source=mail&TAB=history&debug&page=7#details",
            SampleUrlCodec.Instance,
            new SampleUrlState(SampleTab.Items, Search: "red car", Page: 2));

        Assert.Equal("http://localhost/items?tab=items&q=red%20car&page=2&utm_source=mail&debug#details", composed);
    }

    [Fact]
    public void Compose_removes_every_owned_key_for_the_default_state() {
        var composed = UrlLocation.Compose(
            "/items?tab=items&itemId=8c7b1f7e-2a43-4bd9-9d2b-5b1c6f0d1e11&section=advanced&q=x&page=3",
            SampleUrlCodec.Instance,
            new SampleUrlState());

        Assert.Equal("/items", composed);
    }

    [Theory]
    [InlineData("http://localhost/agents?tab=items#x", "/agents?tab=items#x")]
    [InlineData("https://example.test:5032", "/")]
    [InlineData("https://example.test?x=1", "/?x=1")]
    [InlineData("/already/relative?q=1", "/already/relative?q=1")]
    public void PathAndQuery_strips_only_scheme_and_authority(string uri, string expected) {
        Assert.Equal(expected, UrlLocation.PathAndQuery(uri));
    }

    [Fact]
    public void Codec_format_and_normalize_follow_the_canonical_write() {
        var id = Guid.Parse("8c7b1f7e-2a43-4bd9-9d2b-5b1c6f0d1e11");
        var codec = SampleUrlCodec.Instance;

        Assert.Equal("?tab=items&itemId=8c7b1f7e-2a43-4bd9-9d2b-5b1c6f0d1e11&section=advanced",
            codec.Format(new(SampleTab.Items, id, SampleSection.Advanced)));
        Assert.Equal(new SampleUrlState(SampleTab.History), codec.Normalize(new(SampleTab.History, id, SampleSection.Advanced, "x", 4)));
        Assert.Equal("/items?tab=items&q=a", codec.Href("/items", new(SampleTab.Items, Search: "  a ")));
    }

    [Fact]
    public void Owned_values_name_every_owned_key_with_its_canonical_value_or_null() {
        var id = Guid.Parse("8c7b1f7e-2a43-4bd9-9d2b-5b1c6f0d1e11");

        var values = SampleUrlCodec.Instance.OwnedValues(new SampleUrlState(SampleTab.Items, id, Page: 2));

        KeyValuePair<string, string?>[] expected = [
            new(SampleUrlCodec.TabKey, "items"),
            new(SampleUrlCodec.ItemKey, id.ToString("D")),
            new(SampleUrlCodec.SectionKey, null),
            new(SampleUrlCodec.SearchKey, null),
            new(SampleUrlCodec.PageKey, "2")
        ];
        Assert.Equal(expected, values);
    }
}
