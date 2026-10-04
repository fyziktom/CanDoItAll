using static CanDoItAll.AgentFramework.WorkflowAuthoring.UI.WorkflowRouteEditing;
using CanDoItAll.AgentFramework.Workflows.Definitions;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Components.CanvasLib;
using CanDoItAll.Components.OverlayLib;
using CanDoItAll.SharedKernel;
using CanDoItAll.SharedKernel.Configuration;
using CanDoItAll.Modules.Prompts;
using CanDoItAll.Modules.Prompts.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.AgentFramework.WorkflowAuthoring.UI;

public partial class WorkflowCanvasSurface : IHandleEvent, IDisposable {
    private readonly string canvasInstanceId = $"workflow-canvas-{Guid.NewGuid():N}";
    private readonly string ToolboxWindowId = $"workflow-canvas-toolbox-{Guid.NewGuid():N}";
    private readonly string SelectionWindowId = $"workflow-canvas-selection-{Guid.NewGuid():N}";
    private readonly string ComponentsWindowId = $"workflow-canvas-components-{Guid.NewGuid():N}";
    private const string CanvasCreateConnectionActionId = "connection:create";
    private const string CanvasDeleteNodeActionId = "delete";
    private const string CanvasDeleteLinkActionId = "delete-link";
    private const string CanvasLinkTargetKind = "link";

    private static readonly JsonSerializerOptions ExecutorJsonOptions = CreateExecutorJsonOptions(writeIndented: false);
    private static readonly JsonSerializerOptions IndentedExecutorJsonOptions = CreateExecutorJsonOptions(writeIndented: true);

    [Parameter, EditorRequired] public WorkflowDocumentOperations DocumentOperations { get; set; } = default!;
    [Parameter, EditorRequired] public WorkflowPreviewOperations PreviewOperations { get; set; } = default!;
    [Parameter, EditorRequired] public Func<WorkflowPromptBindingRequest, CancellationToken, Task<WorkflowPromptBindingOutcome>> BindPrompt { get; set; } = default!;
    [Parameter] public IReadOnlyList<WorkflowExecutorDescriptor> Executors { get; set; } = [];
    [Parameter] public IReadOnlyList<WorkflowRuntimeBackendDescriptor> Backends { get; set; } = [];
    [Parameter] public IReadOnlyList<WorkflowSecretOption> Secrets { get; set; } = [];
    [Parameter] public string SecretLoadError { get; set; } = string.Empty;
    [Parameter] public CancellationToken OwnerToken { get; set; }
    [Parameter] public RenderFragment<WorkflowPromptPickerContext>? PromptPicker { get; set; }
    [Parameter] public RenderFragment<WorkflowExecutorSettingsContext>? SettingsRenderer { get; set; }

    [Inject]
    public NotificationService NotificationService { get; set; } = default!;

    [Inject]
    public ILogger<WorkflowCanvasSurface> Logger { get; set; } = default!;

    [Parameter] public EventCallback NewDraftRequested { get; set; }

    [Parameter]
    public WorkflowDefinition? Definition { get; set; }

    [Parameter]
    public IReadOnlyList<LlmCallComponent> Components { get; set; } = [];

    [Parameter]
    public IReadOnlyList<WorkflowProviderOption> ProviderOptions { get; set; } = [];

    [Parameter]
    public EventCallback<WorkflowDefinition> DefinitionSaved { get; set; }

    [Parameter]
    public EventCallback<WorkflowRunSnapshot> PreviewRunCompleted { get; set; }

    [Parameter]
    public EventCallback ComponentLibraryChanged { get; set; }

    [Parameter]
    public EventCallback<WorkflowCanvasNodeSelection?> SelectedNodeChanged { get; set; }

    private bool disposed;
    private bool saveUnknown;
    private bool bindingUnknown;
    private bool previewUnknown;
    private long revision;
    private long? validationRevision;
    private long? previewRevision;
    private string observedDraft = string.Empty;
    private readonly List<LlmCallComponent> acceptedBindings = [];
    public WorkflowDefinition? AcceptedDefinition { get; private set; }
    public WorkflowRunId? AcceptedPreviewRunId { get; private set; }
    private bool IsCurrent => !disposed && !OwnerToken.IsCancellationRequested;
    private bool Owns(WorkflowCanvasDocument occurrence) => IsCurrent && ReferenceEquals(document, occurrence);

