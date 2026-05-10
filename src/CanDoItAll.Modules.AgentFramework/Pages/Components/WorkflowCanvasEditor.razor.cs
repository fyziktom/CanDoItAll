using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Components.CanvasLib;
using Microsoft.AspNetCore.Components;

namespace CanDoItAll.Modules.AgentFramework.Pages.Components;

public partial class WorkflowCanvasEditor
{
    [Inject]
    public IWorkflowCatalogService CatalogService { get; set; } = default!;

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
    public EventCallback<WorkflowDefinition> DefinitionSaved { get; set; }

    [Parameter]
    public EventCallback<WorkflowRunSnapshot> PreviewRunCompleted { get; set; }

    [Parameter]
    public EventCallback ComponentLibraryChanged { get; set; }

    private WorkflowCanvasDocument document = WorkflowCanvasDefinitionMapper.CreateDraft([]);
    private IReadOnlyList<LlmCallComponent> componentOptions = [];
    private IReadOnlyList<WorkflowValidationIssue> validationIssues = [];
    private WorkflowTestRunResult? testResult;
    private string loadedDefinitionKey = string.Empty;
    private string? selectedNodeId = "start";
    private string edgeSourceNodeId = "start";
    private string edgeTargetNodeId = "end";
    private WorkflowEdgeKind edgeKind = WorkflowEdgeKind.Direct;
    private string edgeCondition = string.Empty;
    private string testInputJson = "{\"prompt\":\"Summarize this workflow input.\"}";
    private string errorMessage = string.Empty;
    private bool isBusy;
    private bool isTesting;

    private WorkflowCanvasNodeDraft? SelectedNode
        => string.IsNullOrWhiteSpace(selectedNodeId)
            ? null
            : document.Nodes.FirstOrDefault(node => node.Id.Value == selectedNodeId);

    private CanvasWorkbenchSurface CanvasSurface
        => WorkflowCanvasDefinitionMapper.BuildSurface(
            document,
            componentOptions,
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
            Label = "Validation",
            Value = validationIssues.Count == 0 ? "Valid" : validationIssues.Count.ToString(),
            Tone = validationIssues.Count == 0 ? "success" : "warning"
        }
    ];

    protected override void OnParametersSet()
    {
        componentOptions = Components;
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

    private async Task AddNodeAsync(WorkflowNodeKind kind)
    {
        LlmCallComponent? component = null;
        if (kind == WorkflowNodeKind.LlmCall)
        {
            component = componentOptions.FirstOrDefault() ?? await CreateDefaultComponentCoreAsync();
        }

        var node = WorkflowCanvasDefinitionMapper.CreateNode(
            kind,
            document.Nodes,
            componentOptions,
            320 + (document.Nodes.Count * 120),
            220 + ((document.Nodes.Count % 3) * 120));
        if (component is not null)
        {
            WorkflowCanvasDefinitionMapper.ApplyComponent(node, component);
        }

        document.Nodes.Add(node);
        InsertNodeBeforeEnd(node);
        selectedNodeId = node.Id.Value;
        SyncEdgeDefaults();
    }

    private Task AddLlmComponentNodeAsync(LlmCallComponent component)
    {
        var node = WorkflowCanvasDefinitionMapper.CreateNode(
            WorkflowNodeKind.LlmCall,
            document.Nodes,
            componentOptions,
            320 + (document.Nodes.Count * 120),
            220 + ((document.Nodes.Count % 3) * 120));
        WorkflowCanvasDefinitionMapper.ApplyComponent(node, component);
        node.Name = component.Name;
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
            var component = await ComponentLibrary.SaveComponentAsync(new LlmCallComponentSaveRequest(
                Id: null,
                Name: $"Canvas LLM call {DateTimeOffset.UtcNow:HHmmss}",
                ProviderProfileId: null,
                Model: "gpt-5.4",
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
            await AddNodeAsync(kind);
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

    private string ResolveNodeName(WorkflowNodeId nodeId)
    {
        return document.Nodes.FirstOrDefault(node => node.Id == nodeId)?.Name ?? nodeId.Value;
    }

    private static string FormatComponentId(WorkflowComponentId? componentId)
    {
        return componentId?.Value.ToString("D") ?? string.Empty;
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
