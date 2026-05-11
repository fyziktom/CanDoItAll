using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Components.CanvasLib;

namespace CanDoItAll.Modules.AgentFramework.Pages.Components;

internal sealed class WorkflowCanvasDocument
{
    public WorkflowId? DefinitionId { get; set; }

    public WorkflowVersionId? VersionId { get; set; }

    public string Name { get; set; } = "Canvas workflow";

    public string Description { get; set; } = "Workflow authored from the canvas editor.";

    public WorkflowLifecycleStatus Status { get; set; } = WorkflowLifecycleStatus.Draft;

    public WorkflowRuntimePolicy RuntimePolicy { get; set; } = WorkflowSettings.Default.DefaultRuntimePolicy;

    public WorkflowNodeId StartNodeId { get; set; } = new("start");

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public List<WorkflowCanvasNodeDraft> Nodes { get; } = [];

    public List<WorkflowCanvasEdgeDraft> Edges { get; } = [];
}

internal sealed class WorkflowCanvasNodeDraft(WorkflowNodeId id, WorkflowNodeKind kind)
{
    public WorkflowNodeId Id { get; set; } = id;

    public WorkflowNodeKind Kind { get; set; } = kind;

    public string Name { get; set; } = WorkflowCanvasDefinitionMapper.ResolveDefaultNodeName(kind);

    public WorkflowComponentId? ComponentId { get; set; }

    public Guid? AgentId { get; set; }

    public WorkflowId? SubworkflowId { get; set; }

    public WorkflowExternalRequestKind? ExternalRequestKind { get; set; }

    public WorkflowExecutorId? ExecutorId { get; set; }

    public string ExecutorSettingsJson { get; set; } = string.Empty;

    public WorkflowExecutorExecutionPolicy? ExecutionPolicy { get; set; }

    public string Instructions { get; set; } = WorkflowCanvasDefinitionMapper.ResolveDefaultInstructions(kind);

    public WorkflowValueShapeKind InputShapeKind { get; set; } = WorkflowValueShapeKind.Text;

    public WorkflowValueShapeKind ResultShapeKind { get; set; } = WorkflowValueShapeKind.Text;

    public double CanvasX { get; set; }

    public double CanvasY { get; set; }
}

internal sealed class WorkflowCanvasEdgeDraft(
    WorkflowEdgeId id,
    WorkflowNodeId sourceNodeId,
    WorkflowNodeId targetNodeId)
{
    public WorkflowEdgeId Id { get; set; } = id;

    public WorkflowNodeId SourceNodeId { get; set; } = sourceNodeId;

    public WorkflowNodeId TargetNodeId { get; set; } = targetNodeId;

    public WorkflowEdgeKind Kind { get; set; } = WorkflowEdgeKind.Direct;

    public string ConditionExpression { get; set; } = string.Empty;

    public WorkflowEdgeRouting Routing { get; set; } = WorkflowEdgeRouting.Always;
}

internal static class WorkflowCanvasDefinitionMapper
{
    public const string InputPortId = "workflow:input";
    public const string OutputPortId = "workflow:output";

    private static readonly IReadOnlyDictionary<WorkflowNodeKind, string> NodeIcons = new Dictionary<WorkflowNodeKind, string>
    {
        [WorkflowNodeKind.Start] = "play_arrow",
        [WorkflowNodeKind.LlmCall] = "smart_toy",
        [WorkflowNodeKind.Triage] = "call_split",
        [WorkflowNodeKind.StrictLogic] = "rule",
        [WorkflowNodeKind.Executor] = "bolt",
        [WorkflowNodeKind.Artifact] = "description",
        [WorkflowNodeKind.HumanInput] = "approval",
        [WorkflowNodeKind.AgentStep] = "support_agent",
        [WorkflowNodeKind.Subworkflow] = "account_tree",
        [WorkflowNodeKind.End] = "flag"
    };

    private static readonly IReadOnlyDictionary<WorkflowNodeKind, string> NodeAccents = new Dictionary<WorkflowNodeKind, string>
    {
        [WorkflowNodeKind.Start] = "#0369a1",
        [WorkflowNodeKind.LlmCall] = "#047857",
        [WorkflowNodeKind.Triage] = "#7c3aed",
        [WorkflowNodeKind.StrictLogic] = "#b45309",
        [WorkflowNodeKind.Executor] = "#0f766e",
        [WorkflowNodeKind.Artifact] = "#be185d",
        [WorkflowNodeKind.HumanInput] = "#a16207",
        [WorkflowNodeKind.AgentStep] = "#0e7490",
        [WorkflowNodeKind.Subworkflow] = "#4f46e5",
        [WorkflowNodeKind.End] = "#15803d"
    };

