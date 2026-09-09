using Microsoft.Extensions.Logging;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.AgentFramework.Workflows.Templates;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Components.CanvasLib;
using CanDoItAll.Modules.AgentFramework.Pages.Components;
using CanDoItAll.Modules.Security;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.SharedKernel;
using Microsoft.AspNetCore.Components;
using System.Text.Json;
using System.Collections.Immutable;
using CanDoItAll.AgentFramework.Workflows.UI;

namespace CanDoItAll.Modules.AgentFramework.Pages;

public partial class WorkflowsPage : IDisposable {
    private const int HistoryRunPageSize = 8;
    private const int HistoryEventPageSize = 8;
    private const int DashboardTabIndex = 0;
    private const int WorkflowsTabIndex = 1;
    private const int EditorTabIndex = 2;
    private const int HistoryTabIndex = 3;
    private const int AnalyticsTabIndex = 4;
    private static readonly string[] RunResultPreviewPropertyNames =
    [
        "summary",
        "markdown",
        "notes",
        "message"
    ];

    [Inject]
    public IWorkflowCatalogService CatalogService { get; set; } = default!;

    [Inject]
    public IWorkflowComponentLibraryService ComponentLibrary { get; set; } = default!;

    [Inject]
    public IWorkflowExecutorCatalog ExecutorCatalog { get; set; } = default!;

    [Inject]
    public IWorkflowSettingsService SettingsService { get; set; } = default!;

    [Inject]
    public IWorkflowTestRunner TestRunner { get; set; } = default!;

    [Inject]
    public WorkflowExampleCatalogSeedService ExampleCatalogSeedService { get; set; } = default!;

    [Inject]
    public WorkflowTemplatePackLoader TemplatePackLoader { get; set; } = default!;

    [Inject]
    public IWorkflowRuntimeManager RuntimeManager { get; set; } = default!;

    [Inject]
    public IWorkflowExternalResponseService ExternalResponseService { get; set; } = default!;

    [Inject]
    public IWorkflowExternalResponsePageActorContextProvider ExternalResponseActorContextProvider { get; set; } = default!;

    [Inject]
    public IWorkflowRunStore RunStore { get; set; } = default!;

    [Inject]
    public IWorkflowAnalyticsQueryService AnalyticsQueryService { get; set; } = default!;

    [Inject]
    public IWorkflowOverviewQueryService OverviewQueryService { get; set; } = default!;

    [Inject]
    public IProjectStructureRuntimeGateway ProjectStructureGateway { get; set; } = default!;

    [Inject]
    public NavigationManager Navigation { get; set; } = default!;

    [Inject]
    public ILogger<WorkflowsPage> Logger { get; set; } = default!;

    [Inject]
    public NotificationService NotificationService { get; set; } = default!;

    [Inject]
    public IAgentChatLauncher AgentChatLauncher { get; set; } = default!;

    [Inject]
    public IAgentFrameworkWorkspaceService AgentWorkspaceService { get; set; } = default!;

    [SupplyParameterFromQuery(Name = "projectId")]
    public Guid? RequestedProjectId { get; set; }

    [SupplyParameterFromQuery(Name = "workflowId")]
    public Guid? RequestedWorkflowId { get; set; }

    [SupplyParameterFromQuery(Name = "runId")]
    public Guid? RequestedRunId { get; set; }

    private IReadOnlyList<WorkflowCatalogItem> definitions = [];
    private IReadOnlyList<LlmCallComponent> components = [];
    private IReadOnlyList<WorkflowProviderOption> providerOptions = [];
    private IReadOnlyList<WorkflowRunSnapshot> runs = [];
    private IReadOnlyList<WorkflowEventRecord> runEvents = [];
    private IReadOnlyList<WorkflowArtifactRecord> artifacts = [];
    private IReadOnlyList<WorkflowExternalRequestRecord> pendingRequests = [];
    private IReadOnlyList<WorkflowValidationIssue> validationIssues = [];
    private AgentDefinition? workflowCuratorAgent;
    private WorkflowTemplatePack? templatePack;
    private WorkflowTemplateDefinition? selectedTemplate;
    private WorkflowTemplateDefinition? templatePreviewTemplate;
    private WorkflowDefinition? templatePreviewDefinition;
    private LlmCallComponent? templatePreviewComponent;
    private WorkflowSettings settings = WorkflowSettings.Default;
    private WorkflowId? selectedDefinitionId;
    private WorkflowDefinition? selectedDefinition;
    private WorkflowRunSnapshot? selectedRun;
    private WorkflowAgentChatProjectSelection? selectedRouteProject;
    private WorkflowAgentChatNodeSelection? selectedCanvasNode;
    private WorkflowRunSnapshot? runDetail;
    private WorkflowEventRecord? eventDetail;
    private IReadOnlyList<WorkflowEventRecord> runDetailEvents = [];
    private IReadOnlyList<WorkflowArtifactRecord> runDetailArtifacts = [];
    private WorkflowTestRunResult? testResult;
    private string testInputJson = WorkflowPreviewInputSupport.DefaultInputJson;
    private WorkflowPreviewInputState previewInputState = new();
    private IReadOnlyList<ProjectStructureRuntimeProjectSummary> previewProjectOptions = [];
    private string previewInputErrorMessage = string.Empty;
    private string templateSearchText = string.Empty;
    private string templateCatalogueErrorMessage = string.Empty;
    private string templatePreviewErrorMessage = string.Empty;
    private string errorMessage = string.Empty;
    private int activeWorkflowTabIndex;
    private int historyRunPageIndex;
    private int historyRunTotalCount;
    private int historyEventPageIndex;
    private int historyEventTotalCount;
    private bool isLoading = true;
    private bool isBusy;
    private bool isOpeningWorkflowCurator;
    private bool isRunningTest;
    private bool isPreparingTest;
    private Task? definitionLoadTask;
    private bool isPreviewInputDialogOpen;
    private bool isTemplateCatalogueDialogOpen;
    private bool isTemplateCatalogueLoading;
    private bool isTemplatePreviewDialogOpen;
    private bool componentLibraryLoaded;
    private bool historyLoaded;
    private bool selectedDefinitionDetailLoaded;
    private bool selectedDefinitionDetailUnavailable;
    private bool isDefinitionSelectionLoading;
    private bool isRunsPageLoading;
    private bool isRunSelectionLoading;
    private bool hasObservedNavigation;
    private bool hasRouteIdentityFailure;
    private long selectedDefinitionGeneration;
    private long runPageGeneration;
    private long selectedRunGeneration;
    private WorkflowRunId? selectedRunRequestId;
    private long analyticsRefreshVersion;
    private long pageLoadGeneration;
    private AgentChatNavigationIdentity observedNavigation;
    private AgentChatNavigationIdentity selectedDefinitionNavigation;
    private Task? componentLibraryLoadTask;
    private Task? workflowCuratorResolutionTask;
    private readonly HashSet<string> expandedWorkflowTreeNodeIds = [];
    private CanvasWorkbenchUiState templatePreviewCanvasUiState = CreateTemplatePreviewCanvasUiState("start");
    private string? templatePreviewSelectedNodeId = "start";

    private AgentChatContextSurface AgentChatSurface
        => AgentFrameworkWorkflowsChatContextBuilder.Build(
            AgentFrameworkWorkflowsChatContextBuilder.ResolveView(activeWorkflowTabIndex),
            definitions.Count,
            CurrentDefinitionId,
            SelectedDefinitionSummary,
            selectedDefinition,
            selectedRun,
            historyLoaded,
            historyRunTotalCount,
            pendingRequests.Count,
            artifacts.Count,
            validationIssues.Count,
            selectedCanvasNode,
            selectedRouteProject);

    private AgentChatNavigationIdentity AgentChatNavigationFence
        => AgentChatNavigationIdentity.CreateForLocation(
            Navigation.BaseUri,
            Navigation.Uri,
            [
                new("projectId", RequestedProjectId?.ToString("D")),
                new("workflowId", RequestedWorkflowId?.ToString("D")),
                new("runId", RequestedRunId?.ToString("D"))
            ]);

    private Task HandleCanvasSelectedNodeChangedAsync(WorkflowAgentChatNodeSelection? selection) {
        selectedCanvasNode = selection;
        return Task.CompletedTask;
    }

    private AgentChatContextAccessState AgentChatAccessState
        => hasRouteIdentityFailure || IsAgentChatSelectedDefinitionDetailUnavailable
            ? AgentChatContextAccessState.Failed
            : isLoading || IsAgentChatSelectedDefinitionDetailPending || isDefinitionSelectionLoading || isRunsPageLoading || isRunSelectionLoading || isBusy
                ? AgentChatContextAccessState.Loading
                : AgentChatContextAccessState.Ready;

    private WorkflowId? CurrentDefinitionId => selectedDefinition?.Id ?? selectedDefinitionId;

    private WorkflowCatalogItem? SelectedDefinitionSummary
        => CurrentDefinitionId is { } definitionId
            ? definitions.FirstOrDefault(definition => definition.Id == definitionId)
            : null;

    private string WorkflowCuratorDisplayName
        => workflowCuratorAgent?.Name ?? WorkflowCuratorAgentIdentity.DefaultDisplayName;

    private string WorkflowCuratorAvatarImageUrl
        => workflowCuratorAgent?.AvatarImageUrl ?? WorkflowCuratorAgentIdentity.DefaultAvatarImageUrl;

    private string SelectedDefinitionTitle => selectedDefinition?.Name ?? SelectedDefinitionSummary?.Name ?? "Workflow detail";

    private bool IsSelectedDefinitionDetailPending
        => CurrentDefinitionId.HasValue && !selectedDefinitionDetailLoaded && !selectedDefinitionDetailUnavailable;

    private bool IsSelectedDefinitionDetailUnavailable
        => CurrentDefinitionId.HasValue && selectedDefinition is null && selectedDefinitionDetailUnavailable;

    private bool IsAgentChatSelectedDefinitionDetailPending
        => WorkflowTabRequiresDefinitionDetail(activeWorkflowTabIndex) && IsSelectedDefinitionDetailPending;

    private bool IsAgentChatSelectedDefinitionDetailUnavailable
        => WorkflowTabRequiresDefinitionDetail(activeWorkflowTabIndex) && IsSelectedDefinitionDetailUnavailable;

    private string EditorDefinitionKey
        => selectedDefinition is null
            ? "draft"
            : $"{selectedDefinition.Id.Value:D}:{selectedDefinition.VersionId.Value:D}";

    private string ComponentCountText => componentLibraryLoaded ? components.Count.ToString() : "-";

    private string HistoryRunCountText => historyLoaded ? historyRunTotalCount.ToString() : "-";

    private string PendingRequestCountText => historyLoaded ? pendingRequests.Count.ToString() : "-";

    private IReadOnlyList<WorkflowTemplateDefinition> WorkflowTemplates => templatePack?.Workflows ?? [];

    private string WorkflowTemplateSeedText => templatePack?.Manifest.SeedVersion ?? "-";

    private IReadOnlyList<WorkflowTemplateDefinition> FilteredWorkflowTemplates {
        get {
            if (templatePack is null) {
                return [];
            }

            if (string.IsNullOrWhiteSpace(templateSearchText)) {
                return templatePack.Workflows;
            }

            var query = templateSearchText.Trim();
            return templatePack.Workflows
                .Where(template => WorkflowTemplateMatchesSearch(template, query))
                .ToArray();
        }
    }

    private WorkflowTemplateDefinition? SelectedWorkflowTemplate {
        get {
            var templates = FilteredWorkflowTemplates;
            if (selectedTemplate is not null &&
                templates.Any(template => IsSameWorkflowTemplate(template, selectedTemplate))) {
                return selectedTemplate;
            }

            return templates.FirstOrDefault();
        }
    }

    private WorkflowNode? SelectedTemplatePreviewNode
        => templatePreviewDefinition is null || string.IsNullOrWhiteSpace(templatePreviewSelectedNodeId)
            ? null
            : templatePreviewDefinition.Graph.Nodes.FirstOrDefault(node => node.Id.Value == templatePreviewSelectedNodeId);

