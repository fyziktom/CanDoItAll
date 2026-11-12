using CanDoItAll.AgentFramework.Workflows.UI;
using CanDoItAll.AppComponents.Navigation;

namespace CanDoItAll.Modules.AgentFramework.Pages.Navigation;

public enum WorkflowsPageDialog {
    RunDetails
}

public sealed record WorkflowsUrlState {
    public WorkflowTab Tab { get; init; } = WorkflowTab.Dashboard;
    public Guid? ProjectId { get; init; }
    public Guid? WorkflowId { get; init; }
    public Guid? RunId { get; init; }
    public WorkflowsPageDialog? Dialog { get; init; }

    public bool SameSelection(WorkflowsUrlState other)
        => ProjectId == other.ProjectId && WorkflowId == other.WorkflowId && RunId == other.RunId;
}

public sealed class WorkflowsUrlCodec : IUrlStateCodec<WorkflowsUrlState> {
    public const string Path = "/agents/workflows";
    public const string TabKey = "tab";
    public const string ProjectKey = "projectId";
    public const string WorkflowKey = "workflowId";
    public const string RunKey = "runId";
    public const string DialogKey = "dialog";

    public static WorkflowsUrlCodec Instance { get; } = new();

    public static UrlTokenMap<WorkflowTab> Tabs { get; } = new UrlTokenMap<WorkflowTab>(
        (WorkflowTab.Dashboard, "dashboard"),
        (WorkflowTab.Catalog, "workflows"),
        (WorkflowTab.Editor, "editor"),
        (WorkflowTab.History, "history"),
        (WorkflowTab.Analytics, "analytics")).WithAliases(("catalog", WorkflowTab.Catalog));

    public static UrlTokenMap<WorkflowsPageDialog> Dialogs { get; } = new((WorkflowsPageDialog.RunDetails, "run-details"));

    public IReadOnlyList<string> Keys { get; } = [TabKey, ProjectKey, WorkflowKey, RunKey, DialogKey];

    public static WorkflowTab DefaultTab(Guid? runId) => runId is null ? WorkflowTab.Dashboard : WorkflowTab.History;

    public WorkflowsUrlState Read(UrlQuery query) {
        ArgumentNullException.ThrowIfNull(query);
        var runId = query.GetId(RunKey);
        return new() {
            Tab = query.GetToken(TabKey, Tabs, DefaultTab(runId)),
            ProjectId = query.GetId(ProjectKey),
            WorkflowId = query.GetId(WorkflowKey),
            RunId = runId,
            Dialog = runId is null ? null : query.GetToken(DialogKey, Dialogs)
        };
    }

    public void Write(WorkflowsUrlState state, UrlQueryWriter query) {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(query);
        query.Token(TabKey, state.Tab, Tabs, DefaultTab(state.RunId));
        query.Id(ProjectKey, state.ProjectId);
        query.Id(WorkflowKey, state.WorkflowId);
        query.Id(RunKey, state.RunId);
        if (state.RunId is not null) {
            query.Token(DialogKey, state.Dialog, Dialogs);
        }
    }
}
