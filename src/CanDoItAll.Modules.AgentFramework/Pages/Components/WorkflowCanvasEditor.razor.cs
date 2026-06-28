using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Components.CanvasLib;
using CanDoItAll.Components.OverlayLib;
using CanDoItAll.SharedKernel.Configuration;
using CanDoItAll.Modules.Security;
using CanDoItAll.Modules.AgentFramework.Pages;
using Microsoft.AspNetCore.Components;

namespace CanDoItAll.Modules.AgentFramework.Pages.Components;

public partial class WorkflowCanvasEditor
{
    private const string ToolboxWindowId = "workflow-canvas-toolbox";
    private const string SelectionWindowId = "workflow-canvas-selection";
    private const string ComponentsWindowId = "workflow-canvas-components";
    private const string CanvasCreateConnectionActionId = "connection:create";
    private const string CanvasDeleteNodeActionId = "delete";
    private const string CanvasDeleteLinkActionId = "delete-link";
    private const string CanvasLinkTargetKind = "link";

    private static readonly JsonSerializerOptions ExecutorJsonOptions = CreateExecutorJsonOptions(writeIndented: false);
    private static readonly JsonSerializerOptions IndentedExecutorJsonOptions = CreateExecutorJsonOptions(writeIndented: true);

    [Inject]
    public IWorkflowCatalogService CatalogService { get; set; } = default!;

    [Inject]
    public IWorkflowExecutorCatalog ExecutorCatalog { get; set; } = default!;

    [Inject]
    public IWorkflowRuntimeBackendCatalog RuntimeBackendCatalog { get; set; } = default!;

    [Inject]
    public IWorkflowComponentLibraryService ComponentLibrary { get; set; } = default!;

    [Inject]
    public IWorkflowTestRunner TestRunner { get; set; } = default!;

    [Inject]
    public IProjectStructureRuntimeGateway ProjectStructureGateway { get; set; } = default!;

    [Inject]
    public SecretService SecretService { get; set; } = default!;

    [Inject]
    public NotificationService NotificationService { get; set; } = default!;

    [Parameter]
    public WorkflowDefinition? Definition { get; set; }

    [Parameter]
    public IReadOnlyList<LlmCallComponent> Components { get; set; } = [];

    [Parameter]
    public IReadOnlyList<WorkflowProviderOption> ProviderOptions { get; set; } = [];

    private IReadOnlyList<WorkflowProviderOption> ImageProviderOptions => ProviderOptions
        .Where(option => option.Purpose == ProviderProfilePurpose.ImageGeneration)
        .OrderByDescending(option => option.IsEnabled)
        .ThenBy(option => option.Name, StringComparer.OrdinalIgnoreCase)
        .ToList();

    [Parameter]
    public EventCallback<WorkflowDefinition> DefinitionSaved { get; set; }

    [Parameter]
    public EventCallback<WorkflowRunSnapshot> PreviewRunCompleted { get; set; }

    [Parameter]
    public EventCallback ComponentLibraryChanged { get; set; }

    private WorkflowCanvasDocument document = WorkflowCanvasDefinitionMapper.CreateDraft([]);
    private IReadOnlyList<LlmCallComponent> componentOptions = [];
    private IReadOnlyList<WorkflowExecutorDescriptor> executorDescriptors = [];
    private IReadOnlyList<WorkflowValidationIssue> validationIssues = [];
    private WorkflowTestRunResult? testResult;
    private string loadedDefinitionKey = string.Empty;
    private string? selectedNodeId = "start";
    private string edgeSourceNodeId = "start";
    private string edgeTargetNodeId = "end";
    private WorkflowEdgeKind edgeKind = WorkflowEdgeKind.Direct;
    private string edgeCondition = string.Empty;
    private WorkflowEdgeId? editingEdgeId;
    private WorkflowRouteKind edgeRouteKind = WorkflowRouteKind.Always;
    private string edgeRouteLabel = string.Empty;
    private string edgeRouteJsonPath = "$.status";
    private WorkflowRouteOperator edgeRouteOperator = WorkflowRouteOperator.Equals;
    private WorkflowRouteValueKind edgeRouteValueKind = WorkflowRouteValueKind.String;
    private string edgeRouteExpectedValue = "approved";
    private bool edgeRouteCaseSensitive;
    private int? edgeRouteFanOutTargetIndex;
    private string? decisionRouteEditorNodeId;
    private WorkflowEdgeId? decisionRouteEditingEdgeId;
    private string decisionRouteTargetNodeId = string.Empty;
    private WorkflowRouteKind decisionRouteKind = WorkflowRouteKind.SwitchCase;
    private string decisionRouteLabel = string.Empty;
    private string decisionRouteJsonPath = "$.route";
    private WorkflowRouteOperator decisionRouteOperator = WorkflowRouteOperator.Equals;
    private WorkflowRouteValueKind decisionRouteValueKind = WorkflowRouteValueKind.String;
    private string decisionRouteExpectedValue = "case";
    private bool decisionRouteCaseSensitive;
    private int? decisionRouteFanOutTargetIndex;
    private string decisionRouteError = string.Empty;
    private string newComponentProviderProfileId = string.Empty;
    private string newComponentModel = string.Empty;
    private string workflowToolboxSearchText = string.Empty;
    private string? expandedWorkflowToolboxGroupKey = "workflow-decisions";
    private string testInputJson = WorkflowPreviewInputSupport.DefaultInputJson;
    private WorkflowPreviewInputState previewInputState = new();
    private IReadOnlyList<ProjectStructureRuntimeProjectSummary> previewProjectOptions = [];
    private WorkflowDefinition? previewInputDefinition;
    private string previewInputErrorMessage = string.Empty;
    private string errorMessage = string.Empty;
    private IReadOnlyList<SecretListItem> secretPickerItems = [];
    private string secretPickerErrorMessage = string.Empty;
    private CanvasWorkbenchUiState canvasUiState = CreateWorkflowCanvasUiState("start");
    private CanvasWorkbench? workbenchRef;
    private CanvasWorkbenchWindowState toolboxWindowState = CreateWindowState(width: 300, height: 380);
    private CanvasWorkbenchWindowState selectionWindowState = CreateWindowState(width: 260, height: 320);
    private CanvasWorkbenchWindowState componentsWindowState = CreateWindowState(width: 320, height: 380, isVisible: false);
    private bool isNodeDetailsDialogOpen;
    private bool isPreviewInputDialogOpen;
    private bool isBusy;
    private bool isTesting;
    private bool isLoadingSecrets;
    private int workflowInspectorTabIndex;

    private WorkflowCanvasNodeDraft? SelectedNode
        => string.IsNullOrWhiteSpace(selectedNodeId)
            ? null
            : document.Nodes.FirstOrDefault(node => node.Id.Value == selectedNodeId);

    private string EdgeEditorSubmitLabel => editingEdgeId is null ? "Add edge" : "Update edge";

    private string SelectionWindowSummary
        => SelectedNode is null
            ? $"{document.Nodes.Count} nodes"
            : $"{SelectedNode.Kind} · {SelectedNode.Id.Value}";

    private sealed record RemovalBridge(
        WorkflowNodeId SourceNodeId,
        WorkflowNodeId TargetNodeId,
        WorkflowEdgeKind Kind,
        string ConditionExpression,
        WorkflowEdgeRouting Routing);

    private CanvasWorkbenchSurface CanvasSurface
        => WorkflowCanvasDefinitionMapper.BuildSurface(
            document,
            componentOptions,
            executorDescriptors,
            secretPickerItems,
            validationIssues,
            canvasUiState,
            selectedNodeId);

    private IReadOnlyList<CanvasWorkbenchStat> CanvasStats =>
    [
        new()
        {
            Label = "Nodes",
            Value = document.Nodes.Count.ToString(),
            Tone = "info"
        },
        new()
        {
            Label = "Edges",
            Value = document.Edges.Count.ToString(),
            Tone = "secondary"
        },
        new()
        {
            Label = "Components",
            Value = CountUsedComponents().ToString(),
            Tone = "accent"
        },
        new()
        {
            Label = "Executors",
            Value = CountUsedExecutors().ToString(),
            Tone = "warning"
        },
        new()
        {
            Label = "Validation",
            Value = validationIssues.Count == 0 ? "Valid" : validationIssues.Count.ToString(),
            Tone = validationIssues.Count == 0 ? "success" : "warning"
        }
    ];

    private int CountUsedComponents()
        => document.Nodes.Count(node => node.ComponentId.HasValue);

    private int CountUsedExecutors()
        => document.Nodes.Count(node => node.ExecutorId.HasValue);

    protected override void OnParametersSet()
    {
        componentOptions = Components;
        executorDescriptors = ExecutorCatalog.ListExecutors();
        SyncNewComponentDefaults();
        var incomingKey = Definition is null
            ? "draft"
            : $"{Definition.Id}:{Definition.VersionId}";
        if (string.Equals(incomingKey, loadedDefinitionKey, StringComparison.Ordinal))
        {
            return;
        }

        document = Definition is null
            ? WorkflowCanvasDefinitionMapper.CreateDraft(componentOptions)
            : WorkflowCanvasDefinitionMapper.FromDefinition(Definition, componentOptions);
        loadedDefinitionKey = incomingKey;
        selectedNodeId = document.StartNodeId.Value;
        canvasUiState = CreateWorkflowCanvasUiState(selectedNodeId);
        validationIssues = [];
        testResult = null;
        isPreviewInputDialogOpen = false;
        previewInputDefinition = null;
        SyncEdgeDefaults();
    }

    protected override async Task OnInitializedAsync()
    {
        await LoadSecretPickerItemsAsync();
    }

    private Task ToggleToolboxWindowAsync()
    {
        toolboxWindowState = ToggleWindow(toolboxWindowState);
        return Task.CompletedTask;
    }

    private Task ToggleSelectionWindowAsync()
    {
        selectionWindowState = ToggleWindow(selectionWindowState);
        return Task.CompletedTask;
    }

    private Task ToggleComponentsWindowAsync()
    {
        componentsWindowState = ToggleWindow(componentsWindowState);
        return Task.CompletedTask;
    }

    private Task HandleToolboxWindowStateChangedAsync(CanvasWorkbenchWindowState state)
    {
        toolboxWindowState = CanvasWorkbenchWindowState.Normalize(state);
        return Task.CompletedTask;
    }

    private Task HandleSelectionWindowStateChangedAsync(CanvasWorkbenchWindowState state)
    {
        selectionWindowState = CanvasWorkbenchWindowState.Normalize(state);
        return Task.CompletedTask;
    }

    private Task HandleComponentsWindowStateChangedAsync(CanvasWorkbenchWindowState state)
    {
        componentsWindowState = CanvasWorkbenchWindowState.Normalize(state);
        return Task.CompletedTask;
    }

    private Task HandleWorkflowInspectorTabChanged(int index)
    {
        workflowInspectorTabIndex = index;
        return Task.CompletedTask;
    }

    private Task ResetDraftAsync()
    {
        document = WorkflowCanvasDefinitionMapper.CreateDraft(componentOptions);
        loadedDefinitionKey = "draft";
        selectedNodeId = document.StartNodeId.Value;
        canvasUiState = CreateWorkflowCanvasUiState(selectedNodeId);
        validationIssues = [];
        testResult = null;
        errorMessage = string.Empty;
        SyncEdgeDefaults();
        return Task.CompletedTask;
    }

    private async Task AddNodeAsync(
        WorkflowNodeKind kind,
        CanvasWorkbenchCreateActionRequest? request = null,
        LlmCallComponent? requestedComponent = null)
    {
        if (kind == WorkflowNodeKind.Executor)
        {
            await AddExecutorNodeAsync(ResolveDefaultExecutorDescriptor(), request);
            return;
        }

        var component = requestedComponent;
        if (kind == WorkflowNodeKind.LlmCall)
        {
            component ??= componentOptions.FirstOrDefault() ?? await CreateDefaultComponentCoreAsync();
        }

        var position = ResolveCreatePosition(request);
        var node = WorkflowCanvasDefinitionMapper.CreateNode(
            kind,
            document.Nodes,
            componentOptions,
            position.X,
            position.Y);
        if (component is not null)
        {
            WorkflowCanvasDefinitionMapper.ApplyComponent(node, component);
        }

        ApplyCreateRequest(node, request);
        document.Nodes.Add(node);
        InsertNodeBeforeEnd(node);
        SelectNode(node.Id.Value);
        SyncEdgeDefaults();
    }

    private Task AddLlmComponentNodeAsync(
        LlmCallComponent component,
        CanvasWorkbenchCreateActionRequest? request = null)
    {
        var position = ResolveCreatePosition(request);
        var node = WorkflowCanvasDefinitionMapper.CreateNode(
            WorkflowNodeKind.LlmCall,
            document.Nodes,
            componentOptions,
            position.X,
            position.Y);
        WorkflowCanvasDefinitionMapper.ApplyComponent(node, component);
        node.Name = component.Name;
        ApplyCreateRequest(node, request);
        document.Nodes.Add(node);
        InsertNodeBeforeEnd(node);
        SelectNode(node.Id.Value);
        SyncEdgeDefaults();
        return Task.CompletedTask;
    }

    private Task AddExecutorNodeAsync(
        WorkflowExecutorDescriptor? descriptor,
        CanvasWorkbenchCreateActionRequest? request = null)
    {
        var position = ResolveCreatePosition(request);
        var node = WorkflowCanvasDefinitionMapper.CreateNode(
            WorkflowNodeKind.Executor,
            document.Nodes,
            componentOptions,
            position.X,
            position.Y);
        if (descriptor is not null)
        {
            WorkflowCanvasDefinitionMapper.ApplyExecutor(node, descriptor);
        }

        ApplyCreateRequest(node, request);
        ApplyExecutorCreateRequest(node, request);
        document.Nodes.Add(node);
        InsertNodeBeforeEnd(node);
        SelectNode(node.Id.Value);
        SyncEdgeDefaults();
        return Task.CompletedTask;
    }

    private async Task CreateDefaultComponentAsync()
    {
        await CreateDefaultComponentCoreAsync();
    }

    private async Task<LlmCallComponent> CreateDefaultComponentCoreAsync()
    {
        if (isBusy)
        {
            throw new InvalidOperationException("Another workflow canvas operation is already running.");
        }

        isBusy = true;
        try
        {
            var providerOption = ResolveSelectedNewComponentProvider();
            var component = await ComponentLibrary.SaveComponentAsync(new LlmCallComponentSaveRequest(
                Id: null,
                Name: $"Canvas LLM call {DateTimeOffset.UtcNow:HHmmss}",
                ProviderProfileId: providerOption?.ProviderProfileId,
                Model: ResolveNewComponentModel(providerOption),
                Modality: WorkflowModality.Text,
                ModelSettings: new WorkflowModelSettings(
                    Temperature: 0.2,
                    MaxOutputTokens: 800,
                    RequireJsonOutput: false,
                    ResponseFormatJsonSchema: string.Empty),
                Instructions: "Summarize the workflow payload and return a concise result.",
                InputShape: WorkflowValueShape.Text,
                ResultShape: WorkflowValueShape.Text,
                Permissions: AgentPermissionsPolicy.Default));
            componentOptions = [.. componentOptions, component];
            NotificationService.Success("LLM component created", component.Name);
            await ComponentLibraryChanged.InvokeAsync();
            return component;
        }
        finally
        {
            isBusy = false;
        }
    }

