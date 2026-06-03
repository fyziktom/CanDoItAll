using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Components.BaseLib;
using Microsoft.AspNetCore.Components;
using System.Text.Json;

namespace CanDoItAll.Modules.AgentFramework.Pages;

public partial class WorkflowsPage
{
    private const int HistoryRunPageSize = 8;
    private const int HistoryEventPageSize = 8;
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
    public IProjectStructureRuntimeGateway ProjectStructureGateway { get; set; } = default!;

    [Inject]
    public NavigationManager Navigation { get; set; } = default!;

    [Inject]
    public NotificationService NotificationService { get; set; } = default!;

    private IReadOnlyList<WorkflowCatalogItem> definitions = [];
    private IReadOnlyList<LlmCallComponent> components = [];
    private IReadOnlyList<WorkflowProviderOption> providerOptions = [];
    private IReadOnlyList<WorkflowRunSnapshot> runs = [];
    private IReadOnlyList<WorkflowEventRecord> runEvents = [];
    private IReadOnlyList<WorkflowArtifactRecord> artifacts = [];
    private IReadOnlyList<WorkflowExternalRequestRecord> pendingRequests = [];
    private IReadOnlyList<WorkflowValidationIssue> validationIssues = [];
    private WorkflowTemplatePack? templatePack;
    private WorkflowSettings settings = WorkflowSettings.Default;
    private WorkflowDefinition? selectedDefinition;
    private WorkflowRunSnapshot? selectedRun;
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
    private string errorMessage = string.Empty;
    private int activeWorkflowTabIndex;
    private int historyRunPageIndex;
    private int historyRunTotalCount;
    private int historyEventPageIndex;
    private int historyEventTotalCount;
    private bool isLoading = true;
    private bool isBusy;
    private bool isRunningTest;
    private bool isPreviewInputDialogOpen;
    private bool componentLibraryLoaded;
    private Task? componentLibraryLoadTask;
    private readonly HashSet<string> expandedWorkflowTreeNodeIds = [];

    private string SelectedDefinitionTitle => selectedDefinition?.Name ?? "Workflow detail";

    private string ComponentCountText => componentLibraryLoaded ? components.Count.ToString() : "-";

    private IReadOnlyList<WorkflowTemplateDefinition> WorkflowTemplates => templatePack?.Workflows ?? [];

    private string WorkflowTemplateSeedText => templatePack?.Manifest.SeedVersion ?? "-";

    private string ValidationText => validationIssues.Count == 0 ? "Valid" : $"{validationIssues.Count} issue(s)";

    private string ValidationTone => validationIssues.Count == 0 ? "success" : "warning";

    private string RunText => selectedRun is null ? "No run selected" : selectedRun.State.ToString();

    private string RunTone => selectedRun is null ? "neutral" : ResolveRunTone(selectedRun.State);

    private IReadOnlyList<TreeViewNode> WorkflowDefinitionTreeNodes
        => WorkflowDefinitionTreeNodeBuilder.Build(
            definitions,
            selectedDefinition?.Id,
            expandedWorkflowTreeNodeIds);

    private int HistoryRunTotalPages => CalculateTotalPages(historyRunTotalCount, HistoryRunPageSize);

    private int HistoryEventTotalPages => CalculateTotalPages(historyEventTotalCount, HistoryEventPageSize);

    private bool CanGoToPreviousRunPage => historyRunPageIndex > 0;

    private bool CanGoToNextRunPage => historyRunPageIndex + 1 < HistoryRunTotalPages;

    private bool CanGoToPreviousEventPage => historyEventPageIndex > 0;

    private bool CanGoToNextEventPage => historyEventPageIndex + 1 < HistoryEventTotalPages;

    protected override async Task OnInitializedAsync()
    {
        await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        if (isBusy)
        {
            return;
        }

        isBusy = true;
        isLoading = true;
        errorMessage = string.Empty;

        try
        {
            await LoadPageAsync(preferredDefinitionId: selectedDefinition?.Id, preferredRunId: selectedRun?.RunId);
        }
        catch (Exception exception)
        {
            errorMessage = exception.Message;
            NotificationService.Error("Workflow refresh failed", exception.Message);
        }
        finally
        {
            isLoading = false;
            isBusy = false;
        }
    }

    private async Task LoadPageAsync(
        WorkflowId? preferredDefinitionId = null,
        WorkflowRunId? preferredRunId = null)
    {
        var settingsTask = SettingsService.GetSettingsAsync();
        var definitionsTask = CatalogService.ListDefinitionsAsync();
        templatePack ??= TemplatePackLoader.Load();
        await Task.WhenAll(settingsTask, definitionsTask);

        settings = await settingsTask;
        definitions = await definitionsTask;

        var definitionId = preferredDefinitionId ??
                           selectedDefinition?.Id ??
                           definitions.FirstOrDefault()?.Id;
        if (definitionId.HasValue)
        {
            await LoadDefinitionAsync(definitionId.Value);
        }
        else
        {
            selectedDefinition = null;
            validationIssues = [];
        }

        await LoadRunsPageAsync(
            selectedDefinition?.Id,
            pageIndex: 0,
            preferredRunId);

        if (componentLibraryLoaded)
        {
            await RefreshComponentLibraryAsync();
        }
    }

