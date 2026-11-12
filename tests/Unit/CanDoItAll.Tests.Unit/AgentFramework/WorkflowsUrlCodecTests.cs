using CanDoItAll.AgentFramework.Workflows.UI;
using CanDoItAll.AppComponents.Navigation;
using CanDoItAll.Modules.AgentFramework.Pages;
using CanDoItAll.Modules.AgentFramework.Pages.Navigation;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class WorkflowsUrlCodecTests {
    private static readonly WorkflowsUrlCodec Codec = WorkflowsUrlCodec.Instance;
    private static readonly Guid Project = Guid.Parse("0f8fad5b-d9cb-469f-a165-70867728950e");
    private static readonly Guid Workflow = Guid.Parse("7c9e6679-7425-40de-944b-e07fc1f90ae7");
    private static readonly Guid Run = Guid.Parse("4a0a9a2e-7c39-4d3e-9a7f-1d2c3b4a5e6f");

    [Theory]
    [InlineData(WorkflowTab.Dashboard, "")]
    [InlineData(WorkflowTab.Catalog, "?tab=workflows")]
    [InlineData(WorkflowTab.Editor, "?tab=editor")]
    [InlineData(WorkflowTab.History, "?tab=history")]
    [InlineData(WorkflowTab.Analytics, "?tab=analytics")]
    public void Every_tab_has_a_stable_token_and_the_dashboard_is_the_default(WorkflowTab tab, string canonical) {
        var state = new WorkflowsUrlState { Tab = tab };

        Assert.Equal(canonical, Codec.Format(state));
        Assert.Equal(state, Read(canonical));
    }

    [Fact]
    public void Legacy_and_noncanonical_tab_values_normalize_to_the_canonical_token() {
        Assert.Equal("?tab=workflows", Codec.Format(Read("?tab=catalog")));
        Assert.Equal("?tab=history", Codec.Format(Read("?tab=HISTORY")));
        Assert.Equal("", Codec.Format(Read("?tab=unknown")));
    }

    [Fact]
    public void Existing_selection_links_keep_their_shape_and_a_run_link_opens_history() {
        var link = $"?projectId={Project}&workflowId={Workflow}&runId={Run}";

        var state = Read(link);

        Assert.Equal(new WorkflowsUrlState { Tab = WorkflowTab.History, ProjectId = Project, WorkflowId = Workflow, RunId = Run }, state);
        Assert.Equal(link, Codec.Format(state));
        Assert.Equal($"?tab=dashboard&workflowId={Workflow}&runId={Run}",
            Codec.Format(new WorkflowsUrlState { WorkflowId = Workflow, RunId = Run }));
        Assert.Equal(WorkflowTab.Dashboard, Read($"?tab=dashboard&workflowId={Workflow}&runId={Run}").Tab);
    }

    [Fact]
    public void Run_details_dialog_requires_a_run_and_malformed_identities_are_dropped() {
        var dialog = Read($"?workflowId={Workflow}&runId={Run}&dialog=run-details");

        Assert.Equal(WorkflowsPageDialog.RunDetails, dialog.Dialog);
        Assert.Equal($"?workflowId={Workflow}&runId={Run}&dialog=run-details", Codec.Format(dialog));
        Assert.Null(Read($"?workflowId={Workflow}&dialog=run-details").Dialog);
        Assert.Equal($"?workflowId={Workflow}", Codec.Format(Read($"?workflowId={Workflow}&dialog=run-details")));
        Assert.Equal($"?workflowId={Workflow}", Codec.Format(Read($"?workflowId={Workflow}&runId=not-a-guid&projectId=")));
        Assert.Equal($"?workflowId={Workflow}&runId={Run}", Codec.Format(Read($"?workflowId={Workflow}&runId={Run}&dialog=unknown")));
    }

    [Fact]
    public void Incomplete_selection_routes_are_preserved_for_the_page_to_report() {
        var orphanRun = Read($"?runId={Run}");
        var orphanProject = Read($"?projectId={Project}");

        Assert.Equal($"?runId={Run}", Codec.Format(orphanRun));
        Assert.Equal($"?projectId={Project}", Codec.Format(orphanProject));
    }

    [Fact]
    public void Selection_comparison_ignores_presentation_only_state() {
        var history = new WorkflowsUrlState { Tab = WorkflowTab.History, WorkflowId = Workflow, RunId = Run };

        Assert.True(history.SameSelection(history with { Tab = WorkflowTab.Editor, Dialog = WorkflowsPageDialog.RunDetails }));
        Assert.False(history.SameSelection(history with { RunId = null }));
        Assert.False(history.SameSelection(history with { ProjectId = Project }));
    }

    [Fact]
    public void Every_codec_key_has_a_query_trigger_on_the_page() {
        Assert.Empty(UrlStateTriggers.Missing(typeof(WorkflowsPage), Codec.Keys));
    }

    private static WorkflowsUrlState Read(string query) => Codec.Read(UrlQuery.Parse(query));
}
