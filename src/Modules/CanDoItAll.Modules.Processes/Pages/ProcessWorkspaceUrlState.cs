using CanDoItAll.AppComponents.Navigation;
using CanDoItAll.Processes.Projections;
using CanDoItAll.Processes.UI;

namespace CanDoItAll.Modules.Processes.Pages;

public enum ProcessWorkspaceRunView {
    Launch,
    Activity,
    Control,
    Execution,
    Graphs,
    Coordination,
    Evidence
}

public enum ProcessWorkspaceDialog {
    Run
}

public sealed record ProcessWorkspaceUrlState(
    ProcessWorkspaceDetailTabKey? Tab = null,
    string? DefinitionKey = null,
    Guid? ProcessId = null,
    Guid? LaunchPlanId = null,
    string? Search = null,
    ProcessDefinitionCatalogScopeKind Scope = ProcessDefinitionCatalogScopeKind.All,
    Guid? RunId = null,
    ProcessWorkspaceRunView RunView = ProcessWorkspaceRunView.Activity,
    ProcessRuntimeHistoryWindow Window = ProcessRuntimeHistoryWindow.LiveHour,
    ProcessRuntimeHistoryWindow ChatWindow = ProcessRuntimeHistoryWindow.OneDay,
    int Page = 1,
    bool RunDialog = false,
    Guid? EventId = null) {
    public ProcessWorkspaceDetailTabKey EffectiveTab => Tab ?? DefaultTab(RunId, LaunchPlanId);

    // Keeps the visible tab when an intent changes the selection that decides the default tab.
    public ProcessWorkspaceUrlState Pinned => this with { Tab = EffectiveTab };

    public static ProcessWorkspaceDetailTabKey DefaultTab(Guid? runId, Guid? launchPlanId)
        => runId.HasValue || launchPlanId.HasValue ? ProcessWorkspaceDetailTabKey.Runs : ProcessWorkspaceDetailTabKey.Definition;

    public static bool HasEventPages(ProcessWorkspaceDetailTabKey tab)
        => tab is ProcessWorkspaceDetailTabKey.Runs or ProcessWorkspaceDetailTabKey.Graphs;
}

public sealed class ProcessWorkspaceUrlCodec : IUrlStateCodec<ProcessWorkspaceUrlState> {
    public const string TabKey = "tab";
    public const string DefinitionKey = "definitionKey";
    public const string ProcessKey = "processId";
    public const string LaunchPlanKey = "launchPlanId";
    public const string SearchKey = "q";
    public const string ScopeKey = "scope";
    public const string RunKey = "runId";
    public const string RunViewKey = "runView";
    public const string WindowKey = "window";
    public const string ChatWindowKey = "chatWindow";
    public const string PageKey = "page";
    public const string DialogKey = "dialog";
    public const string EventKey = "eventId";
    public const int MaximumTextLength = 200;

    public static ProcessWorkspaceUrlCodec Instance { get; } = new();

    public static UrlTokenMap<ProcessWorkspaceDetailTabKey> Tabs { get; } = new(
        (ProcessWorkspaceDetailTabKey.Definition, "definition"),
        (ProcessWorkspaceDetailTabKey.Roles, "roles"),
        (ProcessWorkspaceDetailTabKey.Steps, "steps"),
        (ProcessWorkspaceDetailTabKey.Runs, "runs"),
        (ProcessWorkspaceDetailTabKey.Graphs, "graphs"),
        (ProcessWorkspaceDetailTabKey.Analytics, "analytics"),
        (ProcessWorkspaceDetailTabKey.Exchange, "exchange"),
        (ProcessWorkspaceDetailTabKey.ManagerChat, "manager-chat"));

    public static UrlTokenMap<ProcessWorkspaceRunView> RunViews { get; } = new(
        (ProcessWorkspaceRunView.Launch, "launch"),
        (ProcessWorkspaceRunView.Activity, "activity"),
        (ProcessWorkspaceRunView.Control, "control"),
        (ProcessWorkspaceRunView.Execution, "execution"),
        (ProcessWorkspaceRunView.Graphs, "graphs"),
        (ProcessWorkspaceRunView.Coordination, "coordination"),
        (ProcessWorkspaceRunView.Evidence, "evidence"));

