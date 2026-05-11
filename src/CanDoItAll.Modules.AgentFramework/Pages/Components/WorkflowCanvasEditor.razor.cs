using System.Text.Json;
using System.Text.Json.Serialization;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Components.CanvasLib;
using CanDoItAll.Components.OverlayLib;
using Microsoft.AspNetCore.Components;

namespace CanDoItAll.Modules.AgentFramework.Pages.Components;

public partial class WorkflowCanvasEditor
{
    private const string ToolboxWindowId = "workflow-canvas-toolbox";
    private const string SelectionWindowId = "workflow-canvas-selection";
    private const string ComponentsWindowId = "workflow-canvas-components";

    private static readonly JsonSerializerOptions ExecutorJsonOptions = CreateExecutorJsonOptions(writeIndented: false);
    private static readonly JsonSerializerOptions IndentedExecutorJsonOptions = CreateExecutorJsonOptions(writeIndented: true);

    [Inject]
    public IWorkflowCatalogService CatalogService { get; set; } = default!;

    [Inject]
    public IWorkflowExecutorCatalog ExecutorCatalog { get; set; } = default!;

    [Inject]
    public IWorkflowComponentLibraryService ComponentLibrary { get; set; } = default!;

    [Inject]
    public IWorkflowTestRunner TestRunner { get; set; } = default!;

    [Inject]
    public NotificationService NotificationService { get; set; } = default!;

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
    private string newComponentProviderProfileId = string.Empty;
    private string newComponentModel = string.Empty;
    private string workflowToolboxSearchText = string.Empty;
    private string? expandedWorkflowToolboxGroupKey = "workflow-nodes";
    private string testInputJson = "{\"prompt\":\"Summarize this workflow input.\"}";
    private string errorMessage = string.Empty;
    private CanvasWorkbench? workbenchRef;
    private CanvasWorkbenchWindowState toolboxWindowState = CreateWindowState(width: 300, height: 380);
    private CanvasWorkbenchWindowState selectionWindowState = CreateWindowState(width: 260, height: 320);
    private CanvasWorkbenchWindowState componentsWindowState = CreateWindowState(width: 320, height: 380, isVisible: false);
    private bool isNodeDetailsDialogOpen;
    private bool isBusy;
    private bool isTesting;

    private WorkflowCanvasNodeDraft? SelectedNode
        => string.IsNullOrWhiteSpace(selectedNodeId)
            ? null
            : document.Nodes.FirstOrDefault(node => node.Id.Value == selectedNodeId);

    private string SelectionWindowSummary
        => SelectedNode is null
            ? $"{document.Nodes.Count} nodes"
            : $"{SelectedNode.Kind} · {SelectedNode.Id.Value}";

