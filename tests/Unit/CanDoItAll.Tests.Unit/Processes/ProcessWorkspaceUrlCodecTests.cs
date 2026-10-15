using CanDoItAll.AppComponents.Navigation;
using CanDoItAll.Modules.Processes;
using CanDoItAll.Modules.Processes.Pages;
using CanDoItAll.Processes.Projections;
using CanDoItAll.Processes.UI;

namespace CanDoItAll.Tests.Unit.Processes;

public sealed class ProcessWorkspaceUrlCodecTests {
    private const string Run = "6f1f0a51-6a3d-4a8f-9f0e-0c2d6f7a4b11";
    private const string Event = "7a2e1b62-7b4e-4b90-8f1f-1d3e7f8b5c22";
    private const string Process = "8b3f2c73-8c5f-4ca1-9a2a-2e4f8a9c6d33";
    private const string LaunchPlan = "9c4a3d84-9d6a-4db2-8b3b-3f5a9b0d7e44";
    private static readonly ProcessWorkspaceUrlCodec Codec = ProcessWorkspaceUrlCodec.Instance;

    [Theory]
    [InlineData("", "")]
    [InlineData("?tab=definition", "")]
    [InlineData("?TAB=RUNS", "?tab=runs")]
    [InlineData("?tab=manager-chat", "?tab=manager-chat")]
    [InlineData("?tab=unknown", "")]
    [InlineData("?runId=6F1F0A51-6A3D-4A8F-9F0E-0C2D6F7A4B11", "?runId=" + Run)]
    [InlineData("?runId=" + Run + "&tab=runs", "?runId=" + Run)]
    [InlineData("?runId=" + Run + "&tab=definition", "?tab=definition&runId=" + Run)]
    [InlineData("?runId=not-a-guid&tab=runs", "?tab=runs")]
    [InlineData("?runId=00000000-0000-0000-0000-000000000000", "")]
    [InlineData("?tab=runs&runView=control", "?tab=runs&runView=control")]
    [InlineData("?runId=" + Run + "&runView=activity", "?runId=" + Run)]
    [InlineData("?tab=definition&runView=control", "")]
    [InlineData("?q=%20%20architecture%20%20", "?q=architecture")]
    [InlineData("?scope=all", "")]
    [InlineData("?scope=PROJECT", "?scope=project")]
    [InlineData("?scope=Project", "?scope=project")]
    [InlineData("?window=7d&chatWindow=1d", "?window=7d")]
    [InlineData("?window=LiveHour&chatWindow=30d", "?chatWindow=30d")]
    [InlineData("?tab=runs&page=1", "?tab=runs")]
    [InlineData("?tab=runs&page=3", "?tab=runs&page=3")]
    [InlineData("?tab=graphs&page=2", "?tab=graphs&page=2")]
    [InlineData("?tab=definition&page=3", "")]
    [InlineData("?tab=runs&page=0", "?tab=runs")]
    [InlineData("?runId=" + Run + "&dialog=run", "?runId=" + Run + "&dialog=run")]
    [InlineData("?dialog=run", "")]
    [InlineData("?runId=" + Run + "&tab=graphs&dialog=run", "?tab=graphs&runId=" + Run)]
    [InlineData("?runId=" + Run + "&dialog=agent", "?runId=" + Run)]
    [InlineData("?runId=" + Run + "&dialog=RUN", "?runId=" + Run + "&dialog=run")]
    [InlineData("?tab=runs&eventId=" + Event, "?tab=runs&eventId=" + Event)]
    [InlineData("?tab=analytics&eventId=" + Event, "?tab=analytics")]
    [InlineData("?runId=" + Run + "&dialog=run&eventId=" + Event, "?runId=" + Run + "&dialog=run")]
    [InlineData("?definitionKey=blazor-app-delivery", "?definitionKey=blazor-app-delivery")]
    [InlineData("?processId=" + Process + "&launchPlanId=" + LaunchPlan, "?processId=" + Process + "&launchPlanId=" + LaunchPlan)]
    [InlineData("?launchPlanId=" + LaunchPlan + "&tab=definition", "?tab=definition&launchPlanId=" + LaunchPlan)]
    public void Locations_have_one_stable_canonical_form(string query, string canonical) {
        var state = Codec.Read(UrlQuery.Parse(query));

        Assert.Equal(canonical, Codec.Format(state));
        Assert.Equal(state, Codec.Normalize(state));
        Assert.Equal(canonical, Codec.Format(Codec.Read(UrlQuery.Parse(canonical))));
    }

