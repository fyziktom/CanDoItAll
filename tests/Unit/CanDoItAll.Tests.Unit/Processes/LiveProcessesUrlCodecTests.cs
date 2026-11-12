using CanDoItAll.AppComponents.Navigation;
using CanDoItAll.Modules.Processes;
using CanDoItAll.Modules.Processes.Pages;
using CanDoItAll.Processes.Projections;

namespace CanDoItAll.Tests.Unit.Processes;

public sealed class LiveProcessesUrlCodecTests {
    private const string Run = "6f1f0a51-6a3d-4a8f-9f0e-0c2d6f7a4b11";
    private const string Step = "7a2e1b62-7b4e-4b90-8f1f-1d3e7f8b5c22";
    private const string Process = "8b3f2c73-8c5f-4ca1-9a2a-2e4f8a9c6d33";
    private static readonly LiveProcessesUrlCodec Codec = LiveProcessesUrlCodec.Instance;

    [Theory]
    [InlineData("", "")]
    [InlineData("?tab=activity", "")]
    [InlineData("?tab=TOOL-HISTORY", "?tab=tool-history")]
    [InlineData("?tab=agents&page=2", "?tab=agents")]
    [InlineData("?tab=graphs&page=2", "?tab=graphs&page=2")]
    [InlineData("?page=2", "?page=2")]
    [InlineData("?page=-1", "")]
    [InlineData("?status=needs-attention", "?status=needs-attention")]
    [InlineData("?status=unknown", "")]
    [InlineData("?status=NeedsAttention", "")]
    [InlineData("?window=30d", "?window=30d")]
    [InlineData("?window=1h", "")]
    [InlineData("?runId=6F1F0A51-6A3D-4A8F-9F0E-0C2D6F7A4B11&processStarted=1", "?runId=" + Run)]
    [InlineData("?processStarted=true", "")]
    [InlineData("?runId=" + Run + "&dialog=run", "?runId=" + Run + "&dialog=run")]
    [InlineData("?runId=" + Run + "&dialog=FILES", "?runId=" + Run + "&dialog=files")]
    [InlineData("?dialog=run", "")]
    [InlineData("?runId=" + Run + "&dialog=run&stepId=" + Step, "?runId=" + Run + "&dialog=run")]
    [InlineData("?stepId=" + Step, "?stepId=" + Step)]
    [InlineData("?processId=" + Process + "&launchPlanId=" + Process, "?processId=" + Process + "&launchPlanId=" + Process)]
    public void Locations_have_one_stable_canonical_form(string query, string canonical) {
        var state = Codec.Read(UrlQuery.Parse(query));

        Assert.Equal(canonical, Codec.Format(state));
        Assert.Equal(state, Codec.Normalize(state));
        Assert.Equal(canonical, Codec.Format(Codec.Read(UrlQuery.Parse(canonical))));
    }

    [Fact]
    public void Every_state_value_round_trips_in_a_fixed_key_order() {
        var state = new LiveProcessesUrlState(
            LiveProcessesTab.ToolHistory,
            Guid.Parse(Run),
            ProcessProjectedRunStatus.Failed,
            ProcessRuntimeHistoryWindow.SevenDays,
            3,
            LiveProcessesDialog.Files,
            null,
            Guid.Parse(Process),
            null);

        var query = Codec.Format(state);

        Assert.Equal($"?tab=tool-history&runId={Run}&status=failed&window=7d&page=3&dialog=files&processId={Process}", query);
        Assert.Equal(state, Codec.Read(UrlQuery.Parse(query)));
    }

    [Fact]
    public void The_launch_signal_is_owned_but_never_part_of_the_state() {
        Assert.Contains(LiveProcessesUrlCodec.ProcessStartedKey, Codec.Keys);
        Assert.DoesNotContain(LiveProcessesUrlCodec.MockScenarioKey, Codec.Keys);
        Assert.Equal(
            Codec.Read(UrlQuery.Parse($"?runId={Run}")),
            Codec.Read(UrlQuery.Parse($"?runId={Run}&processStarted=1")));
    }

    [Fact]
    public void Composing_removes_the_launch_signal_and_keeps_the_scenario_switch() {
        var composed = UrlLocation.Compose(
            $"http://localhost/processes/live?mockScenario=operations&runId={Run}&processStarted=1",
            Codec,
            new LiveProcessesUrlState(LiveProcessesTab.Agents, Guid.Parse(Run)));

        Assert.Equal($"http://localhost/processes/live?tab=agents&runId={Run}&mockScenario=operations", composed);
    }

    [Fact]
    public void Token_maps_cover_every_tab_dialog_and_filterable_status() {
        Assert.All(Enum.GetValues<LiveProcessesTab>(), tab => Assert.True(LiveProcessesUrlCodec.Tabs.Contains(tab)));
        Assert.All(Enum.GetValues<LiveProcessesDialog>(), dialog => Assert.True(LiveProcessesUrlCodec.Dialogs.Contains(dialog)));
        Assert.All(Enum.GetValues<ProcessProjectedRunStatus>().Where(status => status != ProcessProjectedRunStatus.Unknown),
            status => Assert.True(LiveProcessesUrlCodec.Statuses.Contains(status)));
    }

    [Fact]
    public void Every_owned_key_has_a_query_trigger_on_the_routed_host() {
        Assert.Empty(UrlStateTriggers.Missing(typeof(LiveProcessesDashboard), Codec.Keys));
    }
}