    private IReadOnlyList<CanvasWorkbenchStat> TemplatePreviewCanvasStats =>
    [
        new() {
            Label = "Nodes",
            Value = templatePreviewDefinition?.Graph.Nodes.Count.ToString() ?? "0",
            Tone = "info"
        },
        new() {
            Label = "Edges",
            Value = templatePreviewDefinition?.Graph.Edges.Count.ToString() ?? "0",
            Tone = "secondary"
        },
        new() {
            Label = "Inputs",
            Value = templatePreviewDefinition?.InputParameters.Count.ToString() ?? "0",
            Tone = "accent"
        }
    ];

    private CanvasWorkbenchSurface? TemplatePreviewCanvasSurface
        => templatePreviewDefinition is null || templatePreviewComponent is null
            ? null
            : BuildTemplatePreviewCanvasSurface(
                templatePreviewDefinition,
                templatePreviewComponent,
                ExecutorCatalog.ListExecutors(),
                templatePreviewCanvasUiState,
                templatePreviewSelectedNodeId);

    private string ValidationText
        => selectedDefinitionDetailUnavailable
            ? "Unavailable"
            : selectedDefinitionId.HasValue && !selectedDefinitionDetailLoaded
            ? "Deferred"
            : validationIssues.Count == 0 ? "Valid" : $"{validationIssues.Count} issue(s)";

    private string ValidationTone
        => selectedDefinitionDetailUnavailable
            ? "warning"
            : selectedDefinitionId.HasValue && !selectedDefinitionDetailLoaded
            ? "neutral"
            : validationIssues.Count == 0 ? "success" : "warning";

    private string RunText => !historyLoaded ? "History deferred" : selectedRun is null ? "No run selected" : selectedRun.State.ToString();

    private string RunTone => !historyLoaded || selectedRun is null ? "neutral" : ResolveRunTone(selectedRun.State);

    private long presentationRevision;
    private bool disposed;
    private long tabGeneration;
    private long targetGeneration;
    private long overlayGeneration;
    private long templateGeneration;
    private long effectSequence;
    private long? activeTestOperation;
    private CancellationTokenSource pageReadCancellation = new();
    private CancellationTokenSource selectionReadCancellation = new();
    private CancellationTokenSource runReadCancellation = new();
    private CancellationTokenSource overlayReadCancellation = new();
    private readonly CancellationTokenSource lifetimeCancellation = new();
    private bool runUnavailable;
    private readonly Dictionary<(WorkflowExternalRequestId Id, WorkflowExternalRequestVersion Version), string> responseDrafts = [];
    private readonly HashSet<(WorkflowExternalRequestId Id, WorkflowExternalRequestVersion Version)> respondingRequests = [];

    private WorkflowShellPresentation ShellPresentation => new() {
        Revision = presentationRevision,
        ActiveTab = (WorkflowTab)activeWorkflowTabIndex,
        IsLoading = isLoading,
        IsBusy = isBusy || isLoading,
        ErrorMessage = errorMessage,
        DefinitionCount = definitions.Count.ToString(),
        ComponentCount = ComponentCountText,
        RunCount = HistoryRunCountText,
        PendingCount = PendingRequestCountText,
        Backend = settings.DefaultRuntimePolicy.PreferredBackend.ToString(),
        EditorTitle = SelectedDefinitionTitle,
        EditorLoading = IsSelectedDefinitionDetailPending,
        EditorUnavailable = IsSelectedDefinitionDetailUnavailable
    };

    private WorkflowCatalogPresentation CatalogPresentation => new() {
        Revision = presentationRevision,
        IsBusy = isBusy || isLoading,
        DefinitionCount = definitions.Count,
        Tree = WorkflowDefinitionTreeNodes.Select(MapTreeEntry).ToImmutableArray(),
        Definition = selectedDefinition is { } definition
            ? new(definition.Id.Value, definition.Name, definition.Description, definition.Status.ToString(),
                definition.RuntimePolicy.PreferredBackend.ToString(), definition.Graph.Nodes.Count,
                definition.Graph.Edges.Count, definition.Status == WorkflowLifecycleStatus.Draft)
            : SelectedDefinitionSummary is { } summary
                ? new(summary.Id.Value, summary.Name, summary.Description, summary.Status.ToString(),
                    summary.PreferredBackend.ToString(), 0, 0, false)
                : null,
        DetailLoading = IsSelectedDefinitionDetailPending,
        DetailUnavailable = IsSelectedDefinitionDetailUnavailable,
        ValidationText = ValidationText,
        ValidationTone = ValidationTone,
        Issues = validationIssues.Select(issue => new WorkflowValidationView(issue.Code.ToString(), FormatWorkflowMessage(issue.Message))).ToImmutableArray(),
        SettingsSummary = BuildSettingsSummary()
    };

    private WorkflowHistoryPresentation HistoryPresentation => new() {
        Revision = presentationRevision,
        IsBusy = isBusy || isLoading,
        IsLoading = isRunsPageLoading || isRunSelectionLoading,
        RunUnavailable = runUnavailable,
        CanTest = CurrentDefinitionId.HasValue && !selectedDefinitionDetailUnavailable && !hasRouteIdentityFailure && !isBusy && !isLoading,
        IsRunningTest = isRunningTest || isPreparingTest,
        TestInputJson = testInputJson,
        TestSucceeded = testResult?.Succeeded,
        TestMessage = testResult is null ? "" : FormatWorkflowMessage(testResult.ErrorMessage),
        RunText = RunText,
        RunTone = RunTone,
        SelectedRun = selectedRun is null ? null : MapRun(selectedRun),
        Runs = runs.Select(MapRun).ToImmutableArray(),
        Events = runEvents.Select(item => new WorkflowEventView(item.Id, item.Kind.ToString(),
            ResolveEventTone(item.Kind), FormatDate(item.CreatedAtUtc), Truncate(ResolveEventDisplayMessage(item), 140))).ToImmutableArray(),
        Artifacts = artifacts.Select(item => new WorkflowArtifactView(item.Name, item.Kind.ToString(), item.ContentType,
            FormatWorkflowMessage(item.Summary))).ToImmutableArray(),
        Requests = pendingRequests.Select(item => new WorkflowRequestView(item.Id.Value, item.Kind.ToString(), item.EventName,
            item.RequestJson, ResponseDraft(item), respondingRequests.Contains((item.Id, item.Version)))).ToImmutableArray(),
        RunPage = new(historyRunPageIndex, HistoryRunTotalPages, historyRunTotalCount,
            FormatPageLabel(historyRunPageIndex, HistoryRunTotalPages, historyRunTotalCount, "runs")),
        EventPage = new(historyEventPageIndex, HistoryEventTotalPages, historyEventTotalCount,
            FormatPageLabel(historyEventPageIndex, HistoryEventTotalPages, historyEventTotalCount, "events"))
    };

    private WorkflowTemplatePresentation TemplatePresentation => new() {
        Revision = presentationRevision,
        IsLoading = isTemplateCatalogueLoading,
        ErrorMessage = templateCatalogueErrorMessage,
        Search = templateSearchText,
        Seed = WorkflowTemplateSeedText,
        TotalCount = WorkflowTemplates.Count,
        Templates = FilteredWorkflowTemplates.Select(MapTemplate).ToImmutableArray(),
        Selected = SelectedWorkflowTemplate is { } template ? MapTemplate(template) : null
    };

    private static WorkflowTreeEntry MapTreeEntry(TreeViewNode node) => new(new(node.Id), node.Text, node.Icon,
        node.IsExpanded, node.IsSelected, node.Children.Select(MapTreeEntry).ToImmutableArray(), node.Tooltip,
        node.IsDisabled, node.IsSelectable, node.BadgeText, node.DataTestId, node.ChildrenDataTestId);

    private WorkflowRunView MapRun(WorkflowRunSnapshot run) => new(run.RunId.Value, run.State.ToString(),
        ResolveRunTone(run.State), run.Backend.ToString(), FormatDate(run.UpdatedAtUtc), Truncate(ResolveRunDisplaySummary(run), 120),
        selectedRun?.RunId == run.RunId, !IsTerminalRun(run));

    private WorkflowTemplateView MapTemplate(WorkflowTemplateDefinition template) => new(new(template.Key), template.Name,
        template.Description, BuildTemplateNodeKindSummary(template), template.Graph.Nodes.Count, template.Graph.Edges.Count,
        template.InputParameters.Count, templatePack?.RuntimePolicy.PreferredBackend.ToString() ?? "-",
        template.InputParameters.Take(4).Select(input => new WorkflowTemplateInput(input.Label, input.Description,
            input.Key, input.Kind.ToString(), input.Required)).ToImmutableArray(),
        SelectedWorkflowTemplate is { } selected && IsSameWorkflowTemplate(template, selected));

    private string ResponseDraft(WorkflowExternalRequestRecord request)
        => responseDrafts.GetValueOrDefault((request.Id, request.Version), "{\"approved\":true}");

    private async Task HandleIntentAsync(WorkflowIntent intent) {
        if (disposed || intent.Action != WorkflowAction.ChangeTab && intent.Revision != presentationRevision) {
            return;
        }

        switch (intent.Action) {
            case WorkflowAction.Refresh:
                await RefreshAsync();
                break;
            case WorkflowAction.CreateStarter:
                await CreateStarterWorkflowAsync();
                break;
            case WorkflowAction.OpenAgents:
                OpenAgents();
                break;
            case WorkflowAction.ChangeTab:
                await HandleWorkflowTabChangedAsync((int)intent.Tab);
                break;
            case WorkflowAction.SelectDefinition when intent.TreeKey is { } key:
                await HandleWorkflowTreeSelectAsync(key.Value);
                break;
            case WorkflowAction.ToggleTree when intent.TreeKey is { } key:
                await HandleWorkflowTreeToggleAsync(key.Value);
                break;
            case WorkflowAction.OpenTemplates:
                await OpenTemplateCatalogueDialogAsync();
                break;
            case WorkflowAction.Publish when intent.TargetId == CurrentDefinitionId?.Value:
                await PublishSelectedDefinitionAsync();
                break;
            case WorkflowAction.TestInputChanged:
                testInputJson = intent.Text ?? "";
                break;
            case WorkflowAction.RunTest:
                await RunSelectedWorkflowAsync();
                break;
            case WorkflowAction.CancelRun when intent.TargetId == selectedRun?.RunId.Value:
                await CancelSelectedRunAsync();
                break;
            case WorkflowAction.SelectRun when runs.FirstOrDefault(run => run.RunId.Value == intent.TargetId) is { } run:
                await SelectRunAsync(run.RunId);
                break;
            case WorkflowAction.RunDetails when runs.FirstOrDefault(run => run.RunId.Value == intent.TargetId) is { } run:
                await OpenRunDetailDialogAsync(run);
                break;
            case WorkflowAction.EventDetails when runEvents.FirstOrDefault(item => item.Id == intent.TargetId) is { } item:
                OpenEventDetailDialog(item);
                break;
            case WorkflowAction.RunPage:
                await ChangeRunPageAsync(intent.Delta);
                break;
            case WorkflowAction.EventPage:
                await ChangeEventPageAsync(intent.Delta);
                break;
            case WorkflowAction.ResponseChanged when pendingRequests.FirstOrDefault(request => request.Id.Value == intent.TargetId) is { } request:
                responseDrafts[(request.Id, request.Version)] = intent.Text ?? "";
                break;
            case WorkflowAction.Respond when pendingRequests.FirstOrDefault(request => request.Id.Value == intent.TargetId) is { } request:
                await RespondToRequestAsync(request);
                break;
            case WorkflowAction.TemplateSearch:
                HandleWorkflowTemplateSearchChanged(new() { Value = intent.Text });
                break;
            case WorkflowAction.SelectTemplate when WorkflowTemplates.FirstOrDefault(item => item.Key == intent.TemplateKey?.Value) is { } template:
                SelectWorkflowTemplate(template);
                break;
            case WorkflowAction.PreviewTemplate when WorkflowTemplates.FirstOrDefault(item => item.Key == intent.TemplateKey?.Value) is { } template:
                await OpenTemplatePreviewDialogAsync(template);
                break;
        }
    }

    private IReadOnlyList<TreeViewNode> WorkflowDefinitionTreeNodes
        => WorkflowDefinitionTreeNodeBuilder.Build(
            definitions,
            CurrentDefinitionId,
            expandedWorkflowTreeNodeIds);

    private int HistoryRunTotalPages => CalculateTotalPages(historyRunTotalCount, HistoryRunPageSize);

    private int HistoryEventTotalPages => CalculateTotalPages(historyEventTotalCount, HistoryEventPageSize);