    [Fact]
    public void Every_state_value_round_trips_in_a_fixed_key_order() {
        var state = new ProcessWorkspaceUrlState(
            ProcessWorkspaceDetailTabKey.Runs,
            "blazor-app-delivery",
            Guid.Parse(Process),
            Guid.Parse(LaunchPlan),
            "architecture",
            ProcessDefinitionCatalogScopeKind.Global,
            Guid.Parse(Run),
            ProcessWorkspaceRunView.Evidence,
            ProcessRuntimeHistoryWindow.ThirtyDays,
            ProcessRuntimeHistoryWindow.SevenDays,
            4,
            false,
            Guid.Parse(Event));

        var query = Codec.Format(state);

        Assert.Equal(
            $"?definitionKey=blazor-app-delivery&processId={Process}&launchPlanId={LaunchPlan}&q=architecture&scope=global" +
            $"&runId={Run}&runView=evidence&window=30d&chatWindow=7d&page=4&eventId={Event}",
            query);
        Assert.True(Codec.SameState(state, Codec.Read(UrlQuery.Parse(query))));
    }

    [Fact]
    public void The_default_tab_follows_the_run_selection_and_pinning_keeps_the_visible_tab() {
        var selected = new ProcessWorkspaceUrlState(RunId: Guid.Parse(Run));

        Assert.Equal(ProcessWorkspaceDetailTabKey.Runs, selected.EffectiveTab);
        Assert.Equal("?runId=" + Run, Codec.Format(selected));
        Assert.Equal(ProcessWorkspaceDetailTabKey.Definition, (selected with { RunId = null }).EffectiveTab);
        Assert.Equal("?tab=runs", Codec.Format(selected.Pinned with { RunId = null }));
        Assert.Equal(ProcessWorkspaceDetailTabKey.Runs, new ProcessWorkspaceUrlState(LaunchPlanId: Guid.Parse(LaunchPlan)).EffectiveTab);
    }

    [Fact]
    public void Overlays_depend_on_their_tab_and_record() {
        var dialog = new ProcessWorkspaceUrlState(RunId: Guid.Parse(Run), RunDialog: true);

        Assert.Equal($"?runId={Run}&dialog=run", Codec.Format(dialog));
        Assert.Equal($"?tab=steps&runId={Run}", Codec.Format(dialog with { Tab = ProcessWorkspaceDetailTabKey.Steps }));
        Assert.Equal(string.Empty, Codec.Format(dialog with { RunId = null }));
        Assert.Equal($"?runId={Run}&dialog=run", Codec.Format(dialog with { EventId = Guid.Parse(Event) }));
    }

    [Fact]
    public void Composing_keeps_foreign_parameters_and_the_fragment() {
        var composed = UrlLocation.Compose(
            $"http://localhost/processes?utm=mail&RUNID={Run}&q=old#detail",
            Codec,
            new ProcessWorkspaceUrlState(Tab: ProcessWorkspaceDetailTabKey.Steps, DefinitionKey: "blazor-app-delivery"));

        Assert.Equal("http://localhost/processes?tab=steps&definitionKey=blazor-app-delivery&utm=mail#detail", composed);
    }

    [Fact]
    public void Cross_page_links_build_the_canonical_target() {
        Assert.Equal(
            $"/processes?runId={Run}&runView=control",
            Codec.Href("/processes", new(RunId: Guid.Parse(Run), RunView: ProcessWorkspaceRunView.Control)));
        Assert.Equal(
            $"/processes/live?runId={Run}",
            LiveProcessesUrlCodec.Instance.Href("/processes/live", new(RunId: Guid.Parse(Run))));
    }

    [Fact]
    public void Token_maps_cover_every_typed_value() {
        Assert.All(Enum.GetValues<ProcessWorkspaceDetailTabKey>(), tab => Assert.True(ProcessWorkspaceUrlCodec.Tabs.Contains(tab)));
        Assert.All(Enum.GetValues<ProcessWorkspaceRunView>(), view => Assert.True(ProcessWorkspaceUrlCodec.RunViews.Contains(view)));
        Assert.All(Enum.GetValues<ProcessDefinitionCatalogScopeKind>(), scope => Assert.True(ProcessWorkspaceUrlCodec.Scopes.Contains(scope)));
        Assert.All(Enum.GetValues<ProcessRuntimeHistoryWindow>(), window => Assert.True(ProcessWorkspaceUrlCodec.Windows.Contains(window)));
        Assert.All(Enum.GetValues<ProcessWorkspaceDialog>(), dialog => Assert.True(ProcessWorkspaceUrlCodec.Dialogs.Contains(dialog)));
    }

    [Fact]
    public void Every_owned_key_has_a_query_trigger_on_the_routed_host() {
        Assert.Empty(UrlStateTriggers.Missing(typeof(ProcessWorkspaceShell), Codec.Keys));
    }
}
