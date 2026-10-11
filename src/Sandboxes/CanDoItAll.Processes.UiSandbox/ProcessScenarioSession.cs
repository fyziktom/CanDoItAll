using System.Globalization;
using CanDoItAll.Components.Charts;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Projections;
using CanDoItAll.Processes.UI;
using Microsoft.AspNetCore.Components;

namespace CanDoItAll.Processes.UiSandbox;

public sealed class ProcessScenarioSession : IProcessWorkspaceSession, ILiveProcessesSession {
    private readonly Dictionary<ProcessDefinitionCatalogItemKey, ProcessDefinitionEditorProjection> editors = [];
    private readonly Dictionary<Guid, string> notes = [];
    private readonly HashSet<ProcessRunId> hidden = [];
    private ProcessTemplateCatalogQueryProjection templateQuery = new(null, ProcessTemplateCatalogCategoryKind.All, null, ProcessTemplateCatalogPreviewTabKind.Overview, 50);
    private ProcessProjectedRunStatus? statusFilter;
    private int revision;
    public event Action? Changed;
    public ProcessWorkspaceShellScope Scope { get; private set; } = ProcessWorkspaceShellScope.Global;
    public ProcessWorkspaceShellProjection CurrentShell { get; private set; } = default!;
    public ProcessWorkspaceShellProjection? Shell => CurrentShell;
    public ProcessDefinitionCatalogProjection DefinitionCatalog => CurrentShell.DefinitionCatalog;
    public ProcessDefinitionDraft DefinitionDraft { get; private set; } = new();
    public ProcessRoleEditorState RoleEditorState { get; private set; } = new();
    public ProcessStepEditorState StepEditorState { get; private set; } = new();
    public ProcessTemplateBrowserState TemplateBrowserState { get; private set; } = new();
    public string? SelectedDefinitionKey { get; private set; }
    public ProcessDefinitionCatalogScopeKind SelectedDefinitionScope { get; private set; } = ProcessDefinitionCatalogScopeKind.All;
    public string PendingSearchText { get; private set; } = string.Empty;
    public int ActiveDetailTabIndex { get; private set; }
    public int SelectedRunViewIndex { get; private set; } = 1;
    public int SelectedTabIndex { get; private set; }
    public Guid? SelectedRuntimeRunId { get; private set; }
    public int RuntimeEventPage { get; private set; }
    public ProcessRuntimeHistoryWindow RuntimeHistoryWindow { get; private set; } = ProcessRuntimeHistoryWindow.LiveHour;
    public ProcessRuntimeHistoryWindow ManagerChatHistoryWindow => RuntimeHistoryWindow;
    public bool ProcessGraphsHistoryLoaded { get; private set; }
    public ProcessTimelineEventProjection? EventDetail { get; private set; }
    public bool EventDetailDialogOpen => EventDetail is not null;
    public ProcessLiveProcessSnapshot? RunDetailSnapshot { get; private set; }
    public bool RunDetailDialogOpen => RunDetailSnapshot is not null;
    public ProcessRuntimeActiveAgentProjection? AgentDetailAgent { get; private set; }
    public bool AgentDetailDialogOpen => AgentDetailAgent is not null;
    public Guid? FilesRunId { get; private set; }
    public string? ErrorMessage { get; private set; }
    public string? AgentContextNotice { get; private set; }
    public string? CatalogCommandNotice { get; private set; }
    public string? EditorCommandNotice { get; private set; }
    public string? OperatorActionMessage { get; private set; }
    public string? OperatorActionError => ErrorMessage;
    public bool IsBusy { get; private set; }
    public bool IsLoading => IsBusy;
    public bool CanStartAnotherLaunch { get; private set; }
    public string? PendingLaunchStorageKey => CanStartAnotherLaunch ? "scenario-accepted-launch" : null;
    public bool LaunchStarted => CanStartAnotherLaunch;
    public bool IsMockProjection => false;
    public Guid? RunIdQuery => SelectedRuntimeRunId;
    public string ScopeEyebrow => Scope.Kind == ProcessWorkspaceScopeKind.Project ? "Project processes" : "Processes";
    public string HeaderTitle => ScopeEyebrow;
    public string PageTitleText => "Live processes - independent scenarios";
    public string DefinitionTotalText => DefinitionCatalog.Items.Count.ToString(CultureInfo.InvariantCulture);
    public string DefinitionCountText => DefinitionTotalText;
    public string DefinitionTooltip => DefinitionCatalog.Summary;
    public string LiveRunActiveText => VisibleRuns.Count.ToString(CultureInfo.InvariantCulture);
    public string LiveRunAttentionText => AttentionRuns.Count.ToString(CultureInfo.InvariantCulture);
    public string LiveRunTooltip => "Deterministic in-memory process runs.";
    public string ProjectionStatusText => ErrorMessage is null ? "Scenario ready" : "Refresh failed";
    public string ProjectionTone => ErrorMessage is null ? "success" : "warning";
    public string ProjectionTooltip => "Synthetic projections; no database, provider, or production commands.";
    public bool AgentContextDisabled => IsBusy;
    public string AgentContextTooltip => "Show the selected scenario context.";
    public IReadOnlyList<ProcessWorkspaceDetailTabDescriptor> DetailTabs { get; } = [
        new(ProcessWorkspaceDetailTabKey.Definition, "Definition", "edit_note", "Definition identity and governance", null),
        new(ProcessWorkspaceDetailTabKey.Roles, "Roles", "groups", "Role contracts", null),
        new(ProcessWorkspaceDetailTabKey.Steps, "Steps", "account_tree", "Canvas and step contracts", null),
        new(ProcessWorkspaceDetailTabKey.Runs, "Runs", "rocket_launch", "Runtime projections", null),
        new(ProcessWorkspaceDetailTabKey.Graphs, "Graphs", "bar_chart", "Usage history", null),
        new(ProcessWorkspaceDetailTabKey.Analytics, "Analytics", "analytics", "Readiness", null),
        new(ProcessWorkspaceDetailTabKey.Exchange, "Exchange", "sync_alt", "Template catalogue", null),
        new(ProcessWorkspaceDetailTabKey.ManagerChat, "Manager chat", "forum", "Run-scoped conversation", null)
    ];
    public IReadOnlyList<ProcessManagerAgentOption> ManagerChatAgents { get; } = [new(Guid.Parse("51000000-0000-0000-0000-000000000001"), "Scenario process manager")];
    public string ManagerChatAgentSelectValue => ManagerChatAgents[0].Id.ToString("D");
    public string ManagerChatManagerLabel => ManagerChatAgents[0].Name;
    public bool HasManagerChatAgent { get; private set; } = true;
    public bool ManagerChatReloadDisabled => IsBusy;
    public bool ManagerChatIsLoading { get; private set; }
    public string? ManagerChatErrorMessage { get; private set; }
    public IReadOnlyList<ProcessLiveProcessSnapshot> SelectableRuns => CurrentShell.Runtime.Runs;
    public IReadOnlyList<ProcessLiveProcessSnapshot> VisibleRuns => SelectableRuns.Where(run => !hidden.Contains(run.RootRunId) && (statusFilter is null || run.Status == statusFilter)).ToArray();
    public IReadOnlyList<ProcessLiveProcessSnapshot> ActivityCardRuns => VisibleRuns;
    public IReadOnlyList<ProcessLiveProcessSnapshot> AttentionRuns => VisibleRuns.Where(run => run.Status is ProcessProjectedRunStatus.NeedsAttention or ProcessProjectedRunStatus.Failed).ToArray();
    public IReadOnlyList<ProcessRuntimeActiveAgentProjection> VisibleActiveAgents => CurrentShell.Runtime.ActiveAgents;
    public int HiddenRelatedRunCount => SelectableRuns.Count - VisibleRuns.Count;
    public string LastEventText => CurrentShell.Runtime.Events.LastOrDefault()?.EventType ?? "No events";
    public string StatusFilterValue => statusFilter?.ToString() ?? string.Empty;
    public string RuntimeRunSelectValue => SelectedRuntimeRunId?.ToString("D") ?? string.Empty;
    public IReadOnlyList<ToolFamilySummary> ToolFamilySummaries => ProcessRuntimeDisplay.BuildToolFamilySummaries(CurrentShell.Runtime.ToolUsage);
    public CdaChartOptions RuntimeCostChartOptions { get; } = new() { Type = CdaChartType.Area, XAxisType = CdaChartAxisType.DateTime, YAxisTitle = "USD" };

