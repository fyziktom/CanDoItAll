using CanDoItAll.AppComponents.Navigation;
using CanDoItAll.Modules.Plugins.Pages;
using CanDoItAll.Modules.Plugins.Presentation;
using CanDoItAll.Plugins.Abstractions;
using CanDoItAll.Plugins.UI;

namespace CanDoItAll.Tests.Unit.Plugins;

public sealed class PluginsUrlCodecTests {
    private const string Connection = "6f1f0a51-6a3d-4a8f-9f0e-0c2d6f7a4b11";
    private static readonly PluginsUrlCodec Codec = PluginsUrlCodec.Instance;

    [Theory]
    [InlineData("", "")]
    [InlineData("?pluginId=Office365.Mail", "?pluginId=office365.mail")]
    [InlineData("?pluginId=bad%20id", "")]
    [InlineData("?pluginId=bad%2Fid", "")]
    [InlineData("?pluginId=office365.mail&section=MAIN", "?pluginId=office365.mail")]
    [InlineData("?pluginId=office365.mail&section=connections", "?pluginId=office365.mail&section=connections")]
    [InlineData("?section=unknown", "")]
    [InlineData("?section=grants", "?section=grants")]
    [InlineData("?tab=connections", "?section=connections")]
    [InlineData("?tab=Connections&section=grants", "?section=grants")]
    [InlineData("?section=logs&logs=all", "?section=logs&logs=all")]
    [InlineData("?section=logs&logs=selected", "?section=logs")]
    [InlineData("?section=logs&logs=everything", "?section=logs")]
    [InlineData("?section=settings&logs=all", "?section=settings")]
    [InlineData("?dialog=packages", "?dialog=packages")]
    [InlineData("?dialog=other", "")]
    [InlineData("?oauth=connected&connectionId=" + Connection, "")]
    [InlineData("?pluginId=office365.mail&section=connections&oauth=failed&reason=access_denied&connectionId=" + Connection,
        "?pluginId=office365.mail&section=connections")]
    public void Locations_have_one_stable_canonical_form(string query, string canonical) {
        var state = Codec.Read(UrlQuery.Parse(query));

        Assert.Equal(canonical, Codec.Format(state));
        Assert.Equal(Codec.Normalize(state), Codec.Normalize(Codec.Normalize(state)));
        Assert.Equal(canonical, Codec.Format(Codec.Read(UrlQuery.Parse(canonical))));
    }

    [Fact]
    public void The_one_shot_OAuth_return_is_read_but_never_written() {
        var state = Codec.Read(UrlQuery.Parse($"?oauth=Failed&connectionId={Connection}&reason=access_denied"));

        Assert.Equal(new PluginOAuthReturn(PluginOAuthOutcome.Failed, new PluginConnectionId(Guid.Parse(Connection)), "access_denied"), state.OAuthReturn);
        Assert.Null(Codec.Normalize(state).OAuthReturn);
        Assert.Equal(string.Empty, Codec.Format(state));
    }

    [Theory]
    [InlineData("?oauth=failed&reason=%3Cscript%3E")]
    [InlineData("?oauth=failed&reason=two%20words")]
    [InlineData("?oauth=failed&connectionId=not-a-guid")]
    public void Unsafe_or_malformed_OAuth_details_are_dropped_but_the_outcome_is_kept(string query) {
        var oauth = Codec.Read(UrlQuery.Parse(query)).OAuthReturn;

        Assert.Equal(new PluginOAuthReturn(PluginOAuthOutcome.Failed), oauth);
    }

    [Theory]
    [InlineData("?connectionId=" + Connection + "&reason=access_denied")]
    [InlineData("?oauth=maybe&connectionId=" + Connection)]
    public void Connection_details_without_a_known_outcome_are_not_an_OAuth_return(string query) {
        Assert.Null(Codec.Read(UrlQuery.Parse(query)).OAuthReturn);
    }

    [Fact]
    public void States_round_trip_through_their_canonical_form() {
        var plugin = new PluginId("office365.mail");
        PluginsUrlState[] states = [
            new(),
            new(plugin),
            new(plugin, PluginSection.Executors),
            new(plugin, PluginSection.Logs, PluginLogScope.All),
            new(plugin, PluginSection.Grants, Dialog: PluginsDialog.Packages),
            new(Section: PluginSection.Connections)
        ];

        foreach (var state in states) {
            Assert.Equal(state, Codec.Read(UrlQuery.Parse(Codec.Format(state))));
        }
    }

    [Fact]
    public void The_OAuth_return_path_selects_the_connections_of_the_owning_plugin() {
        Assert.Equal("/plugins?pluginId=gmail.mail&section=connections", PluginsUrlCodec.ConnectionsHref(new("Gmail.Mail")));
    }

    [Fact]
    public void Composing_keeps_foreign_parameters_and_removes_the_one_shot_and_legacy_keys() {
        var uri = $"http://localhost/plugins?tab=connections&oauth=connected&connectionId={Connection}&utm=mail#top";
        var composed = UrlLocation.Compose(uri, Codec, Codec.Read(UrlQuery.FromUri(uri)));

        Assert.Equal("http://localhost/plugins?section=connections&utm=mail#top", composed);
    }

    [Fact]
    public void Every_owned_key_has_a_query_trigger_on_the_page() {
        Assert.Empty(UrlStateTriggers.Missing(typeof(PluginsPage), Codec.Keys));
    }
}
