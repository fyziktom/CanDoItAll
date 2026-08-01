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

namespace CanDoItAll.Modules.AgentFramework.Pages;

public partial class WorkflowsPage
{
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
    private string pendingResponseJson = "{\"approved\":true}";
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

    private Task HandleCanvasSelectedNodeChangedAsync(WorkflowAgentChatNodeSelection? selection)
    {
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

    private string PublishButtonTitle
        => validationIssues.Count == 0
            ? "Publish this workflow for production runs."
            : "Resolve validation issues before publishing.";

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

    private string ArtifactCountText => historyLoaded ? artifacts.Count.ToString() : "-";

    private IReadOnlyList<WorkflowTemplateDefinition> WorkflowTemplates => templatePack?.Workflows ?? [];

    private string WorkflowTemplateSeedText => templatePack?.Manifest.SeedVersion ?? "-";

    private IReadOnlyList<WorkflowTemplateDefinition> FilteredWorkflowTemplates
    {
        get
        {
            if (templatePack is null)
            {
                return [];
            }

            if (string.IsNullOrWhiteSpace(templateSearchText))
            {
                return templatePack.Workflows;
            }

            var query = templateSearchText.Trim();
            return templatePack.Workflows
                .Where(template => WorkflowTemplateMatchesSearch(template, query))
                .ToArray();
        }
    }

    private WorkflowTemplateDefinition? SelectedWorkflowTemplate
    {
        get
        {
            var templates = FilteredWorkflowTemplates;
            if (selectedTemplate is not null &&
                templates.Any(template => IsSameWorkflowTemplate(template, selectedTemplate)))
            {
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
        new()
        {
            Label = "Nodes",
            Value = templatePreviewDefinition?.Graph.Nodes.Count.ToString() ?? "0",
            Tone = "info"
        },
        new()
        {
            Label = "Edges",
            Value = templatePreviewDefinition?.Graph.Edges.Count.ToString() ?? "0",
            Tone = "secondary"
        },
        new()
        {
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

    private IReadOnlyList<TreeViewNode> WorkflowDefinitionTreeNodes
        => WorkflowDefinitionTreeNodeBuilder.Build(
            definitions,
            CurrentDefinitionId,
            expandedWorkflowTreeNodeIds);

    private int HistoryRunTotalPages => CalculateTotalPages(historyRunTotalCount, HistoryRunPageSize);

    private int HistoryEventTotalPages => CalculateTotalPages(historyEventTotalCount, HistoryEventPageSize);

    private bool CanGoToPreviousRunPage => historyRunPageIndex > 0;

    private bool CanGoToNextRunPage => historyRunPageIndex + 1 < HistoryRunTotalPages;

    private bool CanGoToPreviousEventPage => historyEventPageIndex > 0;

    private bool CanGoToNextEventPage => historyEventPageIndex + 1 < HistoryEventTotalPages;

    protected override async Task OnParametersSetAsync()
    {
        var navigation = AgentChatNavigationFence;
        if (hasObservedNavigation && observedNavigation == navigation)
        {
            return;
        }

        hasObservedNavigation = true;
        observedNavigation = navigation;
        await RefreshRouteAsync(WorkflowRouteRequest.Create(
            RequestedProjectId,
            RequestedWorkflowId,
            RequestedRunId), navigation);
    }

    private async Task RefreshAsync()
    {
        if (isBusy)
        {
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
        AgentChatNavigationIdentity navigation)
    {
        var generation = BeginPageLoad(clearSelection: true);
        await ExecutePageLoadAsync(
            route.WorkflowId,
            route.RunId,
            route.HasExplicitSelection ? route : null,
            generation,
            navigation);
    }

    private long BeginPageLoad(bool clearSelection)
    {
        var generation = ++pageLoadGeneration;
        isBusy = true;
        isLoading = true;
        errorMessage = string.Empty;
        hasRouteIdentityFailure = false;
        selectedRouteProject = null;
        if (clearSelection)
        {
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
        AgentChatNavigationIdentity navigation)
    {
        var pageLoadCompleted = false;
        try
        {
            await LoadPageCoreAsync(
                preferredDefinitionId,
                preferredRunId,
                requiredRoute,
                generation,
                navigation);
            pageLoadCompleted = true;
        }
        catch (Exception exception)
        {
            if (!IsCurrentPageLoad(generation, navigation))
            {
                return;
            }

            errorMessage = FormatWorkflowException(exception);
            if (requiredRoute is not null)
            {
                FailRouteIdentity(errorMessage);
            }

            NotificationService.Error("Workflow refresh failed", errorMessage);
        }
        finally
        {
            if (IsCurrentPageLoad(generation, navigation))
            {
                isLoading = false;
                isBusy = false;
            }
        }

        if (!pageLoadCompleted || !IsCurrentPageLoad(generation, navigation))
        {
            return;
        }

        StateHasChanged();
        await EnsureWorkflowCuratorAgentAsync();
    }

    private Task LoadPageAsync(
        WorkflowId? preferredDefinitionId = null,
        WorkflowRunId? preferredRunId = null)
    {
        var navigation = AgentChatNavigationFence;
        var generation = ++pageLoadGeneration;
        return LoadPageCoreAsync(
            preferredDefinitionId,
            preferredRunId,
            requiredRoute: null,
            generation,
            navigation);
    }

    private async Task LoadPageCoreAsync(
        WorkflowId? preferredDefinitionId,
        WorkflowRunId? preferredRunId,
        WorkflowRouteRequest? requiredRoute,
        long generation,
        AgentChatNavigationIdentity navigation)
    {
        analyticsRefreshVersion++;
        var settingsTask = SettingsService.GetSettingsAsync();
        var definitionsTask = CatalogService.ListDefinitionsAsync();
        await Task.WhenAll(settingsTask, definitionsTask);

        if (!IsCurrentPageLoad(generation, navigation))
        {
            return;
        }

        var loadedSettings = await settingsTask;
        var loadedDefinitions = await definitionsTask;
        WorkflowAgentChatProjectSelection? routeProject = null;
        var routeError = requiredRoute?.ValidationError ?? string.Empty;
        if (string.IsNullOrEmpty(routeError) &&
            requiredRoute?.WorkflowId is { } requiredDefinitionId &&
            loadedDefinitions.All(definition => definition.Id != requiredDefinitionId))
        {
            routeError = $"Workflow definition '{requiredDefinitionId}' was not found.";
        }

        if (string.IsNullOrEmpty(routeError) &&
            requiredRoute is { ProjectId: { } projectId, WorkflowId: { } workflowId })
        {
            var projectValidation = await ValidateProjectWorkflowRelationAsync(projectId, workflowId);
            if (!IsCurrentPageLoad(generation, navigation))
            {
                return;
            }

            routeProject = projectValidation.Project;
            routeError = projectValidation.ErrorMessage;
        }

        settings = loadedSettings;
        definitions = loadedDefinitions;
        if (!string.IsNullOrEmpty(routeError))
        {
            FailRouteIdentity(routeError);
            return;
        }

        selectedRouteProject = routeProject;

        var definitionId = requiredRoute?.WorkflowId ??
                           preferredDefinitionId ??
                           CurrentDefinitionId ??
                           definitions.FirstOrDefault()?.Id;
        if (definitionId.HasValue)
        {
            SetSelectedDefinitionPlaceholder(definitionId.Value);
        }
        else
        {
            ClearSelectedDefinitionState();
        }

        if (requiredRoute?.WorkflowId.HasValue == true)
        {
            await EnsureSelectedDefinitionLoadedAsync();
            if (!IsCurrentPageLoad(generation, navigation))
            {
                return;
            }

            if (selectedDefinition?.Id != definitionId)
            {
                FailRouteIdentity(errorMessage.Length > 0
                    ? errorMessage
                    : $"Workflow definition '{definitionId}' could not be loaded.");
                return;
            }
        }

        if (ShouldLoadHistory(preferredRunId))
        {
            var definitionGeneration = selectedDefinitionGeneration;
            await EnsureSelectedDefinitionLoadedAsync();
            await LoadRunsPageAsync(
                definitionId,
                pageIndex: 0,
                preferredRunId,
                definitionGeneration);
            if (!IsCurrentPageLoad(generation, navigation))
            {
                return;
            }

            if (requiredRoute?.RunId is { } requiredRunId && selectedRun?.RunId != requiredRunId)
            {
                FailRouteIdentity(
                    $"Workflow run '{requiredRunId}' was not found for workflow '{definitionId}'.");
                return;
            }
        }
        else
        {
            ClearHistoryState(markLoaded: false);
        }

        if (componentLibraryLoaded)
        {
            await RefreshComponentLibraryAsync();
        }
    }

    private async Task SelectDefinitionAsync(WorkflowId definitionId)
    {
        errorMessage = string.Empty;
        SetSelectedDefinitionPlaceholder(definitionId);
        var selectionGeneration = selectedDefinitionGeneration;
        isDefinitionSelectionLoading = true;
        StateHasChanged();
        try
        {
            await EnsureSelectedDefinitionLoadedAsync();
            if (!IsCurrentDefinitionSelection(definitionId, selectionGeneration))
            {
                return;
            }

            if (historyLoaded || WorkflowTabRequiresHistory(activeWorkflowTabIndex))
            {
                await LoadRunsPageAsync(
                    definitionId,
                    pageIndex: 0,
                    expectedDefinitionGeneration: selectionGeneration);
            }
            else
            {
                ClearHistoryState(markLoaded: false);
            }
        }
        finally
        {
            if (IsCurrentDefinitionSelection(definitionId, selectionGeneration))
            {
                isDefinitionSelectionLoading = false;
                StateHasChanged();
            }
        }
    }

    private async Task HandleWorkflowTreeSelectAsync(string nodeId)
    {
        if (!WorkflowDefinitionTreeNodeBuilder.TryReadDefinitionId(nodeId, out var definitionId))
        {
            return;
        }

        await SelectDefinitionAsync(definitionId);
    }

    private Task HandleWorkflowTreeToggleAsync(string nodeId)
    {
        if (!expandedWorkflowTreeNodeIds.Add(nodeId))
        {
            expandedWorkflowTreeNodeIds.Remove(nodeId);
        }

        return Task.CompletedTask;
    }

    private async Task OpenTemplateCatalogueDialogAsync()
    {
        if (isTemplateCatalogueLoading)
        {
            return;
        }

        isTemplateCatalogueDialogOpen = true;
        templateCatalogueErrorMessage = string.Empty;

        if (templatePack is not null)
        {
            SelectDefaultWorkflowTemplate();
            return;
        }

        isTemplateCatalogueLoading = true;
        try
        {
            await EnsureTemplatePackLoadedAsync();
            SelectDefaultWorkflowTemplate();
        }
        catch (Exception exception)
        {
            templateCatalogueErrorMessage = FormatWorkflowException(exception);
            NotificationService.Error("Template catalogue failed", templateCatalogueErrorMessage);
        }
        finally
        {
            isTemplateCatalogueLoading = false;
        }
    }

    private void CloseTemplateCatalogueDialog()
    {
        isTemplateCatalogueDialogOpen = false;
        templateCatalogueErrorMessage = string.Empty;
    }

    private void SelectWorkflowTemplate(WorkflowTemplateDefinition template)
    {
        selectedTemplate = template;
    }

    private void HandleWorkflowTemplateSearchChanged(ChangeEventArgs args)
    {
        templateSearchText = args.Value?.ToString() ?? string.Empty;
        SelectDefaultWorkflowTemplate();
    }

    private void SelectDefaultWorkflowTemplate()
    {
        selectedTemplate = SelectedWorkflowTemplate ?? FilteredWorkflowTemplates.FirstOrDefault();
    }

    private async Task OpenTemplatePreviewDialogAsync(WorkflowTemplateDefinition template)
    {
        selectedTemplate = template;
        templatePreviewErrorMessage = string.Empty;

        try
        {
            await EnsureTemplatePackLoadedAsync();
            if (templatePack is null)
            {
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
        }
        catch (Exception exception)
        {
            templatePreviewErrorMessage = FormatWorkflowException(exception);
            NotificationService.Error("Template preview failed", templatePreviewErrorMessage);
        }
    }

    private void CloseTemplatePreviewDialog()
    {
        isTemplatePreviewDialogOpen = false;
        templatePreviewErrorMessage = string.Empty;
        templatePreviewTemplate = null;
        templatePreviewDefinition = null;
        templatePreviewComponent = null;
        templatePreviewSelectedNodeId = "start";
        templatePreviewCanvasUiState = CreateTemplatePreviewCanvasUiState(templatePreviewSelectedNodeId);
    }

    private async Task AddSelectedTemplateToDraftsAsync()
    {
        if (isBusy ||
            templatePack is null ||
            templatePreviewTemplate is null)
        {
            return;
        }

        isBusy = true;
        templatePreviewErrorMessage = string.Empty;

        try
        {
            await EnsureComponentLibraryLoadedAsync();
            var draftName = ResolveTemplateDraftName(templatePreviewTemplate.Name, definitions);
            var providerOption = ResolveTemplateProviderOption();
            var component = await ComponentLibrary.SaveComponentAsync(CreateTemplateComponentSaveRequest(
                templatePack,
                templatePreviewTemplate,
                draftName,
                providerOption));
            var definition = CreateTemplateWorkflowDefinition(
                templatePack,
                templatePreviewTemplate,
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
                RuntimePolicy: definition.RuntimePolicy)
            {
                InputParameters = definition.InputParameters
            });

            NotificationService.Success("Template added to drafts", saved.Name);
            CloseTemplatePreviewDialog();
            CloseTemplateCatalogueDialog();
            await LoadPageAsync(preferredDefinitionId: saved.Id);
        }
        catch (Exception exception)
        {
            templatePreviewErrorMessage = FormatWorkflowException(exception);
            NotificationService.Error("Template add failed", templatePreviewErrorMessage);
        }
        finally
        {
            isBusy = false;
        }
    }

    private Task HandleTemplatePreviewCanvasSelectionChangedAsync(CanvasWorkbenchSelectionChangedEventArgs args)
    {
        templatePreviewSelectedNodeId = args.PrimaryNodeId ?? args.SelectedNodeIds.FirstOrDefault();
        return Task.CompletedTask;
    }

    private Task HandleTemplatePreviewCanvasStateChangedAsync(string stateJson)
    {
        templatePreviewCanvasUiState = CanvasWorkbenchUiState.Parse(stateJson);
        return Task.CompletedTask;
    }

    private async Task LoadDefinitionAsync(
        WorkflowId definitionId,
        long selectionGeneration)
    {
        WorkflowDefinitionDetail? detail;
        try
        {
            detail = await CatalogService.GetDefinitionAsync(definitionId);
        }
        catch (Exception exception)
        {
            if (!IsCurrentDefinitionSelection(definitionId, selectionGeneration))
            {
                return;
            }

            selectedDefinition = null;
            validationIssues = [];
            selectedDefinitionDetailLoaded = false;
            selectedDefinitionDetailUnavailable = true;
            errorMessage = FormatWorkflowException(exception);
            throw;
        }

        if (!IsCurrentDefinitionSelection(definitionId, selectionGeneration))
        {
            return;
        }

        if (detail is null)
        {
            selectedDefinitionId = definitionId;
            selectedDefinition = null;
            validationIssues = [];
            selectedDefinitionDetailLoaded = false;
            selectedDefinitionDetailUnavailable = true;
            errorMessage = $"Workflow definition '{definitionId}' was not found.";
            return;
        }

        selectedDefinition = detail.Definition;
        validationIssues = detail.Validation.Issues;
        selectedDefinitionId = detail.Definition.Id;
        selectedDefinitionDetailLoaded = true;
        selectedDefinitionDetailUnavailable = false;
    }

    private async Task CreateStarterWorkflowAsync()
    {
        if (isBusy)
        {
            return;
        }

        isBusy = true;
        errorMessage = string.Empty;

        try
        {
            await EnsureComponentLibraryLoadedAsync();
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

            NotificationService.Success("Workflow created", "Starter workflow and LLM component were created.");
            await LoadPageAsync(preferredDefinitionId: definition.Id);
        }
        catch (Exception exception)
        {
            errorMessage = FormatWorkflowException(exception);
            NotificationService.Error("Workflow create failed", errorMessage);
        }
        finally
        {
            isBusy = false;
        }
    }

    private async Task PublishSelectedDefinitionAsync()
    {
        if (isBusy || selectedDefinition is not { Status: WorkflowLifecycleStatus.Draft } draft)
        {
            return;
        }

        isBusy = true;
        errorMessage = string.Empty;

        try
        {
            var published = await CatalogService.ChangeDefinitionStatusAsync(
                new WorkflowDefinitionStatusChangeRequest(
                    draft.Id,
                    draft.VersionId,
                    WorkflowLifecycleStatus.Active));

            NotificationService.Success(
                "Workflow published",
                $"{published.Name} is ready for production runs.");
            ClearSelectedDefinitionState();
            await LoadPageAsync(preferredDefinitionId: published.Id);
            await EnsureSelectedDefinitionLoadedAsync();
        }
        catch (Exception exception)
        {
            errorMessage = FormatWorkflowException(exception);
            NotificationService.Error("Workflow publish failed", errorMessage);
        }
        finally
        {
            isBusy = false;
        }
    }

    private async Task RunSelectedWorkflowAsync()
    {
        if (isRunningTest)
        {
            return;
        }

        await EnsureSelectedDefinitionLoadedAsync();
        if (selectedDefinition is null)
        {
            return;
        }

        var requirements = WorkflowPreviewInputSupport.Analyze(selectedDefinition, ExecutorCatalog.ListExecutors());
        if (requirements.NeedsPreviewDialog)
        {
            await OpenSelectedWorkflowPreviewInputDialogAsync(requirements);
            return;
        }

        await RunSelectedWorkflowCoreAsync(testInputJson, draftDefinition: null, WorkflowPreviewSimulationPlan.Empty);
    }

    private async Task OpenSelectedWorkflowPreviewInputDialogAsync(WorkflowPreviewRequirements requirements)
    {
        previewInputState = new WorkflowPreviewInputState
        {
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

    private async Task LoadPreviewProjectOptionsAsync()
    {
        try
        {
            previewProjectOptions = await ProjectStructureGateway.ListProjectsAsync();
            if (string.IsNullOrWhiteSpace(previewInputState.ProjectId) &&
                previewProjectOptions.Count == 1)
            {
                previewInputState.ProjectId = previewProjectOptions[0].Id.ToString("D");
            }
        }
        catch (Exception exception) when (exception is InvalidOperationException or NotSupportedException)
        {
            previewInputState.ProjectLoadError = $"Project list unavailable: {exception.Message}";
        }
    }

    private async Task StartSelectedWorkflowPreviewFromDialogAsync()
    {
        if (selectedDefinition is null)
        {
            return;
        }

        if (!WorkflowPreviewInputSupport.TryBuildInputJson(previewInputState, out var inputJson, out var inputError))
        {
            previewInputErrorMessage = inputError;
            NotificationService.Error("Preview input needs attention", inputError);
            return;
        }

        testInputJson = inputJson;
        var simulationPlan = WorkflowPreviewInputSupport.BuildSimulationPlan(previewInputState);
        isPreviewInputDialogOpen = false;
        await RunSelectedWorkflowCoreAsync(inputJson, draftDefinition: null, simulationPlan);
    }

    private void ClosePreviewInputDialog()
    {
        isPreviewInputDialogOpen = false;
        previewInputErrorMessage = string.Empty;
    }

    private void HandlePreviewProjectChanged(ChangeEventArgs args)
    {
        previewInputState.ProjectId = args.Value?.ToString() ?? string.Empty;
    }

    private bool IsPreviewSimulationEnabled(WorkflowPreviewSimulationRequirement requirement)
        => previewInputState.SimulatedNodeIds.Contains(requirement.NodeId.Value);

    private void HandlePreviewSimulationChanged(
        WorkflowPreviewSimulationRequirement requirement,
        ChangeEventArgs args)
    {
        var enabled = args.Value is bool value
            ? value
            : bool.TryParse(args.Value?.ToString(), out var parsed) && parsed;
        if (enabled)
        {
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
        WorkflowPreviewSimulationPlan simulationPlan)
    {
        if (selectedDefinition is null || isRunningTest)
        {
            return;
        }

        isRunningTest = true;
        errorMessage = string.Empty;

        try
        {
            testResult = await TestRunner.RunAsync(new WorkflowTestRunRequest(
                selectedDefinition.Id,
                selectedDefinition.VersionId,
                DraftDefinition: draftDefinition,
                InputJson: inputJson,
                RequestedBackend: WorkflowRuntimeBackendKind.InProcess,
                ValidateOnly: false)
            {
                PreviewSimulationPlan = simulationPlan
            });
            if (!testResult.Succeeded)
            {
                errorMessage = WorkflowFailureDisplayFormatter.ToUserMessage(testResult.ErrorMessage);
                NotificationService.Error("Workflow test failed", errorMessage);
            }
            else
            {
                NotificationService.Success("Workflow test completed", testResult.Run?.Summary ?? "Workflow run completed.");
            }

            if (testResult.Run is not null)
            {
                analyticsRefreshVersion++;
            }

            await LoadRunsPageAsync(
                selectedDefinition.Id,
                pageIndex: 0,
                preferredRunId: testResult.Run?.RunId);
            if (testResult.Run is not null)
            {
                await OpenRunDetailDialogAsync(selectedRun ?? testResult.Run);
            }
        }
        catch (Exception exception)
        {
            errorMessage = FormatWorkflowException(exception);
            NotificationService.Error("Workflow test failed", errorMessage);
        }
        finally
        {
            isRunningTest = false;
        }
    }

    private async Task SelectRunAsync(
        WorkflowRunId runId,
        bool resetEventPage = true,
        WorkflowId? expectedDefinitionId = null,
        long? expectedDefinitionGeneration = null)
    {
        var definitionId = expectedDefinitionId ?? CurrentDefinitionId;
        var definitionGeneration = expectedDefinitionGeneration ?? selectedDefinitionGeneration;
        var runGeneration = ++selectedRunGeneration;
        selectedRunRequestId = runId;
        isRunSelectionLoading = true;
        StateHasChanged();

        try
        {
            var run = await RuntimeManager.GetRunAsync(runId);
            if (!IsCurrentRunSelection(definitionId, definitionGeneration, runId, runGeneration))
            {
                return;
            }

            if (run is null || definitionId.HasValue && run.WorkflowId != definitionId.Value)
            {
                ClearSelectedRunState();
                return;
            }

            var eventPageIndex = resetEventPage ? 0 : historyEventPageIndex;
            var eventsTask = RunStore.ListEventPageAsync(new WorkflowEventPageRequest(
                runId,
                eventPageIndex,
                HistoryEventPageSize));
            var artifactsTask = RunStore.ListArtifactsAsync(runId);
            var pendingRequestsTask = RunStore.ListPendingExternalRequestsAsync(runId);
            await Task.WhenAll(eventsTask, artifactsTask, pendingRequestsTask);

            if (!IsCurrentRunSelection(definitionId, definitionGeneration, runId, runGeneration))
            {
                return;
            }

            var eventPage = await eventsTask;
            selectedRun = run;
            runEvents = eventPage.Items;
            historyEventPageIndex = eventPage.PageIndex;
            historyEventTotalCount = eventPage.TotalCount;
            artifacts = await artifactsTask;
            pendingRequests = await pendingRequestsTask;
        }
        catch
        {
            if (IsCurrentRunSelection(definitionId, definitionGeneration, runId, runGeneration))
            {
                throw;
            }
        }
        finally
        {
            if (IsCurrentRunSelection(definitionId, definitionGeneration, runId, runGeneration))
            {
                isRunSelectionLoading = false;
                StateHasChanged();
            }
        }
    }

    private async Task LoadRunsPageAsync(
        WorkflowId? workflowId,
        int pageIndex,
        WorkflowRunId? preferredRunId = null,
        long? expectedDefinitionGeneration = null)
    {
        var definitionGeneration = expectedDefinitionGeneration ?? selectedDefinitionGeneration;
        var pageGeneration = ++runPageGeneration;
        if (!IsCurrentDefinitionSelection(workflowId, definitionGeneration))
        {
            return;
        }

        selectedRunGeneration++;
        selectedRunRequestId = null;
        isRunSelectionLoading = false;
        isRunsPageLoading = true;
        StateHasChanged();

        try
        {
            var runPage = await RunStore.ListRunPageAsync(new WorkflowRunPageRequest(
                workflowId,
                null,
                null,
                string.Empty,
                pageIndex,
                HistoryRunPageSize));

            if (!IsCurrentRunsPage(workflowId, definitionGeneration, pageGeneration))
            {
                return;
            }

            runs = runPage.Items;
            historyRunPageIndex = runPage.PageIndex;
            historyRunTotalCount = runPage.TotalCount;
            historyLoaded = true;

            WorkflowRunId? retainedRunId = selectedRun is not null && selectedRun.WorkflowId == workflowId
                ? selectedRun.RunId
                : null;
            var runId = preferredRunId ??
                        retainedRunId ??
                        runs.FirstOrDefault()?.RunId;
            if (runId.HasValue)
            {
                await SelectRunAsync(
                    runId.Value,
                    expectedDefinitionId: workflowId,
                    expectedDefinitionGeneration: definitionGeneration);
                return;
            }

            ClearSelectedRunState();
        }
        catch
        {
            if (IsCurrentRunsPage(workflowId, definitionGeneration, pageGeneration))
            {
                throw;
            }
        }
        finally
        {
            if (IsCurrentRunsPage(workflowId, definitionGeneration, pageGeneration))
            {
                isRunsPageLoading = false;
                StateHasChanged();
            }
        }
    }

    private async Task ChangeRunPageAsync(int delta)
    {
        var nextPage = Math.Clamp(historyRunPageIndex + delta, 0, Math.Max(0, HistoryRunTotalPages - 1));
        if (nextPage == historyRunPageIndex)
        {
            return;
        }

        await LoadRunsPageAsync(CurrentDefinitionId, nextPage);
    }

    private async Task ChangeEventPageAsync(int delta)
    {
        if (selectedRun is null)
        {
            return;
        }

        var nextPage = Math.Clamp(historyEventPageIndex + delta, 0, Math.Max(0, HistoryEventTotalPages - 1));
        if (nextPage == historyEventPageIndex)
        {
            return;
        }

        historyEventPageIndex = nextPage;
        await SelectRunAsync(selectedRun.RunId, resetEventPage: false);
    }

    private async Task OpenRunDetailDialogAsync(WorkflowRunSnapshot run)
    {
        runDetail = run;
        var eventsTask = RuntimeManager.ListEventsAsync(run.RunId);
        var artifactsTask = RunStore.ListArtifactsAsync(run.RunId);
        await Task.WhenAll(eventsTask, artifactsTask);
        runDetailEvents = await eventsTask;
        runDetailArtifacts = await artifactsTask;
    }

    private void CloseRunDetailDialog()
    {
        runDetail = null;
        runDetailEvents = [];
        runDetailArtifacts = [];
    }

    private void OpenEventDetailDialog(WorkflowEventRecord workflowEvent)
    {
        eventDetail = workflowEvent;
    }

    private void CloseEventDetailDialog()
    {
        eventDetail = null;
    }

    private async Task CancelSelectedRunAsync()
    {
        if (selectedRun is null || IsTerminalRun(selectedRun))
        {
            return;
        }

        try
        {
            selectedRun = await RuntimeManager.CancelAsync(selectedRun.RunId);
            NotificationService.Success("Workflow run cancelled", selectedRun.Summary);
            await LoadPageAsync(preferredDefinitionId: CurrentDefinitionId, preferredRunId: selectedRun.RunId);
        }
        catch (Exception exception)
        {
            errorMessage = FormatWorkflowException(exception);
            NotificationService.Error("Workflow cancel failed", errorMessage);
        }
    }

    private async Task RespondToRequestAsync(WorkflowExternalRequestRecord request)
    {
        try
        {
            selectedRun = await RuntimeManager.RespondToExternalRequestAsync(request.Id, pendingResponseJson);
            NotificationService.Success("Workflow request answered", selectedRun.Summary);
            await LoadPageAsync(preferredDefinitionId: CurrentDefinitionId, preferredRunId: selectedRun.RunId);
        }
        catch (Exception exception)
        {
            errorMessage = FormatWorkflowException(exception);
            NotificationService.Error("Workflow response failed", errorMessage);
        }
    }

    private async Task HandleCanvasDefinitionSavedAsync(WorkflowDefinition definition)
    {
        await LoadPageAsync(preferredDefinitionId: definition.Id, preferredRunId: selectedRun?.RunId);
    }

    private async Task HandleCanvasPreviewRunCompletedAsync(WorkflowRunSnapshot run)
    {
        analyticsRefreshVersion++;
        await LoadRunsPageAsync(run.WorkflowId, pageIndex: 0, preferredRunId: run.RunId);
        await OpenRunDetailDialogAsync(selectedRun ?? run);
    }

    private async Task HandleWorkflowTabChangedAsync(int index)
    {
        activeWorkflowTabIndex = index;
        if (WorkflowTabRequiresDefinitionDetail(index))
        {
            await EnsureSelectedDefinitionLoadedAsync();
        }

        if (WorkflowTabRequiresComponentLibrary(index))
        {
            await EnsureComponentLibraryLoadedAsync();
        }

        if (WorkflowTabRequiresHistory(index))
        {
            await EnsureHistoryLoadedAsync();
        }
    }

    private async Task EnsureHistoryLoadedAsync()
    {
        if (historyLoaded)
        {
            return;
        }

        await LoadRunsPageAsync(CurrentDefinitionId, pageIndex: 0);
    }

    private async Task EnsureComponentLibraryLoadedAsync()
    {
        if (componentLibraryLoaded)
        {
            return;
        }

        componentLibraryLoadTask ??= LoadComponentLibraryAsync();
        try
        {
            await componentLibraryLoadTask;
            componentLibraryLoaded = true;
        }
        finally
        {
            componentLibraryLoadTask = null;
        }
    }

    private async Task RefreshComponentLibraryAsync()
    {
        if (componentLibraryLoadTask is not null)
        {
            await componentLibraryLoadTask;
        }

        componentLibraryLoadTask = LoadComponentLibraryAsync();
        try
        {
            await componentLibraryLoadTask;
            componentLibraryLoaded = true;
        }
        finally
        {
            componentLibraryLoadTask = null;
        }
    }

    private async Task LoadComponentLibraryAsync()
    {
        components = await ComponentLibrary.ListComponentsAsync();
        providerOptions = await ComponentLibrary.ListProviderOptionsAsync();
    }

    private Task EnsureTemplatePackLoadedAsync()
    {
        templatePack ??= TemplatePackLoader.Load();
        return Task.CompletedTask;
    }

    private static bool WorkflowTabRequiresComponentLibrary(int index)
        => index == EditorTabIndex;

    private static bool WorkflowTabRequiresHistory(int index)
        => index == HistoryTabIndex;

    private static bool WorkflowTabRequiresDefinitionDetail(int index)
        => index is WorkflowsTabIndex or EditorTabIndex;

    private bool ShouldLoadHistory(WorkflowRunId? preferredRunId)
        => preferredRunId.HasValue ||
           historyLoaded ||
           WorkflowTabRequiresHistory(activeWorkflowTabIndex);

    private async Task EnsureSelectedDefinitionLoadedAsync()
    {
        if (selectedDefinitionDetailLoaded ||
            selectedDefinitionDetailUnavailable ||
            CurrentDefinitionId is not { } definitionId)
        {
            return;
        }

        var selectionGeneration = selectedDefinitionGeneration;
        await LoadDefinitionAsync(definitionId, selectionGeneration);
        if (IsCurrentDefinitionSelection(definitionId, selectionGeneration))
        {
            StateHasChanged();
        }
    }

    private void SetSelectedDefinitionPlaceholder(WorkflowId definitionId)
    {
        selectedDefinitionGeneration++;
        selectedDefinitionNavigation = AgentChatNavigationFence;
        runPageGeneration++;
        selectedRunGeneration++;
        selectedRunRequestId = null;
        isDefinitionSelectionLoading = false;
        isRunsPageLoading = false;
        isRunSelectionLoading = false;
        if (selectedDefinition?.Id == definitionId && selectedDefinitionDetailLoaded)
        {
            selectedDefinitionId = definitionId;
            return;
        }

        selectedDefinitionId = definitionId;
        selectedDefinition = null;
        validationIssues = [];
        selectedDefinitionDetailLoaded = false;
        selectedDefinitionDetailUnavailable = false;
    }

    private void ClearSelectedDefinitionState()
    {
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
        => selectedDefinitionGeneration == selectionGeneration &&
           selectedDefinitionNavigation == AgentChatNavigationFence &&
           CurrentDefinitionId == definitionId;

    private bool IsCurrentPageLoad(
        long generation,
        AgentChatNavigationIdentity navigation)
        => pageLoadGeneration == generation &&
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

    private void ClearHistoryState(bool markLoaded)
    {
        runPageGeneration++;
        isRunsPageLoading = false;
        runs = [];
        historyRunPageIndex = 0;
        historyRunTotalCount = 0;
        ClearSelectedRunState();
        historyLoaded = markLoaded;
    }

    private void ClearSelectedRunState()
    {
        selectedRunGeneration++;
        selectedRunRequestId = null;
        isRunSelectionLoading = false;
        selectedRun = null;
        runEvents = [];
        artifacts = [];
        pendingRequests = [];
        historyEventPageIndex = 0;
        historyEventTotalCount = 0;
    }

    private void FailRouteIdentity(string message)
    {
        hasRouteIdentityFailure = true;
        errorMessage = message;
        selectedRouteProject = null;
        ClearSelectedDefinitionState();
        ClearHistoryState(markLoaded: false);
    }

    private async Task<ProjectWorkflowRouteValidation> ValidateProjectWorkflowRelationAsync(
        Guid projectId,
        WorkflowId workflowId)
    {
        var projectsTask = ProjectStructureGateway.ListProjectsAsync();
        var structureTask = ProjectStructureGateway.ReadStructureAsync(
            projectId,
            new ProjectStructureRuntimeReadRequest(
                ObjectTypes: [ProjectObjectType.WorkflowDefinition],
                IncludeMetadata: true));
        await Task.WhenAll(projectsTask, structureTask);

        var projects = await projectsTask;
        var project = projects.FirstOrDefault(item => item.Id == projectId);
        if (project is null)
        {
            return ProjectWorkflowRouteValidation.Failed(
                $"Project '{projectId:D}' was not found.");
        }

        var structure = await structureTask;
        if (structure.ProjectId != projectId)
        {
            return ProjectWorkflowRouteValidation.Failed(
                $"Project structure returned project '{structure.ProjectId:D}' for requested project '{projectId:D}'.");
        }

        if (!structure.Nodes.Any(node => IsWorkflowNodeForDefinition(node, workflowId)))
        {
            return ProjectWorkflowRouteValidation.Failed(
                $"Workflow '{workflowId}' is not attached to project '{projectId:D}'.");
        }

        return ProjectWorkflowRouteValidation.Succeeded(new WorkflowAgentChatProjectSelection(
            project.Id,
            project.Name));
    }

    private static bool IsWorkflowNodeForDefinition(
        ProjectStructureRuntimeNodeSummary node,
        WorkflowId workflowId)
    {
        if (node.ObjectType != ProjectObjectType.WorkflowDefinition)
        {
            return false;
        }

        try
        {
            return ProjectObjectMetadataSerializer.Parse(node.MetadataJson).Workflow?.WorkflowId == workflowId;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private bool IsSelectedDefinition(WorkflowCatalogItem item)
    {
        return CurrentDefinitionId == item.Id;
    }

    private static bool IsTerminalRun(WorkflowRunSnapshot? run)
    {
        return run?.State is WorkflowRunState.Completed or WorkflowRunState.Failed or WorkflowRunState.Cancelled;
    }

    private string BuildSettingsSummary()
    {
        var artifactPolicy = settings.ArtifactPolicy.CaptureNodeOutputs
            ? $"captures node outputs up to {settings.ArtifactPolicy.MaxInlinePayloadCharacters:N0} characters"
            : "does not capture node outputs";
        var humanPolicy = settings.HumanInLoopPolicy.AllowHumanInputNodes
            ? $"allows human input with {settings.HumanInLoopPolicy.DefaultRequestTimeoutMinutes} minute timeout"
            : "disables human input nodes";
        return $"Default backend is {settings.DefaultRuntimePolicy.PreferredBackend}; artifact policy {artifactPolicy}; human-in-loop policy {humanPolicy}.";
    }

    private string BuildProviderOptionsSummary()
    {
        if (providerOptions.Count == 0)
        {
            return "No agent chat providers are available; new components use an unbound preview model.";
        }

        var enabledCount = providerOptions.Count(option => option.IsEnabled);
        return $"{enabledCount} enabled chat provider(s) available from the agent provider registry.";
    }

    private static bool WorkflowTemplateMatchesSearch(WorkflowTemplateDefinition template, string query)
        => template.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
           template.Description.Contains(query, StringComparison.OrdinalIgnoreCase) ||
           template.Key.Contains(query, StringComparison.OrdinalIgnoreCase);

    private static bool IsSameWorkflowTemplate(
        WorkflowTemplateDefinition left,
        WorkflowTemplateDefinition right)
        => string.Equals(left.Key, right.Key, StringComparison.OrdinalIgnoreCase);

    private static string BuildTemplateNodeKindSummary(WorkflowTemplateDefinition template)
    {
        if (template.Graph.Nodes.Count == 0)
        {
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
        WorkflowLifecycleStatus status)
    {
        var definition = templatePack.CreateDefinition(template, component);
        return definition with
        {
            Name = name,
            Description = template.Description,
            Status = status,
            RuntimePolicy = templatePack.RuntimePolicy,
            InputParameters = templatePack.CreateInputParameters(template)
        };
    }

    private static LlmCallComponent CreateTransientTemplateComponent(
        WorkflowTemplatePack templatePack,
        WorkflowTemplateDefinition template)
    {
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

    private static string ResolveTemplateModel(WorkflowProviderOption? providerOption)
    {
        if (providerOption is null)
        {
            return ManagedSeedProviderFallbacks.OpenAiDefaultModel;
        }

        return providerOption.ModelOptions.FirstOrDefault(model =>
                   string.Equals(model, ManagedSeedProviderFallbacks.OpenAiDefaultModel, StringComparison.OrdinalIgnoreCase)) ??
               ResolveDefaultModel(providerOption);
    }

    private static AgentPermissionsPolicy CreateTemplateComponentPermissions()
        => AgentPermissionsPolicy.Default with
        {
            CanUseTools = false,
            CanAskOtherAgents = false,
            CanEscalateToHuman = false,
            RequiresApprovalForExternalCalls = false
        };

    private static string ResolveTemplateDraftName(
        string baseName,
        IReadOnlyList<WorkflowCatalogItem> existingDefinitions)
    {
        var normalizedBaseName = NormalizeTemplateDraftBaseName(baseName);
        var existingNames = existingDefinitions
            .Select(definition => definition.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!existingNames.Contains(normalizedBaseName))
        {
            return normalizedBaseName;
        }

        for (var index = 1; index <= 999; index++)
        {
            var candidate = $"{index:00} {normalizedBaseName}";
            if (!existingNames.Contains(candidate))
            {
                return candidate;
            }
        }

        throw new InvalidOperationException($"No available draft name remains for template '{normalizedBaseName}'.");
    }

    private static string NormalizeTemplateDraftBaseName(string name)
    {
        var trimmed = name.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            throw new InvalidOperationException("Workflow template name is required before it can be added to drafts.");
        }

        return trimmed;
    }

    private static CanvasWorkbenchSurface BuildTemplatePreviewCanvasSurface(
        WorkflowDefinition definition,
        LlmCallComponent component,
        IReadOnlyList<WorkflowExecutorDescriptor> executors,
        CanvasWorkbenchUiState uiState,
        string? selectedNodeId)
    {
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
        foreach (var node in surface.Nodes)
        {
            node.ContextActions.Clear();
        }

        return surface;
    }

    private static CanvasWorkbenchUiState CreateTemplatePreviewCanvasUiState(string? selectedNodeId)
        => new()
        {
            ActiveInspectorTab = "workflow",
            Zoom = 0.48,
            PanX = 144,
            PanY = 88,
            SelectedNodeIds = string.IsNullOrWhiteSpace(selectedNodeId) ? [] : [selectedNodeId]
        };

    private string ResolveComponentProviderLabel(LlmCallComponent component)
    {
        if (!component.ProviderProfileId.HasValue)
        {
            return "No provider";
        }

        var provider = providerOptions.FirstOrDefault(option => option.ProviderProfileId == component.ProviderProfileId.Value);
        return provider?.Name ?? "Provider missing";
    }

    private static WorkflowGraph CreateStarterGraph(WorkflowComponentId componentId)
    {
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
        WorkflowValueShape? resultShape = null)
    {
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

    private static string ResolveRunTone(WorkflowRunState state)
    {
        return state switch
        {
            WorkflowRunState.Completed => "success",
            WorkflowRunState.Failed => "danger",
            WorkflowRunState.Cancelled => "neutral",
            WorkflowRunState.WaitingForInput => "warning",
            WorkflowRunState.Running => "info",
            _ => "secondary"
        };
    }

    private static string ResolveEventTone(WorkflowEventKind kind)
    {
        return kind switch
        {
            WorkflowEventKind.Completed or WorkflowEventKind.Output or WorkflowEventKind.ExecutorCompleted => "success",
            WorkflowEventKind.Error or WorkflowEventKind.ExecutorFailed => "danger",
            WorkflowEventKind.WaitingForInput => "warning",
            WorkflowEventKind.Started or WorkflowEventKind.ExecutorInvoked => "info",
            _ => "neutral"
        };
    }

    private static string FormatDate(DateTimeOffset value)
    {
        return value.ToLocalTime().ToString("MMM d, HH:mm");
    }

    private static string FormatFullDate(DateTimeOffset value)
    {
        return value.ToLocalTime().ToString("MMM d, yyyy HH:mm:ss");
    }

    private static string ResolveRunResultPayload(IReadOnlyList<WorkflowEventRecord> events)
    {
        foreach (var outputEvent in events.Reverse().Where(workflowEvent => workflowEvent.Kind == WorkflowEventKind.Output))
        {
            var payloadJson = ResolveEventPayloadJson(outputEvent);
            if (!string.IsNullOrWhiteSpace(payloadJson))
            {
                return payloadJson;
            }
        }

        foreach (var completedEvent in events.Reverse().Where(workflowEvent => workflowEvent.Kind == WorkflowEventKind.ExecutorCompleted))
        {
            var payloadJson = ResolveEventPayloadJson(completedEvent);
            if (!string.IsNullOrWhiteSpace(payloadJson))
            {
                return payloadJson;
            }
        }

        return string.Empty;
    }

    private static string ResolveEventPayloadJson(WorkflowEventRecord workflowEvent)
    {
        if (!string.IsNullOrWhiteSpace(workflowEvent.PayloadJson))
        {
            return workflowEvent.PayloadJson;
        }

        return TryExtractLegacyPayloadJson(workflowEvent.Message, out var payloadJson)
            ? payloadJson
            : string.Empty;
    }

    private static bool TryExtractLegacyPayloadJson(string message, out string payloadJson)
    {
        payloadJson = string.Empty;
        const string marker = "PayloadJson = ";
        var start = message.LastIndexOf(marker, StringComparison.Ordinal);
        if (start < 0)
        {
            return false;
        }

        start += marker.Length;
        while (start < message.Length && char.IsWhiteSpace(message[start]))
        {
            start++;
        }

        if (start >= message.Length || message[start] is not ('{' or '['))
        {
            return false;
        }

        var stack = new Stack<char>();
        var inString = false;
        var escaped = false;
        for (var index = start; index < message.Length; index++)
        {
            var character = message[index];
            if (inString)
            {
                if (escaped)
                {
                    escaped = false;
                }
                else if (character == '\\')
                {
                    escaped = true;
                }
                else if (character == '"')
                {
                    inString = false;
                }

                continue;
            }

            if (character == '"')
            {
                inString = true;
                continue;
            }

            if (character == '{')
            {
                stack.Push('}');
                continue;
            }

            if (character == '[')
            {
                stack.Push(']');
                continue;
            }

            if (character is not ('}' or ']'))
            {
                continue;
            }

            if (stack.Count == 0 || stack.Pop() != character)
            {
                return false;
            }

            if (stack.Count != 0)
            {
                continue;
            }

            var candidate = message[start..(index + 1)];
            try
            {
                using var _ = JsonDocument.Parse(candidate);
                payloadJson = candidate;
                return true;
            }
            catch (JsonException)
            {
                return false;
            }
        }

        return false;
    }

    private static string ResolveRunResultPreview(string payloadJson)
    {
        if (string.IsNullOrWhiteSpace(payloadJson))
        {
            return string.Empty;
        }

        try
        {
            using var document = JsonDocument.Parse(payloadJson);
            if (TryFindResultPreviewText(document.RootElement, out var value))
            {
                return TruncatePreservingWhitespace(value, 3000);
            }
        }
        catch (JsonException)
        {
            return TruncatePreservingWhitespace(payloadJson, 3000);
        }

        return TruncatePreservingWhitespace(payloadJson, 3000);
    }

    private static bool TryFindResultPreviewText(JsonElement element, out string value)
    {
        value = string.Empty;
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var propertyName in RunResultPreviewPropertyNames)
            {
                if (element.TryGetProperty(propertyName, out var property) &&
                    property.ValueKind == JsonValueKind.String &&
                    !string.IsNullOrWhiteSpace(property.GetString()))
                {
                    value = property.GetString()!;
                    return true;
                }
            }

            foreach (var property in element.EnumerateObject())
            {
                if (TryFindResultPreviewText(property.Value, out value))
                {
                    return true;
                }
            }
        }

        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                if (TryFindResultPreviewText(item, out value))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static string TruncatePreservingWhitespace(string value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= maxLength
            ? trimmed
            : $"{trimmed[..Math.Max(0, maxLength - 3)]}...";
    }

    private static int CalculateTotalPages(int totalCount, int pageSize)
    {
        return totalCount <= 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize);
    }

    private static string FormatPageLabel(int pageIndex, int totalPages, int totalCount, string noun)
    {
        if (totalCount == 0)
        {
            return $"0 {noun}";
        }

        return $"Page {pageIndex + 1} of {totalPages} - {totalCount:N0} {noun}";
    }

    private static string Truncate(string value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
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

    private static bool HasTechnicalEventMessage(WorkflowEventRecord workflowEvent)
    {
        if (WorkflowFailureDisplayFormatter.TryResolveDiagnosticTechnicalDetail(workflowEvent, out var technicalDetail))
        {
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
        string ValidationError)
    {
        public static WorkflowRouteRequest Create(
            Guid? projectId,
            Guid? workflowId,
            Guid? runId)
        {
            var hasExplicitSelection = projectId.HasValue || workflowId.HasValue || runId.HasValue;
            if (!hasExplicitSelection)
            {
                return new WorkflowRouteRequest(null, null, null, false, string.Empty);
            }

            if (projectId == Guid.Empty)
            {
                return Invalid("The projectId query value cannot be empty.");
            }

            if (workflowId == Guid.Empty)
            {
                return Invalid("The workflowId query value cannot be empty.");
            }

            if (runId == Guid.Empty)
            {
                return Invalid("The runId query value cannot be empty.");
            }

            if (!workflowId.HasValue && projectId.HasValue)
            {
                return Invalid("A projectId workflow route also requires workflowId.");
            }

            if (!workflowId.HasValue && runId.HasValue)
            {
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
        string ErrorMessage)
    {
        public static ProjectWorkflowRouteValidation Succeeded(WorkflowAgentChatProjectSelection project)
            => new(project, string.Empty);

        public static ProjectWorkflowRouteValidation Failed(string errorMessage)
            => new(null, errorMessage);
    }

    private static string FormatWorkflowException(Exception exception)
        => WorkflowFailureDisplayFormatter.ToUserMessage(exception.GetBaseException().Message);

    private static string FormatShortId(Guid value)
    {
        return value.ToString("N")[..8];
    }

    private WorkflowProviderOption? ResolveDefaultProviderOption()
    {
        return providerOptions.FirstOrDefault(option => option.IsEnabled);
    }

    private static string ResolveDefaultModel(WorkflowProviderOption? providerOption)
    {
        if (providerOption is null)
        {
            return ManagedSeedProviderFallbacks.OpenAiDefaultModel;
        }

        if (!string.IsNullOrWhiteSpace(providerOption.DefaultModel))
        {
            return providerOption.DefaultModel;
        }

        return providerOption.ModelOptions.FirstOrDefault(model => !string.IsNullOrWhiteSpace(model)) ??
               ManagedSeedProviderFallbacks.OpenAiDefaultModel;
    }

    private void OpenAgents()
    {
        Navigation.NavigateTo("/agents");
    }

    private async Task OpenWorkflowCuratorAsync()
    {
        if (isOpeningWorkflowCurator ||
            workflowCuratorAgent is null ||
            !WorkflowCuratorAgentIdentity.Matches(workflowCuratorAgent) ||
            AgentChatAccessState != AgentChatContextAccessState.Ready)
        {
            return;
        }

        isOpeningWorkflowCurator = true;
        try
        {
            await AgentChatLauncher.StartNewChatAsync(workflowCuratorAgent.Id);
            NotificationService.Success("Workflow Curator ready", "Opened a new managed workflow chat.");
        }
        catch (Exception exception)
        {
            NotificationService.Error("Unable to open Workflow Curator", FormatWorkflowException(exception));
        }
        finally
        {
            isOpeningWorkflowCurator = false;
        }
    }

    private async Task<(AgentDefinition? Agent, string? ErrorMessage)> TryResolveWorkflowCuratorAgentAsync()
    {
        try
        {
            var agents = await AgentWorkspaceService.ListAgentsAsync(includeTemplates: false);
            var agent = agents.SingleOrDefault(WorkflowCuratorAgentIdentity.Matches);
            return agent is null
                ? (null, $"The managed agent '{WorkflowCuratorAgentIdentity.AgentId:D}' is not available.")
                : (agent, null);
        }
        catch (Exception exception)
        {
            return (null, FormatWorkflowException(exception));
        }
    }

    private async Task EnsureWorkflowCuratorAgentAsync()
    {
        if (workflowCuratorAgent is not null)
        {
            return;
        }

        var resolutionTask = workflowCuratorResolutionTask;
        if (resolutionTask is null)
        {
            resolutionTask = ResolveWorkflowCuratorAgentAsync();
            workflowCuratorResolutionTask = resolutionTask;
        }

        try
        {
            await resolutionTask;
        }
        finally
        {
            if (ReferenceEquals(workflowCuratorResolutionTask, resolutionTask))
            {
                workflowCuratorResolutionTask = null;
            }
        }
    }

    private async Task ResolveWorkflowCuratorAgentAsync()
    {
        var resolution = await TryResolveWorkflowCuratorAgentAsync();
        workflowCuratorAgent = resolution.Agent;
        if (resolution.ErrorMessage is { } curatorError)
        {
            NotificationService.Warning("Workflow Curator unavailable", curatorError);
        }
    }

    private Task HandleAgentChatExecutionCompletedAsync(AgentChatExecutionCompleted notification)
    {
        ArgumentNullException.ThrowIfNull(notification);
        return RefreshAsync();
    }
}