    public ProcessScenarioSession() => Reload();
    public ProcessWorkspaceShellScope ResolveScope() => Scope;
    public string FormatCurrency(decimal value) => $"USD {value.ToString("F2", CultureInfo.InvariantCulture)}";
    private Task Change(Action action) {
        action();
        Changed?.Invoke();
        return Task.CompletedTask;
    }
    private string NextVersion() => $"scenario-{++revision}";
    private void Reload() {
        var request = new ProcessWorkspaceShellRequest(Scope, new(null, SelectedRuntimeRunId, null),
            new(PendingSearchText, SelectedDefinitionKey is { } key ? new(key) : null, SelectedDefinitionScope, 50), templateQuery, false,
            new(RuntimeHistoryWindow, RuntimeEventPage, 25, SelectedRuntimeRunId));
        CurrentShell = ProcessScenarioData.CreateShell(request, null);
        SelectedDefinitionKey = DefinitionCatalog.SelectedDefinitionKey?.Value;
        if (DefinitionCatalog.SelectedEditor is { } editor) {
            if (editors.TryGetValue(editor.DefinitionKey, out var saved)) {
                editor = saved;
            }
            SetEditor(editor);
            DefinitionDraft.Observe(editor);
        }
        SelectedRuntimeRunId = CurrentShell.Runtime.SelectedRunId;
    }
    private void SetEditor(ProcessDefinitionEditorProjection editor) {
        editors[editor.DefinitionKey] = editor;
        CurrentShell = CurrentShell with { DefinitionCatalog = DefinitionCatalog with { SelectedEditor = editor } };
    }
    public Task RefreshAsync() => Change(() => {
        ErrorMessage = null;
        Reload();
    });
    public Task ForceRefreshAsync() => RefreshAsync();
    public Task ApplyDefinitionSearchAsync() => RefreshAsync();
    public void UpdateDefinitionSearchText(ChangeEventArgs args) => PendingSearchText = args.Value?.ToString() ?? string.Empty;
    public Task ClearDefinitionSearchAsync() => Change(() => {
        PendingSearchText = string.Empty;
        Reload();
    });
    public Task SelectDefinitionAsync(ProcessDefinitionCatalogItemKey key) => Change(() => {
        SelectedDefinitionKey = key.Value;
        Reload();
    });
    public Task SelectDefinitionScopeAsync(ProcessDefinitionCatalogScopeKind scopeKind) => Change(() => {
        SelectedDefinitionScope = scopeKind;
        Reload();
    });
    public Task HandleDetailTabChangedAsync(int index) => Change(() => ActiveDetailTabIndex = index);
    public Task HandleSelectedRunViewChangedAsync(int index) => Change(() => SelectedRunViewIndex = index);
    public Task HandleTabChangedAsync(int index) => Change(() => SelectedTabIndex = index);
    public Task LoadProcessGraphsHistoryAsync() => Change(() => ProcessGraphsHistoryLoaded = true);
    public Task OpenManagerChatTabAsync() => HandleDetailTabChangedAsync(7);
    public Task ChangeRuntimeHistoryWindowAsync(ChangeEventArgs args) => Change(() => {
        RuntimeHistoryWindow = Enum.Parse<ProcessRuntimeHistoryWindow>(args.Value?.ToString() ?? nameof(ProcessRuntimeHistoryWindow.LiveHour));
        Reload();
    });
    public Task ChangeManagerChatHistoryWindowAsync(ChangeEventArgs args) => ChangeRuntimeHistoryWindowAsync(args);
    public Task SelectRuntimeRunAsync(ChangeEventArgs args) => SelectRuntimeRunAsync(Guid.Parse(args.Value?.ToString() ?? string.Empty));
    public Task SelectRuntimeRunAsync(Guid runId) => Change(() => {
        SelectedRuntimeRunId = runId;
        Reload();
    });
    public Task OpenChildRunAsync(Guid runId) => SelectRuntimeRunAsync(runId);
    public Task NextRuntimeEventPageAsync() => Change(() => {
        RuntimeEventPage++;
        Reload();
    });
    public Task PreviousRuntimeEventPageAsync() => Change(() => {
        RuntimeEventPage = Math.Max(0, RuntimeEventPage - 1);
        Reload();
    });
    public Task OpenRunDetailDialogAsync(ProcessLiveProcessSnapshot run) => Change(() => RunDetailSnapshot = run);
    public Task CloseRunDetailDialogAsync() => Change(() => RunDetailSnapshot = null);
    public Task OpenEventDetailDialogAsync(ProcessTimelineEventProjection runtimeEvent) => Change(() => EventDetail = runtimeEvent);
    public Task CloseEventDetailDialogAsync() => Change(() => EventDetail = null);
    public Task OpenAgentDetailDialogAsync(ProcessRuntimeActiveAgentProjection agent) => Change(() => AgentDetailAgent = agent);
    public Task CloseAgentDetailDialogAsync() => Change(() => AgentDetailAgent = null);
    public Task OpenRunFilesAsync(Guid runId) => Change(() => FilesRunId = runId);
    public Task CloseFilesAsync() => Change(() => FilesRunId = null);
    public void OpenAgentContext() {
        AgentContextNotice = $"Scenario context: {ScopeEyebrow}, definition {SelectedDefinitionKey}, run {SelectedRuntimeRunId:D}.";
        Changed?.Invoke();
    }
    public Task SelectManagerChatAgentAsync(ChangeEventArgs args) => Change(OpenAgentContext);
    public Task ReloadManagerChatAsync() => Change(() => AgentContextNotice = "Scenario conversation reloaded.");
    public void SetChatScenario(ProcessChatScenario scenario) {
        HasManagerChatAgent = scenario != ProcessChatScenario.NoAgents;
        ManagerChatIsLoading = scenario == ProcessChatScenario.Loading;
        ManagerChatErrorMessage = scenario == ProcessChatScenario.MissingAgent ? "The requested manager agent is not available." : null;
    }
    public event Action<string>? Navigate;
    public Task ExecuteCommandAsync(ProcessWorkspaceCommandProjection command) => command.Kind switch {
        ProcessWorkspaceCommandKind.RefreshProjections => RefreshAsync(),
        ProcessWorkspaceCommandKind.OpenAgentContext => Change(OpenAgentContext),
        ProcessWorkspaceCommandKind.OpenLiveDashboard => Change(() => Navigate?.Invoke("/live")),
        ProcessWorkspaceCommandKind.LaunchRun => LaunchSelectedDefinitionAsync(),
        ProcessWorkspaceCommandKind.FeedDefaults => Change(() => {
            CatalogCommandNotice = "Scenario defaults are available.";
            Reload();
        }),
        _ => throw new ArgumentOutOfRangeException(nameof(command))
    };
    public Task LaunchSelectedDefinitionAsync() => Change(() => {
        if (!CanStartAnotherLaunch) {
            CanStartAnotherLaunch = true;
            CatalogCommandNotice = $"Scenario launch accepted for {SelectedDefinitionKey}; explicit new intent is required to launch again.";
        }
    });
    public Task StartNewProcessLaunchAsync() => Change(() => {
        CanStartAnotherLaunch = false;
        CatalogCommandNotice = "A new scenario launch intent is ready.";
    });
    public Task ExecuteDefinitionEditorCommandAsync(ProcessDefinitionEditorCommandKind commandKind) => Change(() => {
        var editor = DefinitionCatalog.SelectedEditor!;
        var submitted = DefinitionDraft.Capture();
        var version = new ProcessDefinitionEditorVersionToken(NextVersion());
        var receipt = new ProcessDefinitionEditorCommandReceipt(Guid.NewGuid(), commandKind, ProcessDefinitionEditorCommandStatus.Accepted,
            version, ProcessScenarioData.Now, $"{commandKind} accepted in this scenario.", []);
        var updated = ProcessScenarioData.CreateEditor(editor.DefinitionKey, submitted,
            commandKind == ProcessDefinitionEditorCommandKind.Publish ? ProcessDefinitionAuthoringStatus.Published : ProcessDefinitionAuthoringStatus.Draft,
            version, new([]), receipt) with { RoleEditor = editor.RoleEditor, StepEditor = editor.StepEditor, Canvas = editor.Canvas, TemplateCatalog = editor.TemplateCatalog };
        SetEditor(updated);
        DefinitionDraft.Accept(updated, submitted);
        EditorCommandNotice = receipt.Summary;
    });
    public Task ExecuteDefinitionRoleEditorCommandAsync(ProcessDefinitionRoleEditorCommand command) => Change(() => {
        var version = new ProcessDefinitionRoleEditorVersionToken(NextVersion());
        var receipt = new ProcessDefinitionRoleCommandReceipt(Guid.NewGuid(), command.CommandKind, ProcessDefinitionRoleCommandStatus.Accepted,
            version, ProcessScenarioData.Now, "Role saved in this scenario.", []);
        var projection = ProcessScenarioData.CreateRoleEditor(command.DefinitionKey, command.Draft, version, new([]), receipt);
        RoleEditorState.Accept(projection);
        SetEditor(DefinitionCatalog.SelectedEditor! with { RoleEditor = projection });
    });
    public Task ExecuteDefinitionStepEditorCommandAsync(ProcessDefinitionStepEditorCommand command) => Change(() => {
        var version = new ProcessDefinitionStepEditorVersionToken(NextVersion());
        var receipt = new ProcessDefinitionStepCommandReceipt(Guid.NewGuid(), command.CommandKind, ProcessDefinitionStepCommandStatus.Accepted,
            version, ProcessScenarioData.Now, "Step saved in this scenario.", []);
        var projection = ProcessScenarioData.CreateStepEditor(command.DefinitionKey, command.Draft, version, new([]), receipt, command.CommandKind);
        StepEditorState.Accept(projection);
        SetEditor(DefinitionCatalog.SelectedEditor! with { StepEditor = projection });
    });
    public Task ExecuteDefinitionCanvasCommandAsync(ProcessDefinitionCanvasCommand command) => Change(() => {
        var version = new ProcessDefinitionCanvasVersionToken(NextVersion());
        var receipt = new ProcessDefinitionCanvasCommandReceipt(Guid.NewGuid(), command.CommandKind, ProcessDefinitionCanvasCommandStatus.Accepted,
            version, ProcessScenarioData.Now, "Canvas command accepted in this scenario.");
        var current = DefinitionCatalog.SelectedEditor!.Canvas!;
        var canvas = current with { VersionToken = version, LastCommandReceipt = receipt };
        if (command.NodePositions is { } positions) {
            canvas = canvas with { Nodes = canvas.Nodes.Select(node => positions.FirstOrDefault(position => position.NodeKey == node.NodeKey) is { } position
                ? node with { X = position.X, Y = position.Y } : node).ToArray() };
        } else if (command.CommandKind == ProcessDefinitionCanvasCommandKind.Recompose) {
            canvas = canvas with { Nodes = canvas.Nodes.Select((node, index) => node with { X = 160 + index % 2 * 380, Y = 80 + index / 2 * 280 }).ToArray() };
        } else {
            var kind = command.CommandKind switch {
                ProcessDefinitionCanvasCommandKind.AddStep => ProcessDefinitionCanvasNodeKind.Step,
                ProcessDefinitionCanvasCommandKind.AddBranchRouter => ProcessDefinitionCanvasNodeKind.BranchRouter,
                ProcessDefinitionCanvasCommandKind.AddRoleBinding or ProcessDefinitionCanvasCommandKind.CloneRoleReference => ProcessDefinitionCanvasNodeKind.Role,
                ProcessDefinitionCanvasCommandKind.AddArtifactExpectation or ProcessDefinitionCanvasCommandKind.CloneArtifactReference => ProcessDefinitionCanvasNodeKind.Artifact,
                ProcessDefinitionCanvasCommandKind.AddSubprocessBoundary => ProcessDefinitionCanvasNodeKind.SubprocessBoundary,
                _ => throw new ArgumentOutOfRangeException(nameof(command))
            };
            var node = ProcessScenarioData.CreateCanvasNode(new($"scenario:{kind}:{revision}"), kind, $"Scenario {kind} {revision}",
                "Local canvas addition", "This node belongs to this independent opening.", 550, 420, "info",
                new("architecture-decision"), kind == ProcessDefinitionCanvasNodeKind.Role ? new("solution-architect") : null,
                kind == ProcessDefinitionCanvasNodeKind.Artifact ? "architecture-decision-record" : null, ["Scenario"]);
            canvas = canvas with { Nodes = [.. canvas.Nodes, node] };
        }
        SetEditor(DefinitionCatalog.SelectedEditor! with { Canvas = canvas });
    });
    public Task ApplyTemplateCatalogQueryAsync(ProcessTemplateCatalogQueryProjection query) => Change(() => {
        templateQuery = query;
        var editor = DefinitionCatalog.SelectedEditor!;
        SetEditor(editor with { TemplateCatalog = ProcessScenarioData.CreateTemplateCatalog(editor.DefinitionKey, query,
            editor.TemplateCatalog?.LastImportReceipt, editor.TemplateCatalog?.ImportedComponents ?? []) });
    });
    public Task ExecuteTemplateImportCommandAsync(ProcessTemplateImportCommand command) => Change(() => {
        var editor = DefinitionCatalog.SelectedEditor!;
        var previous = editor.TemplateCatalog!.ImportedComponents;
        var source = ProcessScenarioData.CreateTemplateCatalog(editor.DefinitionKey,
            command.Query with { SearchText = null, Category = ProcessTemplateCatalogCategoryKind.All, SelectedItemKey = command.ItemKey }, null, previous);
        var item = source.SelectedItem ?? throw new InvalidOperationException("The requested scenario template is unavailable.");
        if (item.Kind == ProcessTemplateCatalogItemKind.Artifact && command.TargetStepKey is null) {
            throw new InvalidOperationException("An artifact import requires its target step.");
        }
        var imported = previous.Any(component => component.ItemKey == item.Key && component.TargetStepKey == command.TargetStepKey)
            ? previous
            : [.. previous, new ProcessTemplateImportedComponentProjection(item.Key, item.Kind, item.Title, item.SourceDefinitionKey,
                item.SourceComponentKey, source.Preview!.SourceJsonHash, command.TargetStepKey, ProcessScenarioData.Now)];
        var receipt = new ProcessTemplateImportCommandReceipt(Guid.NewGuid(), command.CommandKind, ProcessTemplateImportCommandStatus.Accepted,
            new(NextVersion()), ProcessScenarioData.Now, "Template component imported in this scenario.");
        SetEditor(editor with { TemplateCatalog = ProcessScenarioData.CreateTemplateCatalog(editor.DefinitionKey, command.Query, receipt,
            imported) });
    });
    public Task ChangeStatusFilterAsync(ChangeEventArgs args) => Change(() => statusFilter = string.IsNullOrEmpty(args.Value?.ToString())
        ? null : Enum.Parse<ProcessProjectedRunStatus>(args.Value!.ToString()!));
    public Task HideRunGroupAsync(ProcessLiveProcessSnapshot run) => Change(() => hidden.Add(run.RootRunId));
    public Task ShowHiddenRunGroupsAsync() => Change(hidden.Clear);
    public string GetOperatorActionNote(ProcessRuntimeOperatorActionProjection action) => notes.GetValueOrDefault(action.StepInstanceId, string.Empty);
    public void SetOperatorActionNote(ProcessRuntimeOperatorActionProjection action, string? value) => notes[action.StepInstanceId] = value ?? string.Empty;
    public bool IsOperatorActionBusy(ProcessRuntimeOperatorActionProjection action) => IsBusy;
    public Task ExecuteOperatorActionAsync(ProcessRuntimeOperatorActionProjection action) => Change(() => {
        OperatorActionMessage = $"Scenario {action.Label} accepted: {GetOperatorActionNote(action)}";
        CurrentShell = CurrentShell with { Runtime = CurrentShell.Runtime with {
            Runs = CurrentShell.Runtime.Runs.Select(run => run with { OperatorActions = run.OperatorActions.Where(candidate => candidate.StepInstanceId != action.StepInstanceId).ToArray() }).ToArray() } };
    });
    public Task OpenDefinitionsAsync() => Change(() => Navigate?.Invoke("/workspace"));
    public Task OpenProcessControlAsync(Guid runId) => Change(() => {
        SelectedRuntimeRunId = runId;
        ActiveDetailTabIndex = 3;
        SelectedRunViewIndex = 2;
        Reload();
    });
    public IReadOnlyList<ProcessRuntimeActiveAgentProjection> ResolveActiveAgents(Guid runId) => VisibleActiveAgents.Where(agent => agent.RunId == runId).ToArray();
    public ProcessRuntimeActiveAgentProjection? ResolvePrimaryAgent(IReadOnlyList<ProcessRuntimeActiveAgentProjection> activeAgents) => activeAgents.FirstOrDefault();
    public ProcessRuntimeOperatorActionProjection? ResolvePrimaryOperatorAction(ProcessLiveProcessSnapshot run) => run.OperatorActions.FirstOrDefault(action => action.IsEnabled);
    public IReadOnlyList<ProcessManagerMessageProjection> ResolveManagerMessages(ProcessRunId runId) => CurrentShell.Runtime.ManagerMessages.Where(message => message.RunId == runId).ToArray();
    public string ResolveAgentStatusTone(ProcessRuntimeActiveAgentProjection agent) => "info";
    public string ResolveCurrentStepText(ProcessLiveProcessSnapshot run) => run.CurrentStep?.StepKey ?? "No current step";
    public string ResolveManagerSummary(ProcessLiveProcessSnapshot run) => ResolveManagerMessages(run.RunId).LastOrDefault()?.Summary ?? "No manager message";
    public string BuildRunCardSummary(ProcessLiveProcessSnapshot run) => $"{run.Status} - {ResolveCurrentStepText(run)}";
    public string BuildAttentionSummary(ProcessLiveProcessSnapshot run) => ResolvePrimaryOperatorAction(run)?.Summary ?? run.Status.ToString();
    public Task InjectReadFailureAsync() => Change(() => ErrorMessage = "The scenario read failed. The last accepted projection remains visible.");
    public Task ChangeScopeAsync() => Change(() => {
        Scope = Scope.Kind == ProcessWorkspaceScopeKind.Global ? ProcessWorkspaceShellScope.ForProject(ProcessScenarioData.ProjectSubprocessProjectId) : ProcessWorkspaceShellScope.Global;
        editors.Clear();
        DefinitionDraft = new();
        RoleEditorState = new();
        StepEditorState = new();
        TemplateBrowserState = new();
        ErrorMessage = null;
        RunDetailSnapshot = null;
        EventDetail = null;
        AgentDetailAgent = null;
        hidden.Clear();
        Reload();
    });
}