    private async Task SelectDefinitionAsync(WorkflowId definitionId)
    {
        errorMessage = string.Empty;
        await LoadDefinitionAsync(definitionId);
        await LoadRunsPageAsync(definitionId, pageIndex: 0);
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

    private async Task LoadDefinitionAsync(WorkflowId definitionId)
    {
        var detail = await CatalogService.GetDefinitionAsync(definitionId);
        if (detail is null)
        {
            selectedDefinition = null;
            validationIssues = [];
            return;
        }

        selectedDefinition = detail.Definition;
        validationIssues = detail.Validation.Issues;
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
            errorMessage = exception.Message;
            NotificationService.Error("Workflow create failed", exception.Message);
        }
        finally
        {
            isBusy = false;
        }
    }

    private async Task RunSelectedWorkflowAsync()
    {
        if (selectedDefinition is null || isRunningTest)
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
            errorMessage = exception.Message;
            NotificationService.Error("Workflow test failed", exception.Message);
        }
        finally
        {
            isRunningTest = false;
        }
    }

    private async Task SelectRunAsync(WorkflowRunId runId, bool resetEventPage = true)
    {
        selectedRun = await RuntimeManager.GetRunAsync(runId);
        if (selectedRun is null)
        {
            runEvents = [];
            artifacts = [];
            pendingRequests = [];
            historyEventPageIndex = 0;
            historyEventTotalCount = 0;
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

        var eventPage = await eventsTask;
        runEvents = eventPage.Items;
        historyEventPageIndex = eventPage.PageIndex;
        historyEventTotalCount = eventPage.TotalCount;
        artifacts = await artifactsTask;
        pendingRequests = await pendingRequestsTask;
    }

    private async Task LoadRunsPageAsync(
        WorkflowId? workflowId,
        int pageIndex,
        WorkflowRunId? preferredRunId = null)
    {
        var runPage = await RunStore.ListRunPageAsync(new WorkflowRunPageRequest(
            workflowId,
            null,
            null,
            string.Empty,
            pageIndex,
            HistoryRunPageSize));
        runs = runPage.Items;
        historyRunPageIndex = runPage.PageIndex;
        historyRunTotalCount = runPage.TotalCount;

        WorkflowRunId? retainedRunId = selectedRun is not null && selectedRun.WorkflowId == workflowId
            ? selectedRun.RunId
            : null;
        var runId = preferredRunId ??
                    retainedRunId ??
                    runs.FirstOrDefault()?.RunId;
        if (runId.HasValue)
        {
            await SelectRunAsync(runId.Value);
            return;
        }

        selectedRun = null;
        runEvents = [];
        artifacts = [];
        pendingRequests = [];
        historyEventPageIndex = 0;
        historyEventTotalCount = 0;
    }

    private async Task ChangeRunPageAsync(int delta)
    {
        var nextPage = Math.Clamp(historyRunPageIndex + delta, 0, Math.Max(0, HistoryRunTotalPages - 1));
        if (nextPage == historyRunPageIndex)
        {
            return;
        }

        await LoadRunsPageAsync(selectedDefinition?.Id, nextPage);
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
            await LoadPageAsync(preferredDefinitionId: selectedDefinition?.Id, preferredRunId: selectedRun.RunId);
        }
        catch (Exception exception)
        {
            errorMessage = exception.Message;
            NotificationService.Error("Workflow cancel failed", exception.Message);
        }
    }

    private async Task RespondToRequestAsync(WorkflowExternalRequestRecord request)
    {
        try
        {
            selectedRun = await RuntimeManager.RespondToExternalRequestAsync(request.Id, pendingResponseJson);
            NotificationService.Success("Workflow request answered", selectedRun.Summary);
            await LoadPageAsync(preferredDefinitionId: selectedDefinition?.Id, preferredRunId: selectedRun.RunId);
        }
        catch (Exception exception)
        {
            errorMessage = exception.Message;
            NotificationService.Error("Workflow response failed", exception.Message);
        }
    }

    private async Task HandleCanvasDefinitionSavedAsync(WorkflowDefinition definition)
    {
        await LoadPageAsync(preferredDefinitionId: definition.Id, preferredRunId: selectedRun?.RunId);
    }

    private async Task HandleCanvasPreviewRunCompletedAsync(WorkflowRunSnapshot run)
    {
        await LoadRunsPageAsync(run.WorkflowId, pageIndex: 0, preferredRunId: run.RunId);
        await OpenRunDetailDialogAsync(selectedRun ?? run);
    }

    private async Task HandleWorkflowTabChangedAsync(int index)
    {
        activeWorkflowTabIndex = index;
        if (WorkflowTabRequiresComponentLibrary(index))
        {
            await EnsureComponentLibraryLoadedAsync();
        }
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

    private static bool WorkflowTabRequiresComponentLibrary(int index)
        => index is 2 or 3 or 5;

    private bool IsSelectedDefinition(WorkflowCatalogItem item)
    {
        return selectedDefinition?.Id == item.Id;
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

    private static string ResolveEventDisplayMessage(WorkflowEventRecord workflowEvent)
        => workflowEvent.Kind is WorkflowEventKind.Error or WorkflowEventKind.ExecutorFailed
            ? WorkflowFailureDisplayFormatter.ToUserMessage(workflowEvent.Message)
            : workflowEvent.Message;

    private static bool HasTechnicalEventMessage(WorkflowEventRecord workflowEvent)
        => workflowEvent.Kind is WorkflowEventKind.Error or WorkflowEventKind.ExecutorFailed &&
           !string.Equals(
               ResolveEventDisplayMessage(workflowEvent),
               workflowEvent.Message,
               StringComparison.Ordinal);

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
}