    protected override async Task OnParametersSetAsync() {
        var navigation = AgentChatNavigationFence;
        if (hasObservedNavigation && observedNavigation == navigation) {
            return;
        }

        hasObservedNavigation = true;
        observedNavigation = navigation;
        await RefreshRouteAsync(WorkflowRouteRequest.Create(
            RequestedProjectId,
            RequestedWorkflowId,
            RequestedRunId), navigation);
    }

    private async Task RefreshAsync() {
        if (isBusy) {
            return;
        }

        var route = WorkflowRouteRequest.Create(
            RequestedProjectId,
            RequestedWorkflowId,
            RequestedRunId);
        var navigation = AgentChatNavigationFence;
        var generation = BeginPageLoad(clearSelection: route.HasExplicitSelection);
        await ExecutePageLoadAsync(
            route.HasExplicitSelection ? route.WorkflowId : CurrentDefinitionId,
            route.HasExplicitSelection ? route.RunId : selectedRun?.RunId,
            route.HasExplicitSelection ? route : null,
            generation,
            navigation);
    }

    private async Task RefreshRouteAsync(
        WorkflowRouteRequest route,
        AgentChatNavigationIdentity navigation) {
        var generation = BeginPageLoad(clearSelection: true);
        await ExecutePageLoadAsync(
            route.WorkflowId,
            route.RunId,
            route.HasExplicitSelection ? route : null,
            generation,
            navigation);
    }

    private long BeginPageLoad(bool clearSelection) {
        var generation = ++pageLoadGeneration;
        RenewRead(ref pageReadCancellation);
        InvalidateSelectionEffects();
        isBusy = true;
        isLoading = true;
        errorMessage = string.Empty;
        hasRouteIdentityFailure = false;
        selectedRouteProject = null;
        if (clearSelection) {
            ClearSelectedDefinitionState();
            ClearHistoryState(markLoaded: false);
        }

        return generation;
    }

    private async Task ExecutePageLoadAsync(
        WorkflowId? preferredDefinitionId,
        WorkflowRunId? preferredRunId,
        WorkflowRouteRequest? requiredRoute,
        long generation,
        AgentChatNavigationIdentity navigation) {
        var pageLoadCompleted = false;
        try {
            await LoadPageCoreAsync(
                preferredDefinitionId,
                preferredRunId,
                requiredRoute,
                generation,
                navigation);
            pageLoadCompleted = true;
        } catch (Exception exception) {
            if (!IsCurrentPageLoad(generation, navigation)) {
                return;
            }

            errorMessage = FormatWorkflowException(exception);
            if (requiredRoute is not null) {
                FailRouteIdentity(errorMessage);
            }

            NotificationService.Error("Workflow refresh failed", errorMessage);
        } finally {
            if (IsCurrentPageLoad(generation, navigation)) {
                isLoading = false;
                isBusy = false;
            }
        }

        if (!pageLoadCompleted || !IsCurrentPageLoad(generation, navigation)) {
            return;
        }

        StateHasChanged();
        await EnsureWorkflowCuratorAgentAsync();
    }

    private Task LoadPageAsync(
        WorkflowId? preferredDefinitionId = null,
        WorkflowRunId? preferredRunId = null) {
        var navigation = AgentChatNavigationFence;
        var generation = ++pageLoadGeneration;
        var targetDefinition = preferredDefinitionId ?? CurrentDefinitionId;
        var route = selectedRouteProject is { } project && targetDefinition == CurrentDefinitionId
            ? WorkflowRouteRequest.Create(project.ProjectId, targetDefinition?.Value, preferredRunId?.Value)
            : null;
        return LoadPageCoreAsync(
            targetDefinition,
            preferredRunId,
            requiredRoute: route,
            generation,
            navigation);
    }

    private async Task LoadPageCoreAsync(
        WorkflowId? preferredDefinitionId,
        WorkflowRunId? preferredRunId,
        WorkflowRouteRequest? requiredRoute,
        long generation,
        AgentChatNavigationIdentity navigation) {
        analyticsRefreshVersion++;
        var cancellation = pageReadCancellation.Token;
        var settingsTask = SettingsService.GetSettingsAsync(cancellation);
        var definitionsTask = CatalogService.ListDefinitionsAsync(cancellation);
        await Task.WhenAll(settingsTask, definitionsTask);

        if (!IsCurrentPageLoad(generation, navigation)) {
            return;
        }

        var loadedSettings = await settingsTask;
        var loadedDefinitions = await definitionsTask;
        WorkflowAgentChatProjectSelection? routeProject = null;
        var routeError = requiredRoute?.ValidationError ?? string.Empty;
        if (string.IsNullOrEmpty(routeError) &&
            requiredRoute?.WorkflowId is { } requiredDefinitionId &&
            loadedDefinitions.All(definition => definition.Id != requiredDefinitionId)) {
            routeError = $"Workflow definition '{requiredDefinitionId}' was not found.";
        }

        if (string.IsNullOrEmpty(routeError) &&
            requiredRoute is { ProjectId: { } projectId, WorkflowId: { } workflowId }) {
            var projectValidation = await ValidateProjectWorkflowRelationAsync(projectId, workflowId, cancellation);
            if (!IsCurrentPageLoad(generation, navigation)) {
                return;
            }

            routeProject = projectValidation.Project;
            routeError = projectValidation.ErrorMessage;
        }

        settings = loadedSettings;
        definitions = loadedDefinitions.ToImmutableArray();
        if (!string.IsNullOrEmpty(routeError)) {
            FailRouteIdentity(routeError);
            return;
        }

        selectedRouteProject = routeProject;

        var definitionId = requiredRoute?.WorkflowId ??
                           preferredDefinitionId ??
                           CurrentDefinitionId ??
                           definitions.FirstOrDefault()?.Id;
        if (definitionId.HasValue) {
            SetSelectedDefinitionPlaceholder(definitionId.Value);
        } else {
            ClearSelectedDefinitionState();
        }

        if (requiredRoute?.WorkflowId.HasValue == true) {
            await EnsureSelectedDefinitionLoadedAsync();
            if (!IsCurrentPageLoad(generation, navigation)) {
                return;
            }

            if (selectedDefinition?.Id != definitionId) {
                FailRouteIdentity(errorMessage.Length > 0
                    ? errorMessage
                    : $"Workflow definition '{definitionId}' could not be loaded.");
                return;
            }
        }

        if (ShouldLoadHistory(preferredRunId)) {
            var definitionGeneration = selectedDefinitionGeneration;
            await EnsureSelectedDefinitionLoadedAsync();
            await LoadRunsPageAsync(
                definitionId,
                pageIndex: 0,
                preferredRunId,
                definitionGeneration);
            if (!IsCurrentPageLoad(generation, navigation)) {
                return;
            }

            if (requiredRoute?.RunId is { } requiredRunId && selectedRun?.RunId != requiredRunId) {
                FailRouteIdentity(
                    $"Workflow run '{requiredRunId}' was not found for workflow '{definitionId}'.");
                return;
            }
        } else {
            ClearHistoryState(markLoaded: false);
        }

        if (componentLibraryLoaded) {
            await RefreshComponentLibraryAsync();
        }
    }

    private async Task SelectDefinitionAsync(WorkflowId definitionId) {
        var projectId = selectedRouteProject?.ProjectId;
        selectedRouteProject = null;
        errorMessage = string.Empty;
        SetSelectedDefinitionPlaceholder(definitionId);
        var selectionGeneration = selectedDefinitionGeneration;
        isDefinitionSelectionLoading = true;
        StateHasChanged();
        try {
            if (projectId.HasValue) {
                var relation = await ValidateProjectWorkflowRelationAsync(projectId.Value, definitionId, selectionReadCancellation.Token);
                if (!IsCurrentDefinitionSelection(definitionId, selectionGeneration)) {
                    return;
                }

                if (relation.Project is null) {
                    FailRouteIdentity(relation.ErrorMessage);
                    return;
                }

                selectedRouteProject = relation.Project;
            }

            await EnsureSelectedDefinitionLoadedAsync();
            if (!IsCurrentDefinitionSelection(definitionId, selectionGeneration)) {
                return;
            }

            if (historyLoaded || WorkflowTabRequiresHistory(activeWorkflowTabIndex)) {
                await LoadRunsPageAsync(
                    definitionId,
                    pageIndex: 0,
                    expectedDefinitionGeneration: selectionGeneration);
            } else {
                ClearHistoryState(markLoaded: false);
            }
        } catch (Exception exception) {
            if (IsCurrentDefinitionSelection(definitionId, selectionGeneration)) {
                FailRouteIdentity(FormatWorkflowException(exception));
            }
        } finally {
            if (IsCurrentDefinitionSelection(definitionId, selectionGeneration)) {
                isDefinitionSelectionLoading = false;
                StateHasChanged();
            }
        }
    }

    private async Task HandleWorkflowTreeSelectAsync(string nodeId) {
        if (!WorkflowDefinitionTreeNodeBuilder.TryReadDefinitionId(nodeId, out var definitionId)) {
            return;
        }

        await SelectDefinitionAsync(definitionId);
    }

    private Task HandleWorkflowTreeToggleAsync(string nodeId) {
        if (!expandedWorkflowTreeNodeIds.Add(nodeId)) {
            expandedWorkflowTreeNodeIds.Remove(nodeId);
        }

        return Task.CompletedTask;
    }

    private async Task OpenTemplateCatalogueDialogAsync() {
        if (isTemplateCatalogueLoading) {
            return;
        }

        isTemplateCatalogueDialogOpen = true;
        templateCatalogueErrorMessage = string.Empty;

        if (templatePack is not null) {
            SelectDefaultWorkflowTemplate();
            return;
        }

        isTemplateCatalogueLoading = true;
        try {
            await EnsureTemplatePackLoadedAsync();
            SelectDefaultWorkflowTemplate();
        } catch (Exception exception) {
            templateCatalogueErrorMessage = FormatWorkflowException(exception);
            NotificationService.Error("Template catalogue failed", templateCatalogueErrorMessage);
        } finally {
            isTemplateCatalogueLoading = false;
        }
    }

    private void CloseTemplateCatalogueDialog() {
        templateGeneration++;
        isTemplateCatalogueDialogOpen = false;
        templateCatalogueErrorMessage = string.Empty;
    }

    private void SelectWorkflowTemplate(WorkflowTemplateDefinition template) {
        presentationRevision++;
        templateGeneration++;
        selectedTemplate = template;
    }

    private void HandleWorkflowTemplateSearchChanged(ChangeEventArgs args) {
        presentationRevision++;
        templateGeneration++;
        templateSearchText = args.Value?.ToString() ?? string.Empty;
        SelectDefaultWorkflowTemplate();
    }

    private void SelectDefaultWorkflowTemplate() {
        selectedTemplate = SelectedWorkflowTemplate ?? FilteredWorkflowTemplates.FirstOrDefault();
    }

    private async Task OpenTemplatePreviewDialogAsync(WorkflowTemplateDefinition template) {
        var owner = CaptureOwner();
        var generation = ++templateGeneration;
        presentationRevision++;
        selectedTemplate = template;
        templatePreviewErrorMessage = string.Empty;

        try {
            await EnsureTemplatePackLoadedAsync();
            if (!Owns(owner) || generation != templateGeneration || templatePack is null) {
                return;
            }

            var component = CreateTransientTemplateComponent(templatePack, template);
            var definition = CreateTemplateWorkflowDefinition(
                templatePack,
                template,
                component,
                NormalizeTemplateDraftBaseName(template.Name),
                WorkflowLifecycleStatus.Draft);
            templatePreviewTemplate = template;
            templatePreviewComponent = component;
            templatePreviewDefinition = definition;
            templatePreviewSelectedNodeId = definition.Graph.StartNodeId.Value;
            templatePreviewCanvasUiState = CreateTemplatePreviewCanvasUiState(templatePreviewSelectedNodeId);
            isTemplatePreviewDialogOpen = true;
        } catch (Exception exception) {
            if (!Owns(owner) || generation != templateGeneration) {
                return;
            }

            templatePreviewErrorMessage = FormatWorkflowException(exception);
            NotificationService.Error("Template preview failed", templatePreviewErrorMessage);
        }
    }