    private Task AddEdgeAsync()
    {
        errorMessage = string.Empty;
        if (string.IsNullOrWhiteSpace(edgeSourceNodeId) ||
            string.IsNullOrWhiteSpace(edgeTargetNodeId) ||
            string.Equals(edgeSourceNodeId, edgeTargetNodeId, StringComparison.Ordinal))
        {
            errorMessage = "Choose different source and target nodes for the workflow edge.";
            return Task.CompletedTask;
        }

        var source = new WorkflowNodeId(edgeSourceNodeId);
        var target = new WorkflowNodeId(edgeTargetNodeId);
        if (document.Edges.Any(edge =>
                edge.SourceNodeId == source &&
                edge.TargetNodeId == target &&
                edge.Id != editingEdgeId))
        {
            errorMessage = "That workflow edge already exists.";
            return Task.CompletedTask;
        }

        edgeKind = ResolveEdgeKindForRoute(edgeRouteKind);
        var routing = BuildEdgeRoutingFromEditor();
        if (!TryValidateEdgeRouting(routing, out var routeError))
        {
            errorMessage = routeError;
            return Task.CompletedTask;
        }

        if (editingEdgeId is { } edgeId &&
            document.Edges.FirstOrDefault(edge => edge.Id == edgeId) is { } existing)
        {
            existing.SourceNodeId = source;
            existing.TargetNodeId = target;
            existing.Kind = edgeKind;
            existing.ConditionExpression = edgeCondition;
            existing.Routing = routing;
        }
        else
        {
            document.Edges.Add(new WorkflowCanvasEdgeDraft(
                CreateEdgeId(source, target),
                source,
                target)
            {
                Kind = edgeKind,
                ConditionExpression = edgeCondition,
                Routing = routing
            });
        }

        ResetEdgeEditor();
        return Task.CompletedTask;
    }

    private Task RemoveEdgeAsync(WorkflowCanvasEdgeDraft edge)
    {
        document.Edges.Remove(edge);
        if (editingEdgeId == edge.Id)
        {
            ResetEdgeEditor();
        }

        return Task.CompletedTask;
    }

    private Task EditEdgeAsync(WorkflowCanvasEdgeDraft edge)
    {
        editingEdgeId = edge.Id;
        edgeSourceNodeId = edge.SourceNodeId.Value;
        edgeTargetNodeId = edge.TargetNodeId.Value;
        edgeKind = edge.Kind;
        edgeCondition = edge.ConditionExpression;
        ApplyRouteToEditor(edge.Routing);
        return Task.CompletedTask;
    }

    private Task CancelEdgeEditAsync()
    {
        ResetEdgeEditor();
        return Task.CompletedTask;
    }

    private Task RemoveSelectedNodeAsync()
    {
        var selected = SelectedNode;
        return selected is null
            ? Task.CompletedTask
            : RemoveNodeAsync(selected);
    }

    private Task RemoveNodeAsync(WorkflowCanvasNodeDraft node)
    {
        errorMessage = string.Empty;
        if (node.Kind is WorkflowNodeKind.Start or WorkflowNodeKind.End)
        {
            return Task.CompletedTask;
        }

        var incomingEdges = document.Edges
            .Where(edge => edge.TargetNodeId == node.Id)
            .ToArray();
        var outgoingEdges = document.Edges
            .Where(edge => edge.SourceNodeId == node.Id)
            .ToArray();
        var bridge = ResolveRemovalBridge(node, incomingEdges, outgoingEdges);

        document.Nodes.Remove(node);
        document.Edges.RemoveAll(edge => edge.SourceNodeId == node.Id || edge.TargetNodeId == node.Id);
        if (bridge is { } bridgeEdge &&
            !document.Edges.Any(edge => edge.SourceNodeId == bridgeEdge.SourceNodeId && edge.TargetNodeId == bridgeEdge.TargetNodeId))
        {
            document.Edges.Add(new WorkflowCanvasEdgeDraft(
                CreateEdgeId(bridgeEdge.SourceNodeId, bridgeEdge.TargetNodeId),
                bridgeEdge.SourceNodeId,
                bridgeEdge.TargetNodeId)
            {
                Kind = bridgeEdge.Kind,
                ConditionExpression = bridgeEdge.ConditionExpression,
                Routing = bridgeEdge.Routing
            });
            NotificationService.Info(
                "Workflow route reconnected",
                $"{ResolveNodeName(bridgeEdge.SourceNodeId)} -> {ResolveNodeName(bridgeEdge.TargetNodeId)}");
        }
        else if (bridge is { } existingBridge)
        {
            NotificationService.Info(
                "Workflow route already connected",
                $"{ResolveNodeName(existingBridge.SourceNodeId)} -> {ResolveNodeName(existingBridge.TargetNodeId)}");
        }
        else if (incomingEdges.Length > 0 || outgoingEdges.Length > 0)
        {
            errorMessage = "Removed node had branching or incomplete routes. Connect the remaining nodes manually before running the workflow.";
            NotificationService.Warning("Workflow route needs attention", errorMessage);
        }

        if (decisionRouteEditorNodeId == node.Id.Value)
        {
            ResetDecisionRouteEditor();
        }

        isNodeDetailsDialogOpen = isNodeDetailsDialogOpen && SelectedNode is not null && SelectedNode != node;
        SelectNode(bridge?.TargetNodeId.Value ?? document.StartNodeId.Value);
        SyncEdgeDefaults();
        return Task.CompletedTask;
    }

    private RemovalBridge? ResolveRemovalBridge(
        WorkflowCanvasNodeDraft node,
        IReadOnlyList<WorkflowCanvasEdgeDraft> incomingEdges,
        IReadOnlyList<WorkflowCanvasEdgeDraft> outgoingEdges)
    {
        if (incomingEdges.Count != 1 || outgoingEdges.Count != 1)
        {
            return null;
        }

        var incoming = incomingEdges[0];
        var outgoing = outgoingEdges[0];
        if (incoming.SourceNodeId == outgoing.TargetNodeId)
        {
            return null;
        }

        if (WouldCreateCycle(incoming.SourceNodeId, outgoing.TargetNodeId, node.Id))
        {
            return null;
        }

        var routeSource = incoming.Routing.Kind != WorkflowRouteKind.Always
            ? incoming
            : outgoing.Routing.Kind != WorkflowRouteKind.Always
                ? outgoing
                : incoming;
        return new RemovalBridge(
            incoming.SourceNodeId,
            outgoing.TargetNodeId,
            ResolveEdgeKindForRoute(routeSource.Routing.Kind),
            routeSource.ConditionExpression,
            routeSource.Routing);
    }

    private bool WouldCreateCycle(
        WorkflowNodeId sourceNodeId,
        WorkflowNodeId targetNodeId,
        WorkflowNodeId removedNodeId)
    {
        var pending = new Queue<WorkflowNodeId>();
        var visited = new HashSet<WorkflowNodeId>();
        pending.Enqueue(targetNodeId);
        while (pending.Count > 0)
        {
            var current = pending.Dequeue();
            if (!visited.Add(current))
            {
                continue;
            }

            if (current == sourceNodeId)
            {
                return true;
            }

            foreach (var edge in document.Edges)
            {
                if (edge.SourceNodeId == removedNodeId || edge.TargetNodeId == removedNodeId)
                {
                    continue;
                }

                if (edge.SourceNodeId == current)
                {
                    pending.Enqueue(edge.TargetNodeId);
                }
            }
        }

        return false;
    }

    private async Task ValidateAsync()
    {
        await ValidateCurrentDefinitionAsync();
    }