    private WorkflowCanvasDocument document = WorkflowCanvasDefinitionMapper.CreateDraft([]);
    private IReadOnlyList<LlmCallComponent> componentOptions = [];
    private IReadOnlyList<WorkflowExecutorDescriptor> executorDescriptors = [];
    private IReadOnlyList<WorkflowValidationIssue> validationIssues = [];
    private WorkflowPreviewOutcome.Completed? testResult;
    private string loadedDefinitionKey = string.Empty;
    private string? selectedNodeId = "start";
    private readonly WorkflowRouteDraft edgeRoute = new();
    private readonly WorkflowRouteDraft decisionRoute = new() { Kind = WorkflowRouteKind.SwitchCase, JsonPath = "$.route", ExpectedValue = "case" };
    private string edgeSourceNodeId = "start";
    private string edgeTargetNodeId = "end";
    private WorkflowEdgeKind edgeKind = WorkflowEdgeKind.Direct;
    private string edgeCondition = string.Empty;
    private WorkflowEdgeId? editingEdgeId;
    private string? decisionRouteEditorNodeId;
    private WorkflowEdgeId? decisionRouteEditingEdgeId;
    private string decisionRouteTargetNodeId = string.Empty;
    private string decisionRouteError = string.Empty;
    private string newComponentProviderProfileId = string.Empty;
    private string newComponentModel = string.Empty;
    private string workflowToolboxSearchText = string.Empty;
    private string? expandedWorkflowToolboxGroupKey = "workflow-decisions";
    private string testInputJson = WorkflowPreviewInputSupport.DefaultInputJson;
    private WorkflowPreviewInputState previewInputState = new();
    private IReadOnlyList<WorkflowPreviewProject> previewProjectOptions = [];
    private WorkflowDefinition? previewInputDefinition;
    private long previewInputRevision;
    private string previewInputErrorMessage = string.Empty;
    private string errorMessage = string.Empty;
    private IReadOnlyList<WorkflowSecretOption> secretPickerItems = [];
    private string secretPickerErrorMessage = string.Empty;
    private CanvasWorkbenchUiState canvasUiState = CreateWorkflowCanvasUiState("start");
    private CanvasWorkbench? workbenchRef;
    private CanvasWorkbenchWindowState toolboxWindowState = CreateWindowState(width: 300, height: 380);
    private CanvasWorkbenchWindowState selectionWindowState = CreateWindowState(width: 260, height: 320);
    private CanvasWorkbenchWindowState componentsWindowState = CreateWindowState(width: 320, height: 380, isVisible: false);
    private bool isNodeDetailsDialogOpen;
    private WorkflowCanvasNodeDraft? detailsNode;
    private bool isPreviewInputDialogOpen;
    private bool isBusy;
    private bool isTesting;
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
        WorkflowPortId? SourcePortId,
        WorkflowPortId? TargetPortId,
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
            selectedNodeId,
            canvasInstanceId);

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
            Value = validationRevision != revision ? "Not validated" : validationIssues.Count == 0 ? "Valid" : validationIssues.Count.ToString(),
            Tone = validationIssues.Count == 0 ? "success" : "warning"
        }
    ];

    private int CountUsedComponents()
        => document.Nodes.Count(node => node.ComponentId.HasValue);

    private int CountUsedExecutors()
        => document.Nodes.Count(node => node.ExecutorId.HasValue);

    protected override async Task OnParametersSetAsync()
    {
        componentOptions = acceptedBindings.AsEnumerable().Reverse().Concat(Components).DistinctBy(item => item.Id).ToArray();
        secretPickerItems = Secrets;
        secretPickerErrorMessage = SecretLoadError;
        executorDescriptors = Executors;
        SyncNewComponentDefaults();
        var incomingKey = Definition is null
            ? "draft"
            : Definition.Id.ToString();
        if (string.Equals(incomingKey, loadedDefinitionKey, StringComparison.Ordinal))
        {
            return;
        }

        if (Definition is not null && Definition.Id == AcceptedDefinition?.Id && Definition.Id == document.DefinitionId) {
            loadedDefinitionKey = incomingKey;
            return;
        }
        isBusy = false;
        isTesting = false;
        activePreviewAttempt = null;
        errorMessage = string.Empty;
        document = Definition is null
            ? WorkflowCanvasDefinitionMapper.CreateDraft(componentOptions)
            : WorkflowCanvasDefinitionMapper.FromDefinition(Definition, componentOptions);
        loadedDefinitionKey = incomingKey;
        saveUnknown = false;
        bindingUnknown = false;
        previewUnknown = false;
        revision = 0;
        validationRevision = null;
        previewRevision = null;
        observedDraft = DraftSignature();
        detailsNode = null;
        isNodeDetailsDialogOpen = false;
        selectedNodeId = document.StartNodeId.Value;
        canvasUiState = CreateWorkflowCanvasUiState(selectedNodeId);
        validationIssues = [];
        testResult = null;
        isPreviewInputDialogOpen = false;
        previewInputDefinition = null;
        SyncEdgeDefaults();
        await NotifySelectedNodeChangedAsync();
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

    private async Task ResetDraftAsync() {
        if (NewDraftRequested.HasDelegate) {
            await NewDraftRequested.InvokeAsync();
            return;
        }
        document = WorkflowCanvasDefinitionMapper.CreateDraft(componentOptions);
        loadedDefinitionKey = "draft";
        isBusy = false;
        isTesting = false;
        activePreviewAttempt = null;
        isPreviewInputDialogOpen = false;
        validationRevision = null;
        previewRevision = null;
        detailsNode = null;
        isNodeDetailsDialogOpen = false;
        selectedNodeId = document.StartNodeId.Value;
        canvasUiState = CreateWorkflowCanvasUiState(selectedNodeId);
        validationIssues = [];
        testResult = null;
        errorMessage = string.Empty;
        SyncEdgeDefaults();
        await NotifySelectedNodeChangedAsync();
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
        await SelectNodeAsync(node.Id.Value);
        SyncEdgeDefaults();
    }

    private async Task AddLlmComponentNodeAsync(
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
        await SelectNodeAsync(node.Id.Value);
        SyncEdgeDefaults();
    }

    private async Task AddExecutorNodeAsync(
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
        ApplyExecutorCreateRequest(node, descriptor, request);
        document.Nodes.Add(node);
        InsertNodeBeforeEnd(node);
        await SelectNodeAsync(node.Id.Value);
        SyncEdgeDefaults();
    }

    private WorkflowPromptPickerContext CreatePromptPickerContext(string text, WorkflowCanvasNodeDraft? node) {
        var occurrence = document;
        return new(text, ResolvePromptPickerProvider(node), ResolvePromptPickerModel(node), (object?)node ?? occurrence,
            isBusy || bindingUnknown, EventCallback.Factory.Create<PromptGallerySelection>(this, selection => {
                if (!Owns(occurrence)) {
                    return Task.CompletedTask;
                }
                return node is null ? HandlePromptGallerySelectionForNewNodeAsync(selection)
                    : HandlePromptGallerySelectionForNodeAsync(selection, node);
            }));
    }

    private async Task HandlePromptGallerySelectionForNewNodeAsync(PromptGallerySelection selection)
    {
        var component = await CreatePromptGalleryBindingAsync(selection, node: null);
        if (component is not null)
        {
            await AddLlmComponentNodeAsync(component);
            await NotifyComponentLibraryChangedAsync();
        }
    }

    private async Task HandlePromptGallerySelectionForNodeAsync(
        PromptGallerySelection selection,
        WorkflowCanvasNodeDraft node)
    {
        var component = await CreatePromptGalleryBindingAsync(selection, node);
        if (component is null)
        {
            return;
        }

        WorkflowCanvasDefinitionMapper.ApplyComponent(node, component);
        node.Instructions = selection.Content;
        node.Name = string.IsNullOrWhiteSpace(node.Name) ? selection.Title : node.Name;
        await NotifyComponentLibraryChangedAsync();
    }

    private async Task<LlmCallComponent?> CreatePromptGalleryBindingAsync(
        PromptGallerySelection selection, WorkflowCanvasNodeDraft? node) {
        if (!IsCurrent || isBusy || bindingUnknown || node is not null && (!document.Nodes.Contains(node) || !ReferenceEquals(node, SelectedNode))) {
            return null;
        }
        var occurrence = document;
        var pair = ResolvePromptBindingExecutionPair(selection, node);
        if (pair is null) {
            NotificationService.Warning("Provider and model required", "Select an enabled compatible provider and an available model before binding this prompt.");
            return null;
        }
        var current = node is null ? null : ResolveSelectedComponent(node);
        var targetSignature = node is null ? null : JsonSerializer.Serialize(node);
        var targetRevision = revision;
        isBusy = true;
        try {
            var outcome = await BindPrompt(new(selection, pair.Provider, pair.Model, current), OwnerToken);
            if (outcome is WorkflowPromptBindingOutcome.Accepted accepted) {
                acceptedBindings.Add(accepted.Component);
                if (!Owns(occurrence)) {
                    return null;
                }
                componentOptions = new[] { accepted.Component }.Concat(componentOptions).DistinctBy(item => item.Id).ToArray();
                if (node is not null && (!document.Nodes.Contains(node) || !ReferenceEquals(node, SelectedNode) || revision != targetRevision || targetSignature != JsonSerializer.Serialize(node))) {
                    errorMessage = "The component was saved. Its original node changed; select the saved component to bind it.";
                    return null;
                }
                NotificationService.Success("Gallery prompt bound", "The accepted component pins this Prompt revision.");
                return accepted.Component;
            }
            if (Owns(occurrence)) {
                bindingUnknown = outcome is WorkflowPromptBindingOutcome.Unknown;
                errorMessage = outcome is WorkflowPromptBindingOutcome.Rejected rejected
                    ? rejected.Message
                    : "The component save outcome is unknown. Inspect the native library before another binding attempt.";
                NotificationService.Warning("Prompt binding needs attention", errorMessage);
            }
        } catch (Exception error) {
            if (Owns(occurrence)) {
                bindingUnknown = true;
                errorMessage = "The component save outcome is unknown. Inspect the native library before another binding attempt.";
                Logger.LogWarning("Workflow component acknowledgement unavailable: {FailureType}", error.GetType().Name);
            }
        } finally {
            if (Owns(occurrence)) {
                isBusy = false;
            }
        }
        return null;
    }

    private async Task NotifyComponentLibraryChangedAsync() {
        try {
            await ComponentLibraryChanged.InvokeAsync();
        } catch (Exception error) {
            if (IsCurrent) {
                errorMessage = "The component is saved and retained in this editor. Library refresh is unavailable.";
                Logger.LogWarning("Workflow component refresh failed after acceptance: {FailureType}", error.GetType().Name);
            }
        }
    }

    private PromptBindingExecutionPair? ResolvePromptBindingExecutionPair(
        PromptGallerySelection selection,
        WorkflowCanvasNodeDraft? node)
    {
        var currentProvider = node is null
            ? ResolveSelectedNewComponentProvider()
            : ResolveSelectedNodeProvider(node);
        var currentModel = node is null ? newComponentModel : node.Model;

        if (currentProvider is { IsSourceManaged: true }) {
            return currentProvider.IsEnabled && SupportsModel(currentProvider, currentModel)
                ? new PromptBindingExecutionPair(currentProvider, currentModel) : null;
        }
        if (currentProvider is null && (node?.ProviderProfileId.HasValue == true || newComponentProviderProfileId.Length > 0)) {
            return null;
        }

        foreach (var supported in selection.SupportedModels.Where(item => item.IsPreferred))
        {
            if (TryResolvePromptBindingPair(supported, currentProvider, out var pair))
            {
                return pair;
            }
        }

        if (selection.SupportedModels.Count == 1 &&
            TryResolvePromptBindingPair(selection.SupportedModels[0], currentProvider, out var singlePair))
        {
            return singlePair;
        }

        if (currentProvider is { IsEnabled: true } &&
            !string.IsNullOrWhiteSpace(currentModel) &&
            SupportsExecutionPair(selection, currentProvider, currentModel))
        {
            return new PromptBindingExecutionPair(currentProvider, currentModel.Trim());
        }

        foreach (var supported in selection.SupportedModels)
        {
            if (TryResolvePromptBindingPair(supported, currentProvider, out var pair))
            {
                return pair;
            }
        }

        if (selection.SupportedModels.Count > 0)
        {
            return null;
        }

        var fallbackProvider = currentProvider is { IsEnabled: true }
            ? currentProvider
            : ResolveDefaultProviderOption();
        return fallbackProvider is null
            ? null
            : new PromptBindingExecutionPair(fallbackProvider, ResolveDefaultModel(fallbackProvider));
    }

    private bool TryResolvePromptBindingPair(
        PromptProviderModel supported,
        WorkflowProviderOption? currentProvider,
        out PromptBindingExecutionPair? pair)
    {
        var providers = ProviderOptions
            .Where(option =>
                option.IsEnabled &&
                string.Equals(option.Kind.ToString(), supported.Provider, StringComparison.OrdinalIgnoreCase) &&
                SupportsModel(option, supported.Model))
            .OrderByDescending(option => option.ProviderProfileId == currentProvider?.ProviderProfileId)
            .ToArray();
        pair = providers.FirstOrDefault() is { } provider
            ? new PromptBindingExecutionPair(provider, supported.Model.Trim())
            : null;
        return pair is not null;
    }

    private static bool SupportsExecutionPair(
        PromptGallerySelection selection,
        WorkflowProviderOption provider,
        string model)
    {
        return selection.SupportedModels.Count == 0 ||
               selection.SupportedModels.Any(item =>
                   string.Equals(item.Provider, provider.Kind.ToString(), StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(item.Model, model.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    private static bool SupportsModel(WorkflowProviderOption provider, string model)
    {
        return !string.IsNullOrWhiteSpace(model) &&
               (!provider.IsSourceManaged && provider.ModelOptions.Count == 0 ||
                provider.ModelOptions.Contains(provider.IsSourceManaged ? model : model.Trim(),
                    provider.IsSourceManaged ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase));
    }

    private string? ResolvePromptPickerProvider(WorkflowCanvasNodeDraft? node)
    {
        var provider = node is null
            ? ResolveSelectedNewComponentProvider()
            : ResolveSelectedNodeProvider(node);
        return provider?.Kind.ToString();
    }

    private string? ResolvePromptPickerModel(WorkflowCanvasNodeDraft? node)
        => node is null
            ? newComponentModel
            : node.Model;

    private static string BuildPromptGalleryError(IReadOnlyList<Error> errors)
        => errors.Count == 0
            ? "The Prompt Gallery did not return a result."
            : string.Join(" ", errors.Select(error => error.Message));

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

        edgeKind = ResolveEdgeKindForRoute(edgeRoute.Kind);
        if (edgeRoute.HasInvalidFanOutIndex) {
            errorMessage = WorkflowRouteDraft.InvalidIndexMessage;
            return Task.CompletedTask;
        }
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

    private async Task RemoveNodeAsync(WorkflowCanvasNodeDraft node)
    {
        errorMessage = string.Empty;
        if (node.Kind is WorkflowNodeKind.Start or WorkflowNodeKind.End)
        {
            return;
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
                SourcePortId = bridgeEdge.SourcePortId,
                TargetPortId = bridgeEdge.TargetPortId,
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

        isNodeDetailsDialogOpen = isNodeDetailsDialogOpen && !ReferenceEquals(detailsNode, node);
        await SelectNodeAsync(bridge?.TargetNodeId.Value ?? document.StartNodeId.Value);
        SyncEdgeDefaults();
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
            incoming.SourcePortId,
            outgoing.TargetPortId,
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
        if (!IsCurrent || HasInvalidDraftFields()) {
            return;
        }
        await ValidateCurrentDefinitionAsync();
    }

    private async Task SaveAsync() {
        if (!IsCurrent || isBusy || saveUnknown || HasInvalidDraftFields()) {
            return;
        }
        var occurrence = document;
        var submitted = WorkflowCanvasDefinitionMapper.ToDefinition(document);
        var submittedNodes = document.Nodes.ToDictionary(node => node.Id);
        var submittedRevision = revision;
        isBusy = true;
        errorMessage = string.Empty;
        try {
            var outcome = await DocumentOperations.Save(new(document.DefinitionId, document.VersionId,
                submitted.Name, submitted.Description, submitted.Status, submitted.Graph, submitted.RuntimePolicy) {
                InputParameters = submitted.InputParameters,
                ExternalNamespace = submitted.ExternalNamespace,
                ExternalKey = submitted.ExternalKey
            }, OwnerToken);
            if (outcome is WorkflowDefinitionSaveOutcome.Accepted accepted) {
                AcceptedDefinition = accepted.Definition;
                if (!Owns(occurrence)) {
                    return;
                }
                WorkflowDocumentAcceptance.Merge(document, submitted, submittedNodes, accepted.Definition, revision != submittedRevision);
                errorMessage = revision == submittedRevision ? string.Empty : "Workflow saved. Later edits remain unsaved.";
                NotificationService.Success("Workflow saved", accepted.Definition.Name);
                try {
                    await DefinitionSaved.InvokeAsync(accepted.Definition);
                } catch (Exception error) {
                    if (Owns(occurrence)) {
                        errorMessage = "Workflow saved. Catalog refresh is unavailable; the accepted identity is retained.";
                        Logger.LogWarning("Workflow refresh failed after save {WorkflowId}/{VersionId}: {FailureType}",
                            accepted.Definition.Id, accepted.Definition.VersionId, error.GetType().Name);
                    }
                }
                return;
            }
            if (Owns(occurrence)) {
                saveUnknown = outcome is WorkflowDefinitionSaveOutcome.Unknown;
                errorMessage = outcome is WorkflowDefinitionSaveOutcome.Rejected { Reason: WorkflowSaveRejection.Conflict }
                    ? "The saved workflow changed. Your draft is retained; reopen the exact current version to resolve the conflict."
                    : saveUnknown ? "The save outcome is unknown. Inspect the native catalog before another save attempt."
                    : "The owner rejected this save without committing it. Review the workflow configuration.";
                NotificationService.Warning("Workflow save needs attention", errorMessage);
            }
        } catch (Exception error) {
            if (Owns(occurrence)) {
                saveUnknown = true;
                errorMessage = "The save outcome is unknown. Inspect the native catalog before another save attempt.";
                Logger.LogWarning("Workflow save acknowledgement unavailable: {FailureType}", error.GetType().Name);
            }
        } finally {
            if (Owns(occurrence)) {
                isBusy = false;
            }
        }
    }

    private async Task RunPreviewAsync()
    {
        if (!IsCurrent || isBusy || isTesting || previewUnknown || HasInvalidDraftFields())
        {
            return;
        }

        var definition = WorkflowCanvasDefinitionMapper.ToDefinition(document);
        WorkflowPreviewRequirements requirements;
        try {
            requirements = PreviewOperations.Analyze(definition);
        } catch (Exception error) {
            errorMessage = "Preview preparation is unavailable. Review the configuration before trying again. No run was submitted.";
            Logger.LogWarning("Workflow preview preparation failed for {WorkflowId}: {FailureType}", definition.Id, error.GetType().Name);
            return;
        }
        if (requirements.NeedsPreviewDialog)
        {
            await OpenPreviewInputDialogAsync(definition, requirements);
            return;
        }

        await RunPreviewCoreAsync(definition, testInputJson, WorkflowPreviewSimulationPlan.Empty, revision);
    }

    private Task OpenPreviewInputDialogAsync(
        WorkflowDefinition definition,
        WorkflowPreviewRequirements requirements)
    {
        previewInputDefinition = definition;
        previewInputRevision = revision;
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
        _ = LoadPreviewProjectOptionsAsync();
        return Task.CompletedTask;
    }

    private async Task LoadPreviewProjectOptionsAsync()
    {
        var occurrence = document;
        var state = previewInputState;
        try
        {
            var projects = await PreviewOperations.Projects(OwnerToken);
            if (!Owns(occurrence) || !isPreviewInputDialogOpen || !ReferenceEquals(state, previewInputState)) {
                return;
            }
            previewProjectOptions = projects;
            if (string.IsNullOrWhiteSpace(previewInputState.ProjectId) &&
                previewProjectOptions.Count == 1)
            {
                previewInputState.ProjectId = previewProjectOptions[0].Id.ToString("D");
            }
        }
        catch (Exception exception)
        {
            if (Owns(occurrence) && isPreviewInputDialogOpen && ReferenceEquals(state, previewInputState)) {
                state.ProjectLoadError = "Project list unavailable. Close and reopen the input dialog to retry the read.";
                Logger.LogWarning("Workflow preview project read failed: {FailureType}", exception.GetType().Name);
            }
        } finally {
            if (Owns(occurrence) && isPreviewInputDialogOpen && ReferenceEquals(state, previewInputState)) {
                await InvokeAsync(StateHasChanged);
            }
        }
    }

    private async Task StartPreviewFromInputDialogAsync()
    {
        if (!IsCurrent || isBusy || previewUnknown || previewInputDefinition is null)
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
        await RunPreviewCoreAsync(definition, inputJson, simulationPlan, previewInputRevision);
    }

    private void ClosePreviewInputDialog()
    {
        isPreviewInputDialogOpen = false;
        previewInputDefinition = null;
        previewInputErrorMessage = string.Empty;
    }

    private IReadOnlyList<WorkflowRuntimeBackendDescriptor> RuntimeBackendOptions
        => Backends;

    private static string BuildRuntimeBackendOptionText(WorkflowRuntimeBackendDescriptor backend)
        => backend.IsRunnable
            ? backend.Kind.ToString()
            : $"{backend.Kind} ({backend.Availability})";

    private async Task RunPreviewCoreAsync(WorkflowDefinition definition, string inputJson,
        WorkflowPreviewSimulationPlan simulationPlan, long submittedRevision) {
        if (!IsCurrent || isBusy || previewUnknown) {
            return;
        }
        var occurrence = document;
        var attempt = new object();
        activePreviewAttempt = attempt;
        isBusy = true;
        isTesting = true;
        errorMessage = string.Empty;
        try {
            var outcome = await PreviewOperations.Run(new(definition, inputJson, simulationPlan), nodeId =>
                Owns(occurrence) && ReferenceEquals(activePreviewAttempt, attempt) && revision == submittedRevision
                    ? InvokeAsync(() => SelectPreviewNodeAsync(nodeId)) : Task.CompletedTask, OwnerToken);
            if (outcome is WorkflowPreviewOutcome.Completed completed) {
                AcceptedPreviewRunId = completed.Run?.RunId;
                if (!Owns(occurrence)) {
                    return;
                }
                testResult = completed;
                previewRevision = submittedRevision;
                if (revision == submittedRevision) {
                    validationIssues = completed.Validation.Issues;
                    validationRevision = submittedRevision;
                }
                if (completed.Run is { } run) {
                    try {
                        await PreviewRunCompleted.InvokeAsync(run);
                    } catch (Exception error) {
                        if (Owns(occurrence)) {
                            errorMessage = "The run is recorded. Opening its details failed; use its accepted run identity in History.";
                            Logger.LogWarning("Workflow preview details unavailable for {RunId}: {FailureType}", run.RunId, error.GetType().Name);
                        }
                    }
                }
                if (!Owns(occurrence)) {
                    return;
                }
                if (!completed.DetailsComplete) {
                    NotificationService.Warning("Workflow run recorded", "The run is recorded. Its detailed status is not available yet.");
                } else if (completed.Succeeded) {
                    NotificationService.Success("Workflow preview completed", completed.Summary);
                } else {
                    errorMessage = "The preview could not be completed. Review the workflow configuration and event diagnostics.";
                    NotificationService.Error("Workflow preview failed", errorMessage);
                }
            } else if (Owns(occurrence)) {
                previewUnknown = outcome is WorkflowPreviewOutcome.Unknown;
                AcceptedPreviewRunId = (outcome as WorkflowPreviewOutcome.Unknown)?.ReservedRunId;
                errorMessage = outcome is WorkflowPreviewOutcome.Rejected rejected ? rejected.Message
                    : "The preview admission outcome is unknown. Inspect the native run store before another preview attempt.";
                NotificationService.Warning("Workflow preview needs attention", errorMessage);
            }
        } catch (Exception error) {
            if (Owns(occurrence)) {
                previewUnknown = true;
                errorMessage = "The preview admission outcome is unknown. Inspect the native run store before another preview attempt.";
                Logger.LogWarning("Workflow preview acknowledgement unavailable: {FailureType}", error.GetType().Name);
            }
        } finally {
            if (Owns(occurrence) && ReferenceEquals(activePreviewAttempt, attempt)) {
                activePreviewAttempt = null;
                isTesting = false;
                isBusy = false;
            }
        }
    }

    private object? activePreviewAttempt;

    private async Task SelectPreviewNodeAsync(WorkflowNodeId nodeId)
    {
        if (!document.Nodes.Any(node => node.Id == nodeId))
        {
            return;
        }

        await SelectNodeAsync(nodeId.Value);
        StateHasChanged();
    }

    private async Task<WorkflowDefinition> ValidateCurrentDefinitionAsync() {
        var occurrence = document;
        var submittedRevision = revision;
        var definition = WorkflowCanvasDefinitionMapper.ToDefinition(document);
        WorkflowValidationResult validation;
        try {
            validation = await DocumentOperations.Validate(definition, OwnerToken);
        } catch (Exception error) {
            if (Owns(occurrence) && revision == submittedRevision) {
                validationRevision = null;
                errorMessage = "Validation is unavailable. The draft is retained and has not been certified.";
                Logger.LogWarning("Workflow validation failed for {WorkflowId}: {FailureType}", definition.Id, error.GetType().Name);
            }
            return definition;
        }
        if (!Owns(occurrence) || revision != submittedRevision) {
            return definition;
        }
        validationIssues = validation.Issues;
        validationRevision = submittedRevision;
        if (validation.Succeeded) {
            NotificationService.Success("Workflow canvas valid", "The submitted workflow canvas has no validation issues.");
        } else {
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
        await NotifySelectedNodeChangedAsync();
    }

    private async Task HandleCanvasStateChangedAsync(string stateJson)
    {
        canvasUiState = CanvasWorkbenchUiState.Parse(stateJson);
        selectedNodeId = canvasUiState.SelectedNodeIds.FirstOrDefault();
        canvasUiState.ActiveInspectorTab = "workflow";
        await NotifySelectedNodeChangedAsync();
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
                if (descriptor.SettingsPresentationMode == WorkflowExecutorSettingsPresentationMode.CustomRenderer)
                {
                    workflowInspectorTabIndex = 1;
                }
            }
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or JsonException)
        {
            errorMessage = "The node could not be created. Review its configuration and retry. Your draft is preserved.";
            NotificationService.Error("Workflow node create failed", errorMessage);
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

        await SelectNodeAsync(node.Id.Value);
        switch (request.ActionId)
        {
            case WorkflowCanvasDefinitionMapper.EditNodeActionId:
                ResetDecisionRouteEditor();
                detailsNode = node;
                isNodeDetailsDialogOpen = true;
                break;
            case WorkflowCanvasDefinitionMapper.AddDecisionRouteActionId when IsDecisionNode(node):
                detailsNode = node;
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

    private async Task CreateEdgeFromCanvasConnectionAsync(CanvasWorkbenchContextActionRequest request)
    {
        errorMessage = string.Empty;
        var hasSource = TryResolveCanvasConnectionEndpoint(request.LinkSourceId, isSource: true, out var sourceNode, out var sourceError);
        var hasTarget = TryResolveCanvasConnectionEndpoint(request.LinkTargetId, isSource: false, out var targetNode, out var targetError);
        if (!hasSource || !hasTarget)
        {
            errorMessage = hasSource ? targetError : sourceError;
            NotificationService.Warning("Workflow connection failed", errorMessage);
            return;
        }

        if (sourceNode.Id == targetNode.Id)
        {
            errorMessage = "Choose different source and target nodes for the workflow edge.";
            NotificationService.Warning("Workflow connection failed", errorMessage);
            return;
        }

        if (document.Edges.Any(edge => edge.SourceNodeId == sourceNode.Id && edge.TargetNodeId == targetNode.Id))
        {
            errorMessage = "That workflow edge already exists.";
            NotificationService.Warning("Workflow connection failed", errorMessage);
            return;
        }

        if (!WorkflowCanvasDefinitionMapper.TryResolveConnectionPort(sourceNode, request.LinkSourcePortId,
                WorkflowPortDirection.Output, out var sourcePort) ||
            !WorkflowCanvasDefinitionMapper.TryResolveConnectionPort(targetNode, request.LinkTargetPortId,
                WorkflowPortDirection.Input, out var targetPort)) {
            errorMessage = "The selected port is unavailable. Select a current connection handle.";
            return;
        }
        document.Edges.Add(new WorkflowCanvasEdgeDraft(CreateEdgeId(sourceNode.Id, targetNode.Id), sourceNode.Id, targetNode.Id) {
            SourcePortId = sourcePort,
            TargetPortId = targetPort
        });
        await SelectNodeAsync(targetNode.Id.Value);
        ResetEdgeEditor();
        NotificationService.Success("Workflow edge connected", $"{sourceNode.Name} -> {targetNode.Name}");
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

        var backendDescriptor = Backends.Single(item => item.Kind == backend);
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
        newComponentModel = model ?? string.Empty;
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

    private static bool ShouldRenderNodeInstructions(WorkflowCanvasNodeDraft node)
    {
        return node.Kind != WorkflowNodeKind.Executor;
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

    private void HandleSelectedProviderChanged(WorkflowCanvasNodeDraft node, ChangeEventArgs args)
    {
        if (!Guid.TryParse(args.Value?.ToString(), out var providerProfileId))
        {
            node.ProviderProfileId = null;
            node.Model = string.Empty;
            return;
        }

        node.ProviderProfileId = providerProfileId;
        node.Model = ResolveDefaultModel(ResolveSelectedNodeProvider(node));
    }

    private Task HandleSelectedModelChangedAsync(WorkflowCanvasNodeDraft node, string? model)
    {
        node.Model = model ?? string.Empty;
        return Task.CompletedTask;
    }

    private void HandleSelectedRequestKindChanged(WorkflowCanvasNodeDraft node, ChangeEventArgs args)
    {
        if (Enum.TryParse<WorkflowExternalRequestKind>(args.Value?.ToString(), out var requestKind))
        {
            node.ExternalRequestKind = requestKind;
        }
    }

    private void HandleSelectedAgentIdChanged(WorkflowCanvasNodeDraft node, ChangeEventArgs args)
        => ApplyIdentityInput(node, WorkflowNodeInputField.AgentId, ReadString(args));

    private void HandleSelectedSubworkflowIdChanged(WorkflowCanvasNodeDraft node, ChangeEventArgs args)
        => ApplyIdentityInput(node, WorkflowNodeInputField.SubworkflowId, ReadString(args));

    private void ApplyIdentityInput(WorkflowCanvasNodeDraft node, WorkflowNodeInputField field, string raw) {
        if (!IsCurrent || !document.Nodes.Contains(node)) {
            return;
        }
        node.RawInputs[field] = raw;
        if (raw.Length > 0 && !Guid.TryParse(raw, out _)) {
            node.InvalidInputs.Add(field);
            return;
        }
        node.InvalidInputs.Remove(field);
        var value = raw.Length == 0 ? (Guid?)null : Guid.Parse(raw);
        if (field == WorkflowNodeInputField.AgentId) {
            node.AgentId = value;
        } else {
            node.SubworkflowId = value is { } id ? new(id) : null;
        }
    }

    private void HandlePolicyInput(WorkflowCanvasNodeDraft node, WorkflowPolicyInput input) {
        if (!IsCurrent || !document.Nodes.Contains(node)) {
            return;
        }
        node.RawInputs[input.Field] = input.Value;
        if (!int.TryParse(input.Value, out var value)) {
            node.InvalidInputs.Add(input.Field);
            return;
        }
        var policy = ResolveSelectedExecutionPolicy(node);
        var updated = input.Field switch {
            WorkflowNodeInputField.TimeoutSeconds => policy with { TimeoutSeconds = value },
            WorkflowNodeInputField.RetryAttempts => policy with { MaxRetryAttempts = value },
            WorkflowNodeInputField.RetryDelay => policy with { RetryDelayMilliseconds = value },
            _ => throw new ArgumentOutOfRangeException(nameof(input))
        };
        if (!WorkflowExecutionPolicyRules.IsValid(updated)) {
            node.InvalidInputs.Add(input.Field);
            return;
        }
        node.InvalidInputs.Remove(input.Field);
        node.ExecutionPolicy = updated;
    }

    private void HandleSelectedInputShapeChanged(WorkflowCanvasNodeDraft node, ChangeEventArgs args)
    {
        if (string.IsNullOrEmpty(args.Value?.ToString())) {
            node.InputShape = null;
            return;
        }
        if (Enum.TryParse<WorkflowValueShapeKind>(args.Value?.ToString(), out var shape))
        {
            node.InputShapeKind = shape;
        }
    }

    private void HandleSelectedResultShapeChanged(WorkflowCanvasNodeDraft node, ChangeEventArgs args)
    {
        if (string.IsNullOrEmpty(args.Value?.ToString())) {
            node.ResultShape = null;
            return;
        }
        if (Enum.TryParse<WorkflowValueShapeKind>(args.Value?.ToString(), out var shape))
        {
            node.ResultShapeKind = shape;
        }
    }

    private async Task SelectNodeAsync(string nodeId)
    {
        selectedNodeId = nodeId;
        canvasUiState.SelectedNodeIds = [nodeId];
        await NotifySelectedNodeChangedAsync();
    }

    private async Task HandleCanvasNodeOpenedAsync(string nodeId)
    {
        await SelectNodeAsync(nodeId);
        detailsNode = SelectedNode;
        isNodeDetailsDialogOpen = detailsNode is not null;
    }

    private Task NotifySelectedNodeChangedAsync()
        => !IsCurrent ? Task.CompletedTask : SelectedNodeChanged.InvokeAsync(
            SelectedNode is { } node && document.DefinitionId is { } definitionId
                ? new WorkflowCanvasNodeSelection(
                    definitionId,
                    node.Id,
                    node.Name,
                    node.Kind)
                : null);

    private Task OpenSelectedNodeDetailsAsync()
    {
        detailsNode = SelectedNode;
        isNodeDetailsDialogOpen = detailsNode is not null;
        return Task.CompletedTask;
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
        if (WorkflowExecutorCanvasCatalog.TryParseCreateActionId(actionId, out var executorId) &&
            TryResolveExecutorDescriptor(executorId, out var descriptor) &&
            descriptor.SettingsPresentationMode == WorkflowExecutorSettingsPresentationMode.CustomRenderer)
        {
            await HandleCanvasCreateActionAsync(request);
            return;
        }

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

    private static bool ShouldRenderSettingsRenderer(WorkflowExecutorDescriptor descriptor)
        => descriptor.ConfigurationSchema.Fields.Count > 0 ||
           descriptor.SettingsPresentationMode == WorkflowExecutorSettingsPresentationMode.CustomRenderer;

    private static bool HasSecretReferenceSettings(WorkflowExecutorDescriptor descriptor)
        => descriptor.ConfigurationSchema.Fields.Any(field =>
            field.FieldType == ConfigurationFieldType.SecretReference);

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

    private async Task AddDecisionNodeAsync(
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
        await SelectNodeAsync(node.Id.Value);
        SyncEdgeDefaults();
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

    private static bool TryCreateExecutorConfigurationState(WorkflowCanvasNodeDraft node,
        WorkflowExecutorDescriptor descriptor, out ConfigurationState state) {
        if (node.PendingConfiguration is { } pending) {
            state = pending;
            return true;
        }
        try {
            state = WorkflowExecutorConfigurationMapper.ReadState(node.ExecutorSettingsJson, descriptor.ConfigurationSchema);
            return true;
        } catch (InvalidOperationException) {
            state = new();
            return false;
        }
    }

    private void HandleSelectedExecutorConfigurationStateChanged(WorkflowCanvasNodeDraft node,
        ConfigurationSchema schema, ConfigurationState state) {
        if (!IsCurrent || !document.Nodes.Contains(node)) {
            return;
        }
        node.PendingConfiguration = state;
        try {
            node.ExecutorSettingsJson = WorkflowExecutorConfigurationMapper.MergeState(node.ExecutorSettingsJson, schema, state);
            node.SettingsError = string.Empty;
            errorMessage = string.Empty;
        } catch (InvalidOperationException) {
            node.SettingsError = "The settings contain invalid fields. Correct them before saving or running this draft.";
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
        WorkflowExecutorDescriptor? descriptor,
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
                    WorkflowExecutionPolicyRules.MinTimeoutSeconds,
                    WorkflowExecutionPolicyRules.MaxTimeoutSeconds)
            };
        }

        if (int.TryParse(GetInputValue(request, "retryAttempts", string.Empty), out var retryAttempts))
        {
            policy = policy with
            {
                MaxRetryAttempts = Math.Clamp(
                    retryAttempts,
                    WorkflowExecutionPolicyRules.MinRetryAttempts,
                    WorkflowExecutionPolicyRules.MaxRetryAttempts)
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
        if (descriptor?.ConfigurationSchema.Fields.Count > 0)
        {
            var state = WorkflowExecutorConfigurationMapper.ReadState(
                node.ExecutorSettingsJson,
                descriptor.ConfigurationSchema);
            foreach (var field in descriptor.ConfigurationSchema.Fields)
            {
                var inputKey = WorkflowExecutorConfigurationMapper.BuildInputKey(field.Key);
                var input = request.InputValues?.FirstOrDefault(candidate =>
                    string.Equals(candidate.Key, inputKey, StringComparison.OrdinalIgnoreCase));
                if (input is not null)
                {
                    state.SetText(field.Key, input.Value);
                }
            }

            _ = WorkflowExecutorConfigurationMapper.SerializeCompleteState(descriptor.ConfigurationSchema, state);
            node.ExecutorSettingsJson = WorkflowExecutorConfigurationMapper.MergeState(node.ExecutorSettingsJson, descriptor.ConfigurationSchema, state);
            return;
        }

    }

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

    private static string FormatExecutorSettingsJson(WorkflowCanvasNodeDraft node) => node.ExecutorSettingsJson;

    private void HandleSelectedExecutorSettingsJsonChanged(WorkflowCanvasNodeDraft node, ChangeEventArgs args) {
        if (!IsCurrent || !document.Nodes.Contains(node)) {
            return;
        }
        node.ExecutorSettingsJson = ReadString(args);
        node.PendingConfiguration = null;
        node.SettingsError = string.Empty;
        try {
            using var parsed = JsonDocument.Parse(string.IsNullOrWhiteSpace(node.ExecutorSettingsJson) ? "{}" : node.ExecutorSettingsJson);
            if (parsed.RootElement.ValueKind != JsonValueKind.Object) {
                node.SettingsError = "Executor settings must be a JSON object.";
            }
        } catch (JsonException) {
            node.SettingsError = "Executor settings contain invalid JSON. The original input is retained.";
        }
    }

    private bool HasInvalidDraftFields() {
        if (!document.Nodes.Any(node => node.SettingsError.Length > 0 || node.InvalidInputs.Count > 0)) {
            return false;
        }
        errorMessage = "Correct invalid node fields before saving or running this draft.";
        NotificationService.Warning("Draft fields need attention", errorMessage);
        return true;
    }

    private static string ReadString(ChangeEventArgs args)
        => args.Value?.ToString() ?? string.Empty;

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
            : newComponentModel;
    }

    private static string ResolveDefaultModel(WorkflowProviderOption? providerOption)
    {
        if (providerOption is null)
        {
            return string.Empty;
        }

        if (!string.IsNullOrWhiteSpace(providerOption.DefaultModel))
        {
            return providerOption.DefaultModel;
        }

        return providerOption.ModelOptions.FirstOrDefault(model => !string.IsNullOrWhiteSpace(model)) ??
               string.Empty;
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

    private WorkflowProviderOption? ResolveSelectedNodeProvider(WorkflowCanvasNodeDraft node)
    {
        return node.ProviderProfileId is { } providerProfileId
            ? ProviderOptions.FirstOrDefault(option => option.ProviderProfileId == providerProfileId)
            : null;
    }

    private WorkflowExecutorDisplayBadge BuildLlmProviderUsageBadge(WorkflowCanvasNodeDraft node)
    {
        if (!node.ProviderProfileId.HasValue)
        {
            return new WorkflowExecutorDisplayBadge("Provider unbound", "warning");
        }

        var provider = ResolveSelectedNodeProvider(node);
        if (provider is null)
        {
            return new WorkflowExecutorDisplayBadge("Provider missing", "danger");
        }

        return provider.IsEnabled
            ? new WorkflowExecutorDisplayBadge("Usage unknown until run", "warning")
            : new WorkflowExecutorDisplayBadge("Provider disabled", "warning");
    }

    private string BuildLlmProviderUsageDescription(
        WorkflowCanvasNodeDraft node,
        LlmCallComponent? component)
    {
        if (component is null)
        {
            return "Select a Gallery prompt before runtime provider usage can be attributed.";
        }

        var provider = ResolveSelectedNodeProvider(node);
        if (!node.ProviderProfileId.HasValue)
        {
            return $"Prompt {component.Name} has no node provider binding; execution cannot produce provider usage evidence.";
        }

        if (provider is null)
        {
            return $"This node references provider {node.ProviderProfileId.Value:D}, but that provider is missing from the registry.";
        }

        if (!provider.IsEnabled)
        {
            return $"Provider {provider.Name} is disabled; runtime usage and cost remain unavailable until the provider is enabled or replaced.";
        }

        return $"Provider {provider.Name} / {node.Model}; actual usage and cost appear only after execution records provider usage observations.";
    }

    private static string BuildPromptIdentity(LlmCallComponent? component)
    {
        if (component is null)
        {
            return "No Gallery prompt selected.";
        }

        var artifact = component.PromptArtifactId?.ToString("D") ?? "missing artifact";
        var revision = component.PromptVersionId?.ToString("D") ?? "missing revision";
        return $"{component.Name} · Gallery item {artifact} · immutable revision {revision}";
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

    private void SyncNewComponentDefaults() {
        if (newComponentProviderProfileId.Length > 0) {
            return;
        }
        var provider = ResolveDefaultProviderOption();
        newComponentProviderProfileId = provider?.ProviderProfileId.ToString("D") ?? string.Empty;
        newComponentModel = ResolveDefaultModel(provider);
    }

    private sealed record PromptBindingExecutionPair(
        WorkflowProviderOption Provider,
        string Model);

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
            SourcePortId = incomingToEnd.SourcePortId,
            ConditionExpression = incomingToEnd.ConditionExpression,
            Routing = incomingToEnd.Routing
        });
        document.Edges.Add(new WorkflowCanvasEdgeDraft(
            CreateEdgeId(node.Id, end.Id),
            node.Id,
            end.Id) {
            TargetPortId = incomingToEnd.TargetPortId
        });
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
        edgeRoute.Kind = WorkflowRouteKind.Always;
        edgeRoute.Label = string.Empty;
        edgeRoute.JsonPath = "$.status";
        edgeRoute.Operator = WorkflowRouteOperator.Equals;
        edgeRoute.ValueKind = WorkflowRouteValueKind.String;
        edgeRoute.ExpectedValue = "approved";
        edgeRoute.CaseSensitive = false;
        edgeRoute.FanOutTargetIndex = null;
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
        edgeRoute.Kind = routing.Kind;
        edgeRoute.Label = routing.Label;
        edgeRoute.JsonPath = string.IsNullOrWhiteSpace(routing.JsonPath) ? "$.status" : routing.JsonPath;
        edgeRoute.Operator = routing.Operator;
        edgeRoute.ValueKind = routing.ExpectedValueKind;
        edgeRoute.ExpectedValue = FormatExpectedValueForEditor(routing);
        edgeRoute.CaseSensitive = routing.CaseSensitive;
        edgeRoute.FanOutTargetIndex = routing.FanOutTargetIndex;
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

        if (decisionRoute.HasInvalidFanOutIndex) {
            decisionRouteError = WorkflowRouteDraft.InvalidIndexMessage;
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
        decisionRoute.Kind = routeKind;
        decisionRoute.Label = ResolveDefaultDecisionRouteLabel(routeKind, routeNumber);
        decisionRoute.JsonPath = ResolveDefaultDecisionRouteJsonPath(routeKind, outgoingRoutes);
        decisionRoute.Operator = routeKind == WorkflowRouteKind.FanOutSelector
            ? WorkflowRouteOperator.Contains
            : WorkflowRouteOperator.Equals;
        decisionRoute.ValueKind = WorkflowRouteValueKind.String;
        decisionRoute.ExpectedValue = routeKind == WorkflowRouteKind.FanOutSelector
            ? $"target-{routeNumber}"
            : $"case-{routeNumber}";
        decisionRoute.CaseSensitive = false;
        decisionRoute.FanOutTargetIndex = routeKind == WorkflowRouteKind.FanOutSelector
            ? outgoingRoutes.Count
            : null;
    }

    private void ResetDecisionRouteEditor()
    {
        decisionRouteEditorNodeId = null;
        decisionRouteEditingEdgeId = null;
        decisionRouteTargetNodeId = string.Empty;
        decisionRoute.Kind = WorkflowRouteKind.SwitchCase;
        decisionRoute.Label = string.Empty;
        decisionRoute.JsonPath = "$.route";
        decisionRoute.Operator = WorkflowRouteOperator.Equals;
        decisionRoute.ValueKind = WorkflowRouteValueKind.String;
        decisionRoute.ExpectedValue = "case";
        decisionRoute.CaseSensitive = false;
        decisionRoute.FanOutTargetIndex = null;
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
        decisionRoute.Kind = routing.Kind == WorkflowRouteKind.Always
            ? WorkflowRouteKind.Predicate
            : routing.Kind;
        decisionRoute.Label = routing.Label;
        decisionRoute.JsonPath = string.IsNullOrWhiteSpace(routing.JsonPath)
            ? ResolveDefaultDecisionRouteJsonPath(decisionRoute.Kind, [])
            : routing.JsonPath;
        decisionRoute.Operator = routing.Operator;
        decisionRoute.ValueKind = routing.ExpectedValueKind;
        decisionRoute.ExpectedValue = FormatExpectedValueForEditor(routing);
        decisionRoute.CaseSensitive = routing.CaseSensitive;
        decisionRoute.FanOutTargetIndex = routing.FanOutTargetIndex;
    }

    private WorkflowEdgeRouting BuildEdgeRoutingFromEditor()
        => BuildRouteFromFields(
            edgeRoute.Kind,
            edgeRoute.Label,
            edgeRoute.JsonPath,
            edgeRoute.Operator,
            edgeRoute.ValueKind,
            edgeRoute.ExpectedValue,
            edgeRoute.FanOutTargetIndex,
            edgeRoute.CaseSensitive);

    private WorkflowEdgeRouting BuildDecisionRouteFromEditor()
        => BuildRouteFromFields(
            decisionRoute.Kind,
            decisionRoute.Label,
            decisionRoute.JsonPath,
            decisionRoute.Operator,
            decisionRoute.ValueKind,
            decisionRoute.ExpectedValue,
            decisionRoute.FanOutTargetIndex,
            decisionRoute.CaseSensitive);

    private void HandleEdgeRouteKindChanged(ChangeEventArgs args)
    {
        if (!Enum.TryParse<WorkflowRouteKind>(args.Value?.ToString(), out var routeKind))
        {
            return;
        }

        edgeRoute.Kind = routeKind;
        edgeKind = ResolveEdgeKindForRoute(routeKind);
        if (routeKind == WorkflowRouteKind.SwitchDefault)
        {
            edgeRoute.JsonPath = string.Empty;
            edgeRoute.ExpectedValue = string.Empty;
            edgeRoute.FanOutTargetIndex = null;
        }
        else if (routeKind == WorkflowRouteKind.SwitchCase ||
                 !WorkflowRoutingValidation.RequiresExpectedValue(edgeRoute.Operator))
        {
            edgeRoute.Operator = WorkflowRouteOperator.Equals;
        }

        if (routeKind != WorkflowRouteKind.SwitchDefault &&
            string.IsNullOrWhiteSpace(edgeRoute.JsonPath))
        {
            edgeRoute.JsonPath = "$.status";
        }
    }


    private void HandleDecisionRouteKindChanged(ChangeEventArgs args)
    {
        if (!Enum.TryParse<WorkflowRouteKind>(args.Value?.ToString(), out var routeKind) ||
            routeKind == WorkflowRouteKind.Always)
        {
            return;
        }

        decisionRoute.Kind = routeKind;
        if (routeKind == WorkflowRouteKind.SwitchDefault)
        {
            decisionRoute.JsonPath = string.Empty;
            decisionRoute.ExpectedValue = string.Empty;
            decisionRoute.FanOutTargetIndex = null;
            decisionRoute.Operator = WorkflowRouteOperator.Exists;
            decisionRoute.ValueKind = WorkflowRouteValueKind.Json;
            decisionRoute.Label = string.IsNullOrWhiteSpace(decisionRoute.Label)
                ? "DEFAULT"
                : decisionRoute.Label;
            return;
        }

        if (string.IsNullOrWhiteSpace(decisionRoute.JsonPath))
        {
            decisionRoute.JsonPath = routeKind == WorkflowRouteKind.FanOutSelector
                ? "$.targets"
                : "$.route";
        }

        if (routeKind == WorkflowRouteKind.FanOutSelector)
        {
            decisionRoute.Operator = WorkflowRouteOperator.Contains;
            decisionRoute.FanOutTargetIndex ??= CountCurrentDecisionRoutes();
            if (string.IsNullOrWhiteSpace(decisionRoute.ExpectedValue))
            {
                decisionRoute.ValueKind = WorkflowRouteValueKind.String;
                decisionRoute.ExpectedValue = $"target-{decisionRoute.FanOutTargetIndex.Value + 1}";
            }
        }
        else
        {
            decisionRoute.FanOutTargetIndex = null;
            if (!WorkflowRoutingValidation.RequiresExpectedValue(decisionRoute.Operator))
            {
                decisionRoute.Operator = WorkflowRouteOperator.Equals;
            }

            if (string.IsNullOrWhiteSpace(decisionRoute.ExpectedValue))
            {
                decisionRoute.ValueKind = WorkflowRouteValueKind.String;
                decisionRoute.ExpectedValue = $"case-{CountCurrentDecisionRoutes() + 1}";
            }
        }

        if (string.IsNullOrWhiteSpace(decisionRoute.Label))
        {
            decisionRoute.Label = ResolveDefaultDecisionRouteLabel(routeKind, CountCurrentDecisionRoutes() + 1);
        }
    }


    private int CountCurrentDecisionRoutes()
        => string.IsNullOrWhiteSpace(decisionRouteEditorNodeId)
            ? 0
            : document.Edges.Count(edge => edge.SourceNodeId.Value == decisionRouteEditorNodeId);

    async Task IHandleEvent.HandleEventAsync(EventCallbackWorkItem callback, object? argument) {
        if (!IsCurrent) {
            return;
        }
        var pending = callback.InvokeAsync(argument);
        TrackDraftChange();
        StateHasChanged();
        await pending;
        if (IsCurrent) {
            TrackDraftChange();
            StateHasChanged();
        }
    }

    private string DraftSignature() {
        var snapshot = WorkflowCanvasDefinitionMapper.ToDefinition(document);
        return JsonSerializer.Serialize(new { snapshot.Name, snapshot.Description, snapshot.Status,
            snapshot.Graph, snapshot.RuntimePolicy, snapshot.InputParameters,
            RawSettings = document.Nodes.Select(node => new { node.Id, node.SettingsError, node.PendingConfiguration?.Values, node.RawInputs }) });
    }

    private void TrackDraftChange() {
        var current = DraftSignature();
        if (!string.Equals(current, observedDraft, StringComparison.Ordinal)) {
            observedDraft = current;
            revision++;
        }
    }

    public void Dispose() {
        disposed = true;
        activePreviewAttempt = null;
    }
}