    private void CloseTemplatePreviewDialog() {
        templateGeneration++;
        isTemplatePreviewDialogOpen = false;
        templatePreviewErrorMessage = string.Empty;
        templatePreviewTemplate = null;
        templatePreviewDefinition = null;
        templatePreviewComponent = null;
        templatePreviewSelectedNodeId = "start";
        templatePreviewCanvasUiState = CreateTemplatePreviewCanvasUiState(templatePreviewSelectedNodeId);
    }

    private async Task AddSelectedTemplateToDraftsAsync() {
        if (isBusy ||
            templatePack is null ||
            templatePreviewTemplate is null) {
            return;
        }

        var owner = CaptureOwner();
        var generation = templateGeneration;
        var pack = templatePack;
        var template = templatePreviewTemplate;
        var draftName = ResolveTemplateDraftName(template.Name, definitions);
        isBusy = true;
        templatePreviewErrorMessage = string.Empty;

        try {
            await EnsureComponentLibraryLoadedAsync();
            if (!componentLibraryLoaded || !Owns(owner) || generation != templateGeneration) {
                return;
            }
            var providerOption = ResolveTemplateProviderOption();
            var component = await ComponentLibrary.SaveComponentAsync(CreateTemplateComponentSaveRequest(
                pack,
                template,
                draftName,
                providerOption));
            var definition = CreateTemplateWorkflowDefinition(
                pack,
                template,
                component,
                draftName,
                WorkflowLifecycleStatus.Draft);
            var saved = await CatalogService.SaveDefinitionAsync(new WorkflowDefinitionSaveRequest(
                Id: null,
                ExpectedVersionId: null,
                Name: definition.Name,
                Description: definition.Description,
                Status: WorkflowLifecycleStatus.Draft,
                Graph: definition.Graph,
                RuntimePolicy: definition.RuntimePolicy) {
                InputParameters = definition.InputParameters
            });

            if (!Owns(owner) || generation != templateGeneration) {
                return;
            }

            NotificationService.Success("Template added to drafts", saved.Name);
            CloseTemplatePreviewDialog();
            CloseTemplateCatalogueDialog();
            isBusy = false;
            await LoadPageAsync(preferredDefinitionId: saved.Id);
        } catch (Exception exception) {
            if (!Owns(owner) || generation != templateGeneration) {
                return;
            }

            templatePreviewErrorMessage = FormatWorkflowException(exception);
            NotificationService.Error("Template add failed", templatePreviewErrorMessage);
        } finally {
            if (Owns(owner)) {
                isBusy = false;
            }
        }
    }

    private Task HandleTemplatePreviewCanvasSelectionChangedAsync(CanvasWorkbenchSelectionChangedEventArgs args) {
        templatePreviewSelectedNodeId = args.PrimaryNodeId ?? args.SelectedNodeIds.FirstOrDefault();
        return Task.CompletedTask;
    }

    private Task HandleTemplatePreviewCanvasStateChangedAsync(string stateJson) {
        templatePreviewCanvasUiState = CanvasWorkbenchUiState.Parse(stateJson);
        return Task.CompletedTask;
    }

    private async Task LoadDefinitionAsync(
        WorkflowId definitionId,
        long selectionGeneration) {
        WorkflowDefinitionDetail? detail;
        try {
            detail = await CatalogService.GetDefinitionAsync(definitionId, cancellationToken: selectionReadCancellation.Token);
        } catch (Exception exception) {
            if (!IsCurrentDefinitionSelection(definitionId, selectionGeneration)) {
                return;
            }

            selectedDefinition = null;
            validationIssues = [];
            selectedDefinitionDetailLoaded = false;
            selectedDefinitionDetailUnavailable = true;
            errorMessage = FormatWorkflowException(exception);
            NotificationService.Error("Workflow detail failed", errorMessage);
            return;
        }

        if (!IsCurrentDefinitionSelection(definitionId, selectionGeneration)) {
            return;
        }

        if (detail is null || detail.Definition.Id != definitionId) {
            selectedDefinitionId = definitionId;
            selectedDefinition = null;
            validationIssues = [];
            selectedDefinitionDetailLoaded = false;
            selectedDefinitionDetailUnavailable = true;
            errorMessage = $"Workflow definition '{definitionId}' was not found.";
            return;
        }

        selectedDefinition = FreezeDefinition(detail.Definition);
        validationIssues = detail.Validation.Issues.ToImmutableArray();
        selectedDefinitionId = detail.Definition.Id;
        selectedDefinitionDetailLoaded = true;
        selectedDefinitionDetailUnavailable = false;
    }

    private async Task CreateStarterWorkflowAsync() {
        if (isBusy) {
            return;
        }

        var owner = CaptureOwner();
        isBusy = true;
        errorMessage = string.Empty;

        try {
            await EnsureComponentLibraryLoadedAsync();
            if (!componentLibraryLoaded || !Owns(owner)) {
                return;
            }
            var providerOption = ResolveDefaultProviderOption();
            var component = await ComponentLibrary.SaveComponentAsync(new LlmCallComponentSaveRequest(
                Id: null,
                Name: $"Starter LLM call {DateTimeOffset.UtcNow:HHmmss}",
                ProviderProfileId: providerOption?.ProviderProfileId,
                Model: ResolveDefaultModel(providerOption),
                Modality: WorkflowModality.Text,
                ModelSettings: new WorkflowModelSettings(
                    Temperature: 0.2,
                    MaxOutputTokens: 800,
                    RequireJsonOutput: false,
                    ResponseFormatJsonSchema: string.Empty),
                Instructions: "Summarize the workflow input and return a concise result.",
                InputShape: WorkflowValueShape.Text,
                ResultShape: WorkflowValueShape.Text,
                Permissions: AgentPermissionsPolicy.Default));
            var definition = await CatalogService.SaveDefinitionAsync(new WorkflowDefinitionSaveRequest(
                Id: null,
                ExpectedVersionId: null,
                Name: $"Starter workflow {DateTimeOffset.UtcNow:HHmmss}",
                Description: "Starter workflow with one prepared LLM Call Component and in-process preview policy.",
                Status: WorkflowLifecycleStatus.Draft,
                Graph: CreateStarterGraph(component.Id),
                RuntimePolicy: new WorkflowRuntimePolicy(
                    WorkflowRuntimeBackendKind.InProcess,
                    AllowInProcessPreviewRuns: true,
                    RequireDurableProductionRuns: false,
                    ExposeAzureFunctionsStatusEndpoint: false,
                    ExposeAzureFunctionsMcpTool: false)));

            if (!Owns(owner)) {
                return;
            }

            isBusy = false;
            NotificationService.Success("Workflow created", "Starter workflow and LLM component were created.");
            await LoadPageAsync(preferredDefinitionId: definition.Id);
        } catch (Exception exception) {
            if (!Owns(owner)) {
                return;
            }

            errorMessage = FormatWorkflowException(exception);
            NotificationService.Error("Workflow create failed", errorMessage);
        } finally {
            if (Owns(owner)) {
                isBusy = false;
            }
        }
    }

    private async Task PublishSelectedDefinitionAsync() {
        if (isBusy || selectedDefinition is not { Status: WorkflowLifecycleStatus.Draft } draft) {
            return;
        }

        var owner = CaptureOwner();
        isBusy = true;
        errorMessage = string.Empty;

        try {
            var published = await CatalogService.ChangeDefinitionStatusAsync(
                new WorkflowDefinitionStatusChangeRequest(
                    draft.Id,
                    draft.VersionId,
                    WorkflowLifecycleStatus.Active));

            if (!Owns(owner)) {
                return;
            }

            isBusy = false;
            NotificationService.Success(
                "Workflow published",
                $"{published.Name} is ready for production runs.");
            selectedDefinitionDetailLoaded = false;
            await LoadPageAsync(preferredDefinitionId: published.Id);
            if (!disposed && CurrentDefinitionId == published.Id) {
                await EnsureSelectedDefinitionLoadedAsync();
            }
        } catch (Exception exception) {
            if (!Owns(owner)) {
                return;
            }

            errorMessage = FormatWorkflowException(exception);
            NotificationService.Error("Workflow publish failed", errorMessage);
        } finally {
            if (Owns(owner)) {
                isBusy = false;
            }
        }
    }

    private async Task RunSelectedWorkflowAsync() {
        if (disposed || isRunningTest || isPreparingTest) {
            return;
        }

        var owner = CaptureOwner();
        isPreparingTest = true;
        try {
            await EnsureSelectedDefinitionLoadedAsync();
            if (!Owns(owner) || selectedDefinition is null) {
                return;
            }

            var requirements = WorkflowPreviewInputSupport.Analyze(selectedDefinition, ExecutorCatalog.ListExecutors());
            if (requirements.NeedsPreviewDialog) {
                await OpenSelectedWorkflowPreviewInputDialogAsync(requirements);
                return;
            }

            await RunSelectedWorkflowCoreAsync(testInputJson, draftDefinition: null, WorkflowPreviewSimulationPlan.Empty);
        } finally {
            if (Owns(owner)) {
                isPreparingTest = false;
            }
        }
    }

    private async Task OpenSelectedWorkflowPreviewInputDialogAsync(WorkflowPreviewRequirements requirements) {
        previewInputState = new WorkflowPreviewInputState {
            InputJson = testInputJson,
            ProjectId = WorkflowPreviewInputSupport.TryReadJsonString(testInputJson, "$.projectId") ??
                        WorkflowPreviewInputSupport.TryReadJsonString(testInputJson, "$.project.id") ??
                        string.Empty,
            ParentNodeId = WorkflowPreviewInputSupport.TryReadJsonString(testInputJson, "$.nodeId") ??
                           WorkflowPreviewInputSupport.TryReadJsonString(testInputJson, "$.runContext.workflowNodeId") ??
                           string.Empty,
            Requirements = requirements
        };
        previewInputErrorMessage = string.Empty;
        previewProjectOptions = [];
        isPreviewInputDialogOpen = true;
        await LoadPreviewProjectOptionsAsync();
    }

    private async Task LoadPreviewProjectOptionsAsync() {
        var state = previewInputState;
        var owner = CaptureOwner();
        try {
            var projects = await ProjectStructureGateway.ListProjectsAsync(selectionReadCancellation.Token);
            if (!Owns(owner) || !isPreviewInputDialogOpen || !ReferenceEquals(state, previewInputState)) {
                return;
            }

            previewProjectOptions = projects.ToImmutableArray();
            if (string.IsNullOrWhiteSpace(previewInputState.ProjectId) &&
                previewProjectOptions.Count == 1) {
                previewInputState.ProjectId = previewProjectOptions[0].Id.ToString("D");
            }
        } catch (Exception exception) when (exception is InvalidOperationException or NotSupportedException) {
            if (Owns(owner) && isPreviewInputDialogOpen && ReferenceEquals(state, previewInputState)) {
                previewInputState.ProjectLoadError = FormatWorkflowException(exception);
            }
        }
    }

    private async Task StartSelectedWorkflowPreviewFromDialogAsync() {
        if (selectedDefinition is null) {
            return;
        }

        if (!WorkflowPreviewInputSupport.TryBuildInputJson(previewInputState, out var inputJson, out var inputError)) {
            previewInputErrorMessage = inputError;
            NotificationService.Error("Preview input needs attention", inputError);
            return;
        }

        testInputJson = inputJson;
        var simulationPlan = WorkflowPreviewInputSupport.BuildSimulationPlan(previewInputState);
        isPreviewInputDialogOpen = false;
        await RunSelectedWorkflowCoreAsync(inputJson, draftDefinition: null, simulationPlan);
    }

    private void ClosePreviewInputDialog() {
        isPreviewInputDialogOpen = false;
        previewInputErrorMessage = string.Empty;
    }

    private void HandlePreviewProjectChanged(ChangeEventArgs args) {
        previewInputState.ProjectId = args.Value?.ToString() ?? string.Empty;
    }

    private bool IsPreviewSimulationEnabled(WorkflowPreviewSimulationRequirement requirement)
        => previewInputState.SimulatedNodeIds.Contains(requirement.NodeId.Value);

    private void HandlePreviewSimulationChanged(
        WorkflowPreviewSimulationRequirement requirement,
        ChangeEventArgs args) {
        var enabled = args.Value is bool value
            ? value
            : bool.TryParse(args.Value?.ToString(), out var parsed) && parsed;
        if (enabled) {
            previewInputState.SimulatedNodeIds.Add(requirement.NodeId.Value);
            return;
        }

        previewInputState.SimulatedNodeIds.Remove(requirement.NodeId.Value);
    }