    public static UrlTokenMap<ProcessDefinitionCatalogScopeKind> Scopes { get; } = new(
        (ProcessDefinitionCatalogScopeKind.All, "all"),
        (ProcessDefinitionCatalogScopeKind.Global, "global"),
        (ProcessDefinitionCatalogScopeKind.Project, "project"));

    public static UrlTokenMap<ProcessRuntimeHistoryWindow> Windows { get; } = new(
        (ProcessRuntimeHistoryWindow.LiveHour, "1h"),
        (ProcessRuntimeHistoryWindow.OneDay, "1d"),
        (ProcessRuntimeHistoryWindow.SevenDays, "7d"),
        (ProcessRuntimeHistoryWindow.ThirtyDays, "30d"));

    public static UrlTokenMap<ProcessWorkspaceDialog> Dialogs { get; } = new(
        (ProcessWorkspaceDialog.Run, "run"));

    public IReadOnlyList<string> Keys { get; } = [
        TabKey, DefinitionKey, ProcessKey, LaunchPlanKey, SearchKey, ScopeKey, RunKey, RunViewKey,
        WindowKey, ChatWindowKey, PageKey, DialogKey, EventKey
    ];

    public ProcessWorkspaceUrlState Read(UrlQuery query) {
        var runId = query.GetId(RunKey);
        var launchPlanId = query.GetId(LaunchPlanKey);
        var defaultTab = ProcessWorkspaceUrlState.DefaultTab(runId, launchPlanId);
        var tab = query.GetToken(TabKey, Tabs) ?? defaultTab;
        var onRuns = tab == ProcessWorkspaceDetailTabKey.Runs;
        var runDialog = onRuns && runId.HasValue && query.GetToken(DialogKey, Dialogs) == ProcessWorkspaceDialog.Run;
        return new(
            tab == defaultTab ? null : tab,
            query.GetText(DefinitionKey, MaximumTextLength),
            query.GetId(ProcessKey),
            launchPlanId,
            query.GetText(SearchKey, MaximumTextLength),
            query.GetToken(ScopeKey, Scopes, ProcessDefinitionCatalogScopeKind.All),
            runId,
            onRuns ? query.GetToken(RunViewKey, RunViews, ProcessWorkspaceRunView.Activity) : ProcessWorkspaceRunView.Activity,
            query.GetToken(WindowKey, Windows, ProcessRuntimeHistoryWindow.LiveHour),
            query.GetToken(ChatWindowKey, Windows, ProcessRuntimeHistoryWindow.OneDay),
            ProcessWorkspaceUrlState.HasEventPages(tab) ? query.GetPage(PageKey) : 1,
            runDialog,
            onRuns && !runDialog ? query.GetId(EventKey) : null);
    }

    public void Write(ProcessWorkspaceUrlState state, UrlQueryWriter query) {
        var tab = state.EffectiveTab;
        var onRuns = tab == ProcessWorkspaceDetailTabKey.Runs;
        var runDialog = onRuns && state.RunDialog && state.RunId.HasValue;
        query.Token(TabKey, tab, Tabs, ProcessWorkspaceUrlState.DefaultTab(state.RunId, state.LaunchPlanId));
        query.Text(DefinitionKey, state.DefinitionKey);
        query.Id(ProcessKey, state.ProcessId);
        query.Id(LaunchPlanKey, state.LaunchPlanId);
        query.Text(SearchKey, state.Search);
        query.Token(ScopeKey, state.Scope, Scopes, ProcessDefinitionCatalogScopeKind.All);
        query.Id(RunKey, state.RunId);
        if (onRuns) {
            query.Token(RunViewKey, state.RunView, RunViews, ProcessWorkspaceRunView.Activity);
        }
        query.Token(WindowKey, state.Window, Windows, ProcessRuntimeHistoryWindow.LiveHour);
        query.Token(ChatWindowKey, state.ChatWindow, Windows, ProcessRuntimeHistoryWindow.OneDay);
        if (ProcessWorkspaceUrlState.HasEventPages(tab)) {
            query.Page(PageKey, state.Page);
        }
        if (runDialog) {
            query.Token(DialogKey, ProcessWorkspaceDialog.Run, Dialogs);
        } else if (onRuns) {
            query.Id(EventKey, state.EventId);
        }
    }
}
