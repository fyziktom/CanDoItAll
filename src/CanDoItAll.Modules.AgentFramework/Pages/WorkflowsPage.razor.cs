using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Components.BaseLib;
using Microsoft.AspNetCore.Components;

namespace CanDoItAll.Modules.AgentFramework.Pages;

public partial class WorkflowsPage
{
    [Inject]
    public IWorkflowCatalogService CatalogService { get; set; } = default!;

    [Inject]
    public IWorkflowComponentLibraryService ComponentLibrary { get; set; } = default!;

    [Inject]
    public IWorkflowSettingsService SettingsService { get; set; } = default!;

    [Inject]
    public IWorkflowTestRunner TestRunner { get; set; } = default!;

    [Inject]
    public IWorkflowRuntimeManager RuntimeManager { get; set; } = default!;

    [Inject]
    public IWorkflowRunStore RunStore { get; set; } = default!;

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
    private WorkflowSettings settings = WorkflowSettings.Default;
    private WorkflowDefinition? selectedDefinition;
    private WorkflowRunSnapshot? selectedRun;
    private WorkflowTestRunResult? testResult;
    private string testInputJson = "{\"prompt\":\"Summarize this workflow input.\"}";
    private string pendingResponseJson = "{\"approved\":true}";
    private string errorMessage = string.Empty;
    private bool isLoading = true;
    private bool isBusy;
    private bool isRunningTest;

    private string SelectedDefinitionTitle => selectedDefinition?.Name ?? "Workflow detail";

    private string ValidationText => validationIssues.Count == 0 ? "Valid" : $"{validationIssues.Count} issue(s)";

    private string ValidationTone => validationIssues.Count == 0 ? "success" : "warning";

    private string RunText => selectedRun is null ? "No run selected" : selectedRun.State.ToString();

    private string RunTone => selectedRun is null ? "neutral" : ResolveRunTone(selectedRun.State);

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
        var componentsTask = ComponentLibrary.ListComponentsAsync();
        var providerOptionsTask = ComponentLibrary.ListProviderOptionsAsync();
        var runsTask = RunStore.ListRunsAsync();
        await Task.WhenAll(settingsTask, definitionsTask, componentsTask, providerOptionsTask, runsTask);

        settings = await settingsTask;
        definitions = await definitionsTask;
        components = await componentsTask;
        providerOptions = await providerOptionsTask;
        runs = await runsTask;

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

        var runId = preferredRunId ??
                    selectedRun?.RunId ??
                    runs.FirstOrDefault(run => selectedDefinition is null || run.WorkflowId == selectedDefinition.Id)?.RunId;
        if (runId.HasValue)
        {
            await SelectRunAsync(runId.Value);
        }
        else
        {
            selectedRun = null;
            runEvents = [];
            artifacts = [];
            pendingRequests = [];
        }
    }

    private async Task SelectDefinitionAsync(WorkflowId definitionId)
    {
        errorMessage = string.Empty;
        await LoadDefinitionAsync(definitionId);
        runs = await RunStore.ListRunsAsync(definitionId);
        var firstRun = runs.FirstOrDefault();
        if (firstRun is null)
        {
            selectedRun = null;
            runEvents = [];
            artifacts = [];
            pendingRequests = [];
            return;
        }

        await SelectRunAsync(firstRun.RunId);
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

        isRunningTest = true;
        errorMessage = string.Empty;

        try
        {
            testResult = await TestRunner.RunAsync(new WorkflowTestRunRequest(
                selectedDefinition.Id,
                selectedDefinition.VersionId,
                DraftDefinition: null,
                InputJson: testInputJson,
                RequestedBackend: WorkflowRuntimeBackendKind.InProcess,
                ValidateOnly: false));
            if (!testResult.Succeeded)
            {
                errorMessage = testResult.ErrorMessage;
                NotificationService.Error("Workflow test failed", testResult.ErrorMessage);
            }
            else
            {
                NotificationService.Success("Workflow test completed", testResult.Run?.Summary ?? "Workflow run completed.");
            }

            runs = await RunStore.ListRunsAsync(selectedDefinition.Id);
            if (testResult.Run is not null)
            {
                await SelectRunAsync(testResult.Run.RunId);
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

    private async Task SelectRunAsync(WorkflowRunId runId)
    {
        selectedRun = await RuntimeManager.GetRunAsync(runId);
        if (selectedRun is null)
        {
            runEvents = [];
            artifacts = [];
            pendingRequests = [];
            return;
        }

        var eventsTask = RuntimeManager.ListEventsAsync(runId);
        var artifactsTask = RunStore.ListArtifactsAsync(runId);
        var pendingRequestsTask = RunStore.ListPendingExternalRequestsAsync(runId);
        await Task.WhenAll(eventsTask, artifactsTask, pendingRequestsTask);

        runEvents = await eventsTask;
        artifacts = await artifactsTask;
        pendingRequests = await pendingRequestsTask;
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
        runs = await RunStore.ListRunsAsync(run.WorkflowId);
        await SelectRunAsync(run.RunId);
    }

    private async Task RefreshComponentLibraryAsync()
    {
        components = await ComponentLibrary.ListComponentsAsync();
        providerOptions = await ComponentLibrary.ListProviderOptionsAsync();
    }

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

    private WorkflowProviderOption? ResolveDefaultProviderOption()
    {
        return providerOptions.FirstOrDefault(option => option.IsEnabled);
    }

    private static string ResolveDefaultModel(WorkflowProviderOption? providerOption)
    {
        if (providerOption is null)
        {
            return "gpt-5.4";
        }

        if (!string.IsNullOrWhiteSpace(providerOption.DefaultModel))
        {
            return providerOption.DefaultModel;
        }

        return providerOption.ModelOptions.FirstOrDefault(model => !string.IsNullOrWhiteSpace(model)) ?? "gpt-5.4";
    }

    private void OpenAgents()
    {
        Navigation.NavigateTo("/agents");
    }
}