    public static WorkflowCanvasDocument CreateDraft(IReadOnlyList<LlmCallComponent> components)
    {
        ArgumentNullException.ThrowIfNull(components);

        var document = new WorkflowCanvasDocument
        {
            Name = "Canvas workflow",
            Description = "Workflow authored from the canvas editor.",
            RuntimePolicy = new WorkflowRuntimePolicy(
                WorkflowRuntimeBackendKind.InProcess,
                AllowInProcessPreviewRuns: true,
                RequireDurableProductionRuns: false,
                ExposeAzureFunctionsStatusEndpoint: false,
                ExposeAzureFunctionsMcpTool: false)
        };
        var start = CreateNode(
            WorkflowNodeKind.Start,
            document.Nodes,
            components,
            120,
            220);
        var end = CreateNode(
            WorkflowNodeKind.End,
            document.Nodes,
            components,
            680,
            220);
        document.Nodes.Add(start);
        document.Nodes.Add(end);
        document.Edges.Add(new WorkflowCanvasEdgeDraft(
            new WorkflowEdgeId("start-to-end"),
            start.Id,
            end.Id));
        document.StartNodeId = start.Id;
        return document;
    }

    public static WorkflowCanvasDocument FromDefinition(
        WorkflowDefinition definition,
        IReadOnlyList<LlmCallComponent> components)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(components);

        var document = new WorkflowCanvasDocument
        {
            DefinitionId = definition.Id,
            VersionId = definition.VersionId,
            Name = definition.Name,
            Description = definition.Description,
            Status = definition.Status,
            RuntimePolicy = definition.RuntimePolicy,
            StartNodeId = definition.Graph.StartNodeId,
            CreatedAtUtc = definition.CreatedAtUtc,
            UpdatedAtUtc = definition.UpdatedAtUtc
        };

        for (var index = 0; index < definition.Graph.Nodes.Count; index++)
        {
            var node = definition.Graph.Nodes[index];
            var draft = new WorkflowCanvasNodeDraft(node.Id, node.Kind)
            {
                Name = node.Name,
                ComponentId = node.Settings.ComponentId,
                AgentId = node.Settings.AgentId,
                SubworkflowId = node.Settings.SubworkflowId,
                ExternalRequestKind = node.Settings.ExternalRequestKind,
                ExecutorId = node.Settings.ExecutorId,
                ExecutorSettingsJson = node.Settings.ExecutorSettingsJson,
                ExecutionPolicy = node.Settings.ExecutionPolicy,
                Instructions = node.Settings.Instructions,
                InputShapeKind = ResolveInputShapeKind(node, components),
                ResultShapeKind = ResolveResultShapeKind(node, components),
                CanvasX = node.CanvasX != 0 ? node.CanvasX : 120 + (index * 280),
                CanvasY = node.CanvasY != 0 ? node.CanvasY : 220
            };
            document.Nodes.Add(draft);
        }

        foreach (var edge in definition.Graph.Edges)
        {
            document.Edges.Add(new WorkflowCanvasEdgeDraft(edge.Id, edge.SourceNodeId, edge.TargetNodeId)
            {
                Kind = edge.Kind,
                ConditionExpression = edge.ConditionExpression,
                Routing = edge.Routing ?? WorkflowEdgeRouting.Always
            });
        }