    private static string BuildPreviewSimulationTestId(WorkflowPreviewSimulationRequirement requirement)
        => $"workflows-preview-simulate-{requirement.NodeId.Value}";

    private async Task RunSelectedWorkflowCoreAsync(
        string inputJson,
        WorkflowDefinition? draftDefinition,
        WorkflowPreviewSimulationPlan simulationPlan) {
        if (disposed || selectedDefinition is not { } definition || isRunningTest) {
            return;
        }

        var owner = CaptureOwner();
        var operation = ++effectSequence;
        activeTestOperation = operation;
        isRunningTest = true;
        errorMessage = string.Empty;
        var request = new WorkflowTestRunRequest(definition.Id, definition.VersionId,
            DraftDefinition: draftDefinition is null ? null : FreezeDefinition(draftDefinition),
            InputJson: inputJson, RequestedBackend: WorkflowRuntimeBackendKind.InProcess, ValidateOnly: false) {
            PreviewSimulationPlan = simulationPlan with { Steps = simulationPlan.Steps.ToImmutableArray() }
        };
        try {
            var result = await TestRunner.RunAsync(request);
            if (!Owns(owner) || activeTestOperation != operation) {
                return;
            }

            testResult = result;
            if (!result.Succeeded) {
                errorMessage = WorkflowFailureDisplayFormatter.ToUserMessage(result.ErrorMessage);
                NotificationService.Error("Workflow test failed", errorMessage);
            } else {
                NotificationService.Success("Workflow test completed", FormatWorkflowMessage(result.Run?.Summary ?? "Workflow run completed."));
            }

            if (result.Run is { } run && run.WorkflowId != definition.Id) {
                errorMessage = "The test returned a run for a different workflow.";
                return;
            }

            analyticsRefreshVersion++;
            await LoadRunsPageAsync(definition.Id, pageIndex: 0, preferredRunId: result.Run?.RunId,
                expectedDefinitionGeneration: selectedDefinitionGeneration);
            if (Owns(owner) && activeTestOperation == operation && result.Run is { } completed) {
                await OpenRunDetailDialogAsync(selectedRun ?? completed);
            }
        } catch (Exception exception) {
            if (Owns(owner) && activeTestOperation == operation) {
                errorMessage = FormatWorkflowException(exception);
                NotificationService.Error("Workflow test failed", errorMessage);
            }
        } finally {
            if (activeTestOperation == operation) {
                activeTestOperation = null;
                isRunningTest = false;
            }
        }
    }

    private async Task SelectRunAsync(
        WorkflowRunId runId,
        bool resetEventPage = true,
        WorkflowId? expectedDefinitionId = null,
        long? expectedDefinitionGeneration = null,
        int? requestedEventPage = null) {
        var definitionId = expectedDefinitionId ?? CurrentDefinitionId;
        var definitionGeneration = expectedDefinitionGeneration ?? selectedDefinitionGeneration;
        if (!IsCurrentDefinitionSelection(definitionId, definitionGeneration)) {
            return;
        }

        RenewRead(ref runReadCancellation);
        var cancellation = runReadCancellation.Token;
        if (selectedRun?.RunId != runId) {
            ClearSelectedRunState();
        }

        presentationRevision++;
        runUnavailable = false;
        var runGeneration = ++selectedRunGeneration;
        selectedRunRequestId = runId;
        isRunSelectionLoading = true;
        StateHasChanged();

        try {
            var run = await RuntimeManager.GetRunAsync(runId, cancellation);
            if (!IsCurrentRunSelection(definitionId, definitionGeneration, runId, runGeneration)) {
                return;
            }

            if (run is null || run.RunId != runId || definitionId.HasValue && run.WorkflowId != definitionId.Value) {
                ClearSelectedRunState();
                runUnavailable = true;
                errorMessage = $"Workflow run '{runId}' was not found for the selected workflow.";
                return;
            }

            var eventPageIndex = requestedEventPage ?? (resetEventPage ? 0 : historyEventPageIndex);
            var eventsTask = RunStore.ListEventPageAsync(new WorkflowEventPageRequest(
                runId,
                eventPageIndex,
                HistoryEventPageSize), cancellation);
            var artifactsTask = RunStore.ListArtifactsAsync(runId, cancellation);
            var pendingRequestsTask = RunStore.ListPendingExternalRequestsAsync(runId, cancellation);
            await Task.WhenAll(eventsTask, artifactsTask, pendingRequestsTask);

            if (!IsCurrentRunSelection(definitionId, definitionGeneration, runId, runGeneration)) {
                return;
            }

            var eventPage = await eventsTask;
            selectedRun = run;
            runEvents = eventPage.Items.Where(item => item.RunId == runId).ToImmutableArray();
            historyEventPageIndex = eventPage.PageIndex;
            historyEventTotalCount = eventPage.TotalCount;
            artifacts = (await artifactsTask).Where(item => item.RunId == runId).ToImmutableArray();
            pendingRequests = (await pendingRequestsTask).Where(item => item.RunId == runId).ToImmutableArray();
            presentationRevision++;
        } catch (Exception exception) {
            if (IsCurrentRunSelection(definitionId, definitionGeneration, runId, runGeneration)) {
                if (selectedRun?.RunId != runId) {
                    ClearSelectedRunState();
                    runUnavailable = true;
                }

                errorMessage = FormatWorkflowException(exception);
                NotificationService.Error("Workflow run failed", errorMessage);
            }
        } finally {
            if (IsCurrentRunSelection(definitionId, definitionGeneration, runId, runGeneration)) {
                isRunSelectionLoading = false;
                StateHasChanged();
            }
        }
    }

    private async Task LoadRunsPageAsync(
        WorkflowId? workflowId,
        int pageIndex,
        WorkflowRunId? preferredRunId = null,
        long? expectedDefinitionGeneration = null) {
        var definitionGeneration = expectedDefinitionGeneration ?? selectedDefinitionGeneration;
        var pageGeneration = ++runPageGeneration;
        if (!IsCurrentDefinitionSelection(workflowId, definitionGeneration)) {
            return;
        }

        selectedRunGeneration++;
        selectedRunRequestId = null;
        isRunSelectionLoading = false;
        isRunsPageLoading = true;
        StateHasChanged();

        try {
            var runPage = await RunStore.ListRunPageAsync(new WorkflowRunPageRequest(
                workflowId,
                null,
                null,
                string.Empty,
                pageIndex,
                HistoryRunPageSize), selectionReadCancellation.Token);

            if (!IsCurrentRunsPage(workflowId, definitionGeneration, pageGeneration)) {
                return;
            }

            runs = runPage.Items.Where(run => !workflowId.HasValue || run.WorkflowId == workflowId).ToImmutableArray();
            historyRunPageIndex = runPage.PageIndex;
            historyRunTotalCount = runPage.TotalCount;
            historyLoaded = true;

            WorkflowRunId? retainedRunId = selectedRun is not null && selectedRun.WorkflowId == workflowId
                ? selectedRun.RunId
                : null;
            var runId = preferredRunId ??
                        retainedRunId ??
                        runs.FirstOrDefault()?.RunId;
            if (runId.HasValue) {
                await SelectRunAsync(
                    runId.Value,
                    expectedDefinitionId: workflowId,
                    expectedDefinitionGeneration: definitionGeneration);
                return;
            }

            ClearSelectedRunState();
        } catch (Exception exception) {
            if (IsCurrentRunsPage(workflowId, definitionGeneration, pageGeneration)) {
                errorMessage = FormatWorkflowException(exception);
                NotificationService.Error("Workflow history failed", errorMessage);
            }
        } finally {
            if (IsCurrentRunsPage(workflowId, definitionGeneration, pageGeneration)) {
                isRunsPageLoading = false;
                StateHasChanged();
            }
        }
    }

    private async Task ChangeRunPageAsync(int delta) {
        var nextPage = Math.Clamp(historyRunPageIndex + delta, 0, Math.Max(0, HistoryRunTotalPages - 1));
        if (nextPage == historyRunPageIndex) {
            return;
        }

        await LoadRunsPageAsync(CurrentDefinitionId, nextPage);
    }

    private async Task ChangeEventPageAsync(int delta) {
        if (selectedRun is null) {
            return;
        }

        var nextPage = Math.Clamp(historyEventPageIndex + delta, 0, Math.Max(0, HistoryEventTotalPages - 1));
        if (nextPage == historyEventPageIndex) {
            return;
        }

        await SelectRunAsync(selectedRun.RunId, resetEventPage: false, requestedEventPage: nextPage);
    }

    private async Task OpenRunDetailDialogAsync(WorkflowRunSnapshot run) {
        var owner = CaptureOwner();
        var generation = ++overlayGeneration;
        RenewRead(ref overlayReadCancellation);
        var cancellation = overlayReadCancellation.Token;
        runDetail = run;
        runDetailEvents = [];
        runDetailArtifacts = [];
        try {
            var eventsTask = RuntimeManager.ListEventsAsync(run.RunId, cancellation);
            var artifactsTask = RunStore.ListArtifactsAsync(run.RunId, cancellation);
            await Task.WhenAll(eventsTask, artifactsTask);
            if (!Owns(owner) || generation != overlayGeneration || runDetail?.RunId != run.RunId) {
                return;
            }

            runDetailEvents = (await eventsTask).Where(item => item.RunId == run.RunId).ToImmutableArray();
            runDetailArtifacts = (await artifactsTask).Where(item => item.RunId == run.RunId).ToImmutableArray();
        } catch (Exception exception) {
            if (Owns(owner) && generation == overlayGeneration) {
                errorMessage = FormatWorkflowException(exception);
                NotificationService.Error("Workflow details failed", errorMessage);
            }
        }
    }

    private void CloseRunDetailDialog() {
        overlayGeneration++;
        if (!disposed) {
            RenewRead(ref overlayReadCancellation);
        }

        runDetail = null;
        runDetailEvents = [];
        runDetailArtifacts = [];
    }

    private void OpenEventDetailDialog(WorkflowEventRecord workflowEvent) {
        eventDetail = workflowEvent;
    }

    private void CloseEventDetailDialog() {
        eventDetail = null;
    }

    private async Task CancelSelectedRunAsync() {
        if (disposed || isBusy || selectedRun is not { } run || IsTerminalRun(run)) {
            return;
        }

        var owner = CaptureOwner();
        var runGeneration = selectedRunGeneration;
        isBusy = true;
        try {
            var cancelled = await RuntimeManager.CancelAsync(run.RunId);
            if (!Owns(owner) || runGeneration != selectedRunGeneration) {
                return;
            }

            if (cancelled.RunId != run.RunId || cancelled.WorkflowId != run.WorkflowId) {
                errorMessage = "Cancellation returned a different workflow run.";
                return;
            }

            NotificationService.Success("Workflow run cancelled", FormatWorkflowMessage(cancelled.Summary));
            await LoadRunsPageAsync(run.WorkflowId, historyRunPageIndex, run.RunId, selectedDefinitionGeneration);
        } catch (Exception exception) {
            if (Owns(owner) && runGeneration == selectedRunGeneration) {
                errorMessage = FormatWorkflowException(exception);
                NotificationService.Error("Workflow cancel failed", errorMessage);
            }
        } finally {
            if (Owns(owner)) {
                isBusy = false;
            }
        }
    }

    private async Task RespondToRequestAsync(WorkflowExternalRequestRecord request) {
        var key = (request.Id, request.Version);
        if (disposed || selectedRun?.RunId != request.RunId || !respondingRequests.Add(key)) {
            return;
        }

        var owner = CaptureOwner();
        var runGeneration = selectedRunGeneration;
        var responseJson = ResponseDraft(request);
        try {
            using var responseDocument = JsonDocument.Parse(responseJson);
            var actorContext = await ExternalResponseActorContextProvider.GetCurrentAsync();
            var result = await ExternalResponseService.SubmitAsync(new WorkflowExternalResponseCommand(
                actorContext, request.Id, request.Version, responseDocument.RootElement.Clone(),
                WorkflowExternalResponseCallerRequestFactory.CreateUiIdempotencyKey(request.Id, request.Version),
                WorkflowExternalResponseCallerRequestFactory.CreateUiCorrelationId(request.Id, request.Version)));
            if (!Owns(owner) || runGeneration != selectedRunGeneration || selectedRun?.RunId != request.RunId) {
                return;
            }

            if (!IsAcceptedExternalResponseOutcome(result.Outcome)) {
                errorMessage = FormatWorkflowMessage(result.SafeMessage);
                NotificationService.Error("Workflow response failed", errorMessage);
                return;
            }

            if (result.Run is { } run && (run.RunId != request.RunId || run.WorkflowId != owner.DefinitionId)) {
                errorMessage = "The response returned a different workflow run.";
                return;
            }

            NotificationService.Success("Workflow request answered", FormatWorkflowMessage(result.SafeMessage));
            await LoadPageAsync(preferredDefinitionId: owner.DefinitionId, preferredRunId: request.RunId);
        } catch (Exception exception) {
            if (Owns(owner) && runGeneration == selectedRunGeneration) {
                errorMessage = FormatWorkflowException(exception);
                NotificationService.Error("Workflow response failed", errorMessage);
            }
        } finally {
            respondingRequests.Remove(key);
        }
    }