    private async Task SaveAsync()
    {
        if (isBusy)
        {
            return;
        }

        isBusy = true;
        errorMessage = string.Empty;
        try
        {
            var definition = WorkflowCanvasDefinitionMapper.ToDefinition(document);
            var saved = await CatalogService.SaveDefinitionAsync(new WorkflowDefinitionSaveRequest(
                document.DefinitionId,
                document.VersionId,
                definition.Name,
                definition.Description,
                definition.Status,
                definition.Graph,
                definition.RuntimePolicy));
            document = WorkflowCanvasDefinitionMapper.FromDefinition(saved, componentOptions);
            loadedDefinitionKey = $"{saved.Id}:{saved.VersionId}";
            SelectNode(document.StartNodeId.Value);
            validationIssues = (await CatalogService.ValidateDefinitionAsync(saved)).Issues;
            NotificationService.Success("Workflow saved", saved.Name);
            await DefinitionSaved.InvokeAsync(saved);
        }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentException)
        {
            errorMessage = exception.Message;
            NotificationService.Error("Workflow save failed", exception.Message);
        }
        finally
        {
            isBusy = false;
        }
    }

    private async Task RunPreviewAsync()
    {
        if (isBusy || isTesting)
        {
            return;
        }

        var definition = WorkflowCanvasDefinitionMapper.ToDefinition(document);
        var requirements = WorkflowPreviewInputSupport.Analyze(definition, executorDescriptors);
        if (requirements.NeedsPreviewDialog)
        {
            await OpenPreviewInputDialogAsync(definition, requirements);
            return;
        }

        await RunPreviewCoreAsync(definition, testInputJson, WorkflowPreviewSimulationPlan.Empty);
    }

    private async Task OpenPreviewInputDialogAsync(
        WorkflowDefinition definition,
        WorkflowPreviewRequirements requirements)
    {
        previewInputDefinition = definition;
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

    private async Task StartPreviewFromInputDialogAsync()
    {
        if (previewInputDefinition is null)
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
        var definition = previewInputDefinition;
        isPreviewInputDialogOpen = false;
        previewInputDefinition = null;
        await RunPreviewCoreAsync(definition, inputJson, simulationPlan);
    }

    private void ClosePreviewInputDialog()
    {
        isPreviewInputDialogOpen = false;
        previewInputDefinition = null;
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
        => $"workflow-canvas-preview-simulate-{requirement.NodeId.Value}";

    private IReadOnlyList<WorkflowRuntimeBackendDescriptor> RuntimeBackendOptions
        => RuntimeBackendCatalog.ListBackends();

    private static string BuildRuntimeBackendOptionText(WorkflowRuntimeBackendDescriptor backend)
        => backend.IsRunnable
            ? backend.Kind.ToString()
            : $"{backend.Kind} ({backend.Availability})";

    private async Task RunPreviewCoreAsync(
        WorkflowDefinition definition,
        string inputJson,
        WorkflowPreviewSimulationPlan simulationPlan)
    {
        isBusy = true;
        isTesting = true;
        errorMessage = string.Empty;
        try
        {
            using var progressScope = WorkflowNodeExecutionProgressScope.Push(new CanvasPreviewNodeSelectionObserver(this));
            testResult = await TestRunner.RunAsync(
                new WorkflowTestRunRequest(
                    WorkflowId: null,
                    VersionId: null,
                    DraftDefinition: definition,
                    InputJson: inputJson,
                    RequestedBackend: WorkflowRuntimeBackendKind.InProcess,
                    ValidateOnly: false)
                {
                    PreviewSimulationPlan = simulationPlan
                });
            validationIssues = testResult.Validation.Issues;
            if (testResult.Run is not null)
            {
                await PreviewRunCompleted.InvokeAsync(testResult.Run);
            }

            if (!testResult.Succeeded)
            {
                errorMessage = WorkflowFailureDisplayFormatter.ToUserMessage(testResult.ErrorMessage);
                NotificationService.Error("Workflow preview failed", errorMessage);
                return;
            }

            NotificationService.Success("Workflow preview completed", testResult.Run?.Summary ?? "Workflow preview completed.");
        }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentException)
        {
            errorMessage = exception.Message;
            NotificationService.Error("Workflow preview failed", exception.Message);
        }
        finally
        {
            isTesting = false;
            isBusy = false;
        }
    }

    private Task SelectPreviewNodeAsync(WorkflowNodeId nodeId)
    {
        if (!document.Nodes.Any(node => node.Id == nodeId))
        {
            return Task.CompletedTask;
        }

        SelectNode(nodeId.Value);
        StateHasChanged();
        return Task.CompletedTask;
    }

    private async Task<WorkflowDefinition> ValidateCurrentDefinitionAsync()
    {
        var definition = WorkflowCanvasDefinitionMapper.ToDefinition(document);
        var validation = await CatalogService.ValidateDefinitionAsync(definition);
        validationIssues = validation.Issues;
        if (validation.Succeeded)
        {
            NotificationService.Success("Workflow canvas valid", "The current workflow canvas has no validation issues.");
        }
        else
        {
            NotificationService.Warning("Workflow canvas has issues", $"{validation.Issues.Count} validation issue(s) found.");
        }

        return definition;
    }

    private async Task HandleCanvasSelectionChangedAsync(CanvasWorkbenchSelectionChangedEventArgs args)
    {
        selectedNodeId = args.PrimaryNodeId ?? args.SelectedNodeIds.FirstOrDefault();
        canvasUiState.SelectedNodeIds = string.IsNullOrWhiteSpace(selectedNodeId)
            ? []
            : [selectedNodeId];

        if (IsHttpExecutorNode(SelectedNode))
        {
            await LoadSecretPickerItemsAsync();
        }
    }

    private Task HandleCanvasStateChangedAsync(string stateJson)
    {
        canvasUiState = CanvasWorkbenchUiState.Parse(stateJson);
        selectedNodeId = canvasUiState.SelectedNodeIds.FirstOrDefault();
        canvasUiState.ActiveInspectorTab = "workflow";
        return Task.CompletedTask;
    }

    private Task HandleCanvasNodesMovedAsync(CanvasWorkbenchNodesMovedEventArgs args)
    {
        foreach (var position in args.Positions)
        {
            var node = document.Nodes.FirstOrDefault(item => item.Id.Value == position.NodeId);
            if (node is null)
            {
                continue;
            }

            node.CanvasX = position.X;
            node.CanvasY = position.Y;
        }

        return Task.CompletedTask;
    }

    private async Task HandleCanvasCreateActionAsync(CanvasWorkbenchCreateActionRequest request)
    {
        try
        {
            if (WorkflowCanvasDecisionCatalog.TryParseCreateActionId(request.ActionId, out var decisionKind))
            {
                await AddDecisionNodeAsync(decisionKind, request);
                return;
            }

            if (WorkflowCanvasDefinitionMapper.TryParseCreateActionId(request.ActionId, out var kind))
            {
                await AddNodeAsync(kind, request, ResolveRequestedComponent(request));
                return;
            }

            if (WorkflowExecutorCanvasCatalog.TryParseCreateActionId(request.ActionId, out var executorId) &&
                TryResolveExecutorDescriptor(executorId, out var descriptor))
            {
                await AddExecutorNodeAsync(descriptor, request);
            }
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or JsonException)
        {
            errorMessage = exception.Message;
            NotificationService.Error("Workflow node create failed", exception.Message);
        }
    }

    private Task HandleCanvasNodeEditedAsync(CanvasWorkbenchNodeEditRequest request)
    {
        var node = document.Nodes.FirstOrDefault(item => item.Id.Value == request.NodeId);
        if (node is null)
        {
            return Task.CompletedTask;
        }

        node.Name = request.Title;
        node.Instructions = request.Notes;
        return Task.CompletedTask;
    }

    private async Task HandleCanvasContextActionAsync(CanvasWorkbenchContextActionRequest request)
    {
        if (string.Equals(request.TargetKind, CanvasLinkTargetKind, StringComparison.Ordinal))
        {
            await HandleCanvasLinkContextActionAsync(request);
            return;
        }

        if (string.IsNullOrWhiteSpace(request.NodeId))
        {
            return;
        }

        var node = document.Nodes.FirstOrDefault(item => item.Id.Value == request.NodeId);
        if (node is null)
        {
            return;
        }

        SelectNode(node.Id.Value);
        switch (request.ActionId)
        {
            case WorkflowCanvasDefinitionMapper.EditNodeActionId:
                ResetDecisionRouteEditor();
                isNodeDetailsDialogOpen = true;
                break;
            case WorkflowCanvasDefinitionMapper.AddDecisionRouteActionId when IsDecisionNode(node):
                isNodeDetailsDialogOpen = true;
                BeginDecisionRouteEdit(node, routeEdge: null);
                break;
            case WorkflowCanvasDefinitionMapper.RemoveNodeActionId:
            case CanvasDeleteNodeActionId:
                await RemoveNodeAsync(node);
                break;
        }
    }

    private Task HandleCanvasLinkContextActionAsync(CanvasWorkbenchContextActionRequest request)
    {
        return request.ActionId switch
        {
            CanvasCreateConnectionActionId => CreateEdgeFromCanvasConnectionAsync(request),
            CanvasDeleteLinkActionId => RemoveEdgeFromCanvasConnectionAsync(request),
            _ => Task.CompletedTask
        };
    }

    private Task CreateEdgeFromCanvasConnectionAsync(CanvasWorkbenchContextActionRequest request)
    {
        errorMessage = string.Empty;
        var hasSource = TryResolveCanvasConnectionEndpoint(request.LinkSourceId, isSource: true, out var sourceNode, out var sourceError);
        var hasTarget = TryResolveCanvasConnectionEndpoint(request.LinkTargetId, isSource: false, out var targetNode, out var targetError);
        if (!hasSource || !hasTarget)
        {
            errorMessage = hasSource ? targetError : sourceError;
            NotificationService.Warning("Workflow connection failed", errorMessage);
            return Task.CompletedTask;
        }

        if (sourceNode.Id == targetNode.Id)
        {
            errorMessage = "Choose different source and target nodes for the workflow edge.";
            NotificationService.Warning("Workflow connection failed", errorMessage);
            return Task.CompletedTask;
        }

        if (document.Edges.Any(edge => edge.SourceNodeId == sourceNode.Id && edge.TargetNodeId == targetNode.Id))
        {
            errorMessage = "That workflow edge already exists.";
            NotificationService.Warning("Workflow connection failed", errorMessage);
            return Task.CompletedTask;
        }

        document.Edges.Add(new WorkflowCanvasEdgeDraft(
            CreateEdgeId(sourceNode.Id, targetNode.Id),
            sourceNode.Id,
            targetNode.Id));
        SelectNode(targetNode.Id.Value);
        ResetEdgeEditor();
        NotificationService.Success("Workflow edge connected", $"{sourceNode.Name} -> {targetNode.Name}");
        return Task.CompletedTask;
    }

    private Task RemoveEdgeFromCanvasConnectionAsync(CanvasWorkbenchContextActionRequest request)
    {
        errorMessage = string.Empty;
        if (string.IsNullOrWhiteSpace(request.LinkSourceId) ||
            string.IsNullOrWhiteSpace(request.LinkTargetId))
        {
            errorMessage = "Choose a workflow edge before removing it.";
            NotificationService.Warning("Workflow edge removal failed", errorMessage);
            return Task.CompletedTask;
        }

        var edge = document.Edges.FirstOrDefault(item =>
            string.Equals(item.SourceNodeId.Value, request.LinkSourceId, StringComparison.Ordinal) &&
            string.Equals(item.TargetNodeId.Value, request.LinkTargetId, StringComparison.Ordinal));
        if (edge is null)
        {
            errorMessage = "The selected workflow edge no longer exists.";
            NotificationService.Warning("Workflow edge removal failed", errorMessage);
            return Task.CompletedTask;
        }

        document.Edges.Remove(edge);
        if (editingEdgeId == edge.Id)
        {
            ResetEdgeEditor();
        }

        NotificationService.Info("Workflow edge removed", $"{ResolveNodeName(edge.SourceNodeId)} -> {ResolveNodeName(edge.TargetNodeId)}");
        return Task.CompletedTask;
    }

    private bool TryResolveCanvasConnectionEndpoint(
        string? nodeId,
        bool isSource,
        out WorkflowCanvasNodeDraft node,
        out string error)
    {
        node = null!;
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(nodeId))
        {
            error = isSource
                ? "Choose a source workflow node for the edge."
                : "Choose a target workflow node for the edge.";
            return false;
        }

        node = document.Nodes.FirstOrDefault(item => string.Equals(item.Id.Value, nodeId, StringComparison.Ordinal))!;
        if (node is null)
        {
            error = $"Workflow node '{nodeId}' does not exist.";
            return false;
        }

        if (isSource && node.Kind == WorkflowNodeKind.End)
        {
            error = "End nodes cannot start workflow edges.";
            return false;
        }

        if (!isSource && node.Kind == WorkflowNodeKind.Start)
        {
            error = "Start nodes cannot receive workflow edges.";
            return false;
        }

        return true;
    }

    private void HandleNameChanged(ChangeEventArgs args)
    {
        document.Name = args.Value?.ToString() ?? string.Empty;
    }

    private void HandleDescriptionChanged(ChangeEventArgs args)
    {
        document.Description = args.Value?.ToString() ?? string.Empty;
    }

    private void HandleRuntimeBackendChanged(ChangeEventArgs args)
    {
        if (!Enum.TryParse<WorkflowRuntimeBackendKind>(args.Value?.ToString(), out var backend))
        {
            return;
        }

        var backendDescriptor = RuntimeBackendCatalog.GetRequiredBackend(backend);
        if (!backendDescriptor.IsRunnable)
        {
            NotificationService.Warning("Runtime backend unavailable", backendDescriptor.AvailabilityReason);
            return;
        }

        document.RuntimePolicy = document.RuntimePolicy with
        {
            PreferredBackend = backend,
            RequireDurableProductionRuns = backend != WorkflowRuntimeBackendKind.InProcess
        };
    }

    private void HandleNewComponentProviderChanged(ChangeEventArgs args)
    {
        newComponentProviderProfileId = args.Value?.ToString() ?? string.Empty;
        newComponentModel = ResolveDefaultModel(ResolveSelectedNewComponentProvider());
    }

    private Task HandleNewComponentModelChangedAsync(string? model)
    {
        newComponentModel = string.IsNullOrWhiteSpace(model)
            ? string.Empty
            : model.Trim();
        return Task.CompletedTask;
    }

    private void HandleSelectedNodeNameChanged(WorkflowCanvasNodeDraft node, ChangeEventArgs args)
    {
        node.Name = args.Value?.ToString() ?? string.Empty;
    }

    private void HandleSelectedInstructionsChanged(WorkflowCanvasNodeDraft node, ChangeEventArgs args)
    {
        node.Instructions = args.Value?.ToString() ?? string.Empty;
    }

    private void HandleSelectedNodeKindChanged(WorkflowCanvasNodeDraft node, ChangeEventArgs args)
    {
        if (!Enum.TryParse<WorkflowNodeKind>(args.Value?.ToString(), out var kind))
        {
            return;
        }

        node.Kind = kind;
        if (string.IsNullOrWhiteSpace(node.Instructions))
        {
            node.Instructions = WorkflowCanvasDefinitionMapper.ResolveDefaultInstructions(kind);
        }

        if (kind == WorkflowNodeKind.HumanInput)
        {
            node.ExternalRequestKind ??= WorkflowExternalRequestKind.HumanInput;
        }

        if (kind == WorkflowNodeKind.Executor)
        {
            ApplySelectedExecutor(node, ResolveDefaultExecutorDescriptor());
            return;
        }

        node.ExecutorId = null;
        node.ExecutorSettingsJson = string.Empty;
        node.ExecutionPolicy = null;
    }

    private void HandleSelectedComponentChanged(WorkflowCanvasNodeDraft node, ChangeEventArgs args)
    {
        if (!Guid.TryParse(args.Value?.ToString(), out var componentGuid))
        {
            node.ComponentId = null;
            return;
        }

        var componentId = new WorkflowComponentId(componentGuid);
        var component = componentOptions.FirstOrDefault(item => item.Id == componentId);
        if (component is not null)
        {
            WorkflowCanvasDefinitionMapper.ApplyComponent(node, component);
        }
    }

    private void HandleSelectedRequestKindChanged(WorkflowCanvasNodeDraft node, ChangeEventArgs args)
    {
        if (Enum.TryParse<WorkflowExternalRequestKind>(args.Value?.ToString(), out var requestKind))
        {
            node.ExternalRequestKind = requestKind;
        }
    }

    private void HandleSelectedAgentIdChanged(WorkflowCanvasNodeDraft node, ChangeEventArgs args)
    {
        node.AgentId = Guid.TryParse(args.Value?.ToString(), out var agentId)
            ? agentId
            : null;
    }

    private void HandleSelectedSubworkflowIdChanged(WorkflowCanvasNodeDraft node, ChangeEventArgs args)
    {
        node.SubworkflowId = Guid.TryParse(args.Value?.ToString(), out var workflowId)
            ? new WorkflowId(workflowId)
            : null;
    }

    private void HandleSelectedInputShapeChanged(WorkflowCanvasNodeDraft node, ChangeEventArgs args)
    {
        if (Enum.TryParse<WorkflowValueShapeKind>(args.Value?.ToString(), out var shape))
        {
            node.InputShapeKind = shape;
        }
    }

    private void HandleSelectedResultShapeChanged(WorkflowCanvasNodeDraft node, ChangeEventArgs args)
    {
        if (Enum.TryParse<WorkflowValueShapeKind>(args.Value?.ToString(), out var shape))
        {
            node.ResultShapeKind = shape;
        }
    }

    private void SelectNode(string nodeId)
    {
        selectedNodeId = nodeId;
        canvasUiState.SelectedNodeIds = [nodeId];
    }

    private async Task HandleCanvasNodeOpenedAsync(string nodeId)
    {
        SelectNode(nodeId);
        if (IsHttpExecutorNode(SelectedNode))
        {
            await LoadSecretPickerItemsAsync();
        }

        isNodeDetailsDialogOpen = SelectedNode is not null;
    }

    private async Task OpenSelectedNodeDetailsAsync()
    {
        if (IsHttpExecutorNode(SelectedNode))
        {
            await LoadSecretPickerItemsAsync();
        }

        isNodeDetailsDialogOpen = SelectedNode is not null;
    }

    private Task CloseNodeDetailsDialogAsync()
    {
        isNodeDetailsDialogOpen = false;
        return Task.CompletedTask;
    }

    private async Task OpenLlmComponentCreateAsync(LlmCallComponent component)
    {
        var actionId = WorkflowCanvasDefinitionMapper.BuildCreateActionId(WorkflowNodeKind.LlmCall);
        await OpenCreateComposerAsync(
            actionId,
            title: component.Name,
            notes: component.Instructions,
            objectSubtype: component.Id.Value.ToString("D"));
    }

    private Task HandleWorkflowToolboxItemSelectedAsync(string actionId)
        => OpenCreateComposerAsync(actionId);

    private async Task OpenCreateComposerAsync(
        string actionId,
        string? title = null,
        string? notes = null,
        string? objectSubtype = null)
    {
        if (!TryResolveCreateAction(actionId, out var action))
        {
            errorMessage = $"Workflow create action '{actionId}' is not registered.";
            return;
        }

        if (workbenchRef is null)
        {
            errorMessage = "Workflow canvas is not ready for modal creation yet.";
            return;
        }

        var request = BuildCreateActionRequest(action, title, notes, objectSubtype);
        await workbenchRef.OpenCreateDialogAsync(action, request);
    }

    private bool TryResolveCreateAction(string actionId, out CanvasWorkbenchAction action)
    {
        foreach (var candidate in CanvasSurface.Chrome.QuickCreateActions)
        {
            if (TryResolveCreateAction(candidate, actionId, out action))
            {
                return true;
            }
        }

        action = default!;
        return false;
    }

    private static bool TryResolveCreateAction(
        CanvasWorkbenchAction candidate,
        string actionId,
        out CanvasWorkbenchAction action)
    {
        if (string.Equals(candidate.ActionId, actionId, StringComparison.Ordinal))
        {
            action = candidate;
            return true;
        }

        foreach (var child in candidate.Children)
        {
            if (TryResolveCreateAction(child, actionId, out action))
            {
                return true;
            }
        }

        action = default!;
        return false;
    }

    private CanvasWorkbenchCreateActionRequest BuildCreateActionRequest(
        CanvasWorkbenchAction action,
        string? title,
        string? notes,
        string? objectSubtype)
    {
        var position = ResolveCreatePosition(request: null);
        return new CanvasWorkbenchCreateActionRequest(
            action.ActionId,
            SourceNodeId: selectedNodeId,
            X: position.X,
            Y: position.Y,
            ParentNodeId: selectedNodeId,
            Title: string.IsNullOrWhiteSpace(title) ? action.Label : title.Trim(),
            Subtitle: action.Description,
            Notes: string.IsNullOrWhiteSpace(notes) ? action.Description : notes.Trim(),
            PlacementKind: "child",
            CreateMode: "dialog",
            ObjectSubtype: string.IsNullOrWhiteSpace(objectSubtype) ? action.ObjectSubtype : objectSubtype.Trim(),
            UploadedFile: null);
    }

    private void ExpandWorkflowToolboxGroup(string groupKey)
    {
        if (!HasWorkflowToolboxSearch)
        {
            expandedWorkflowToolboxGroupKey = groupKey;
        }
    }

    private bool HasWorkflowToolboxSearch
        => !string.IsNullOrWhiteSpace(workflowToolboxSearchText);

    private IReadOnlyList<OverlayToolboxBadge> WorkflowToolboxBadges
        =>
        [
            new("Workflow", "info"),
            new($"{executorDescriptors.Count(executor => executor.CanExecute)} executors", "label")
        ];

    private IReadOnlyList<OverlayToolboxSection> WorkflowToolboxSections
        => BuildWorkflowToolboxSections();

    private IReadOnlyList<OverlayToolboxSection> BuildWorkflowToolboxSections()
    {
        var decisionItems = WorkflowCanvasDecisionCatalog.DecisionBlockKinds
            .Select(kind =>
            {
                var action = WorkflowCanvasDecisionCatalog.BuildCreateAction(kind);
                return new OverlayToolboxItem(
                    action.ActionId,
                    action.Label,
                    action.Description,
                    Icon: action.Icon,
                    Tone: action.Tone,
                    DataTestId: $"workflow-toolbox-decision-{kind}");
            })
            .Where(MatchesWorkflowToolboxSearch)
            .ToList();

        var workflowNodeItems = WorkflowCanvasDefinitionMapper.CreatableNodeKinds
            .Select(kind => new OverlayToolboxItem(
                WorkflowCanvasDefinitionMapper.BuildCreateActionId(kind),
                WorkflowCanvasDefinitionMapper.ResolveDefaultNodeName(kind),
                WorkflowCanvasDefinitionMapper.ResolveDefaultInstructions(kind),
                Icon: ResolveWorkflowToolboxNodeIcon(kind),
                Tone: ResolveWorkflowToolboxNodeTone(kind),
                DataTestId: $"workflow-toolbox-node-{kind}"))
            .Where(MatchesWorkflowToolboxSearch)
            .ToList();

        var builtInExecutorGroups = executorDescriptors
            .Where(executor => !WorkflowExecutorCanvasCatalog.IsPluginExecutor(executor))
            .GroupBy(executor => executor.Category)
            .OrderBy(group => group.Key)
            .Select(group => BuildExecutorToolboxGroup(group.Key, group))
            .Where(group => group.Items.Count > 0)
            .ToList();
        var pluginExecutorGroups = executorDescriptors
            .Where(WorkflowExecutorCanvasCatalog.IsPluginExecutor)
            .GroupBy(executor => executor.Source.PluginId, StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => WorkflowExecutorCanvasCatalog.ResolvePluginDisplayName(group), StringComparer.OrdinalIgnoreCase)
            .Select(BuildPluginExecutorToolboxGroup)
            .Where(group => group.Items.Count > 0)
            .ToList();
        var executorGroups = builtInExecutorGroups
            .Concat(pluginExecutorGroups)
            .ToList();

        var sections = new List<OverlayToolboxSection>();
        if (decisionItems.Count > 0)
        {
            sections.Add(new OverlayToolboxSection(
                "workflow-decisions",
                "Decisions",
                "IF/ELSE, SWITCH/default, and fan-out split blocks.",
                [
                    new OverlayToolboxGroup(
                        "workflow-decisions",
                        "Branching blocks",
                        "Drop a configured split node with default branches.",
                        decisionItems,
                        Icon: "call_split",
                        Tone: "accent",
                        IsExpanded: IsWorkflowToolboxGroupExpanded("workflow-decisions"),
                        DataTestId: "workflow-toolbox-group-workflow-decisions",
                        BodyDataTestId: "workflow-toolbox-group-body-workflow-decisions")
                ],
                Tone: "accent",
                DataTestId: "workflow-toolbox-section-decisions"));
        }

        if (workflowNodeItems.Count > 0)
        {
            sections.Add(new OverlayToolboxSection(
                "workflow-nodes",
                "Workflow nodes",
                "Control, AI, human, artifact, and orchestration steps.",
                [
                    new OverlayToolboxGroup(
                        "workflow-nodes",
                        "Typed nodes",
                        "Core workflow node kinds",
                        workflowNodeItems,
                        Icon: "account_tree",
                        Tone: "info",
                        IsExpanded: IsWorkflowToolboxGroupExpanded("workflow-nodes"),
                        DataTestId: "workflow-toolbox-group-workflow-nodes",
                        BodyDataTestId: "workflow-toolbox-group-body-workflow-nodes")
                ],
                Tone: "info",
                DataTestId: "workflow-toolbox-section-nodes"));
        }

        if (executorGroups.Count > 0)
        {
            sections.Add(new OverlayToolboxSection(
                "workflow-executors",
                "Executors",
                "Typed tool execution nodes backed by the executor catalog.",
                executorGroups,
                Tone: "accent",
                DataTestId: "workflow-toolbox-section-executors"));
        }

        if (sections.Count == 0)
        {
            expandedWorkflowToolboxGroupKey = null;
        }

        return sections;
    }

    private OverlayToolboxGroup BuildExecutorToolboxGroup(
        WorkflowExecutorCategoryKind category,
        IEnumerable<WorkflowExecutorDescriptor> descriptors)
    {
        var items = descriptors
            .OrderBy(executor => executor.CanExecute ? 0 : 1)
            .ThenBy(executor => executor.Name, StringComparer.OrdinalIgnoreCase)
            .Select(executor => new OverlayToolboxItem(
                WorkflowExecutorCanvasCatalog.BuildCreateActionId(executor.Id),
                executor.Name,
                WorkflowExecutorCanvasCatalog.BuildExecutorSummary(executor),
                Icon: executor.IconName,
                Tone: WorkflowExecutorCanvasCatalog.ResolveTone(executor.Category),
                IsDisabled: !executor.CanExecute,
                DataTestId: $"workflow-toolbox-executor-{executor.Id.Value.Replace('.', '-')}"))
            .Where(MatchesWorkflowToolboxSearch)
            .ToList();

        var key = $"executor-{category}";
        return new OverlayToolboxGroup(
            key,
            WorkflowExecutorCanvasCatalog.ResolveCategoryLabel(category),
            WorkflowExecutorCanvasCatalog.ResolveCategoryDescription(category),
            items,
            Icon: WorkflowExecutorCanvasCatalog.ResolveCategoryIcon(category),
            Tone: WorkflowExecutorCanvasCatalog.ResolveTone(category),
            IsExpanded: IsWorkflowToolboxGroupExpanded(key),
            DataTestId: $"workflow-toolbox-group-{key}",
            BodyDataTestId: $"workflow-toolbox-group-body-{key}");
    }

    private OverlayToolboxGroup BuildPluginExecutorToolboxGroup(
        IEnumerable<WorkflowExecutorDescriptor> descriptors)
    {
        var materialized = descriptors.ToList();
        var pluginName = WorkflowExecutorCanvasCatalog.ResolvePluginDisplayName(materialized);
        var pluginKey = materialized
            .Select(executor => executor.Source.PluginId)
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? SlugForDataTestId(pluginName);
        var items = materialized
            .OrderBy(executor => executor.CanExecute ? 0 : 1)
            .ThenBy(executor => executor.Name, StringComparer.OrdinalIgnoreCase)
            .Select(executor => new OverlayToolboxItem(
                WorkflowExecutorCanvasCatalog.BuildCreateActionId(executor.Id),
                executor.Name,
                WorkflowExecutorCanvasCatalog.BuildExecutorSummary(executor),
                Icon: executor.IconName,
                Tone: "accent",
                IsDisabled: !executor.CanExecute,
                DataTestId: $"workflow-toolbox-plugin-executor-{SlugForDataTestId(executor.Id.Value)}"))
            .Where(MatchesWorkflowToolboxSearch)
            .ToList();
        var key = $"executor-plugin-{SlugForDataTestId(pluginKey)}";

        return new OverlayToolboxGroup(
            key,
            pluginName,
            $"Executors contributed by {pluginName}.",
            items,
            Icon: WorkflowExecutorCanvasCatalog.ResolvePluginIconName(materialized),
            Tone: "accent",
            IsExpanded: IsWorkflowToolboxGroupExpanded(key),
            DataTestId: $"workflow-toolbox-group-{key}",
            BodyDataTestId: $"workflow-toolbox-group-body-{key}");
    }

    private static string SlugForDataTestId(string value)
    {
        var builder = new StringBuilder(value.Length);
        var lastWasSeparator = false;
        foreach (var character in value)
        {
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(char.ToLowerInvariant(character));
                lastWasSeparator = false;
                continue;
            }

            if (!lastWasSeparator && builder.Length > 0)
            {
                builder.Append('-');
                lastWasSeparator = true;
            }
        }

        var slug = builder.ToString().Trim('-');
        return string.IsNullOrWhiteSpace(slug) ? "plugin" : slug;
    }

    private bool MatchesWorkflowToolboxSearch(OverlayToolboxItem item)
    {
        if (!HasWorkflowToolboxSearch)
        {
            return true;
        }

        var search = workflowToolboxSearchText.Trim();
        return item.Label.Contains(search, StringComparison.OrdinalIgnoreCase) ||
               item.Summary.Contains(search, StringComparison.OrdinalIgnoreCase) ||
               item.ActionId.Contains(search, StringComparison.OrdinalIgnoreCase);
    }

    private bool IsWorkflowToolboxGroupExpanded(string groupKey)
    {
        if (HasWorkflowToolboxSearch)
        {
            return true;
        }

        expandedWorkflowToolboxGroupKey ??= groupKey;
        return string.Equals(expandedWorkflowToolboxGroupKey, groupKey, StringComparison.Ordinal);
    }

    private WorkflowExecutorDescriptor? ResolveDefaultExecutorDescriptor()
        => executorDescriptors.FirstOrDefault(executor => executor.CanExecute)
           ?? executorDescriptors.FirstOrDefault();

    private bool TryResolveExecutorDescriptor(
        WorkflowExecutorId executorId,
        out WorkflowExecutorDescriptor descriptor)
    {
        descriptor = executorDescriptors.FirstOrDefault(item => item.Id == executorId)!;
        return descriptor is not null;
    }

    private WorkflowExecutorDescriptor? ResolveSelectedExecutorDescriptor(WorkflowCanvasNodeDraft node)
        => node.ExecutorId.HasValue && TryResolveExecutorDescriptor(node.ExecutorId.Value, out var descriptor)
            ? descriptor
            : null;

    private void HandleSelectedExecutorChanged(WorkflowCanvasNodeDraft node, ChangeEventArgs args)
    {
        var rawValue = args.Value?.ToString();
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            node.ExecutorId = null;
            node.ExecutorSettingsJson = string.Empty;
            node.ExecutionPolicy = null;
            return;
        }

        var executorId = new WorkflowExecutorId(rawValue);
        if (TryResolveExecutorDescriptor(executorId, out var descriptor))
        {
            ApplySelectedExecutor(node, descriptor);
        }
    }

    private static void ApplySelectedExecutor(
        WorkflowCanvasNodeDraft node,
        WorkflowExecutorDescriptor? descriptor)
    {
        if (descriptor is null)
        {
            return;
        }

        WorkflowCanvasDefinitionMapper.ApplyExecutor(node, descriptor);
    }

    private Task AddDecisionNodeAsync(
        WorkflowDecisionBlockKind decisionKind,
        CanvasWorkbenchCreateActionRequest request)
    {
        var position = ResolveCreatePosition(request);
        var node = WorkflowCanvasDefinitionMapper.CreateNode(
            WorkflowNodeKind.Triage,
            document.Nodes,
            componentOptions,
            position.X,
            position.Y);

        node.Name = WorkflowCanvasDecisionCatalog.ResolveLabel(decisionKind);
        node.Instructions = ResolveDecisionInstructions(decisionKind);
        node.InputShapeKind = WorkflowValueShapeKind.Json;
        node.ResultShapeKind = WorkflowValueShapeKind.Json;
        ApplyCreateRequest(node, request);
        if (string.Equals(node.Name, request.Title, StringComparison.Ordinal) &&
            string.IsNullOrWhiteSpace(request.Title))
        {
            node.Name = WorkflowCanvasDecisionCatalog.ResolveLabel(decisionKind);
        }

        document.Nodes.Add(node);
        InsertNodeBeforeEnd(node);
        AddDefaultDecisionBranches(node, decisionKind, request);
        SelectNode(node.Id.Value);
        SyncEdgeDefaults();
        return Task.CompletedTask;
    }

    private void AddDefaultDecisionBranches(
        WorkflowCanvasNodeDraft decisionNode,
        WorkflowDecisionBlockKind decisionKind,
        CanvasWorkbenchCreateActionRequest request)
    {
        var end = document.Nodes.FirstOrDefault(node => node.Kind == WorkflowNodeKind.End);
        document.Edges.RemoveAll(edge => edge.SourceNodeId == decisionNode.Id && edge.TargetNodeId == end?.Id);

        var branchSpecs = BuildDecisionBranchSpecs(decisionKind, request);
        for (var index = 0; index < branchSpecs.Count; index++)
        {
            var spec = branchSpecs[index];
            var branchNode = WorkflowCanvasDefinitionMapper.CreateNode(
                WorkflowNodeKind.StrictLogic,
                document.Nodes,
                componentOptions,
                decisionNode.CanvasX + 300,
                decisionNode.CanvasY + ((index - ((branchSpecs.Count - 1) / 2d)) * 135));
            branchNode.Name = spec.TargetName;
            branchNode.Instructions = spec.TargetInstructions;
            branchNode.InputShapeKind = WorkflowValueShapeKind.Json;
            branchNode.ResultShapeKind = WorkflowValueShapeKind.Json;
            document.Nodes.Add(branchNode);

            document.Edges.Add(new WorkflowCanvasEdgeDraft(
                CreateEdgeId(decisionNode.Id, branchNode.Id),
                decisionNode.Id,
                branchNode.Id)
            {
                Kind = ResolveEdgeKindForRoute(spec.Routing.Kind),
                Routing = spec.Routing
            });

            if (end is not null)
            {
                document.Edges.Add(new WorkflowCanvasEdgeDraft(
                    CreateEdgeId(branchNode.Id, end.Id),
                    branchNode.Id,
                    end.Id));
            }
        }
    }

    private IReadOnlyList<DecisionBranchSpec> BuildDecisionBranchSpecs(
        WorkflowDecisionBlockKind decisionKind,
        CanvasWorkbenchCreateActionRequest request)
    {
        var jsonPath = NormalizeJsonPath(GetInputValue(request, "jsonPath", ResolveDefaultDecisionJsonPath(decisionKind)));
        return decisionKind switch
        {
            WorkflowDecisionBlockKind.IfElse => BuildIfElseBranchSpecs(request, jsonPath),
            WorkflowDecisionBlockKind.Switch => BuildSwitchBranchSpecs(request, jsonPath),
            WorkflowDecisionBlockKind.FanOut => BuildFanOutBranchSpecs(request, jsonPath),
            _ => []
        };
    }

    private static IReadOnlyList<DecisionBranchSpec> BuildIfElseBranchSpecs(
        CanvasWorkbenchCreateActionRequest request,
        string jsonPath)
    {
        var expectedValue = GetInputValue(request, "expectedValue", "approved");
        var expectedJson = JsonSerializer.Serialize(expectedValue);
        var trueLabel = GetInputValue(request, "trueLabel", "IF").ToUpperInvariant();
        var falseLabel = GetInputValue(request, "falseLabel", "ELSE").ToUpperInvariant();
        return
        [
            new DecisionBranchSpec(
                trueLabel,
                $"Handle {trueLabel}",
                $"Continue when {jsonPath} equals {expectedValue}.",
                WorkflowEdgeRouting.Predicate(
                    jsonPath,
                    WorkflowRouteOperator.Equals,
                    expectedJson,
                    WorkflowRouteValueKind.String,
                    trueLabel)),
            new DecisionBranchSpec(
                falseLabel,
                $"Handle {falseLabel}",
                $"Continue when {jsonPath} does not equal {expectedValue}.",
                WorkflowEdgeRouting.Predicate(
                    jsonPath,
                    WorkflowRouteOperator.NotEquals,
                    expectedJson,
                    WorkflowRouteValueKind.String,
                    falseLabel))
        ];
    }

    private static IReadOnlyList<DecisionBranchSpec> BuildSwitchBranchSpecs(
        CanvasWorkbenchCreateActionRequest request,
        string jsonPath)
    {
        var cases = SplitBranchValues(GetInputValue(request, "caseValues", "high, medium, low"));
        if (cases.Count == 0)
        {
            cases = ["case-1", "case-2"];
        }

        var specs = cases
            .Take(6)
            .Select((value, index) => new DecisionBranchSpec(
                $"Case {index + 1}",
                ToBranchTargetName(value),
                $"Handle switch case '{value}' from {jsonPath}.",
                WorkflowEdgeRouting.SwitchCase(
                    jsonPath,
                    JsonSerializer.Serialize(value),
                    WorkflowRouteValueKind.String,
                    $"Case {index + 1}")))
            .ToList();
        var defaultLabel = GetInputValue(request, "defaultLabel", "DEFAULT").ToUpperInvariant();
        specs.Add(new DecisionBranchSpec(
            defaultLabel,
            "Unhandled",
            "Handle values that do not match any configured switch case.",
            WorkflowEdgeRouting.SwitchDefault(defaultLabel)));
        return specs;
    }

    private static IReadOnlyList<DecisionBranchSpec> BuildFanOutBranchSpecs(
        CanvasWorkbenchCreateActionRequest request,
        string jsonPath)
    {
        var branches = SplitBranchValues(GetInputValue(
            request,
            "branchLabels",
            "validate payment, check inventory, reserve shipment, send confirmation"));
        if (branches.Count == 0)
        {
            branches = ["branch-1", "branch-2", "branch-3"];
        }

        return branches
            .Take(8)
            .Select((value, index) => new DecisionBranchSpec(
                $"Fan-out {index + 1}",
                ToBranchTargetName(value),
                $"Run when {jsonPath} contains '{value}'.",
                WorkflowEdgeRouting.FanOutSelector(
                    jsonPath,
                    WorkflowRouteOperator.Contains,
                    JsonSerializer.Serialize(value),
                    WorkflowRouteValueKind.String,
                    index,
                    ToBranchTargetName(value))))
            .ToList();
    }

    private static string ResolveDecisionInstructions(WorkflowDecisionBlockKind kind)
        => kind switch
        {
            WorkflowDecisionBlockKind.IfElse => "Evaluate a deterministic predicate and route to the IF or ELSE branch.",
            WorkflowDecisionBlockKind.Switch => "Evaluate a deterministic discriminator and route to a matching case or default branch.",
            WorkflowDecisionBlockKind.FanOut => "Evaluate a deterministic selector and route to every selected downstream branch.",
            _ => "Route the workflow payload to the correct downstream branch."
        };

    private static string ResolveDefaultDecisionJsonPath(WorkflowDecisionBlockKind kind)
        => kind switch
        {
            WorkflowDecisionBlockKind.Switch => "$.category",
            WorkflowDecisionBlockKind.FanOut => "$.targets",
            _ => "$.status"
        };

    private static string NormalizeJsonPath(string value)
        => string.IsNullOrWhiteSpace(value) ? "$.status" : value.Trim();

    private static string GetInputValue(
        CanvasWorkbenchCreateActionRequest? request,
        string key,
        string fallback)
    {
        if (request?.InputValues is null)
        {
            return fallback;
        }

        var value = request.InputValues.FirstOrDefault(input =>
            string.Equals(input.Key, key, StringComparison.OrdinalIgnoreCase))?.Value;
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }

    private static List<string> SplitBranchValues(string value)
    {
        return value
            .Split([',', ';', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string ToBranchTargetName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "Branch";
        }

        var normalized = value.Trim().Replace('-', ' ').Replace('_', ' ');
        return string.Join(
            " ",
            normalized
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(word => word.Length == 1
                    ? word.ToUpperInvariant()
                    : char.ToUpperInvariant(word[0]) + word[1..].ToLowerInvariant()));
    }

    private sealed record DecisionBranchSpec(
        string Label,
        string TargetName,
        string TargetInstructions,
        WorkflowEdgeRouting Routing);

    private sealed class CanvasPreviewNodeSelectionObserver(WorkflowCanvasEditor editor) : IWorkflowNodeExecutionProgressObserver
    {
        public ValueTask RecordAsync(
            WorkflowNodeExecutionProgress progress,
            CancellationToken cancellationToken = default)
        {
            if (progress.State != WorkflowNodeExecutionProgressState.Started)
            {
                return ValueTask.CompletedTask;
            }

            return new ValueTask(editor.InvokeAsync(() => editor.SelectPreviewNodeAsync(progress.NodeId)));
        }
    }

    private static string ResolveWorkflowToolboxNodeIcon(WorkflowNodeKind kind)
        => kind switch
        {
            WorkflowNodeKind.LlmCall => "smart_toy",
            WorkflowNodeKind.Triage => "call_split",
            WorkflowNodeKind.StrictLogic => "rule",
            WorkflowNodeKind.Artifact => "description",
            WorkflowNodeKind.HumanInput => "approval",
            WorkflowNodeKind.AgentStep => "support_agent",
            WorkflowNodeKind.Subworkflow => "account_tree",
            _ => "circle"
        };

    private static string ResolveWorkflowToolboxNodeTone(WorkflowNodeKind kind)
        => kind switch
        {
            WorkflowNodeKind.LlmCall => "success",
            WorkflowNodeKind.Triage => "accent",
            WorkflowNodeKind.StrictLogic => "warning",
            WorkflowNodeKind.Artifact => "danger",
            WorkflowNodeKind.HumanInput => "warning",
            WorkflowNodeKind.AgentStep or WorkflowNodeKind.Subworkflow => "info",
            _ => "neutral"
        };

    private WorkflowExecutorExecutionPolicy ResolveSelectedExecutionPolicy(WorkflowCanvasNodeDraft node)
        => node.ExecutionPolicy
           ?? ResolveSelectedExecutorDescriptor(node)?.DefaultPolicy
           ?? WorkflowExecutorExecutionPolicy.Default;

    private void UpdateSelectedExecutionPolicy(
        WorkflowCanvasNodeDraft node,
        Func<WorkflowExecutorExecutionPolicy, WorkflowExecutorExecutionPolicy> update)
    {
        node.ExecutionPolicy = update(ResolveSelectedExecutionPolicy(node));
    }

    private TSettings ReadExecutorSettings<TSettings>(WorkflowCanvasNodeDraft node)
        where TSettings : new()
    {
        var settingsJson = string.IsNullOrWhiteSpace(node.ExecutorSettingsJson)
            ? ResolveSelectedExecutorDescriptor(node)?.DefaultSettingsJson
            : node.ExecutorSettingsJson;
        if (string.IsNullOrWhiteSpace(settingsJson))
        {
            return new TSettings();
        }

        try
        {
            return JsonSerializer.Deserialize<TSettings>(settingsJson, ExecutorJsonOptions) ?? new TSettings();
        }
        catch (JsonException)
        {
            return new TSettings();
        }
    }

    private void UpdateExecutorSettings<TSettings>(
        WorkflowCanvasNodeDraft node,
        Func<TSettings, TSettings> update)
        where TSettings : new()
    {
        var updated = update(ReadExecutorSettings<TSettings>(node));
        node.ExecutorSettingsJson = JsonSerializer.Serialize(updated, ExecutorJsonOptions);
        errorMessage = string.Empty;
    }

    private ConfigurationState CreateExecutorConfigurationState(
        WorkflowCanvasNodeDraft node,
        WorkflowExecutorDescriptor descriptor)
    {
        var settingsJson = string.IsNullOrWhiteSpace(node.ExecutorSettingsJson)
            ? descriptor.DefaultSettingsJson
            : node.ExecutorSettingsJson;

        return ReadConfigurationState(settingsJson, descriptor.ConfigurationSchema);
    }

    private void HandleSelectedExecutorConfigurationStateChanged(
        WorkflowCanvasNodeDraft node,
        ConfigurationSchema schema,
        ConfigurationState state)
    {
        node.ExecutorSettingsJson = SerializeConfigurationState(schema, state);
        errorMessage = string.Empty;
    }

    private static ConfigurationState ReadConfigurationState(
        string? settingsJson,
        ConfigurationSchema schema)
    {
        if (string.IsNullOrWhiteSpace(settingsJson))
        {
            return new ConfigurationState();
        }

        try
        {
            using var document = JsonDocument.Parse(settingsJson);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return new ConfigurationState();
            }

            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var property in document.RootElement.EnumerateObject())
            {
                values[property.Name] = NormalizeConfigurationValue(
                    property.Name,
                    property.Value.ValueKind == JsonValueKind.String
                        ? property.Value.GetString() ?? string.Empty
                        : property.Value.GetRawText(),
                    schema);
            }

            return new ConfigurationState(values);
        }
        catch (JsonException)
        {
            return new ConfigurationState();
        }
    }

    private static string NormalizeConfigurationValue(
        string key,
        string value,
        ConfigurationSchema schema)
    {
        var field = schema.Fields.FirstOrDefault(candidate => string.Equals(candidate.Key, key, StringComparison.OrdinalIgnoreCase));
        if (field?.FieldType != ConfigurationFieldType.Select)
        {
            return value;
        }

        var option = field.Options.FirstOrDefault(candidate =>
            string.Equals(candidate.Value, value, StringComparison.OrdinalIgnoreCase) ||
            candidate.AcceptedValues.Any(acceptedValue => string.Equals(acceptedValue, value, StringComparison.OrdinalIgnoreCase)));
        return option?.Value ?? value;
    }

    private static string SerializeConfigurationState(
        ConfigurationSchema schema,
        ConfigurationState state)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            var writtenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var field in schema.Fields)
            {
                if (!state.Values.TryGetValue(field.Key, out var value) ||
                    string.IsNullOrWhiteSpace(value))
                {
                    continue;
                }

                WriteConfigurationValue(writer, field.Key, value, field);
                writtenKeys.Add(field.Key);
            }

            foreach (var item in state.Values)
            {
                if (writtenKeys.Contains(item.Key) ||
                    string.IsNullOrWhiteSpace(item.Value))
                {
                    continue;
                }

                writer.WriteString(item.Key, item.Value);
            }

            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void WriteConfigurationValue(
        Utf8JsonWriter writer,
        string key,
        string value,
        ConfigurationFieldDescriptor field)
    {
        switch (field.FieldType)
        {
            case ConfigurationFieldType.Number:
                if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number))
                {
                    writer.WriteNumber(key, number);
                    return;
                }

                break;
            case ConfigurationFieldType.Boolean:
                if (bool.TryParse(value, out var boolean))
                {
                    writer.WriteBoolean(key, boolean);
                    return;
                }

                break;
            case ConfigurationFieldType.Json:
                if (TryWriteRawJsonValue(writer, key, value))
                {
                    return;
                }

                break;
            default:
                break;
        }

        writer.WriteString(key, value);
    }

    private static bool TryWriteRawJsonValue(
        Utf8JsonWriter writer,
        string key,
        string value)
    {
        try
        {
            using var document = JsonDocument.Parse(value);
            writer.WritePropertyName(key);
            document.RootElement.WriteTo(writer);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private void UpdateEnumExecutorSettings<TSettings, TEnum>(
        WorkflowCanvasNodeDraft node,
        ChangeEventArgs args,
        Func<TSettings, TEnum, TSettings> update)
        where TSettings : new()
        where TEnum : struct, Enum
    {
        if (Enum.TryParse<TEnum>(ReadString(args), out var parsed))
        {
            UpdateExecutorSettings<TSettings>(node, settings => update(settings, parsed));
        }
    }

    private string BuildHeadersJson(WorkflowCanvasNodeDraft node)
        => JsonSerializer.Serialize(ReadExecutorSettings<WorkflowHttpExecutorSettings>(node).Headers, IndentedExecutorJsonOptions);

    private void UpdateHttpHeadersJson(WorkflowCanvasNodeDraft node, ChangeEventArgs args)
    {
        if (!TryDeserializeJson<IReadOnlyDictionary<string, string>>(args.Value?.ToString(), out var headers))
        {
            return;
        }

        UpdateExecutorSettings<WorkflowHttpExecutorSettings>(node, settings => settings with
        {
            Headers = headers ?? new Dictionary<string, string>()
        });
    }

    private void UpdateHttpSecretId(WorkflowCanvasNodeDraft node, ChangeEventArgs args)
    {
        var selected = Guid.TryParse(args.Value?.ToString(), out var secretId) && secretId != Guid.Empty
            ? secretPickerItems.FirstOrDefault(item => item.Id == secretId)
            : null;

        UpdateExecutorSettings<WorkflowHttpExecutorSettings>(node, settings => settings with
        {
            SecretHeader = settings.SecretHeader with
            {
                SecretId = selected?.Id,
                SecretNameSnapshot = selected?.Name ?? string.Empty,
                Purpose = WorkflowSecretPurposes.HttpHeader
            }
        });
    }

    private void UpdateHttpSecretHeaderName(WorkflowCanvasNodeDraft node, ChangeEventArgs args)
    {
        UpdateExecutorSettings<WorkflowHttpExecutorSettings>(node, settings => settings with
        {
            SecretHeader = settings.SecretHeader with
            {
                HeaderName = ReadString(args)
            }
        });
    }

    private void UpdateHttpSecretValueFormat(WorkflowCanvasNodeDraft node, ChangeEventArgs args)
    {
        if (!Enum.TryParse<WorkflowHttpSecretValueFormat>(ReadString(args), out var valueFormat))
        {
            return;
        }

        UpdateExecutorSettings<WorkflowHttpExecutorSettings>(node, settings => settings with
        {
            SecretHeader = settings.SecretHeader with
            {
                ValueFormat = valueFormat
            }
        });
    }

    private void UpdateHttpSecretCustomPrefix(WorkflowCanvasNodeDraft node, ChangeEventArgs args)
    {
        UpdateExecutorSettings<WorkflowHttpExecutorSettings>(node, settings => settings with
        {
            SecretHeader = settings.SecretHeader with
            {
                CustomPrefix = ReadString(args)
            }
        });
    }

    private async Task LoadSecretPickerItemsAsync()
    {
        isLoadingSecrets = true;
        secretPickerErrorMessage = string.Empty;
        try
        {
            secretPickerItems = await SecretService.ListForPickerAsync();
        }
        catch (Exception exception)
        {
            secretPickerItems = [];
            secretPickerErrorMessage = $"Secret list failed to load. {exception.Message}";
        }
        finally
        {
            isLoadingSecrets = false;
        }
    }

    private static bool IsHttpExecutorNode(WorkflowCanvasNodeDraft? node)
        => node is
        {
            Kind: WorkflowNodeKind.Executor,
            ExecutorId: { } executorId
        } && executorId == WorkflowExecutorIds.HttpFetch;

    private string BuildCellWritesJson(WorkflowCanvasNodeDraft node)
        => JsonSerializer.Serialize(ReadExecutorSettings<WorkflowSpreadsheetExecutorSettings>(node).CellWrites, IndentedExecutorJsonOptions);

    private string BuildRangeWritesJson(WorkflowCanvasNodeDraft node)
        => JsonSerializer.Serialize(ReadExecutorSettings<WorkflowSpreadsheetExecutorSettings>(node).RangeWrites, IndentedExecutorJsonOptions);

    private void UpdateSpreadsheetCellWritesJson(WorkflowCanvasNodeDraft node, ChangeEventArgs args)
    {
        if (!TryDeserializeJson<IReadOnlyList<WorkflowSpreadsheetCellWrite>>(args.Value?.ToString(), out var writes))
        {
            return;
        }

        UpdateExecutorSettings<WorkflowSpreadsheetExecutorSettings>(node, settings => settings with
        {
            CellWrites = writes ?? []
        });
    }

    private void UpdateSpreadsheetRangeWritesJson(WorkflowCanvasNodeDraft node, ChangeEventArgs args)
    {
        if (!TryDeserializeJson<IReadOnlyList<WorkflowSpreadsheetRangeWrite>>(args.Value?.ToString(), out var writes))
        {
            return;
        }

        UpdateExecutorSettings<WorkflowSpreadsheetExecutorSettings>(node, settings => settings with
        {
            RangeWrites = writes ?? []
        });
    }

    private bool TryDeserializeJson<T>(string? json, out T? value)
    {
        try
        {
            value = string.IsNullOrWhiteSpace(json)
                ? default
                : JsonSerializer.Deserialize<T>(json, ExecutorJsonOptions);
            errorMessage = string.Empty;
            return true;
        }
        catch (JsonException exception)
        {
            value = default;
            errorMessage = exception.Message;
            return false;
        }
    }

    private static CanvasWorkbenchWindowState CreateWindowState(
        double width,
        double height,
        bool isVisible = true)
        => CanvasWorkbenchWindowState.Normalize(
            new CanvasWorkbenchWindowState
            {
                IsVisible = isVisible,
                Width = width,
                Height = height
            });

    private static CanvasWorkbenchUiState CreateWorkflowCanvasUiState(string? selectedNodeId)
        => new()
        {
            SelectedNodeIds = string.IsNullOrWhiteSpace(selectedNodeId) ? [] : [selectedNodeId],
            ActiveInspectorTab = "workflow"
        };

    private static CanvasWorkbenchWindowState ToggleWindow(CanvasWorkbenchWindowState state)
    {
        var next = CanvasWorkbenchWindowState.Normalize(state);
        next.IsVisible = !next.IsVisible;
        next.IsMinimized = false;
        return CanvasWorkbenchWindowState.Normalize(next);
    }

    private (double X, double Y) ResolveCreatePosition(CanvasWorkbenchCreateActionRequest? request)
    {
        if (request is not null &&
            (Math.Abs(request.X) > 0.01 || Math.Abs(request.Y) > 0.01))
        {
            return (request.X, request.Y);
        }

        return (
            320 + (document.Nodes.Count * 120),
            220 + ((document.Nodes.Count % 3) * 120));
    }

    private static void ApplyCreateRequest(
        WorkflowCanvasNodeDraft node,
        CanvasWorkbenchCreateActionRequest? request)
    {
        if (request is null)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(request.Title))
        {
            node.Name = request.Title.Trim();
        }

        if (!string.IsNullOrWhiteSpace(request.Notes))
        {
            node.Instructions = request.Notes.Trim();
        }

        if (Enum.TryParse<WorkflowValueShapeKind>(
                GetInputValue(request, "inputShape", node.InputShapeKind.ToString()),
                out var inputShape))
        {
            node.InputShapeKind = inputShape;
        }

        if (Enum.TryParse<WorkflowValueShapeKind>(
                GetInputValue(request, "resultShape", node.ResultShapeKind.ToString()),
                out var resultShape))
        {
            node.ResultShapeKind = resultShape;
        }

        if (node.Kind == WorkflowNodeKind.HumanInput &&
            Enum.TryParse<WorkflowExternalRequestKind>(
                GetInputValue(request, "externalRequestKind", node.ExternalRequestKind?.ToString() ?? WorkflowExternalRequestKind.HumanInput.ToString()),
                out var requestKind))
        {
            node.ExternalRequestKind = requestKind;
        }

        if (node.Kind == WorkflowNodeKind.AgentStep &&
            Guid.TryParse(GetInputValue(request, "agentId", string.Empty), out var agentId))
        {
            node.AgentId = agentId;
        }

        if (node.Kind == WorkflowNodeKind.Subworkflow &&
            Guid.TryParse(GetInputValue(request, "subworkflowId", string.Empty), out var workflowId))
        {
            node.SubworkflowId = new WorkflowId(workflowId);
        }
    }

    private static void ApplyExecutorCreateRequest(
        WorkflowCanvasNodeDraft node,
        CanvasWorkbenchCreateActionRequest? request)
    {
        if (request is null || node.Kind != WorkflowNodeKind.Executor)
        {
            return;
        }

        var policy = node.ExecutionPolicy ?? WorkflowExecutorExecutionPolicy.Default;
        if (int.TryParse(GetInputValue(request, "timeoutSeconds", string.Empty), out var timeoutSeconds))
        {
            policy = policy with
            {
                TimeoutSeconds = Math.Clamp(
                    timeoutSeconds,
                    WorkflowExecutorPolicyLimits.MinTimeoutSeconds,
                    WorkflowExecutorPolicyLimits.MaxTimeoutSeconds)
            };
        }

        if (int.TryParse(GetInputValue(request, "retryAttempts", string.Empty), out var retryAttempts))
        {
            policy = policy with
            {
                MaxRetryAttempts = Math.Clamp(
                    retryAttempts,
                    WorkflowExecutorPolicyLimits.MinRetryAttempts,
                    WorkflowExecutorPolicyLimits.MaxRetryAttempts)
            };
        }

        var captureOutput = GetInputValue(request, "captureOutput", string.Empty);
        if (bool.TryParse(captureOutput, out var captureOutputArtifact))
        {
            policy = policy with
            {
                CaptureOutputArtifact = captureOutputArtifact
            };
        }

        node.ExecutionPolicy = policy;
        if (node.ExecutorId == WorkflowExecutorIds.StorageFile)
        {
            var settings = ReadExecutorCreateSettings<WorkflowStorageFileExecutorSettings>(node);
            if (Enum.TryParse<WorkflowStorageFileOperation>(GetInputValue(request, "storageOperation", settings.Operation.ToString()), out var operation))
            {
                settings = settings with { Operation = operation };
            }

            settings = settings with
            {
                Path = GetInputValue(request, "storagePath", settings.Path),
                DestinationPath = GetInputValue(request, "storageDestinationPath", settings.DestinationPath),
                Content = GetInputValue(request, "storageContent", settings.Content),
                ContentFromInput = ReadCreateBool(request, "storageContentFromInput", settings.ContentFromInput),
                Query = GetInputValue(request, "storageQuery", settings.Query),
                SearchPattern = GetInputValue(request, "storageSearchPattern", settings.SearchPattern),
                MaxResults = ReadCreateInt(request, "storageMaxResults", settings.MaxResults),
                MaxCharacters = ReadCreateInt(request, "storageMaxCharacters", settings.MaxCharacters),
                Overwrite = ReadCreateBool(request, "storageOverwrite", settings.Overwrite)
            };
            node.ExecutorSettingsJson = JsonSerializer.Serialize(settings, ExecutorJsonOptions);
            return;
        }

        if (node.ExecutorId == WorkflowExecutorIds.HttpFetch)
        {
            var settings = ReadExecutorCreateSettings<WorkflowHttpExecutorSettings>(node);
            if (Enum.TryParse<WorkflowHttpMethodKind>(GetInputValue(request, "httpMethod", settings.Method.ToString()), out var method))
            {
                settings = settings with { Method = method };
            }

            var headers = settings.Headers;
            var headersJson = GetInputValue(request, "httpHeadersJson", string.Empty);
            if (!string.IsNullOrWhiteSpace(headersJson))
            {
                headers = JsonSerializer.Deserialize<IReadOnlyDictionary<string, string>>(headersJson, ExecutorJsonOptions)
                          ?? new Dictionary<string, string>();
            }

            var secretHeader = settings.SecretHeader;
            var httpSecretId = GetInputValue(request, "httpSecretId", secretHeader.SecretId?.ToString("D") ?? string.Empty);
            if (Guid.TryParse(httpSecretId, out var parsedSecretId) && parsedSecretId != Guid.Empty)
            {
                secretHeader = secretHeader with
                {
                    SecretId = parsedSecretId,
                    SecretNameSnapshot = ResolveSecretNameSnapshot(parsedSecretId),
                    Purpose = WorkflowSecretPurposes.HttpHeader
                };
            }
            else
            {
                secretHeader = secretHeader with
                {
                    SecretId = null,
                    SecretNameSnapshot = string.Empty
                };
            }

            if (Enum.TryParse<WorkflowHttpSecretValueFormat>(GetInputValue(request, "httpSecretValueFormat", secretHeader.ValueFormat.ToString()), out var secretValueFormat))
            {
                secretHeader = secretHeader with
                {
                    ValueFormat = secretValueFormat
                };
            }

            secretHeader = secretHeader with
            {
                HeaderName = GetInputValue(request, "httpSecretHeaderName", secretHeader.HeaderName),
                CustomPrefix = GetInputValue(request, "httpSecretCustomPrefix", secretHeader.CustomPrefix)
            };

            settings = settings with
            {
                Url = GetInputValue(request, "httpUrl", settings.Url),
                UrlJsonPath = GetInputValue(request, "httpUrlJsonPath", settings.UrlJsonPath),
                Headers = headers,
                SecretHeader = secretHeader,
                Body = GetInputValue(request, "httpBody", settings.Body),
                MaxResponseBytes = ReadCreateInt(request, "httpMaxResponseBytes", settings.MaxResponseBytes),
                IncludeInputPayload = ReadCreateBool(request, "httpIncludeInputPayload", settings.IncludeInputPayload)
            };
            node.ExecutorSettingsJson = JsonSerializer.Serialize(settings, ExecutorJsonOptions);
            return;
        }

        if (node.ExecutorId == WorkflowExecutorIds.Spreadsheet)
        {
            var settings = ReadExecutorCreateSettings<WorkflowSpreadsheetExecutorSettings>(node);
            if (Enum.TryParse<WorkflowSpreadsheetOperation>(GetInputValue(request, "spreadsheetOperation", settings.Operation.ToString()), out var operation))
            {
                settings = settings with { Operation = operation };
            }

            settings = settings with
            {
                WorkbookPath = GetInputValue(request, "spreadsheetWorkbookPath", settings.WorkbookPath),
                OutputWorkbookPath = GetInputValue(request, "spreadsheetOutputWorkbookPath", settings.OutputWorkbookPath),
                WorksheetName = GetInputValue(request, "spreadsheetWorksheetName", settings.WorksheetName),
                CellAddress = GetInputValue(request, "spreadsheetCellAddress", settings.CellAddress),
                RangeAddress = GetInputValue(request, "spreadsheetRangeAddress", settings.RangeAddress),
                Value = GetInputValue(request, "spreadsheetValue", settings.Value),
                CreateWorkbookIfMissing = ReadCreateBool(request, "spreadsheetCreateWorkbookIfMissing", settings.CreateWorkbookIfMissing),
                Overwrite = ReadCreateBool(request, "spreadsheetOverwrite", settings.Overwrite),
                MaxRows = ReadCreateInt(request, "spreadsheetMaxRows", settings.MaxRows),
                MaxColumns = ReadCreateInt(request, "spreadsheetMaxColumns", settings.MaxColumns)
            };
            node.ExecutorSettingsJson = JsonSerializer.Serialize(settings, ExecutorJsonOptions);
            return;
        }

        if (node.ExecutorId == WorkflowExecutorIds.ProjectStructure)
        {
            var settings = ReadExecutorCreateSettings<WorkflowProjectStructureExecutorSettings>(node);
            if (Enum.TryParse<WorkflowProjectStructureOperation>(GetInputValue(request, "projectStructureOperation", settings.Operation.ToString()), out var operation))
            {
                settings = settings with { Operation = operation };
            }

            var projectId = settings.ProjectId;
            var projectIdValue = GetInputValue(request, "projectStructureProjectId", projectId?.ToString("D") ?? string.Empty);
            if (Guid.TryParse(projectIdValue, out var parsedProjectId) && parsedProjectId != Guid.Empty)
            {
                projectId = parsedProjectId;
            }

            settings = settings with
            {
                ProjectId = projectId,
                ProjectIdJsonPath = GetInputValue(request, "projectStructureProjectIdJsonPath", settings.ProjectIdJsonPath),
                NodeId = GetInputValue(request, "projectStructureNodeId", settings.NodeId),
                NodeIdJsonPath = GetInputValue(request, "projectStructureNodeIdJsonPath", settings.NodeIdJsonPath),
                AssetKind = GetInputValue(request, "projectStructureAssetKind", settings.AssetKind),
                Title = GetInputValue(request, "projectStructureTitle", settings.Title),
                Content = GetInputValue(request, "projectStructureContent", settings.Content),
                ContentFromInput = ReadCreateBool(request, "projectStructureContentFromInput", settings.ContentFromInput),
                SourceWorkspacePath = GetInputValue(request, "projectStructureSourceWorkspacePath", settings.SourceWorkspacePath),
                ContentType = GetInputValue(request, "projectStructureContentType", settings.ContentType)
            };
            node.ExecutorSettingsJson = JsonSerializer.Serialize(settings, ExecutorJsonOptions);
            return;
        }

        if (node.ExecutorId == WorkflowExecutorIds.ImageGeneration)
        {
            var settings = ReadExecutorCreateSettings<WorkflowImageGenerationExecutorSettings>(node);
            if (Enum.TryParse<WorkflowImageGenerationOperation>(GetInputValue(request, "imageOperation", settings.Operation.ToString()), out var operation))
            {
                settings = settings with { Operation = operation };
            }

            var providerProfileId = settings.ProviderProfileId;
            var providerIdValue = GetInputValue(request, "imageProviderProfileId", providerProfileId?.ToString("D") ?? string.Empty);
            if (Guid.TryParse(providerIdValue, out var parsedProviderId) && parsedProviderId != Guid.Empty)
            {
                providerProfileId = parsedProviderId;
            }

            settings = settings with
            {
                Prompt = GetInputValue(request, "imagePrompt", settings.Prompt),
                ProviderProfileId = providerProfileId,
                Model = GetInputValue(request, "imageModel", settings.Model),
                Size = GetInputValue(request, "imageSize", settings.Size),
                Quality = GetInputValue(request, "imageQuality", settings.Quality),
                OutputFormat = GetInputValue(request, "imageOutputFormat", settings.OutputFormat),
                OutputWorkspacePath = GetInputValue(request, "imageOutputWorkspacePath", settings.OutputWorkspacePath)
            };
            node.ExecutorSettingsJson = JsonSerializer.Serialize(settings, ExecutorJsonOptions);
        }
    }

    private static TSettings ReadExecutorCreateSettings<TSettings>(WorkflowCanvasNodeDraft node)
        where TSettings : new()
    {
        if (string.IsNullOrWhiteSpace(node.ExecutorSettingsJson))
        {
            return new TSettings();
        }

        return JsonSerializer.Deserialize<TSettings>(node.ExecutorSettingsJson, ExecutorJsonOptions) ?? new TSettings();
    }

    private static string ResolveSecretNameSnapshot(Guid secretId)
        => secretId == Guid.Empty ? string.Empty : secretId.ToString("D");

    private static int ReadCreateInt(
        CanvasWorkbenchCreateActionRequest request,
        string key,
        int fallback)
        => int.TryParse(GetInputValue(request, key, string.Empty), out var value) ? value : fallback;

    private static bool ReadCreateBool(
        CanvasWorkbenchCreateActionRequest request,
        string key,
        bool fallback)
        => bool.TryParse(GetInputValue(request, key, string.Empty), out var value) ? value : fallback;

    private LlmCallComponent? ResolveRequestedComponent(CanvasWorkbenchCreateActionRequest request)
    {
        if (!Guid.TryParse(request.ObjectSubtype, out var componentId))
        {
            return null;
        }

        return componentOptions.FirstOrDefault(component => component.Id.Value == componentId);
    }

    private string BuildNodeDetailsDialogSubtitle(WorkflowCanvasNodeDraft node)
        => node.Kind == WorkflowNodeKind.Executor && ResolveSelectedExecutorDescriptor(node) is { } descriptor
            ? $"{descriptor.Category} executor · {node.Id.Value}"
            : $"{node.Kind} · {node.Id.Value}";

    private string FormatExecutorSettingsJson(WorkflowCanvasNodeDraft node)
    {
        if (string.IsNullOrWhiteSpace(node.ExecutorSettingsJson))
        {
            return "{}";
        }

        try
        {
            using var parsed = JsonDocument.Parse(node.ExecutorSettingsJson);
            return JsonSerializer.Serialize(parsed.RootElement, IndentedExecutorJsonOptions);
        }
        catch (JsonException)
        {
            return node.ExecutorSettingsJson;
        }
    }

    private void HandleSelectedExecutorSettingsJsonChanged(
        WorkflowCanvasNodeDraft node,
        ChangeEventArgs args)
    {
        var value = ReadString(args);
        if (!string.IsNullOrWhiteSpace(value))
        {
            try
            {
                using var _ = JsonDocument.Parse(value);
            }
            catch (JsonException exception)
            {
                errorMessage = exception.Message;
                return;
            }
        }

        node.ExecutorSettingsJson = value.Trim();
        errorMessage = string.Empty;
    }

    private static string ReadString(ChangeEventArgs args)
        => args.Value?.ToString() ?? string.Empty;

    private static int ReadInt(ChangeEventArgs args, int fallback)
        => int.TryParse(args.Value?.ToString(), out var value) ? value : fallback;

    private static bool ReadBool(ChangeEventArgs args)
        => args.Value is bool value
            ? value
            : bool.TryParse(args.Value?.ToString(), out var parsed) && parsed;

    private static JsonSerializerOptions CreateExecutorJsonOptions(bool writeIndented)
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            WriteIndented = writeIndented
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private string ResolveNodeName(WorkflowNodeId nodeId)
    {
        return document.Nodes.FirstOrDefault(node => node.Id == nodeId)?.Name ?? nodeId.Value;
    }

    private static string FormatComponentId(WorkflowComponentId? componentId)
    {
        return componentId?.Value.ToString("D") ?? string.Empty;
    }

    private WorkflowProviderOption? ResolveDefaultProviderOption()
    {
        return ProviderOptions.FirstOrDefault(option => option.IsEnabled);
    }

    private WorkflowProviderOption? ResolveSelectedNewComponentProvider()
    {
        if (!Guid.TryParse(newComponentProviderProfileId, out var providerId))
        {
            return null;
        }

        return ProviderOptions.FirstOrDefault(option => option.ProviderProfileId == providerId);
    }

    private string ResolveNewComponentModel(WorkflowProviderOption? providerOption)
    {
        return string.IsNullOrWhiteSpace(newComponentModel)
            ? ResolveDefaultModel(providerOption)
            : newComponentModel.Trim();
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

    private string ResolveComponentProviderLabel(LlmCallComponent component)
    {
        if (!component.ProviderProfileId.HasValue)
        {
            return "No provider";
        }

        var provider = ProviderOptions.FirstOrDefault(option => option.ProviderProfileId == component.ProviderProfileId.Value);
        return provider?.Name ?? "Provider missing";
    }

    private LlmCallComponent? ResolveSelectedComponent(WorkflowCanvasNodeDraft node)
    {
        return node.ComponentId.HasValue
            ? componentOptions.FirstOrDefault(component => component.Id == node.ComponentId.Value)
            : null;
    }

    private WorkflowProviderOption? ResolveComponentProviderOption(LlmCallComponent? component)
    {
        return component?.ProviderProfileId is { } providerProfileId
            ? ProviderOptions.FirstOrDefault(option => option.ProviderProfileId == providerProfileId)
            : null;
    }

    private WorkflowExecutorDisplayBadge BuildLlmProviderUsageBadge(LlmCallComponent? component)
    {
        if (component is null)
        {
            return new WorkflowExecutorDisplayBadge("Provider unselected", "warning");
        }

        if (!component.ProviderProfileId.HasValue)
        {
            return new WorkflowExecutorDisplayBadge("Provider unbound", "warning");
        }

        var provider = ResolveComponentProviderOption(component);
        if (provider is null)
        {
            return new WorkflowExecutorDisplayBadge("Provider missing", "danger");
        }

        return provider.IsEnabled
            ? new WorkflowExecutorDisplayBadge("Usage unknown until run", "warning")
            : new WorkflowExecutorDisplayBadge("Provider disabled", "warning");
    }

    private string BuildLlmProviderUsageDescription(LlmCallComponent? component)
    {
        if (component is null)
        {
            return "Select an LLM component before runtime provider usage can be attributed.";
        }

        var provider = ResolveComponentProviderOption(component);
        if (!component.ProviderProfileId.HasValue)
        {
            return $"Component {component.Name} has no provider binding; execution cannot produce provider usage evidence.";
        }

        if (provider is null)
        {
            return $"Component {component.Name} references provider {component.ProviderProfileId.Value:D}, but that provider is missing from the registry.";
        }

        if (!provider.IsEnabled)
        {
            return $"Provider {provider.Name} is disabled; runtime usage and cost remain unavailable until the provider is enabled or replaced.";
        }

        return $"Provider {provider.Name} / {component.Model}; actual usage and cost appear only after execution records provider usage observations.";
    }

    private string BuildProviderOptionsSummary()
    {
        if (ProviderOptions.Count == 0)
        {
            return "No agent chat providers are available; new components use an unbound preview model.";
        }

        var enabledCount = ProviderOptions.Count(option => option.IsEnabled);
        return $"{enabledCount} enabled chat provider(s) available from the agent provider registry.";
    }

    private static string BuildProviderOptionLabel(WorkflowProviderOption option)
    {
        var label = $"{option.Name} - {option.Kind} - {option.Transport}";
        return option.IsEnabled ? label : $"{label} (disabled)";
    }

    private WorkflowProviderOption? ResolveImageProviderOption(Guid? providerProfileId)
    {
        return providerProfileId.HasValue
            ? ImageProviderOptions.FirstOrDefault(option => option.ProviderProfileId == providerProfileId.Value)
            : null;
    }

    private string BuildImageProviderOptionsSummary()
    {
        if (ImageProviderOptions.Count == 0)
        {
            return "No image-generation providers are available.";
        }

        var enabledCount = ImageProviderOptions.Count(option => option.IsEnabled);
        return $"{enabledCount} enabled image provider(s) available.";
    }

    private void SyncNewComponentDefaults()
    {
        if (ProviderOptions.Count == 0)
        {
            newComponentProviderProfileId = string.Empty;
            if (string.IsNullOrWhiteSpace(newComponentModel))
            {
                newComponentModel = ManagedSeedProviderFallbacks.OpenAiDefaultModel;
            }

            return;
        }

        var selectedProvider = ResolveSelectedNewComponentProvider();
        if (selectedProvider is null || !selectedProvider.IsEnabled)
        {
            selectedProvider = ResolveDefaultProviderOption();
            newComponentProviderProfileId = selectedProvider?.ProviderProfileId.ToString("D") ?? string.Empty;
            newComponentModel = ResolveDefaultModel(selectedProvider);
            return;
        }

        if (string.IsNullOrWhiteSpace(newComponentModel))
        {
            newComponentModel = ResolveDefaultModel(selectedProvider);
            return;
        }

        if (selectedProvider.ModelOptions.Count > 0 &&
            !selectedProvider.ModelOptions.Contains(newComponentModel, StringComparer.OrdinalIgnoreCase))
        {
            newComponentModel = ResolveDefaultModel(selectedProvider);
        }
    }

    private void InsertNodeBeforeEnd(WorkflowCanvasNodeDraft node)
    {
        var end = document.Nodes.FirstOrDefault(item => item.Kind == WorkflowNodeKind.End);
        var start = document.Nodes.FirstOrDefault(item => item.Id == document.StartNodeId);
        if (end is null || start is null || node.Kind == WorkflowNodeKind.End)
        {
            return;
        }

        var incomingToEnd = document.Edges.FirstOrDefault(edge => edge.TargetNodeId == end.Id);
        if (incomingToEnd is null)
        {
            document.Edges.Add(new WorkflowCanvasEdgeDraft(
                CreateEdgeId(start.Id, node.Id),
                start.Id,
                node.Id));
            document.Edges.Add(new WorkflowCanvasEdgeDraft(
                CreateEdgeId(node.Id, end.Id),
                node.Id,
                end.Id));
            return;
        }

        document.Edges.Remove(incomingToEnd);
        document.Edges.Add(new WorkflowCanvasEdgeDraft(
            CreateEdgeId(incomingToEnd.SourceNodeId, node.Id),
            incomingToEnd.SourceNodeId,
            node.Id)
        {
            Kind = incomingToEnd.Kind,
            ConditionExpression = incomingToEnd.ConditionExpression,
            Routing = incomingToEnd.Routing
        });
        document.Edges.Add(new WorkflowCanvasEdgeDraft(
            CreateEdgeId(node.Id, end.Id),
            node.Id,
            end.Id));
    }

    private WorkflowEdgeId CreateEdgeId(WorkflowNodeId source, WorkflowNodeId target)
    {
        var baseId = $"{source.Value}-to-{target.Value}";
        var existingIds = document.Edges
            .Select(edge => edge.Id.Value)
            .ToHashSet(StringComparer.Ordinal);
        if (!existingIds.Contains(baseId))
        {
            return new WorkflowEdgeId(baseId);
        }

        for (var index = 1; ; index++)
        {
            var candidate = $"{baseId}-{index}";
            if (!existingIds.Contains(candidate))
            {
                return new WorkflowEdgeId(candidate);
            }
        }
    }

    private void SyncEdgeDefaults()
    {
        edgeSourceNodeId = document.Nodes.FirstOrDefault(node => node.Kind != WorkflowNodeKind.End)?.Id.Value ?? string.Empty;
        edgeTargetNodeId = document.Nodes.FirstOrDefault(node => node.Kind != WorkflowNodeKind.Start && node.Id.Value != edgeSourceNodeId)?.Id.Value ?? string.Empty;
        edgeKind = WorkflowEdgeKind.Direct;
        edgeCondition = string.Empty;
        editingEdgeId = null;
        edgeRouteKind = WorkflowRouteKind.Always;
        edgeRouteLabel = string.Empty;
        edgeRouteJsonPath = "$.status";
        edgeRouteOperator = WorkflowRouteOperator.Equals;
        edgeRouteValueKind = WorkflowRouteValueKind.String;
        edgeRouteExpectedValue = "approved";
        edgeRouteCaseSensitive = false;
        edgeRouteFanOutTargetIndex = null;
    }

    private void ResetEdgeEditor()
    {
        SyncEdgeDefaults();
    }

    private static bool IsDecisionNode(WorkflowCanvasNodeDraft node)
        => node.Kind == WorkflowNodeKind.Triage;

    private IReadOnlyList<WorkflowCanvasEdgeDraft> GetDecisionRouteEdges(WorkflowCanvasNodeDraft node)
        => document.Edges
            .Where(edge => edge.SourceNodeId == node.Id)
            .OrderBy(edge => edge.Routing.Kind == WorkflowRouteKind.SwitchDefault ? 1 : 0)
            .ThenBy(edge => edge.Id.Value, StringComparer.Ordinal)
            .ToArray();

    private void ApplyRouteToEditor(WorkflowEdgeRouting routing)
    {
        edgeRouteKind = routing.Kind;
        edgeRouteLabel = routing.Label;
        edgeRouteJsonPath = string.IsNullOrWhiteSpace(routing.JsonPath) ? "$.status" : routing.JsonPath;
        edgeRouteOperator = routing.Operator;
        edgeRouteValueKind = routing.ExpectedValueKind;
        edgeRouteExpectedValue = FormatExpectedValueForEditor(routing);
        edgeRouteCaseSensitive = routing.CaseSensitive;
        edgeRouteFanOutTargetIndex = routing.FanOutTargetIndex;
    }

    private Task StartAddDecisionRouteAsync(WorkflowCanvasNodeDraft node)
    {
        BeginDecisionRouteEdit(node, routeEdge: null);
        return Task.CompletedTask;
    }

    private Task StartEditDecisionRouteAsync(
        WorkflowCanvasNodeDraft node,
        WorkflowCanvasEdgeDraft routeEdge)
    {
        BeginDecisionRouteEdit(node, routeEdge);
        return Task.CompletedTask;
    }

    private Task CancelDecisionRouteEditAsync()
    {
        ResetDecisionRouteEditor();
        return Task.CompletedTask;
    }

    private Task RemoveDecisionRouteAsync(
        WorkflowCanvasNodeDraft node,
        WorkflowCanvasEdgeDraft routeEdge)
    {
        if (routeEdge.SourceNodeId != node.Id)
        {
            return Task.CompletedTask;
        }

        document.Edges.Remove(routeEdge);
        if (decisionRouteEditingEdgeId == routeEdge.Id)
        {
            ResetDecisionRouteEditor();
        }

        SyncEdgeDefaults();
        return Task.CompletedTask;
    }

    private Task SaveDecisionRouteAsync(WorkflowCanvasNodeDraft node)
    {
        decisionRouteError = string.Empty;
        if (!IsDecisionNode(node) ||
            !string.Equals(decisionRouteEditorNodeId, node.Id.Value, StringComparison.Ordinal))
        {
            decisionRouteError = "Open a decision route editor before saving.";
            return Task.CompletedTask;
        }

        var routing = BuildDecisionRouteFromEditor();
        if (!TryValidateEdgeRouting(routing, out var routeError))
        {
            decisionRouteError = routeError;
            return Task.CompletedTask;
        }

        var target = ResolveDecisionRouteTargetNode(node, routing);
        if (target is null)
        {
            return Task.CompletedTask;
        }

        if (document.Edges.Any(edge =>
                edge.SourceNodeId == node.Id &&
                edge.TargetNodeId == target.Id &&
                edge.Id != decisionRouteEditingEdgeId))
        {
            decisionRouteError = "That decision route already points to the selected output.";
            return Task.CompletedTask;
        }

        var edgeKind = ResolveEdgeKindForRoute(routing.Kind);
        if (decisionRouteEditingEdgeId is { } edgeId &&
            document.Edges.FirstOrDefault(edge => edge.Id == edgeId) is { } existing)
        {
            existing.SourceNodeId = node.Id;
            existing.TargetNodeId = target.Id;
            existing.Kind = edgeKind;
            existing.ConditionExpression = string.Empty;
            existing.Routing = routing;
        }
        else
        {
            document.Edges.Add(new WorkflowCanvasEdgeDraft(
                CreateEdgeId(node.Id, target.Id),
                node.Id,
                target.Id)
            {
                Kind = edgeKind,
                Routing = routing
            });
        }

        ResetDecisionRouteEditor();
        SyncEdgeDefaults();
        NotificationService.Success("Decision route saved", WorkflowCanvasDefinitionMapper.ResolveRouteLabel(
            new WorkflowCanvasEdgeDraft(new WorkflowEdgeId("route-preview"), node.Id, target.Id)
            {
                Routing = routing
            }));
        return Task.CompletedTask;
    }

    private void BeginDecisionRouteEdit(
        WorkflowCanvasNodeDraft node,
        WorkflowCanvasEdgeDraft? routeEdge)
    {
        decisionRouteEditorNodeId = node.Id.Value;
        decisionRouteEditingEdgeId = routeEdge?.Id;
        decisionRouteError = string.Empty;

        if (routeEdge is not null)
        {
            decisionRouteTargetNodeId = routeEdge.TargetNodeId.Value;
            ApplyRouteToDecisionEditor(routeEdge.Routing);
            return;
        }

        var outgoingRoutes = GetDecisionRouteEdges(node);
        var routeKind = InferDecisionRouteKind(outgoingRoutes);
        var routeNumber = outgoingRoutes.Count + 1;
        decisionRouteTargetNodeId = string.Empty;
        decisionRouteKind = routeKind;
        decisionRouteLabel = ResolveDefaultDecisionRouteLabel(routeKind, routeNumber);
        decisionRouteJsonPath = ResolveDefaultDecisionRouteJsonPath(routeKind, outgoingRoutes);
        decisionRouteOperator = routeKind == WorkflowRouteKind.FanOutSelector
            ? WorkflowRouteOperator.Contains
            : WorkflowRouteOperator.Equals;
        decisionRouteValueKind = WorkflowRouteValueKind.String;
        decisionRouteExpectedValue = routeKind == WorkflowRouteKind.FanOutSelector
            ? $"target-{routeNumber}"
            : $"case-{routeNumber}";
        decisionRouteCaseSensitive = false;
        decisionRouteFanOutTargetIndex = routeKind == WorkflowRouteKind.FanOutSelector
            ? outgoingRoutes.Count
            : null;
    }

    private void ResetDecisionRouteEditor()
    {
        decisionRouteEditorNodeId = null;
        decisionRouteEditingEdgeId = null;
        decisionRouteTargetNodeId = string.Empty;
        decisionRouteKind = WorkflowRouteKind.SwitchCase;
        decisionRouteLabel = string.Empty;
        decisionRouteJsonPath = "$.route";
        decisionRouteOperator = WorkflowRouteOperator.Equals;
        decisionRouteValueKind = WorkflowRouteValueKind.String;
        decisionRouteExpectedValue = "case";
        decisionRouteCaseSensitive = false;
        decisionRouteFanOutTargetIndex = null;
        decisionRouteError = string.Empty;
    }

    private WorkflowCanvasNodeDraft? ResolveDecisionRouteTargetNode(
        WorkflowCanvasNodeDraft sourceNode,
        WorkflowEdgeRouting routing)
    {
        if (string.IsNullOrWhiteSpace(decisionRouteTargetNodeId))
        {
            return CreateDecisionRouteTargetNode(sourceNode, routing);
        }

        var targetId = new WorkflowNodeId(decisionRouteTargetNodeId.Trim());
        var target = document.Nodes.FirstOrDefault(node => node.Id == targetId);
        if (target is null)
        {
            decisionRouteError = "Choose an existing output node or create a new branch output.";
            return null;
        }

        if (target.Id == sourceNode.Id)
        {
            decisionRouteError = "A decision route cannot target its own decision node.";
            return null;
        }

        return target;
    }

    private WorkflowCanvasNodeDraft CreateDecisionRouteTargetNode(
        WorkflowCanvasNodeDraft sourceNode,
        WorkflowEdgeRouting routing)
    {
        var routeCount = GetDecisionRouteEdges(sourceNode).Count;
        var target = WorkflowCanvasDefinitionMapper.CreateNode(
            WorkflowNodeKind.StrictLogic,
            document.Nodes,
            componentOptions,
            sourceNode.CanvasX + 320,
            sourceNode.CanvasY + (Math.Max(routeCount, 0) * 120));
        var targetName = ResolveDecisionRouteTargetName(routing, routeCount + 1);
        target.Name = targetName;
        target.Instructions = $"Handle {targetName} routed from {sourceNode.Name}.";
        target.InputShapeKind = WorkflowValueShapeKind.Json;
        target.ResultShapeKind = WorkflowValueShapeKind.Json;
        document.Nodes.Add(target);

        var end = document.Nodes.FirstOrDefault(node => node.Kind == WorkflowNodeKind.End);
        if (end is not null)
        {
            document.Edges.Add(new WorkflowCanvasEdgeDraft(
                CreateEdgeId(target.Id, end.Id),
                target.Id,
                end.Id));
        }

        return target;
    }

    private static WorkflowRouteKind InferDecisionRouteKind(IReadOnlyList<WorkflowCanvasEdgeDraft> outgoingRoutes)
    {
        if (outgoingRoutes.Any(edge => edge.Routing.Kind == WorkflowRouteKind.FanOutSelector))
        {
            return WorkflowRouteKind.FanOutSelector;
        }

        if (outgoingRoutes.Any(edge => edge.Routing.Kind is WorkflowRouteKind.SwitchCase or WorkflowRouteKind.SwitchDefault))
        {
            return WorkflowRouteKind.SwitchCase;
        }

        return WorkflowRouteKind.Predicate;
    }

    private static string ResolveDefaultDecisionRouteLabel(
        WorkflowRouteKind routeKind,
        int routeNumber)
        => routeKind switch
        {
            WorkflowRouteKind.FanOutSelector => $"Fan-out {routeNumber}",
            WorkflowRouteKind.SwitchCase => $"Case {routeNumber}",
            WorkflowRouteKind.SwitchDefault => "DEFAULT",
            _ => $"IF {routeNumber}"
        };

    private static string ResolveDefaultDecisionRouteJsonPath(
        WorkflowRouteKind routeKind,
        IReadOnlyList<WorkflowCanvasEdgeDraft> outgoingRoutes)
    {
        var existingPath = outgoingRoutes
            .Select(edge => edge.Routing.JsonPath)
            .FirstOrDefault(path => !string.IsNullOrWhiteSpace(path));
        if (!string.IsNullOrWhiteSpace(existingPath))
        {
            return existingPath;
        }

        return routeKind switch
        {
            WorkflowRouteKind.FanOutSelector => "$.targets",
            WorkflowRouteKind.SwitchCase => "$.route",
            _ => "$.status"
        };
    }

    private static string ResolveDecisionRouteTargetName(
        WorkflowEdgeRouting routing,
        int routeNumber)
    {
        if (!string.IsNullOrWhiteSpace(routing.Label))
        {
            return ToBranchTargetName(routing.Label);
        }

        var expectedValue = FormatExpectedValueForEditor(routing);
        if (!string.IsNullOrWhiteSpace(expectedValue))
        {
            return ToBranchTargetName(expectedValue);
        }

        return routing.Kind switch
        {
            WorkflowRouteKind.SwitchDefault => "Unhandled",
            WorkflowRouteKind.FanOutSelector => $"Fan Out {routeNumber}",
            _ => $"Branch {routeNumber}"
        };
    }

    private void ApplyRouteToDecisionEditor(WorkflowEdgeRouting routing)
    {
        decisionRouteKind = routing.Kind == WorkflowRouteKind.Always
            ? WorkflowRouteKind.Predicate
            : routing.Kind;
        decisionRouteLabel = routing.Label;
        decisionRouteJsonPath = string.IsNullOrWhiteSpace(routing.JsonPath)
            ? ResolveDefaultDecisionRouteJsonPath(decisionRouteKind, [])
            : routing.JsonPath;
        decisionRouteOperator = routing.Operator;
        decisionRouteValueKind = routing.ExpectedValueKind;
        decisionRouteExpectedValue = FormatExpectedValueForEditor(routing);
        decisionRouteCaseSensitive = routing.CaseSensitive;
        decisionRouteFanOutTargetIndex = routing.FanOutTargetIndex;
    }

    private WorkflowEdgeRouting BuildEdgeRoutingFromEditor()
        => BuildRouteFromFields(
            edgeRouteKind,
            edgeRouteLabel,
            edgeRouteJsonPath,
            edgeRouteOperator,
            edgeRouteValueKind,
            edgeRouteExpectedValue,
            edgeRouteFanOutTargetIndex,
            edgeRouteCaseSensitive);

    private WorkflowEdgeRouting BuildDecisionRouteFromEditor()
        => BuildRouteFromFields(
            decisionRouteKind,
            decisionRouteLabel,
            decisionRouteJsonPath,
            decisionRouteOperator,
            decisionRouteValueKind,
            decisionRouteExpectedValue,
            decisionRouteFanOutTargetIndex,
            decisionRouteCaseSensitive);

    private static WorkflowEdgeRouting BuildRouteFromFields(
        WorkflowRouteKind routeKind,
        string routeLabel,
        string routeJsonPath,
        WorkflowRouteOperator routeOperator,
        WorkflowRouteValueKind routeValueKind,
        string routeExpectedValue,
        int? routeFanOutTargetIndex,
        bool routeCaseSensitive)
    {
        var label = routeLabel.Trim();
        var jsonPath = routeJsonPath.Trim();
        var expectedValueJson = ShowsExpectedValue(routeKind, routeOperator)
            ? NormalizeExpectedValueJson(routeExpectedValue, routeValueKind)
            : string.Empty;

        return routeKind switch
        {
            WorkflowRouteKind.Always => WorkflowEdgeRouting.Always with
            {
                Label = label
            },
            WorkflowRouteKind.SwitchCase => WorkflowEdgeRouting.SwitchCase(
                jsonPath,
                expectedValueJson,
                routeValueKind,
                label,
                routeCaseSensitive),
            WorkflowRouteKind.SwitchDefault => WorkflowEdgeRouting.SwitchDefault(label),
            WorkflowRouteKind.FanOutSelector => WorkflowEdgeRouting.FanOutSelector(
                jsonPath,
                routeOperator,
                expectedValueJson,
                routeValueKind,
                routeFanOutTargetIndex,
                label,
                routeCaseSensitive),
            _ => WorkflowEdgeRouting.Predicate(
                jsonPath,
                routeOperator,
                expectedValueJson,
                routeValueKind,
                label,
                routeCaseSensitive)
        };
    }

    private static bool TryValidateEdgeRouting(WorkflowEdgeRouting routing, out string error)
    {
        error = string.Empty;
        if (routing.FanOutTargetIndex is < 0)
        {
            error = "Fan-out target index must be zero or greater.";
            return false;
        }

        if (WorkflowRoutingValidation.RequiresJsonPath(routing) &&
            !WorkflowRoutingValidation.TryParseJsonPath(routing.JsonPath, out _, out var pathError))
        {
            error = $"Route JSON path is invalid: {pathError}.";
            return false;
        }

        if (!WorkflowRoutingValidation.TryValidateExpectedValue(routing, out var valueError))
        {
            error = $"Route expected value is invalid: {valueError}.";
            return false;
        }

        return true;
    }

    private static WorkflowEdgeKind ResolveEdgeKindForRoute(WorkflowRouteKind routeKind)
        => routeKind switch
        {
            WorkflowRouteKind.Predicate or WorkflowRouteKind.SwitchCase or WorkflowRouteKind.SwitchDefault => WorkflowEdgeKind.Conditional,
            WorkflowRouteKind.FanOutSelector => WorkflowEdgeKind.FanOut,
            _ => WorkflowEdgeKind.Direct
        };

    private void HandleEdgeRouteKindChanged(ChangeEventArgs args)
    {
        if (!Enum.TryParse<WorkflowRouteKind>(args.Value?.ToString(), out var routeKind))
        {
            return;
        }

        edgeRouteKind = routeKind;
        edgeKind = ResolveEdgeKindForRoute(routeKind);
        if (routeKind == WorkflowRouteKind.SwitchDefault)
        {
            edgeRouteJsonPath = string.Empty;
            edgeRouteExpectedValue = string.Empty;
            edgeRouteFanOutTargetIndex = null;
        }
        else if (routeKind == WorkflowRouteKind.SwitchCase ||
                 !WorkflowRoutingValidation.RequiresExpectedValue(edgeRouteOperator))
        {
            edgeRouteOperator = WorkflowRouteOperator.Equals;
        }

        if (routeKind != WorkflowRouteKind.SwitchDefault &&
            string.IsNullOrWhiteSpace(edgeRouteJsonPath))
        {
            edgeRouteJsonPath = "$.status";
        }
    }

    private void HandleEdgeFanOutTargetIndexChanged(ChangeEventArgs args)
    {
        var value = ReadString(args);
        edgeRouteFanOutTargetIndex = int.TryParse(value, out var targetIndex)
            ? targetIndex
            : null;
    }

    private void HandleDecisionRouteKindChanged(ChangeEventArgs args)
    {
        if (!Enum.TryParse<WorkflowRouteKind>(args.Value?.ToString(), out var routeKind) ||
            routeKind == WorkflowRouteKind.Always)
        {
            return;
        }

        decisionRouteKind = routeKind;
        if (routeKind == WorkflowRouteKind.SwitchDefault)
        {
            decisionRouteJsonPath = string.Empty;
            decisionRouteExpectedValue = string.Empty;
            decisionRouteFanOutTargetIndex = null;
            decisionRouteOperator = WorkflowRouteOperator.Exists;
            decisionRouteValueKind = WorkflowRouteValueKind.Json;
            decisionRouteLabel = string.IsNullOrWhiteSpace(decisionRouteLabel)
                ? "DEFAULT"
                : decisionRouteLabel;
            return;
        }

        if (string.IsNullOrWhiteSpace(decisionRouteJsonPath))
        {
            decisionRouteJsonPath = routeKind == WorkflowRouteKind.FanOutSelector
                ? "$.targets"
                : "$.route";
        }

        if (routeKind == WorkflowRouteKind.FanOutSelector)
        {
            decisionRouteOperator = WorkflowRouteOperator.Contains;
            decisionRouteFanOutTargetIndex ??= CountCurrentDecisionRoutes();
            if (string.IsNullOrWhiteSpace(decisionRouteExpectedValue))
            {
                decisionRouteValueKind = WorkflowRouteValueKind.String;
                decisionRouteExpectedValue = $"target-{decisionRouteFanOutTargetIndex.Value + 1}";
            }
        }
        else
        {
            decisionRouteFanOutTargetIndex = null;
            if (!WorkflowRoutingValidation.RequiresExpectedValue(decisionRouteOperator))
            {
                decisionRouteOperator = WorkflowRouteOperator.Equals;
            }

            if (string.IsNullOrWhiteSpace(decisionRouteExpectedValue))
            {
                decisionRouteValueKind = WorkflowRouteValueKind.String;
                decisionRouteExpectedValue = $"case-{CountCurrentDecisionRoutes() + 1}";
            }
        }

        if (string.IsNullOrWhiteSpace(decisionRouteLabel))
        {
            decisionRouteLabel = ResolveDefaultDecisionRouteLabel(routeKind, CountCurrentDecisionRoutes() + 1);
        }
    }

    private void HandleDecisionRouteFanOutTargetIndexChanged(ChangeEventArgs args)
    {
        var value = ReadString(args);
        decisionRouteFanOutTargetIndex = int.TryParse(value, out var targetIndex)
            ? targetIndex
            : null;
    }

    private int CountCurrentDecisionRoutes()
        => string.IsNullOrWhiteSpace(decisionRouteEditorNodeId)
            ? 0
            : document.Edges.Count(edge => edge.SourceNodeId.Value == decisionRouteEditorNodeId);

    private static bool IsPredicateRoute(WorkflowRouteKind routeKind)
        => routeKind is WorkflowRouteKind.Predicate or WorkflowRouteKind.SwitchCase or WorkflowRouteKind.FanOutSelector;

    private static bool ShowsOperator(WorkflowRouteKind routeKind)
        => routeKind is WorkflowRouteKind.Predicate or WorkflowRouteKind.FanOutSelector;

    private static bool ShowsExpectedValue(WorkflowRouteKind routeKind, WorkflowRouteOperator routeOperator)
        => routeKind == WorkflowRouteKind.SwitchCase ||
           IsPredicateRoute(routeKind) && WorkflowRoutingValidation.RequiresExpectedValue(routeOperator);

    private static bool ShowsCaseSensitivity(WorkflowRouteKind routeKind, WorkflowRouteOperator routeOperator)
        => ShowsExpectedValue(routeKind, routeOperator) &&
           routeOperator is
               WorkflowRouteOperator.Equals or
               WorkflowRouteOperator.NotEquals or
               WorkflowRouteOperator.Contains or
               WorkflowRouteOperator.StartsWith or
               WorkflowRouteOperator.EndsWith;

    private static string NormalizeExpectedValueJson(string value, WorkflowRouteValueKind valueKind)
    {
        var trimmed = value.Trim();
        if (valueKind == WorkflowRouteValueKind.String)
        {
            if (TryDeserializeRouteJson<string>(trimmed, out _))
            {
                return trimmed;
            }

            return JsonSerializer.Serialize(trimmed, ExecutorJsonOptions);
        }

        return valueKind switch
        {
            WorkflowRouteValueKind.Null => "null",
            WorkflowRouteValueKind.Boolean when bool.TryParse(trimmed, out var boolean) => boolean ? "true" : "false",
            _ => trimmed
        };
    }

    private static string FormatExpectedValueForEditor(WorkflowEdgeRouting routing)
    {
        if (!WorkflowRoutingValidation.RequiresExpectedValue(routing.Operator))
        {
            return string.Empty;
        }

        if (routing.ExpectedValueKind == WorkflowRouteValueKind.String &&
            TryDeserializeRouteJson<string>(routing.ExpectedValueJson, out var value))
        {
            return value ?? string.Empty;
        }

        return routing.ExpectedValueJson;
    }

    private static bool TryDeserializeRouteJson<TValue>(string json, out TValue? value)
    {
        try
        {
            value = string.IsNullOrWhiteSpace(json)
                ? default
                : JsonSerializer.Deserialize<TValue>(json, ExecutorJsonOptions);
            return true;
        }
        catch (JsonException)
        {
            value = default;
            return false;
        }
    }
}