    private CanvasWorkbenchSurface CanvasSurface
        => WorkflowCanvasDefinitionMapper.BuildSurface(
            document,
            componentOptions,
            executorDescriptors,
            validationIssues,
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
            Value = componentOptions.Count.ToString(),
            Tone = "success"
        },
        new()
        {
            Label = "Executors",
            Value = executorDescriptors.Count(executor => executor.IsImplemented).ToString(),
            Tone = "info"
        },
        new()
        {
            Label = "Validation",
            Value = validationIssues.Count == 0 ? "Valid" : validationIssues.Count.ToString(),
            Tone = validationIssues.Count == 0 ? "success" : "warning"
        }
    ];

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
        validationIssues = [];
        testResult = null;
        SyncEdgeDefaults();
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

    private Task ResetDraftAsync()
    {
        document = WorkflowCanvasDefinitionMapper.CreateDraft(componentOptions);
        loadedDefinitionKey = "draft";
        selectedNodeId = document.StartNodeId.Value;
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
        selectedNodeId = node.Id.Value;
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
        selectedNodeId = node.Id.Value;
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
        document.Nodes.Add(node);
        InsertNodeBeforeEnd(node);
        selectedNodeId = node.Id.Value;
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
        if (document.Edges.Any(edge => edge.SourceNodeId == source && edge.TargetNodeId == target))
        {
            errorMessage = "That workflow edge already exists.";
            return Task.CompletedTask;
        }

        document.Edges.Add(new WorkflowCanvasEdgeDraft(
            CreateEdgeId(source, target),
            source,
            target)
        {
            Kind = edgeKind,
            ConditionExpression = edgeCondition
        });
        edgeCondition = string.Empty;
        return Task.CompletedTask;
    }

    private Task RemoveEdgeAsync(WorkflowCanvasEdgeDraft edge)
    {
        document.Edges.Remove(edge);
        return Task.CompletedTask;
    }

    private Task RemoveSelectedNodeAsync()
    {
        var selected = SelectedNode;
        if (selected is null || selected.Kind is WorkflowNodeKind.Start or WorkflowNodeKind.End)
        {
            return Task.CompletedTask;
        }

        document.Nodes.Remove(selected);
        document.Edges.RemoveAll(edge => edge.SourceNodeId == selected.Id || edge.TargetNodeId == selected.Id);
        selectedNodeId = document.StartNodeId.Value;
        SyncEdgeDefaults();
        return Task.CompletedTask;
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
            selectedNodeId = document.StartNodeId.Value;
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

        isBusy = true;
        isTesting = true;
        errorMessage = string.Empty;
        try
        {
            var definition = WorkflowCanvasDefinitionMapper.ToDefinition(document);
            testResult = await TestRunner.RunAsync(new WorkflowTestRunRequest(
                WorkflowId: null,
                VersionId: null,
                DraftDefinition: definition,
                InputJson: testInputJson,
                RequestedBackend: WorkflowRuntimeBackendKind.InProcess,
                ValidateOnly: false));
            validationIssues = testResult.Validation.Issues;
            if (!testResult.Succeeded)
            {
                errorMessage = testResult.ErrorMessage;
                NotificationService.Error("Workflow preview failed", testResult.ErrorMessage);
                return;
            }

            NotificationService.Success("Workflow preview completed", testResult.Run?.Summary ?? "Workflow preview completed.");
            if (testResult.Run is not null)
            {
                await PreviewRunCompleted.InvokeAsync(testResult.Run);
            }
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

    private Task HandleCanvasSelectionChangedAsync(CanvasWorkbenchSelectionChangedEventArgs args)
    {
        selectedNodeId = args.PrimaryNodeId ?? args.SelectedNodeIds.FirstOrDefault();
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

    private void HandleNewComponentModelChanged(ChangeEventArgs args)
    {
        newComponentModel = args.Value?.ToString()?.Trim() ?? string.Empty;
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
    }

    private Task HandleCanvasNodeOpenedAsync(string nodeId)
    {
        SelectNode(nodeId);
        isNodeDetailsDialogOpen = SelectedNode is not null;
        return Task.CompletedTask;
    }

    private Task OpenSelectedNodeDetailsAsync()
    {
        isNodeDetailsDialogOpen = SelectedNode is not null;
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
            new($"{executorDescriptors.Count(executor => executor.IsImplemented)} executors", "label")
        ];

    private IReadOnlyList<OverlayToolboxSection> WorkflowToolboxSections
        => BuildWorkflowToolboxSections();

    private IReadOnlyList<OverlayToolboxSection> BuildWorkflowToolboxSections()
    {
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

        var executorGroups = executorDescriptors
            .GroupBy(executor => executor.Category)
            .OrderBy(group => group.Key)
            .Select(group => BuildExecutorToolboxGroup(group.Key, group))
            .Where(group => group.Items.Count > 0)
            .ToList();

        var sections = new List<OverlayToolboxSection>();
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
            .OrderBy(executor => executor.IsImplemented ? 0 : 1)
            .ThenBy(executor => executor.Name, StringComparer.OrdinalIgnoreCase)
            .Select(executor => new OverlayToolboxItem(
                WorkflowExecutorCanvasCatalog.BuildCreateActionId(executor.Id),
                executor.Name,
                executor.Description,
                Icon: executor.IconName,
                Tone: WorkflowExecutorCanvasCatalog.ResolveTone(executor.Category),
                IsDisabled: !executor.IsImplemented,
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
        => executorDescriptors.FirstOrDefault(executor => executor.IsImplemented)
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

    private IReadOnlyList<string> ResolveNewComponentModelOptions()
    {
        return ResolveSelectedNewComponentProvider()?.ModelOptions ?? [];
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
            return "gpt-5.4";
        }

        if (!string.IsNullOrWhiteSpace(providerOption.DefaultModel))
        {
            return providerOption.DefaultModel;
        }

        return providerOption.ModelOptions.FirstOrDefault(model => !string.IsNullOrWhiteSpace(model)) ?? "gpt-5.4";
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

    private void SyncNewComponentDefaults()
    {
        if (ProviderOptions.Count == 0)
        {
            newComponentProviderProfileId = string.Empty;
            if (string.IsNullOrWhiteSpace(newComponentModel))
            {
                newComponentModel = "gpt-5.4";
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
            ConditionExpression = incomingToEnd.ConditionExpression
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
    }
}
