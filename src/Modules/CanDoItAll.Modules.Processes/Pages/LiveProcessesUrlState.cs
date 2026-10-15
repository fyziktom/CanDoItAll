using CanDoItAll.AppComponents.Navigation;
using CanDoItAll.Processes.Projections;

namespace CanDoItAll.Modules.Processes.Pages;

public enum LiveProcessesTab {
    Activity,
    Agents,
    Graphs,
    ToolHistory
}

public enum LiveProcessesDialog {
    Run,
    Files
}

public sealed record LiveProcessesUrlState(
    LiveProcessesTab Tab = LiveProcessesTab.Activity,
    Guid? RunId = null,
    ProcessProjectedRunStatus? Status = null,
    ProcessRuntimeHistoryWindow Window = ProcessRuntimeHistoryWindow.LiveHour,
    int Page = 1,
    LiveProcessesDialog? Dialog = null,
    Guid? StepId = null,
    Guid? ProcessId = null,
    Guid? LaunchPlanId = null) {
    public static bool HasEventPages(LiveProcessesTab tab) => tab != LiveProcessesTab.Agents;
}

public sealed class LiveProcessesUrlCodec : IUrlStateCodec<LiveProcessesUrlState> {
    public const string TabKey = "tab";
    public const string RunKey = "runId";
    public const string StatusKey = "status";
    public const string WindowKey = "window";
    public const string PageKey = "page";
    public const string DialogKey = "dialog";
    public const string StepKey = "stepId";
    public const string ProcessKey = "processId";
    public const string LaunchPlanKey = "launchPlanId";

    // A one-shot launch signal: owned so canonicalization removes it, never part of the state.
    public const string ProcessStartedKey = "processStarted";

    // A development scenario switch that the page reads but never owns, so every navigation preserves it.
    public const string MockScenarioKey = "mockScenario";

    public static LiveProcessesUrlCodec Instance { get; } = new();

    public static UrlTokenMap<LiveProcessesTab> Tabs { get; } = new(
        (LiveProcessesTab.Activity, "activity"),
        (LiveProcessesTab.Agents, "agents"),
        (LiveProcessesTab.Graphs, "graphs"),
        (LiveProcessesTab.ToolHistory, "tool-history"));

    public static UrlTokenMap<ProcessProjectedRunStatus> Statuses { get; } = new(
        (ProcessProjectedRunStatus.Active, "active"),
        (ProcessProjectedRunStatus.NeedsAttention, "needs-attention"),
        (ProcessProjectedRunStatus.Failed, "failed"),
        (ProcessProjectedRunStatus.Completed, "completed"),
        (ProcessProjectedRunStatus.Cancelled, "cancelled"));

    public static UrlTokenMap<LiveProcessesDialog> Dialogs { get; } = new(
        (LiveProcessesDialog.Run, "run"),
        (LiveProcessesDialog.Files, "files"));

    public IReadOnlyList<string> Keys { get; } = [
        TabKey, RunKey, StatusKey, WindowKey, PageKey, DialogKey, StepKey, ProcessKey, LaunchPlanKey, ProcessStartedKey
    ];

    public LiveProcessesUrlState Read(UrlQuery query) {
        var tab = query.GetToken(TabKey, Tabs, LiveProcessesTab.Activity);
        var runId = query.GetId(RunKey);
        var dialog = runId.HasValue ? query.GetToken(DialogKey, Dialogs) : null;
        return new(
            tab,
            runId,
            query.GetToken(StatusKey, Statuses),
            query.GetToken(WindowKey, ProcessWorkspaceUrlCodec.Windows, ProcessRuntimeHistoryWindow.LiveHour),
            LiveProcessesUrlState.HasEventPages(tab) ? query.GetPage(PageKey) : 1,
            dialog,
            dialog is null ? query.GetId(StepKey) : null,
            query.GetId(ProcessKey),
            query.GetId(LaunchPlanKey));
    }

    public void Write(LiveProcessesUrlState state, UrlQueryWriter query) {
        var dialog = state.RunId.HasValue ? state.Dialog : null;
        query.Token(TabKey, state.Tab, Tabs, LiveProcessesTab.Activity);
        query.Id(RunKey, state.RunId);
        query.Token(StatusKey, state.Status, Statuses);
        query.Token(WindowKey, state.Window, ProcessWorkspaceUrlCodec.Windows, ProcessRuntimeHistoryWindow.LiveHour);
        if (LiveProcessesUrlState.HasEventPages(state.Tab)) {
            query.Page(PageKey, state.Page);
        }
        query.Token(DialogKey, dialog, Dialogs);
        if (dialog is null) {
            query.Id(StepKey, state.StepId);
        }
        query.Id(ProcessKey, state.ProcessId);
        query.Id(LaunchPlanKey, state.LaunchPlanId);
    }
}