    private static bool IsAcceptedExternalResponseOutcome(
        WorkflowExternalResponseServiceOutcome outcome)
        => outcome is WorkflowExternalResponseServiceOutcome.Completed or
            WorkflowExternalResponseServiceOutcome.WaitingAgain or
            WorkflowExternalResponseServiceOutcome.Denied or
            WorkflowExternalResponseServiceOutcome.Resuming;

    private async Task HandleCanvasDefinitionSavedAsync(WorkflowDefinition definition) {
        if (!disposed && (CurrentDefinitionId is null || CurrentDefinitionId == definition.Id)) {
            await LoadPageAsync(preferredDefinitionId: definition.Id, preferredRunId: selectedRun?.RunId);
        }
    }

    private async Task HandleCanvasPreviewRunCompletedAsync(WorkflowRunSnapshot run) {
        var owner = CaptureOwner();
        if (disposed || owner.DefinitionId.HasValue && run.WorkflowId != owner.DefinitionId) {
            return;
        }

        analyticsRefreshVersion++;
        if (owner.DefinitionId.HasValue) {
            await LoadRunsPageAsync(run.WorkflowId, pageIndex: 0, preferredRunId: run.RunId);
        }

        if (Owns(owner)) {
            await OpenRunDetailDialogAsync(selectedRun ?? run);
        }
    }

    private async Task HandleWorkflowTabChangedAsync(int index) {
        if (disposed || index == activeWorkflowTabIndex || !Enum.IsDefined((WorkflowTab)index)) {
            return;
        }

        activeWorkflowTabIndex = index;
        presentationRevision++;
        var generation = ++tabGeneration;
        definitionLoadTask = null;
        componentLibraryLoadTask = null;
        RenewRead(ref selectionReadCancellation);
        RenewRead(ref runReadCancellation);
        selectedDefinitionGeneration++;
        runPageGeneration++;
        selectedRunGeneration++;
        selectedRunRequestId = null;
        isDefinitionSelectionLoading = false;
        isRunsPageLoading = false;
        isRunSelectionLoading = false;
        if (WorkflowTabRequiresDefinitionDetail(index)) {
            await EnsureSelectedDefinitionLoadedAsync();
        }

        if (disposed || generation != tabGeneration) {
            return;
        }

        if (WorkflowTabRequiresComponentLibrary(index)) {
            await EnsureComponentLibraryLoadedAsync();
        }

        if (disposed || generation != tabGeneration) {
            return;
        }

        if (WorkflowTabRequiresHistory(index)) {
            await EnsureHistoryLoadedAsync();
        }
    }

    private async Task EnsureHistoryLoadedAsync() {
        if (historyLoaded) {
            return;
        }

        if (CurrentDefinitionId is null) {
            ClearHistoryState(markLoaded: true);
            return;
        }

        await LoadRunsPageAsync(CurrentDefinitionId, pageIndex: 0);
    }

    private Task EnsureComponentLibraryLoadedAsync() {
        if (disposed || componentLibraryLoaded) {
            return Task.CompletedTask;
        }

        return componentLibraryLoadTask is { IsCompleted: false } pending ? pending : componentLibraryLoadTask = LoadComponentLibraryAsync();
    }

    private async Task RefreshComponentLibraryAsync() {
        if (disposed) {
            return;
        }

        var pending = componentLibraryLoadTask;
        if (pending is not null) {
            await pending;
        }

        if (!disposed) {
            componentLibraryLoaded = false;
            componentLibraryLoadTask = LoadComponentLibraryAsync();
            await componentLibraryLoadTask;
        }
    }

    private async Task LoadComponentLibraryAsync() {
        var owner = CaptureOwner();
        var cancellation = selectionReadCancellation.Token;
        try {
            var loadedComponents = await ComponentLibrary.ListComponentsAsync(cancellation);
            var loadedProviders = await ComponentLibrary.ListProviderOptionsAsync(cancellation);
            if (!Owns(owner) || cancellation.IsCancellationRequested) {
                return;
            }

            components = loadedComponents.ToImmutableArray();
            providerOptions = loadedProviders.ToImmutableArray();
            componentLibraryLoaded = true;
        } catch (Exception exception) {
            if (Owns(owner) && !cancellation.IsCancellationRequested) {
                errorMessage = FormatWorkflowException(exception);
                NotificationService.Error("Workflow library failed", errorMessage);
            }
        } finally {
            if (Owns(owner)) {
                componentLibraryLoadTask = null;
            }
        }
    }

    private Task EnsureTemplatePackLoadedAsync() {
        templatePack ??= TemplatePackLoader.Load();
        return Task.CompletedTask;
    }

    private static bool WorkflowTabRequiresComponentLibrary(int index)
        => index == EditorTabIndex;

    private static bool WorkflowTabRequiresHistory(int index)
        => index == HistoryTabIndex;

    private static bool WorkflowTabRequiresDefinitionDetail(int index)
        => index is WorkflowsTabIndex or EditorTabIndex or HistoryTabIndex;

    private bool ShouldLoadHistory(WorkflowRunId? preferredRunId)
        => preferredRunId.HasValue ||
           historyLoaded ||
           WorkflowTabRequiresHistory(activeWorkflowTabIndex);

    private async Task EnsureSelectedDefinitionLoadedAsync() {
        if (selectedDefinitionDetailLoaded ||
            selectedDefinitionDetailUnavailable ||
            CurrentDefinitionId is not { } definitionId) {
            return;
        }

        var selectionGeneration = selectedDefinitionGeneration;
        var load = definitionLoadTask ??= LoadDefinitionAsync(definitionId, selectionGeneration);
        await load;
        if (IsCurrentDefinitionSelection(definitionId, selectionGeneration)) {
            StateHasChanged();
        }
    }

    private void SetSelectedDefinitionPlaceholder(WorkflowId definitionId) {
        InvalidateSelectionEffects();
        RenewRead(ref selectionReadCancellation);
        RenewRead(ref runReadCancellation);
        selectedDefinitionGeneration++;
        selectedDefinitionNavigation = AgentChatNavigationFence;
        runPageGeneration++;
        selectedRunGeneration++;
        selectedRunRequestId = null;
        isDefinitionSelectionLoading = false;
        isRunsPageLoading = false;
        isRunSelectionLoading = false;
        if (selectedDefinition?.Id == definitionId && selectedDefinitionDetailLoaded) {
            selectedDefinitionId = definitionId;
            return;
        }

        ClearHistoryState(markLoaded: historyLoaded);
        selectedDefinitionId = definitionId;
        selectedDefinition = null;
        validationIssues = [];
        selectedDefinitionDetailLoaded = false;
        selectedDefinitionDetailUnavailable = false;
    }

    private void ClearSelectedDefinitionState() {
        InvalidateSelectionEffects();
        RenewRead(ref selectionReadCancellation);
        RenewRead(ref runReadCancellation);
        selectedDefinitionGeneration++;
        selectedDefinitionNavigation = default;
        runPageGeneration++;
        selectedRunGeneration++;
        selectedRunRequestId = null;
        isDefinitionSelectionLoading = false;
        isRunsPageLoading = false;
        isRunSelectionLoading = false;
        selectedDefinitionId = null;
        selectedDefinition = null;
        validationIssues = [];
        selectedDefinitionDetailLoaded = false;
        selectedDefinitionDetailUnavailable = false;
    }

    private bool IsCurrentDefinitionSelection(
        WorkflowId? definitionId,
        long selectionGeneration)
        => !disposed && selectedDefinitionGeneration == selectionGeneration &&
           selectedDefinitionNavigation == AgentChatNavigationFence &&
           CurrentDefinitionId == definitionId;

    private bool IsCurrentPageLoad(
        long generation,
        AgentChatNavigationIdentity navigation)
        => !disposed && pageLoadGeneration == generation &&
           navigation == AgentChatNavigationFence;

    private bool IsCurrentRunsPage(
        WorkflowId? definitionId,
        long definitionGeneration,
        long pageGeneration)
        => runPageGeneration == pageGeneration &&
           IsCurrentDefinitionSelection(definitionId, definitionGeneration);

    private bool IsCurrentRunSelection(
        WorkflowId? definitionId,
        long definitionGeneration,
        WorkflowRunId runId,
        long runGeneration)
        => selectedRunGeneration == runGeneration &&
           selectedRunRequestId == runId &&
           IsCurrentDefinitionSelection(definitionId, definitionGeneration);

    private void ClearHistoryState(bool markLoaded) {
        runPageGeneration++;
        isRunsPageLoading = false;
        runs = [];
        historyRunPageIndex = 0;
        historyRunTotalCount = 0;
        ClearSelectedRunState();
        historyLoaded = markLoaded;
    }

    private void ClearSelectedRunState() {
        presentationRevision++;
        CloseRunDetailDialog();
        CloseEventDetailDialog();
        selectedRunGeneration++;
        selectedRunRequestId = null;
        isRunSelectionLoading = false;
        runUnavailable = false;
        responseDrafts.Clear();
        selectedRun = null;
        runEvents = [];
        artifacts = [];
        pendingRequests = [];
        historyEventPageIndex = 0;
        historyEventTotalCount = 0;
    }

    private void FailRouteIdentity(string message) {
        hasRouteIdentityFailure = true;
        errorMessage = message;
        selectedRouteProject = null;
        ClearSelectedDefinitionState();
        ClearHistoryState(markLoaded: false);
    }

    private async Task<ProjectWorkflowRouteValidation> ValidateProjectWorkflowRelationAsync(
        Guid projectId,
        WorkflowId workflowId,
        CancellationToken cancellation) {
        var projectsTask = ProjectStructureGateway.ListProjectsAsync(cancellation);
        var structureTask = ProjectStructureGateway.ReadStructureAsync(
            projectId,
            new ProjectStructureRuntimeReadRequest(
                ObjectTypes: [ProjectObjectType.WorkflowDefinition],
                IncludeMetadata: true), cancellation);
        await Task.WhenAll(projectsTask, structureTask);

        var projects = await projectsTask;
        var project = projects.FirstOrDefault(item => item.Id == projectId);
        if (project is null) {
            return ProjectWorkflowRouteValidation.Failed(
                $"Project '{projectId:D}' was not found.");
        }

        var structure = await structureTask;
        if (structure.ProjectId != projectId) {
            return ProjectWorkflowRouteValidation.Failed(
                $"Project structure returned project '{structure.ProjectId:D}' for requested project '{projectId:D}'.");
        }

        if (!structure.Nodes.Any(node => IsWorkflowNodeForDefinition(node, workflowId))) {
            return ProjectWorkflowRouteValidation.Failed(
                $"Workflow '{workflowId}' is not attached to project '{projectId:D}'.");
        }

        return ProjectWorkflowRouteValidation.Succeeded(new WorkflowAgentChatProjectSelection(
            project.Id,
            project.Name));
    }

    private static bool IsWorkflowNodeForDefinition(
        ProjectStructureRuntimeNodeSummary node,
        WorkflowId workflowId) {
        if (node.ObjectType != ProjectObjectType.WorkflowDefinition) {
            return false;
        }

        try {
            return ProjectObjectMetadataSerializer.Parse(node.MetadataJson).Workflow?.WorkflowId == workflowId;
        } catch (InvalidOperationException) {
            return false;
        }
    }

    private static bool IsTerminalRun(WorkflowRunSnapshot? run) {
        return run?.State is WorkflowRunState.Completed or WorkflowRunState.Failed or WorkflowRunState.Cancelled;
    }

