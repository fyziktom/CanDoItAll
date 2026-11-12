using CanDoItAll.AppComponents.Navigation;
using CanDoItAll.Modules.Memory.Pages;

namespace CanDoItAll.Tests.Unit.Memory;

public sealed class MemoryUrlCodecTests {
    private static readonly MemoryUrlCodec Codec = MemoryUrlCodec.Instance;

    [Theory]
    [InlineData("", "")]
    [InlineData("?tab=providers", "")]
    [InlineData("?tab=Query", "?tab=query")]
    [InlineData("?tab=provider-ui", "?tab=provider-ui")]
    [InlineData("?tab=4", "")]
    [InlineData("?tab=unknown", "")]
    [InlineData("?providerId=provider.a", "?providerId=provider.a")]
    [InlineData("?providerId=%20provider.a%20&tab=events", "?providerId=provider.a&tab=events")]
    [InlineData("?providerId=Provider.A", "?providerId=Provider.A")]
    [InlineData("?providerId=", "")]
    [InlineData("?providerId=bad%0Aid", "")]
    [InlineData("?providerId=team%20knowledge&tab=operations", "?providerId=team%20knowledge&tab=operations")]
    public void Locations_have_one_stable_canonical_form(string query, string canonical) {
        var state = Codec.Read(UrlQuery.Parse(query));

        Assert.Equal(canonical, Codec.Format(state));
        Assert.Equal(state, Codec.Normalize(state));
        Assert.Equal(canonical, Codec.Format(Codec.Read(UrlQuery.Parse(canonical))));
    }

    [Fact]
    public void Every_tab_has_a_stable_token_and_round_trips() {
        Assert.Equal(Enum.GetValues<MemoryWorkspaceTab>(), MemoryUrlCodec.Tabs.Values);
        foreach (var tab in Enum.GetValues<MemoryWorkspaceTab>()) {
            var state = new MemoryUrlState("provider.a", tab);
            Assert.Equal(state, Codec.Read(UrlQuery.Parse(Codec.Format(state))));
        }
    }

    [Fact]
    public void Provider_identifiers_longer_than_the_protocol_allows_are_dropped() {
        var longest = new string('p', MemoryUrlCodec.ProviderIdMaximumLength);
        var tooLong = longest + "p";

        Assert.Equal(longest, Codec.Read(UrlQuery.Parse("?providerId=" + longest)).ProviderId);
        Assert.Null(Codec.Read(UrlQuery.Parse("?providerId=" + tooLong)).ProviderId);
    }

    [Fact]
    public void Composing_keeps_foreign_parameters() {
        var composed = UrlLocation.Compose("http://localhost/memory?utm=mail&TAB=QUERY", Codec, new MemoryUrlState("provider.b", MemoryWorkspaceTab.Query));

        Assert.Equal("http://localhost/memory?providerId=provider.b&tab=query&utm=mail", composed);
    }

    [Fact]
    public void Every_owned_key_has_a_query_trigger_on_the_page() {
        Assert.Empty(UrlStateTriggers.Missing(typeof(MemoryProvidersPage), Codec.Keys));
    }
}
