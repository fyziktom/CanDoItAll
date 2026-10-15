using CanDoItAll.AppComponents.Navigation;
using CanDoItAll.Modules.Workspace;
using CanDoItAll.Modules.Workspace.Pages;

namespace CanDoItAll.Tests.Unit.Workspace;

public sealed class SettingsUrlCodecTests {
    private const string Record = "6f1f0a51-6a3d-4a8f-9f0e-0c2d6f7a4b11";
    private static readonly Guid RecordId = Guid.Parse(Record);
    private static readonly SettingsUrlCodec Codec = SettingsUrlCodec.Instance;

    [Theory]
    [InlineData("", "")]
    [InlineData("?tab=workspace", "")]
    [InlineData("?tab=WORKSPACE", "")]
    [InlineData("?tab=invalid", "")]
    [InlineData("?tab=Secrets", "?tab=secrets")]
    [InlineData("?tab=data-sources", "?tab=data-sources")]
    [InlineData("?tab=storage", "?tab=storage")]
    [InlineData("?tab=files", "?tab=files")]
    [InlineData("?tab=provider-history", "?tab=provider-history")]
    [InlineData("?tab=providers", "?tab=providers")]
    [InlineData("?tab=api-access", "?tab=api-access")]
    [InlineData("?tab=secrets&secretId=6F1F0A51-6A3D-4A8F-9F0E-0C2D6F7A4B11", "?tab=secrets&secretId=" + Record)]
    [InlineData("?tab=data-sources&profileId=" + Record, "?tab=data-sources&profileId=" + Record)]
    [InlineData("?tab=storage&storageId=" + Record, "?tab=storage&storageId=" + Record)]
    [InlineData("?tab=storage&storageId=not-a-guid", "?tab=storage")]
    [InlineData("?tab=secrets&secretId=00000000-0000-0000-0000-000000000000", "?tab=secrets")]
    [InlineData("?tab=api-access&q=%20alice%20&page=1", "?tab=api-access&q=alice")]
    [InlineData("?tab=api-access&page=3", "?tab=api-access&page=3")]
    [InlineData("?tab=api-access&q=alice&page=0", "?tab=api-access&q=alice")]
    [InlineData("?tab=api-access&page=abc", "?tab=api-access")]
    public void Locations_have_one_stable_canonical_form(string query, string canonical) {
        var state = Codec.Read(UrlQuery.Parse(query));

        Assert.Equal(canonical, Codec.Format(state));
        Assert.Equal(state, Codec.Normalize(state));
        Assert.Equal(canonical, Codec.Format(Codec.Read(UrlQuery.Parse(canonical))));
    }

    [Theory]
    [InlineData("?tab=files&secretId=" + Record, "?tab=files")]
    [InlineData("?secretId=" + Record, "")]
    [InlineData("?tab=secrets&profileId=" + Record + "&storageId=" + Record, "?tab=secrets")]
    [InlineData("?tab=data-sources&secretId=" + Record + "&q=alice&page=2", "?tab=data-sources")]
    [InlineData("?tab=storage&profileId=" + Record, "?tab=storage")]
    public void A_selection_or_query_on_a_tab_that_does_not_use_it_is_dropped(string query, string canonical) {
        Assert.Equal(canonical, Codec.Format(Codec.Read(UrlQuery.Parse(query))));
    }

    [Fact]
    public void Search_text_longer_than_the_owner_accepts_is_dropped() {
        var tooLong = new string('a', SettingsUrlCodec.SearchMaximumLength + 1);
        var longest = new string('b', SettingsUrlCodec.SearchMaximumLength);

        Assert.Null(Codec.Read(UrlQuery.Parse("?tab=api-access&q=" + tooLong)).Search);
        Assert.Equal(longest, Codec.Read(UrlQuery.Parse("?tab=api-access&q=" + longest)).Search);
    }

    [Fact]
    public void States_round_trip_through_their_canonical_form() {
        SettingsUrlState[] states = [
            new(),
            new(WorkspaceSection.Secrets, SecretId: RecordId),
            new(WorkspaceSection.DataSources, ProfileId: RecordId),
            new(WorkspaceSection.Storage, StorageId: RecordId),
            new(WorkspaceSection.ApiAccess, Search: "alice", Page: 4),
            new(WorkspaceSection.Files),
            new(WorkspaceSection.ProviderHistory),
            new(WorkspaceSection.Providers)
        ];

        foreach (var state in states) {
            Assert.Equal(state, Codec.Read(UrlQuery.Parse(Codec.Format(state))));
        }
    }

    [Fact]
    public void Inbound_shell_and_activity_links_are_already_canonical() {
        Assert.Equal("/settings", Codec.Href(SettingsUrlCodec.Path, new()));
        Assert.Equal("/settings?tab=data-sources", Codec.Href(SettingsUrlCodec.Path, new(WorkspaceSection.DataSources)));
        Assert.Equal("/settings?tab=storage", Codec.Href(SettingsUrlCodec.Path, new(WorkspaceSection.Storage)));
        Assert.Equal($"/settings?tab=storage&storageId={Record}", Codec.Href(SettingsUrlCodec.Path, new(WorkspaceSection.Storage, StorageId: RecordId)));
    }

    [Fact]
    public void Composing_a_selection_keeps_foreign_parameters_and_the_fragment() {
        var composed = UrlLocation.Compose("http://localhost/settings?TAB=Secrets&utm=mail#editor", Codec,
            new SettingsUrlState(WorkspaceSection.Secrets, SecretId: RecordId));

        Assert.Equal($"http://localhost/settings?tab=secrets&secretId={Record}&utm=mail#editor", composed);
    }

    [Fact]
    public void Every_owned_key_has_a_query_trigger_on_the_page() {
        Assert.Empty(UrlStateTriggers.Missing(typeof(SettingsPage), Codec.Keys));
    }
}