    private string BuildSettingsSummary() {
        var artifactPolicy = settings.ArtifactPolicy.CaptureNodeOutputs
            ? $"captures node outputs up to {settings.ArtifactPolicy.MaxInlinePayloadCharacters:N0} characters"
            : "does not capture node outputs";
        var humanPolicy = settings.HumanInLoopPolicy.AllowHumanInputNodes
            ? $"allows human input with {settings.HumanInLoopPolicy.DefaultRequestTimeoutMinutes} minute timeout"
            : "disables human input nodes";
        return $"Default backend is {settings.DefaultRuntimePolicy.PreferredBackend}; artifact policy {artifactPolicy}; human-in-loop policy {humanPolicy}.";
    }

    private static bool WorkflowTemplateMatchesSearch(WorkflowTemplateDefinition template, string query)
        => template.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
           template.Description.Contains(query, StringComparison.OrdinalIgnoreCase) ||
           template.Key.Contains(query, StringComparison.OrdinalIgnoreCase);

    private static bool IsSameWorkflowTemplate(
        WorkflowTemplateDefinition left,
        WorkflowTemplateDefinition right)
        => string.Equals(left.Key, right.Key, StringComparison.OrdinalIgnoreCase);

    private static string BuildTemplateNodeKindSummary(WorkflowTemplateDefinition template) {
        if (template.Graph.Nodes.Count == 0) {
            return "No nodes";
        }

        return string.Join(
            ", ",
            template.Graph.Nodes
                .GroupBy(node => string.IsNullOrWhiteSpace(node.Kind) ? "Unknown" : node.Kind)
                .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
                .Select(group => $"{group.Count()} {group.Key}"));
    }

    private static WorkflowDefinition CreateTemplateWorkflowDefinition(
        WorkflowTemplatePack templatePack,
        WorkflowTemplateDefinition template,
        LlmCallComponent component,
        string name,
        WorkflowLifecycleStatus status) {
        var definition = templatePack.CreateDefinition(template, component);
        return definition with {
            Name = name,
            Description = template.Description,
            Status = status,
            RuntimePolicy = templatePack.RuntimePolicy,
            InputParameters = templatePack.CreateInputParameters(template)
        };
    }

    private static LlmCallComponent CreateTransientTemplateComponent(
        WorkflowTemplatePack templatePack,
        WorkflowTemplateDefinition template) {
        var now = DateTimeOffset.UtcNow;
        return new LlmCallComponent(
            WorkflowComponentId.New(),
            $"Preview LLM: {NormalizeTemplateDraftBaseName(template.Name)}",
            ProviderProfileId: null,
            Model: ManagedSeedProviderFallbacks.OpenAiDefaultModel,
            Modality: WorkflowModality.Text,
            ModelSettings: templatePack.CreateModelSettings(),
            Instructions: templatePack.CreateComponentInstructions(template),
            InputShape: templatePack.JsonShape,
            ResultShape: templatePack.JsonShape,
            Permissions: CreateTemplateComponentPermissions(),
            CreatedAtUtc: now,
            UpdatedAtUtc: now);
    }

    private static LlmCallComponentSaveRequest CreateTemplateComponentSaveRequest(
        WorkflowTemplatePack templatePack,
        WorkflowTemplateDefinition template,
        string draftName,
        WorkflowProviderOption? providerOption)
        => new(
            Id: null,
            Name: $"Draft LLM: {draftName}",
            ProviderProfileId: providerOption?.ProviderProfileId,
            Model: ResolveTemplateModel(providerOption),
            Modality: WorkflowModality.Text,
            ModelSettings: templatePack.CreateModelSettings(),
            Instructions: templatePack.CreateComponentInstructions(template),
            InputShape: templatePack.JsonShape,
            ResultShape: templatePack.JsonShape,
            Permissions: CreateTemplateComponentPermissions());

    private WorkflowProviderOption? ResolveTemplateProviderOption()
        => providerOptions.FirstOrDefault(provider =>
               provider.IsEnabled &&
               provider.SupportsStructuredOutput &&
               provider.ModelOptions.Contains(ManagedSeedProviderFallbacks.OpenAiDefaultModel, StringComparer.OrdinalIgnoreCase)) ??
           providerOptions.FirstOrDefault(provider => provider.IsEnabled && provider.SupportsStructuredOutput) ??
           ResolveDefaultProviderOption();

    private static string ResolveTemplateModel(WorkflowProviderOption? providerOption) {
        if (providerOption is null) {
            return ManagedSeedProviderFallbacks.OpenAiDefaultModel;
        }

        return providerOption.ModelOptions.FirstOrDefault(model =>
                   string.Equals(model, ManagedSeedProviderFallbacks.OpenAiDefaultModel, StringComparison.OrdinalIgnoreCase)) ??
               ResolveDefaultModel(providerOption);
    }

    private static AgentPermissionsPolicy CreateTemplateComponentPermissions()
        => AgentPermissionsPolicy.Default with {
            CanUseTools = false,
            CanAskOtherAgents = false,
            CanEscalateToHuman = false,
            RequiresApprovalForExternalCalls = false
        };

    private static string ResolveTemplateDraftName(
        string baseName,
        IReadOnlyList<WorkflowCatalogItem> existingDefinitions) {
        var normalizedBaseName = NormalizeTemplateDraftBaseName(baseName);
        var existingNames = existingDefinitions
            .Select(definition => definition.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!existingNames.Contains(normalizedBaseName)) {
            return normalizedBaseName;
        }

        for (var index = 1; index <= 999; index++) {
            var candidate = $"{index:00} {normalizedBaseName}";
            if (!existingNames.Contains(candidate)) {
                return candidate;
            }
        }

        throw new InvalidOperationException($"No available draft name remains for template '{normalizedBaseName}'.");
    }

    private static string NormalizeTemplateDraftBaseName(string name) {
        var trimmed = name.Trim();
        if (string.IsNullOrWhiteSpace(trimmed)) {
            throw new InvalidOperationException("Workflow template name is required before it can be added to drafts.");
        }

        return trimmed;
    }

    private static CanvasWorkbenchSurface BuildTemplatePreviewCanvasSurface(
        WorkflowDefinition definition,
        LlmCallComponent component,
        IReadOnlyList<WorkflowExecutorDescriptor> executors,
        CanvasWorkbenchUiState uiState,
        string? selectedNodeId) {
        var document = WorkflowCanvasDefinitionMapper.FromDefinition(definition, [component]);
        var surface = WorkflowCanvasDefinitionMapper.BuildSurface(
            document,
            [component],
            executors,
            secrets: [],
            validationIssues: [],
            uiState,
            selectedNodeId);
        surface.Chrome.HintText = "Read-only workflow template preview.";
        surface.Chrome.ShowQuickCreateRail = false;
        surface.Chrome.QuickCreateActions.Clear();
        surface.Chrome.GroupContextActions.Clear();
        foreach (var node in surface.Nodes) {
            node.ContextActions.Clear();
        }

        return surface;
    }

    private static CanvasWorkbenchUiState CreateTemplatePreviewCanvasUiState(string? selectedNodeId)
        => new() {
            ActiveInspectorTab = "workflow",
            Zoom = 0.48,
            PanX = 144,
            PanY = 88,
            SelectedNodeIds = string.IsNullOrWhiteSpace(selectedNodeId) ? [] : [selectedNodeId]
        };

    private static WorkflowGraph CreateStarterGraph(WorkflowComponentId componentId) {
        var start = new WorkflowNodeId("start");
        var llm = new WorkflowNodeId("llm");
        var end = new WorkflowNodeId("end");
        return new WorkflowGraph(
            start,
            [
                CreateNode(start, WorkflowNodeKind.Start, resultShape: WorkflowValueShape.Text),
                CreateNode(llm, WorkflowNodeKind.LlmCall, componentId, inputShape: WorkflowValueShape.Text, resultShape: WorkflowValueShape.Text),
                CreateNode(end, WorkflowNodeKind.End, inputShape: WorkflowValueShape.Text)
            ],
            [
                new WorkflowEdge(
                    new WorkflowEdgeId("start-to-llm"),
                    start,
                    SourcePortId: null,
                    llm,
                    TargetPortId: null,
                    WorkflowEdgeKind.Direct,
                    ConditionExpression: string.Empty),
                new WorkflowEdge(
                    new WorkflowEdgeId("llm-to-end"),
                    llm,
                    SourcePortId: null,
                    end,
                    TargetPortId: null,
                    WorkflowEdgeKind.Direct,
                    ConditionExpression: string.Empty)
            ]);
    }

    private static WorkflowNode CreateNode(
        WorkflowNodeId id,
        WorkflowNodeKind kind,
        WorkflowComponentId? componentId = null,
        WorkflowValueShape? inputShape = null,
        WorkflowValueShape? resultShape = null) {
        return new WorkflowNode(
            id,
            kind,
            id.Value,
            [],
            new WorkflowNodeSettings(
                componentId,
                AgentId: null,
                SubworkflowId: null,
                ExternalRequestKind: null,
                Instructions: string.Empty,
                InputShape: inputShape ?? WorkflowValueShape.Text,
                ResultShape: resultShape ?? WorkflowValueShape.Text));
    }

    private static string ResolveRunTone(WorkflowRunState state) {
        return state switch {
            WorkflowRunState.Completed => "success",
            WorkflowRunState.Failed => "danger",
            WorkflowRunState.Cancelled => "neutral",
            WorkflowRunState.WaitingForInput => "warning",
            WorkflowRunState.Running => "info",
            _ => "secondary"
        };
    }

    private static string ResolveEventTone(WorkflowEventKind kind) {
        return kind switch {
            WorkflowEventKind.Completed or WorkflowEventKind.Output or WorkflowEventKind.ExecutorCompleted => "success",
            WorkflowEventKind.Error or WorkflowEventKind.ExecutorFailed => "danger",
            WorkflowEventKind.WaitingForInput => "warning",
            WorkflowEventKind.Started or WorkflowEventKind.ExecutorInvoked => "info",
            _ => "neutral"
        };
    }

    private static string FormatDate(DateTimeOffset value) {
        return value.ToLocalTime().ToString("MMM d, HH:mm");
    }

    private static string FormatFullDate(DateTimeOffset value) {
        return value.ToLocalTime().ToString("MMM d, yyyy HH:mm:ss");
    }

    private static string ResolveRunResultPayload(IReadOnlyList<WorkflowEventRecord> events) {
        foreach (var outputEvent in events.Reverse().Where(workflowEvent => workflowEvent.Kind == WorkflowEventKind.Output)) {
            var payloadJson = ResolveEventPayloadJson(outputEvent);
            if (!string.IsNullOrWhiteSpace(payloadJson)) {
                return payloadJson;
            }
        }

        foreach (var completedEvent in events.Reverse().Where(workflowEvent => workflowEvent.Kind == WorkflowEventKind.ExecutorCompleted)) {
            var payloadJson = ResolveEventPayloadJson(completedEvent);
            if (!string.IsNullOrWhiteSpace(payloadJson)) {
                return payloadJson;
            }
        }

        return string.Empty;
    }

    private static string ResolveEventPayloadJson(WorkflowEventRecord workflowEvent) {
        if (!string.IsNullOrWhiteSpace(workflowEvent.PayloadJson)) {
            return workflowEvent.PayloadJson;
        }

        return TryExtractLegacyPayloadJson(workflowEvent.Message, out var payloadJson)
            ? payloadJson
            : string.Empty;
    }

    private static bool TryExtractLegacyPayloadJson(string message, out string payloadJson) {
        payloadJson = string.Empty;
        const string marker = "PayloadJson = ";
        var start = message.LastIndexOf(marker, StringComparison.Ordinal);
        if (start < 0) {
            return false;
        }

        start += marker.Length;
        while (start < message.Length && char.IsWhiteSpace(message[start])) {
            start++;
        }

        if (start >= message.Length || message[start] is not ('{' or '[')) {
            return false;
        }

        var stack = new Stack<char>();
        var inString = false;
        var escaped = false;
        for (var index = start; index < message.Length; index++) {
            var character = message[index];
            if (inString) {
                if (escaped) {
                    escaped = false;
                } else if (character == '\\') {
                    escaped = true;
                } else if (character == '"') {
                    inString = false;
                }

                continue;
            }

            if (character == '"') {
                inString = true;
                continue;
            }

            if (character == '{') {
                stack.Push('}');
                continue;
            }

            if (character == '[') {
                stack.Push(']');
                continue;
            }

            if (character is not ('}' or ']')) {
                continue;
            }

            if (stack.Count == 0 || stack.Pop() != character) {
                return false;
            }

            if (stack.Count != 0) {
                continue;
            }

            var candidate = message[start..(index + 1)];
            try {
                using var _ = JsonDocument.Parse(candidate);
                payloadJson = candidate;
                return true;
            } catch (JsonException) {
                return false;
            }
        }

        return false;
    }