        EnsureStartAndEnd(document, components);
        return document;
    }

    public static WorkflowDefinition ToDefinition(WorkflowCanvasDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        EnsureStartAndEnd(document, []);
        var nodes = document.Nodes
            .Select(node => new WorkflowNode(
                node.Id,
                node.Kind,
                string.IsNullOrWhiteSpace(node.Name) ? ResolveDefaultNodeName(node.Kind) : node.Name.Trim(),
                BuildPorts(node),
                new WorkflowNodeSettings(
                    node.ComponentId,
                    node.AgentId,
                    node.SubworkflowId,
                    node.ExternalRequestKind,
                    node.Instructions.Trim(),
                    CreateShape(node.InputShapeKind),
                    CreateShape(node.ResultShapeKind))
                {
                    ExecutorId = node.ExecutorId,
                    ExecutorSettingsJson = node.ExecutorSettingsJson.Trim(),
                    ExecutionPolicy = node.ExecutionPolicy
                },
                node.CanvasX,
                node.CanvasY))
            .ToArray();
        var edges = document.Edges
            .Select(edge => new WorkflowEdge(
                edge.Id,
                edge.SourceNodeId,
                new WorkflowPortId(OutputPortId),
                edge.TargetNodeId,
                new WorkflowPortId(InputPortId),
                edge.Kind,
                edge.ConditionExpression.Trim())
            {
                Routing = edge.Routing
            })
            .ToArray();
        var now = DateTimeOffset.UtcNow;
        return new WorkflowDefinition(
            document.DefinitionId ?? WorkflowId.New(),
            document.VersionId ?? WorkflowVersionId.New(),
            string.IsNullOrWhiteSpace(document.Name) ? "Canvas workflow" : document.Name.Trim(),
            document.Description.Trim(),
            document.Status,
            new WorkflowGraph(document.StartNodeId, nodes, edges),
            document.RuntimePolicy,
            document.CreatedAtUtc,
            now);
    }

    public static CanvasWorkbenchSurface BuildSurface(
        WorkflowCanvasDocument document,
        IReadOnlyList<LlmCallComponent> components,
        IReadOnlyList<WorkflowExecutorDescriptor> executors,
        IReadOnlyList<WorkflowValidationIssue> validationIssues,
        CanvasWorkbenchUiState uiState,
        string? selectedNodeId)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(components);
        ArgumentNullException.ThrowIfNull(executors);
        ArgumentNullException.ThrowIfNull(validationIssues);
        ArgumentNullException.ThrowIfNull(uiState);

        var issuesByNode = validationIssues
            .Where(issue => issue.NodeId.HasValue)
            .GroupBy(issue => issue.NodeId!.Value)
            .ToDictionary(group => group.Key, group => group.ToArray());
        var componentsById = components.ToDictionary(component => component.Id);
        var executorsById = executors.ToDictionary(executor => executor.Id);
        var nodes = document.Nodes
            .Select(node => BuildWorkbenchNode(node, componentsById, executorsById, issuesByNode))
            .ToList();
        var decisionSummariesBySource = document.Edges
            .GroupBy(edge => edge.SourceNodeId)
            .Where(group => group.Count() > 1 || group.Any(edge => edge.Routing.Kind != WorkflowRouteKind.Always))
            .ToDictionary(
                group => group.Key.Value,
                group => $"{group.Count()} route(s)");
        foreach (var node in nodes)
        {
            if (!decisionSummariesBySource.TryGetValue(node.Id, out var summary))
            {
                continue;
            }

            node.Family = "workflow-decision";
            node.Icon = "call_split";
            node.BranchLabel = summary;
            node.PaletteKey = "workflow-decision";
            node.AccentColor = "#0f766e";
            node.FooterChips.Add(new CanvasWorkbenchChip
            {
                Text = summary,
                Tone = "info"
            });
        }

        var links = document.Edges
            .Select(edge => new CanvasWorkbenchLink
            {
                SourceId = edge.SourceNodeId.Value,
                SourcePortId = OutputPortId,
                TargetId = edge.TargetNodeId.Value,
                TargetPortId = InputPortId,
                Kind = edge.Routing.Kind.ToString(),
                Label = ResolveRouteLabel(edge),
                Summary = BuildRouteSummary(edge),
                Tone = ResolveRouteTone(edge),
                IsUserAuthored = true
            })
            .ToList();
        var selectedNodeIds = string.IsNullOrWhiteSpace(selectedNodeId)
            ? []
            : nodes.Any(node => string.Equals(node.Id, selectedNodeId, StringComparison.Ordinal))
                ? new List<string> { selectedNodeId }
                : [];

        var resolvedUiState = CanvasWorkbenchUiState.Parse(uiState.ToJson());
        resolvedUiState.SelectedNodeIds = selectedNodeIds;
        resolvedUiState.ActiveInspectorTab = "workflow";

        return new CanvasWorkbenchSurface
        {
            SurfaceId = document.DefinitionId?.ToString() ?? "workflow-canvas-draft",
            Mode = "workflow-authoring",
            DependencySourceId = document.VersionId?.ToString() ?? "draft",
            Nodes = nodes,
            Links = links,
            UiState = resolvedUiState,
            Chrome = BuildChrome(executors)
        };
    }

    public static WorkflowCanvasNodeDraft CreateNode(
        WorkflowNodeKind kind,
        IReadOnlyList<WorkflowCanvasNodeDraft> existingNodes,
        IReadOnlyList<LlmCallComponent> components,
        double x,
        double y)
    {
        ArgumentNullException.ThrowIfNull(existingNodes);
        ArgumentNullException.ThrowIfNull(components);

        var node = new WorkflowCanvasNodeDraft(CreateNodeId(kind, existingNodes), kind)
        {
            Name = ResolveDefaultNodeName(kind),
            Instructions = ResolveDefaultInstructions(kind),
            CanvasX = x,
            CanvasY = y,
            InputShapeKind = WorkflowValueShapeKind.Text,
            ResultShapeKind = WorkflowValueShapeKind.Text
        };
        if (kind == WorkflowNodeKind.HumanInput)
        {
            node.ExternalRequestKind = WorkflowExternalRequestKind.HumanInput;
        }

        if (kind == WorkflowNodeKind.LlmCall && components.FirstOrDefault() is { } component)
        {
            ApplyComponent(node, component);
        }

        return node;
    }

    public static void ApplyComponent(WorkflowCanvasNodeDraft node, LlmCallComponent component)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(component);

        node.ComponentId = component.Id;
        node.InputShapeKind = component.InputShape.Kind;
        node.ResultShapeKind = component.ResultShape.Kind;
        if (string.IsNullOrWhiteSpace(node.Instructions) ||
            string.Equals(node.Instructions, ResolveDefaultInstructions(node.Kind), StringComparison.Ordinal))
        {
            node.Instructions = component.Instructions;
        }
    }

    public static void ApplyExecutor(WorkflowCanvasNodeDraft node, WorkflowExecutorDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(descriptor);

        node.Kind = WorkflowNodeKind.Executor;
        node.ExecutorId = descriptor.Id;
        node.ExecutorSettingsJson = descriptor.DefaultSettingsJson;
        node.ExecutionPolicy = descriptor.DefaultPolicy;
        node.InputShapeKind = descriptor.InputShape.Kind;
        node.ResultShapeKind = descriptor.ResultShape.Kind;
        if (string.IsNullOrWhiteSpace(node.Name) ||
            string.Equals(node.Name, ResolveDefaultNodeName(WorkflowNodeKind.Executor), StringComparison.Ordinal))
        {
            node.Name = descriptor.Name;
        }

        if (string.IsNullOrWhiteSpace(node.Instructions) ||
            string.Equals(node.Instructions, ResolveDefaultInstructions(WorkflowNodeKind.Executor), StringComparison.Ordinal))
        {
            node.Instructions = descriptor.Description;
        }
    }

    public static string ResolveDefaultNodeName(WorkflowNodeKind kind)
    {
        return kind switch
        {
            WorkflowNodeKind.Start => "Start",
            WorkflowNodeKind.LlmCall => "LLM call",
            WorkflowNodeKind.Triage => "Triage",
            WorkflowNodeKind.StrictLogic => "Strict logic",
            WorkflowNodeKind.Executor => "Executor",
            WorkflowNodeKind.Artifact => "Artifact output",
            WorkflowNodeKind.HumanInput => "Human input",
            WorkflowNodeKind.AgentStep => "Agent step",
            WorkflowNodeKind.Subworkflow => "Subworkflow",
            WorkflowNodeKind.End => "End",
            _ => kind.ToString()
        };
    }

    public static string ResolveDefaultInstructions(WorkflowNodeKind kind)
    {
        return kind switch
        {
            WorkflowNodeKind.LlmCall => "Call the selected model with strict task instructions and return only the requested result.",
            WorkflowNodeKind.Triage => "Classify the input and route it to the correct downstream branch.",
            WorkflowNodeKind.StrictLogic => "Apply deterministic business rules to the current workflow payload.",
            WorkflowNodeKind.Executor => "Execute a typed tool with explicit settings, timeout, retry, and output policy.",
            WorkflowNodeKind.Artifact => "Capture the current payload as a workflow artifact.",
            WorkflowNodeKind.HumanInput => "Request explicit human input before continuing.",
            WorkflowNodeKind.AgentStep => "Invoke the selected agent with the current workflow payload.",
            WorkflowNodeKind.Subworkflow => "Invoke the selected subworkflow with the current workflow payload.",
            WorkflowNodeKind.Start => "Accept the workflow input payload.",
            WorkflowNodeKind.End => "Return the final workflow output.",
            _ => string.Empty
        };
    }

    public static string ResolveRouteModeLabel(WorkflowRouteKind kind)
    {
        return kind switch
        {
            WorkflowRouteKind.Always => "Direct",
            WorkflowRouteKind.Predicate => "IF predicate",
            WorkflowRouteKind.SwitchCase => "Switch case",
            WorkflowRouteKind.SwitchDefault => "Switch default",
            WorkflowRouteKind.FanOutSelector => "Fan-out selector",
            _ => kind.ToString()
        };
    }

    public static string BuildRouteSummary(WorkflowCanvasEdgeDraft edge)
    {
        ArgumentNullException.ThrowIfNull(edge);

        var routing = edge.Routing ?? WorkflowEdgeRouting.Always;
        var summary = routing.Kind switch
        {
            WorkflowRouteKind.Always => "Always",
            WorkflowRouteKind.Predicate => $"{routing.JsonPath} {WorkflowRoutingValidation.FormatOperator(routing.Operator)} {FormatExpectedValue(routing)}",
            WorkflowRouteKind.SwitchCase => $"case {FormatExpectedValue(routing)} from {routing.JsonPath}",
            WorkflowRouteKind.SwitchDefault => "default branch",
            WorkflowRouteKind.FanOutSelector => $"target {routing.FanOutTargetIndex?.ToString() ?? "auto"} when {routing.JsonPath} {WorkflowRoutingValidation.FormatOperator(routing.Operator)} {FormatExpectedValue(routing)}",
            _ => routing.Kind.ToString()
        };

        return string.IsNullOrWhiteSpace(routing.Label)
            ? summary
            : $"{routing.Label}: {summary}";
    }

    public static string ResolveRouteLabel(WorkflowCanvasEdgeDraft edge)
    {
        ArgumentNullException.ThrowIfNull(edge);

        var routing = edge.Routing ?? WorkflowEdgeRouting.Always;
        if (!string.IsNullOrWhiteSpace(routing.Label))
        {
            return routing.Label.Trim();
        }

        return routing.Kind switch
        {
            WorkflowRouteKind.Always => "Direct",
            WorkflowRouteKind.Predicate => "IF",
            WorkflowRouteKind.SwitchCase => $"Case {FormatExpectedValue(routing)}",
            WorkflowRouteKind.SwitchDefault => "Default",
            WorkflowRouteKind.FanOutSelector => $"Fan-out {routing.FanOutTargetIndex?.ToString() ?? string.Empty}".Trim(),
            _ => routing.Kind.ToString()
        };
    }

    public static string ResolveRouteTone(WorkflowCanvasEdgeDraft edge)
    {
        ArgumentNullException.ThrowIfNull(edge);

        return edge.Routing.Kind switch
        {
            WorkflowRouteKind.Always => "neutral",
            WorkflowRouteKind.Predicate => "success",
            WorkflowRouteKind.SwitchCase => "info",
            WorkflowRouteKind.SwitchDefault => "default",
            WorkflowRouteKind.FanOutSelector => "fanout",
            _ => "neutral"
        };
    }

    private static string FormatExpectedValue(WorkflowEdgeRouting routing)
    {
        if (!WorkflowRoutingValidation.RequiresExpectedValue(routing.Operator))
        {
            return string.Empty;
        }

        if (routing.ExpectedValueKind == WorkflowRouteValueKind.String &&
            routing.ExpectedValueJson.Length >= 2 &&
            routing.ExpectedValueJson[0] == '"' &&
            routing.ExpectedValueJson[^1] == '"')
        {
            return routing.ExpectedValueJson[1..^1];
        }

        return routing.ExpectedValueJson;
    }

    public static IReadOnlyList<WorkflowNodeKind> CreatableNodeKinds { get; } =
    [
        WorkflowNodeKind.LlmCall,
        WorkflowNodeKind.Triage,
        WorkflowNodeKind.StrictLogic,
        WorkflowNodeKind.Artifact,
        WorkflowNodeKind.HumanInput,
        WorkflowNodeKind.AgentStep,
        WorkflowNodeKind.Subworkflow
    ];

    private static WorkflowNodeId CreateNodeId(
        WorkflowNodeKind kind,
        IReadOnlyList<WorkflowCanvasNodeDraft> existingNodes)
    {
        var prefix = kind switch
        {
            WorkflowNodeKind.LlmCall => "llm",
            WorkflowNodeKind.StrictLogic => "logic",
            WorkflowNodeKind.Executor => "executor",
            WorkflowNodeKind.HumanInput => "human",
            WorkflowNodeKind.AgentStep => "agent",
            WorkflowNodeKind.Subworkflow => "subworkflow",
            WorkflowNodeKind.Artifact => "artifact",
            WorkflowNodeKind.Triage => "triage",
            WorkflowNodeKind.Start => "start",
            WorkflowNodeKind.End => "end",
            _ => "node"
        };
        var existingIds = existingNodes
            .Select(node => node.Id.Value)
            .ToHashSet(StringComparer.Ordinal);
        if (!existingIds.Contains(prefix))
        {
            return new WorkflowNodeId(prefix);
        }

        for (var index = 1; ; index++)
        {
            var candidate = $"{prefix}-{index}";
            if (!existingIds.Contains(candidate))
            {
                return new WorkflowNodeId(candidate);
            }
        }
    }

    private static void EnsureStartAndEnd(
        WorkflowCanvasDocument document,
        IReadOnlyList<LlmCallComponent> components)
    {
        if (!document.Nodes.Any(node => node.Kind == WorkflowNodeKind.Start))
        {
            var start = CreateNode(WorkflowNodeKind.Start, document.Nodes, components, 120, 220);
            document.Nodes.Insert(0, start);
            document.StartNodeId = start.Id;
        }

        if (!document.Nodes.Any(node => node.Id == document.StartNodeId))
        {
            document.StartNodeId = document.Nodes.First(node => node.Kind == WorkflowNodeKind.Start).Id;
        }

        if (!document.Nodes.Any(node => node.Kind == WorkflowNodeKind.End))
        {
            document.Nodes.Add(CreateNode(WorkflowNodeKind.End, document.Nodes, components, 760, 220));
        }
    }

    private static CanvasWorkbenchChrome BuildChrome(IReadOnlyList<WorkflowExecutorDescriptor> executors)
    {
        return new CanvasWorkbenchChrome
        {
            HintText = "Select workflow nodes, add typed steps from the toolbox, connect ports, validate, and run previews.",
            EmptyStateKicker = "Workflow canvas",
            EmptyStateTitle = "No workflow nodes",
            EmptyStateDescription = "Use the toolbox to add typed workflow steps.",
            FocusActionLabel = "Focus start",
            ShowQuickCreateRail = true,
            CollapseOnDoubleClick = false,
            QuickCreateActions = CreatableNodeKinds
                .Select(kind => new CanvasWorkbenchAction
                {
                    ActionId = BuildCreateActionId(kind),
                    Label = ResolveDefaultNodeName(kind),
                    MenuLabel = ResolveDefaultNodeName(kind),
                    Description = ResolveDefaultInstructions(kind),
                    Icon = ResolveIcon(kind),
                    Tone = ResolveTone(kind),
                    RequiresInput = true,
                    CreateMode = "dialog",
                    TitlePlaceholder = ResolveDefaultNodeName(kind),
                    NotesPlaceholder = ResolveDefaultInstructions(kind),
                    SubmitLabel = "Add node"
                })
                .Concat(WorkflowExecutorCanvasCatalog.BuildQuickCreateActions(executors))
                .ToList()
        };
    }

    private static CanvasWorkbenchNode BuildWorkbenchNode(
        WorkflowCanvasNodeDraft node,
        IReadOnlyDictionary<WorkflowComponentId, LlmCallComponent> componentsById,
        IReadOnlyDictionary<WorkflowExecutorId, WorkflowExecutorDescriptor> executorsById,
        IReadOnlyDictionary<WorkflowNodeId, WorkflowValidationIssue[]> issuesByNode)
    {
        var issueCount = issuesByNode.TryGetValue(node.Id, out var issues) ? issues.Length : 0;
        var component = node.ComponentId is { } componentId && componentsById.TryGetValue(componentId, out var matchedComponent)
            ? matchedComponent
            : null;
        var executor = node.ExecutorId is { } executorId && executorsById.TryGetValue(executorId, out var matchedExecutor)
            ? matchedExecutor
            : null;
        return new CanvasWorkbenchNode
        {
            Id = node.Id.Value,
            Kind = node.Kind.ToString(),
            Family = node.Kind is WorkflowNodeKind.Start or WorkflowNodeKind.End ? "workflow-boundary" : "workflow-step",
            Icon = ResolveIcon(node, executor),
            Title = string.IsNullOrWhiteSpace(node.Name) ? ResolveDefaultNodeName(node.Kind) : node.Name,
            Subtitle = ResolveSubtitle(node, component, executor),
            LeadText = string.IsNullOrWhiteSpace(node.Instructions) ? ResolveDefaultInstructions(node.Kind) : node.Instructions,
            Status = issueCount == 0 ? "valid" : "invalid",
            StatusPill = issueCount == 0 ? node.Kind.ToString() : $"{issueCount} issue(s)",
            PaletteKey = issueCount == 0 ? ResolveTone(node, executor) : "danger",
            AccentColor = issueCount == 0 ? ResolveAccent(node.Kind) : "#b91c1c",
            DurationLabel = node.Kind == WorkflowNodeKind.HumanInput ? "Wait" : "Step",
            X = node.CanvasX,
            Y = node.CanvasY,
            Chips = BuildNodeChips(node, component, executor),
            FooterChips = BuildFooterChips(node),
            Annotations = issueCount == 0
                ? []
                : issues!
                    .Select((issue, index) => new CanvasWorkbenchAnnotation
                    {
                        Id = $"{node.Id.Value}-issue-{index}",
                        Kind = "validation",
                        Tone = "danger",
                        Label = issue.Code.ToString(),
                        Description = issue.Message,
                        Icon = "error"
                    })
                    .ToList(),
            ContextActions =
            [
                new CanvasWorkbenchAction
                {
                    ActionId = "workflow-node:edit",
                    Label = "Edit node",
                    MenuLabel = "Edit",
                    Icon = "edit",
                    Tone = "info"
                },
                new CanvasWorkbenchAction
                {
                    ActionId = "workflow-node:remove",
                    Label = "Remove node",
                    MenuLabel = "Remove",
                    Icon = "delete",
                    Tone = "danger"
                }
            ],
            InputPorts = node.Kind == WorkflowNodeKind.Start ? [] : [BuildInputPort(node)],
            OutputPorts = node.Kind == WorkflowNodeKind.End ? [] : [BuildOutputPort(node)]
        };
    }

    private static List<CanvasWorkbenchChip> BuildNodeChips(
        WorkflowCanvasNodeDraft node,
        LlmCallComponent? component,
        WorkflowExecutorDescriptor? executor)
    {
        var chips = new List<CanvasWorkbenchChip>
        {
            new()
            {
                Text = $"In {node.InputShapeKind}",
                Tone = "neutral"
            },
            new()
            {
                Text = $"Out {node.ResultShapeKind}",
                Tone = "info"
            }
        };
        if (component is not null)
        {
            chips.Add(new CanvasWorkbenchChip
            {
                Text = component.Model,
                Tone = "accent"
            });
        }

        if (executor is not null)
        {
            chips.Add(new CanvasWorkbenchChip
            {
                Text = executor.IsImplemented ? executor.Name : $"{executor.Name} planned",
                Tone = executor.IsImplemented ? "accent" : "warning"
            });
        }

        if (node.Kind == WorkflowNodeKind.HumanInput && node.ExternalRequestKind.HasValue)
        {
            chips.Add(new CanvasWorkbenchChip
            {
                Text = node.ExternalRequestKind.Value.ToString(),
                Tone = "accent"
            });
        }

        return chips;
    }

    private static List<CanvasWorkbenchChip> BuildFooterChips(WorkflowCanvasNodeDraft node)
    {
        return node.Kind switch
        {
            WorkflowNodeKind.AgentStep when node.AgentId.HasValue =>
            [
                new CanvasWorkbenchChip
                {
                    Text = node.AgentId.Value.ToString("D"),
                    Tone = "neutral"
                }
            ],
            WorkflowNodeKind.Subworkflow when node.SubworkflowId.HasValue =>
            [
                new CanvasWorkbenchChip
                {
                    Text = node.SubworkflowId.Value.ToString(),
                    Tone = "neutral"
                }
            ],
            _ => []
        };
    }

    private static CanvasWorkbenchPort BuildInputPort(WorkflowCanvasNodeDraft node)
    {
        return new CanvasWorkbenchPort
        {
            Id = InputPortId,
            Label = node.InputShapeKind.ToString(),
            Side = "left",
            Tone = "neutral",
            Kind = node.InputShapeKind.ToString(),
            IsRequired = true
        };
    }

    private static CanvasWorkbenchPort BuildOutputPort(WorkflowCanvasNodeDraft node)
    {
        return new CanvasWorkbenchPort
        {
            Id = OutputPortId,
            Label = node.ResultShapeKind.ToString(),
            Side = "right",
            Tone = "info",
            Kind = node.ResultShapeKind.ToString(),
            IsRequired = true
        };
    }

    private static IReadOnlyList<WorkflowPort> BuildPorts(WorkflowCanvasNodeDraft node)
    {
        var ports = new List<WorkflowPort>();
        if (node.Kind != WorkflowNodeKind.Start)
        {
            ports.Add(new WorkflowPort(
                new WorkflowPortId(InputPortId),
                "Input",
                WorkflowPortDirection.Input,
                CreateShape(node.InputShapeKind),
                Required: true));
        }

        if (node.Kind != WorkflowNodeKind.End)
        {
            ports.Add(new WorkflowPort(
                new WorkflowPortId(OutputPortId),
                "Output",
                WorkflowPortDirection.Output,
                CreateShape(node.ResultShapeKind),
                Required: true));
        }

        return ports;
    }

    private static WorkflowValueShape CreateShape(WorkflowValueShapeKind kind)
    {
        return new WorkflowValueShape(kind, string.Empty, kind.ToString());
    }

    private static WorkflowValueShapeKind ResolveInputShapeKind(
        WorkflowNode node,
        IReadOnlyList<LlmCallComponent> components)
    {
        if (node.Kind == WorkflowNodeKind.LlmCall &&
            node.Settings.ComponentId is { } componentId &&
            components.FirstOrDefault(component => component.Id == componentId) is { } component)
        {
            return component.InputShape.Kind;
        }

        return node.Settings.InputShape?.Kind ?? WorkflowValueShapeKind.Text;
    }

    private static WorkflowValueShapeKind ResolveResultShapeKind(
        WorkflowNode node,
        IReadOnlyList<LlmCallComponent> components)
    {
        if (node.Kind == WorkflowNodeKind.LlmCall &&
            node.Settings.ComponentId is { } componentId &&
            components.FirstOrDefault(component => component.Id == componentId) is { } component)
        {
            return component.ResultShape.Kind;
        }

        return node.Settings.ResultShape?.Kind ?? WorkflowValueShapeKind.Text;
    }

    private static string ResolveSubtitle(
        WorkflowCanvasNodeDraft node,
        LlmCallComponent? component,
        WorkflowExecutorDescriptor? executor)
    {
        return node.Kind switch
        {
            WorkflowNodeKind.LlmCall => component is null ? "Prepared LLM Call Component required" : component.Name,
            WorkflowNodeKind.Triage => "Conditional routing",
            WorkflowNodeKind.StrictLogic => "Deterministic logic",
            WorkflowNodeKind.Executor => executor is null ? "Executor required" : $"{executor.Category} executor",
            WorkflowNodeKind.Artifact => "Artifact capture",
            WorkflowNodeKind.HumanInput => "RequestPort style pause",
            WorkflowNodeKind.AgentStep => node.AgentId.HasValue ? "Agent executor" : "Agent id required later",
            WorkflowNodeKind.Subworkflow => node.SubworkflowId.HasValue ? "Nested workflow" : "Subworkflow id required later",
            WorkflowNodeKind.Start => "Workflow input",
            WorkflowNodeKind.End => "Workflow output",
            _ => node.Kind.ToString()
        };
    }

    private static string ResolveIcon(WorkflowNodeKind kind)
    {
        return NodeIcons.TryGetValue(kind, out var icon) ? icon : "circle";
    }

    private static string ResolveIcon(WorkflowCanvasNodeDraft node, WorkflowExecutorDescriptor? executor)
    {
        if (node.Kind == WorkflowNodeKind.Executor &&
            executor is not null &&
            !string.IsNullOrWhiteSpace(executor.IconName))
        {
            return executor.IconName;
        }

        return ResolveIcon(node.Kind);
    }

    private static string ResolveAccent(WorkflowNodeKind kind)
    {
        return NodeAccents.TryGetValue(kind, out var accent) ? accent : "#334155";
    }

    private static string ResolveTone(WorkflowNodeKind kind)
    {
        return kind switch
        {
            WorkflowNodeKind.LlmCall => "success",
            WorkflowNodeKind.Triage => "accent",
            WorkflowNodeKind.StrictLogic => "warning",
            WorkflowNodeKind.Executor => "info",
            WorkflowNodeKind.Artifact => "danger",
            WorkflowNodeKind.HumanInput => "warning",
            WorkflowNodeKind.AgentStep => "info",
            WorkflowNodeKind.Subworkflow => "info",
            WorkflowNodeKind.Start or WorkflowNodeKind.End => "success",
            _ => "neutral"
        };
    }

    private static string ResolveTone(WorkflowCanvasNodeDraft node, WorkflowExecutorDescriptor? executor)
    {
        if (node.Kind == WorkflowNodeKind.Executor && executor is not null)
        {
            return WorkflowExecutorCanvasCatalog.ResolveTone(executor.Category);
        }

        return ResolveTone(node.Kind);
    }

    public static string BuildCreateActionId(WorkflowNodeKind kind)
    {
        return $"workflow-node:create:{kind}";
    }

    public static bool TryParseCreateActionId(string actionId, out WorkflowNodeKind kind)
    {
        const string prefix = "workflow-node:create:";
        if (actionId.StartsWith(prefix, StringComparison.Ordinal) &&
            Enum.TryParse(actionId[prefix.Length..], ignoreCase: false, out kind))
        {
            return true;
        }

        kind = default;
        return false;
    }
}