    private static string ResolveRunResultPreview(string payloadJson) {
        if (string.IsNullOrWhiteSpace(payloadJson)) {
            return string.Empty;
        }

        try {
            using var document = JsonDocument.Parse(payloadJson);
            if (TryFindResultPreviewText(document.RootElement, out var value)) {
                return TruncatePreservingWhitespace(value, 3000);
            }
        } catch (JsonException) {
            return TruncatePreservingWhitespace(payloadJson, 3000);
        }

        return TruncatePreservingWhitespace(payloadJson, 3000);
    }

    private static bool TryFindResultPreviewText(JsonElement element, out string value) {
        value = string.Empty;
        if (element.ValueKind == JsonValueKind.Object) {
            foreach (var propertyName in RunResultPreviewPropertyNames) {
                if (element.TryGetProperty(propertyName, out var property) &&
                    property.ValueKind == JsonValueKind.String &&
                    !string.IsNullOrWhiteSpace(property.GetString())) {
                    value = property.GetString()!;
                    return true;
                }
            }

            foreach (var property in element.EnumerateObject()) {
                if (TryFindResultPreviewText(property.Value, out value)) {
                    return true;
                }
            }
        }

        if (element.ValueKind == JsonValueKind.Array) {
            foreach (var item in element.EnumerateArray()) {
                if (TryFindResultPreviewText(item, out value)) {
                    return true;
                }
            }
        }

        return false;
    }

    private static string TruncatePreservingWhitespace(string value, int maxLength) {
        if (string.IsNullOrWhiteSpace(value)) {
            return string.Empty;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= maxLength
            ? trimmed
            : $"{trimmed[..Math.Max(0, maxLength - 3)]}...";
    }

    private static int CalculateTotalPages(int totalCount, int pageSize) {
        return totalCount <= 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize);
    }

    private static string FormatPageLabel(int pageIndex, int totalPages, int totalCount, string noun) {
        if (totalCount == 0) {
            return $"0 {noun}";
        }

        return $"Page {pageIndex + 1} of {totalPages} - {totalCount:N0} {noun}";
    }

    private static string Truncate(string value, int maxLength) {
        if (string.IsNullOrWhiteSpace(value)) {
            return "No message.";
        }

        var normalized = string.Join(" ", value.Split(['\r', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries)).Trim();
        return normalized.Length <= maxLength
            ? normalized
            : $"{normalized[..Math.Max(0, maxLength - 3)]}...";
    }

    private static string FormatWorkflowMessage(string message)
        => WorkflowFailureDisplayFormatter.ToUserMessage(message);

    private static string ResolveRunDisplaySummary(WorkflowRunSnapshot run)
        => run.State == WorkflowRunState.Failed
            ? WorkflowFailureDisplayFormatter.ToUserMessage(run.Summary)
            : run.Summary;

    private static string ResolveEventDisplayMessage(WorkflowEventRecord workflowEvent)
        => WorkflowFailureDisplayFormatter.ToUserMessage(workflowEvent);

    private static bool HasTechnicalEventMessage(WorkflowEventRecord workflowEvent) {
        if (WorkflowFailureDisplayFormatter.TryResolveDiagnosticTechnicalDetail(workflowEvent, out var technicalDetail)) {
            return !string.Equals(
                ResolveEventDisplayMessage(workflowEvent),
                technicalDetail,
                StringComparison.Ordinal);
        }

        return workflowEvent.Kind is WorkflowEventKind.Error or WorkflowEventKind.ExecutorFailed &&
               !string.Equals(
                   ResolveEventDisplayMessage(workflowEvent),
                   workflowEvent.Message,
                   StringComparison.Ordinal);
    }

    private static string ResolveEventTechnicalMessage(WorkflowEventRecord workflowEvent)
        => WorkflowFailureDisplayFormatter.TryResolveDiagnosticTechnicalDetail(workflowEvent, out var technicalDetail)
            ? technicalDetail
            : workflowEvent.Message;

    private sealed record WorkflowRouteRequest(
        Guid? ProjectId,
        WorkflowId? WorkflowId,
        WorkflowRunId? RunId,
        bool HasExplicitSelection,
        string ValidationError) {
        public static WorkflowRouteRequest Create(
            Guid? projectId,
            Guid? workflowId,
            Guid? runId) {
            var hasExplicitSelection = projectId.HasValue || workflowId.HasValue || runId.HasValue;
            if (!hasExplicitSelection) {
                return new WorkflowRouteRequest(null, null, null, false, string.Empty);
            }

            if (projectId == Guid.Empty) {
                return Invalid("The projectId query value cannot be empty.");
            }

            if (workflowId == Guid.Empty) {
                return Invalid("The workflowId query value cannot be empty.");
            }

            if (runId == Guid.Empty) {
                return Invalid("The runId query value cannot be empty.");
            }

            if (!workflowId.HasValue && projectId.HasValue) {
                return Invalid("A projectId workflow route also requires workflowId.");
            }

            if (!workflowId.HasValue && runId.HasValue) {
                return Invalid("A runId workflow route also requires workflowId.");
            }

            return new WorkflowRouteRequest(
                projectId,
                workflowId.HasValue ? new WorkflowId(workflowId.Value) : null,
                runId.HasValue ? new WorkflowRunId(runId.Value) : null,
                true,
                string.Empty);

            static WorkflowRouteRequest Invalid(string message)
                => new(null, null, null, true, message);
        }
    }

    private sealed record ProjectWorkflowRouteValidation(
        WorkflowAgentChatProjectSelection? Project,
        string ErrorMessage) {
        public static ProjectWorkflowRouteValidation Succeeded(WorkflowAgentChatProjectSelection project)
            => new(project, string.Empty);

        public static ProjectWorkflowRouteValidation Failed(string errorMessage)
            => new(null, errorMessage);
    }

    private string FormatWorkflowException(Exception exception) {
        Logger.LogWarning("Workflow operation failed for workflow {WorkflowId}, run {RunId}, page generation {PageGeneration}, and failure type {FailureType}.",
            CurrentDefinitionId?.Value, selectedRun?.RunId.Value, pageLoadGeneration, exception.GetBaseException().GetType().Name);
        return exception is JsonException ? "Enter valid JSON and try again."
            : "The workflow operation could not be completed. Refresh the workspace and try again.";
    }

    private static string FormatShortId(Guid value) {
        return value.ToString("N")[..8];
    }

    private WorkflowProviderOption? ResolveDefaultProviderOption() {
        return providerOptions.FirstOrDefault(option => option.IsEnabled);
    }

    private static string ResolveDefaultModel(WorkflowProviderOption? providerOption) {
        if (providerOption is null) {
            return ManagedSeedProviderFallbacks.OpenAiDefaultModel;
        }

        if (!string.IsNullOrWhiteSpace(providerOption.DefaultModel)) {
            return providerOption.DefaultModel;
        }

        return providerOption.ModelOptions.FirstOrDefault(model => !string.IsNullOrWhiteSpace(model)) ??
               ManagedSeedProviderFallbacks.OpenAiDefaultModel;
    }

    private void OpenAgents() {
        Navigation.NavigateTo("/agents");
    }

    private async Task OpenWorkflowCuratorAsync() {
        if (isOpeningWorkflowCurator ||
            workflowCuratorAgent is null ||
            !WorkflowCuratorAgentIdentity.Matches(workflowCuratorAgent) ||
            AgentChatAccessState != AgentChatContextAccessState.Ready) {
            return;
        }

        var owner = CaptureOwner();
        var curatorId = workflowCuratorAgent.Id;
        isOpeningWorkflowCurator = true;
        try {
            await AgentChatLauncher.StartNewChatAsync(curatorId);
            if (!Owns(owner)) {
                return;
            }

            NotificationService.Success("Workflow Curator ready", "Opened a new managed workflow chat.");
        } catch (Exception exception) {
            if (Owns(owner)) {
                NotificationService.Error("Unable to open Workflow Curator", FormatWorkflowException(exception));
            }
        } finally {
            isOpeningWorkflowCurator = false;
        }
    }

    private async Task<(AgentDefinition? Agent, string? ErrorMessage)> TryResolveWorkflowCuratorAgentAsync() {
        try {
            var agents = await AgentWorkspaceService.ListAgentsAsync(includeTemplates: false, cancellationToken: lifetimeCancellation.Token);
            var agent = agents.SingleOrDefault(WorkflowCuratorAgentIdentity.Matches);
            return agent is null
                ? (null, $"The managed agent '{WorkflowCuratorAgentIdentity.AgentId:D}' is not available.")
                : (agent, null);
        } catch (Exception exception) {
            return (null, FormatWorkflowException(exception));
        }
    }

    private async Task EnsureWorkflowCuratorAgentAsync() {
        if (workflowCuratorAgent is not null) {
            return;
        }

        var resolutionTask = workflowCuratorResolutionTask;
        if (resolutionTask is null) {
            resolutionTask = ResolveWorkflowCuratorAgentAsync();
            workflowCuratorResolutionTask = resolutionTask;
        }

        try {
            await resolutionTask;
        } finally {
            if (ReferenceEquals(workflowCuratorResolutionTask, resolutionTask)) {
                workflowCuratorResolutionTask = null;
            }
        }
    }

    private async Task ResolveWorkflowCuratorAgentAsync() {
        var resolution = await TryResolveWorkflowCuratorAgentAsync();
        if (disposed) {
            return;
        }

        workflowCuratorAgent = resolution.Agent;
        if (resolution.ErrorMessage is { } curatorError) {
            NotificationService.Warning("Workflow Curator unavailable", curatorError);
        }
    }

    private Task HandleAgentChatExecutionCompletedAsync(AgentChatExecutionCompleted notification) {
        ArgumentNullException.ThrowIfNull(notification);
        return RefreshAsync();
    }

    private readonly record struct PageOwner(long PageGeneration, long TargetGeneration,
        WorkflowId? DefinitionId, AgentChatNavigationIdentity Navigation);

    private PageOwner CaptureOwner() => new(pageLoadGeneration, targetGeneration, CurrentDefinitionId, AgentChatNavigationFence);

    private bool Owns(PageOwner owner) => !disposed && owner.PageGeneration == pageLoadGeneration
        && owner.TargetGeneration == targetGeneration && owner.DefinitionId == CurrentDefinitionId
        && owner.Navigation == AgentChatNavigationFence;

    private void InvalidateSelectionEffects() {
        targetGeneration++;
        presentationRevision++;
        activeTestOperation = null;
        isRunningTest = false;
        isPreparingTest = false;
        definitionLoadTask = null;
        testResult = null;
        testInputJson = WorkflowPreviewInputSupport.DefaultInputJson;
        isBusy = false;
        componentLibraryLoadTask = null;
        CloseRunDetailDialog();
        CloseEventDetailDialog();
        ClosePreviewInputDialog();
        CloseTemplatePreviewDialog();
        CloseTemplateCatalogueDialog();
    }

    private static void RenewRead(ref CancellationTokenSource source) {
        var previous = source;
        source = new();
        previous.Cancel();
        previous.Dispose();
    }

    private static WorkflowDefinition FreezeDefinition(WorkflowDefinition definition) => definition with {
        Graph = definition.Graph with {
            Nodes = definition.Graph.Nodes.Select(node => node with { Ports = node.Ports.ToImmutableArray() }).ToImmutableArray(),
            Edges = definition.Graph.Edges.ToImmutableArray()
        },
        InputParameters = definition.InputParameters.ToImmutableArray()
    };

    public void Dispose() {
        if (disposed) {
            return;
        }

        disposed = true;
        InvalidateSelectionEffects();
        foreach (var cancellation in new[] { pageReadCancellation, selectionReadCancellation, runReadCancellation,
            overlayReadCancellation, lifetimeCancellation }) {
            cancellation.Cancel();
            cancellation.Dispose();
        }

        responseDrafts.Clear();
        GC.SuppressFinalize(this);
    }
}
