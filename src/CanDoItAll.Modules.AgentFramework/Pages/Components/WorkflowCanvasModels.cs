using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Components.CanvasLib;
using CanDoItAll.Modules.Security;

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
    public const string EditNodeActionId = "workflow-node:edit";
    public const string RemoveNodeActionId = "workflow-node:remove";
    public const string AddDecisionRouteActionId = "workflow-decision:add-route";

    private const string DecisionRoutesActionId = "workflow-decision:routes";

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
        IReadOnlyList<SecretListItem> secrets,
        IReadOnlyList<WorkflowValidationIssue> validationIssues,
        CanvasWorkbenchUiState uiState,
        string? selectedNodeId)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(components);
        ArgumentNullException.ThrowIfNull(executors);
        ArgumentNullException.ThrowIfNull(secrets);
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
        var decisionNodeIds = document.Nodes
            .Where(node => node.Kind == WorkflowNodeKind.Triage)
            .Select(node => node.Id.Value)
            .ToHashSet(StringComparer.Ordinal);
        foreach (var node in nodes)
        {
            if (decisionNodeIds.Contains(node.Id))
            {
                AddDecisionContextActions(node);
            }

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
            Mode = CanvasWorkbenchModes.Authoring,
            DependencySourceId = document.VersionId?.ToString() ?? "draft",
            Nodes = nodes,
            Links = links,
            UiState = resolvedUiState,
            Chrome = BuildChrome(executors, secrets)
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
            WorkflowRouteKind.Predicate when IsNegativePredicateLabel(edge.Routing.Label) ||
                                             edge.Routing.Operator == WorkflowRouteOperator.NotEquals ||
                                             edge.Routing.Operator == WorkflowRouteOperator.IsFalsy => "danger",
            WorkflowRouteKind.Predicate => "success",
            WorkflowRouteKind.SwitchCase => "info",
            WorkflowRouteKind.SwitchDefault => "default",
            WorkflowRouteKind.FanOutSelector => "fanout",
            _ => "neutral"
        };
    }

    private static bool IsNegativePredicateLabel(string label)
    {
        if (string.IsNullOrWhiteSpace(label))
        {
            return false;
        }

        return label.Contains("else", StringComparison.OrdinalIgnoreCase) ||
               label.Contains("false", StringComparison.OrdinalIgnoreCase) ||
               label.Contains("reject", StringComparison.OrdinalIgnoreCase) ||
               label.Contains("no ", StringComparison.OrdinalIgnoreCase);
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

    private static CanvasWorkbenchChrome BuildChrome(
        IReadOnlyList<WorkflowExecutorDescriptor> executors,
        IReadOnlyList<SecretListItem> secrets)
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
            QuickCreateActions = WorkflowCanvasDecisionCatalog.BuildQuickCreateActions()
                .Concat(CreatableNodeKinds
                    .Select(kind => new CanvasWorkbenchAction
                    {
                        ActionId = BuildCreateActionId(kind),
                        Label = ResolveDefaultNodeName(kind),
                        MenuLabel = ResolveDefaultNodeName(kind),
                        Description = ResolveDefaultInstructions(kind),
                        Icon = ResolveIcon(kind),
                        Tone = ResolveTone(kind),
                        SetupRendererKey = $"workflow-node-{kind.ToString().ToLowerInvariant()}",
                        RequiresInput = true,
                        CreateMode = "dialog",
                        TitlePlaceholder = ResolveDefaultNodeName(kind),
                        NotesPlaceholder = ResolveDefaultInstructions(kind),
                        SubmitLabel = "Add node",
                        InputFields = BuildNodeSetupFields(kind),
                        DefaultInputValues = BuildNodeDefaultInputValues(kind)
                    }))
                .Concat(WorkflowExecutorCanvasCatalog.BuildQuickCreateActions(executors, secrets))
                .ToList()
        };
    }

    private static List<CanvasWorkbenchInputField> BuildNodeSetupFields(WorkflowNodeKind kind)
    {
        var fields = new List<CanvasWorkbenchInputField>
        {
            CreateShapeField(
                "inputShape",
                "Input shape",
                sectionDescription: "Set the node contract before it lands on the canvas."),
            CreateShapeField(
                "resultShape",
                "Result shape",
                sectionDescription: "Set the node contract before it lands on the canvas.")
        };

        if (kind == WorkflowNodeKind.HumanInput)
        {
            fields.Add(new CanvasWorkbenchInputField
            {
                Key = "externalRequestKind",
                SectionKey = "request",
                SectionTitle = "Request",
                SectionDescription = "Choose how the workflow should pause for outside input.",
                Label = "Request kind",
                InputMode = "select",
                IsRequired = true,
                Options =
                [
                    new CanvasWorkbenchInputOption { Value = WorkflowExternalRequestKind.HumanInput.ToString(), Label = "Human input" },
                    new CanvasWorkbenchInputOption { Value = WorkflowExternalRequestKind.Approval.ToString(), Label = "Approval" }
                ]
            });
        }

        if (kind == WorkflowNodeKind.AgentStep)
        {
            fields.Add(new CanvasWorkbenchInputField
            {
                Key = "agentId",
                SectionKey = "binding",
                SectionTitle = "Binding",
                SectionDescription = "Optional for now; bind the node to an agent when the id is known.",
                Label = "Agent id",
                Placeholder = "00000000-0000-0000-0000-000000000000"
            });
        }

        if (kind == WorkflowNodeKind.Subworkflow)
        {
            fields.Add(new CanvasWorkbenchInputField
            {
                Key = "subworkflowId",
                SectionKey = "binding",
                SectionTitle = "Binding",
                SectionDescription = "Optional for now; bind the node to another workflow when the id is known.",
                Label = "Subworkflow id",
                Placeholder = "00000000-0000-0000-0000-000000000000"
            });
        }

        return fields;
    }

    private static List<CanvasWorkbenchInputValue> BuildNodeDefaultInputValues(WorkflowNodeKind kind)
        =>
        [
            new CanvasWorkbenchInputValue { Key = "inputShape", Value = WorkflowValueShapeKind.Text.ToString() },
            new CanvasWorkbenchInputValue
            {
                Key = "resultShape",
                Value = kind is WorkflowNodeKind.StrictLogic or WorkflowNodeKind.Triage
                    ? WorkflowValueShapeKind.Json.ToString()
                    : WorkflowValueShapeKind.Text.ToString()
            },
            new CanvasWorkbenchInputValue
            {
                Key = "externalRequestKind",
                Value = WorkflowExternalRequestKind.HumanInput.ToString()
            }
        ];

    private static CanvasWorkbenchInputField CreateShapeField(
        string key,
        string label,
        string sectionDescription)
        => new()
        {
            Key = key,
            SectionKey = "contract",
            SectionTitle = "Contract",
            SectionDescription = sectionDescription,
            Label = label,
            InputMode = "select",
            IsRequired = true,
            Options = Enum.GetValues<WorkflowValueShapeKind>()
                .Select(shape => new CanvasWorkbenchInputOption
                {
                    Value = shape.ToString(),
                    Label = shape.ToString()
                })
                .ToList()
        };

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
            MarkerIcon = ResolveSourceMarkerIcon(executor),
            MarkerLabel = ResolveSourceMarkerLabel(executor),
            MarkerTone = ResolveSourceMarkerTone(executor),
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
                    ActionId = EditNodeActionId,
                    Label = "Edit node",
                    MenuLabel = "Edit",
                    Icon = "edit",
                    Tone = "info"
                },
                new CanvasWorkbenchAction
                {
                    ActionId = RemoveNodeActionId,
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

    private static void AddDecisionContextActions(CanvasWorkbenchNode node)
    {
        if (node.ContextActions.Any(action => string.Equals(action.ActionId, DecisionRoutesActionId, StringComparison.Ordinal)))
        {
            return;
        }

        node.ContextActions.Insert(Math.Min(1, node.ContextActions.Count), new CanvasWorkbenchAction
        {
            ActionId = DecisionRoutesActionId,
            Label = "Routes",
            MenuLabel = "Routes",
            Description = "Add or edit decision outputs.",
            Icon = "alt_route",
            Tone = "accent",
            SubmenuLayout = "hive",
            Children =
            [
                new CanvasWorkbenchAction
                {
                    ActionId = AddDecisionRouteActionId,
                    Label = "Add route",
                    MenuLabel = "Add route",
                    Description = "Add another decision output route.",
                    Icon = "add",
                    Tone = "success"
                }
            ]
        });
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
            var availabilityBadge = WorkflowExecutorDisplayAdapter.BuildAvailabilityBadge(executor);
            var sideEffectBadge = WorkflowExecutorDisplayAdapter.BuildSideEffectBadge(executor);
            chips.Add(new CanvasWorkbenchChip
            {
                Text = executor.CanExecute ? executor.Name : $"{executor.Name} {availabilityBadge.Text.ToLowerInvariant()}",
                Tone = executor.CanExecute ? "accent" : availabilityBadge.Tone
            });
            chips.Add(new CanvasWorkbenchChip
            {
                Text = sideEffectBadge.Text,
                Tone = sideEffectBadge.Tone
            });
            if (WorkflowExecutorDisplayAdapter.BuildRetrySafetyBadge(
                    executor,
                    node.ExecutionPolicy ?? executor.DefaultPolicy) is { } retryBadge)
            {
                chips.Add(new CanvasWorkbenchChip
                {
                    Text = retryBadge.Text,
                    Tone = retryBadge.Tone
                });
            }
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

    private static string ResolveSourceMarkerIcon(WorkflowExecutorDescriptor? executor)
        => executor is not null &&
           executor.Source.Kind != WorkflowExecutorSourceKind.BuiltIn &&
           !string.IsNullOrWhiteSpace(executor.Source.PluginId)
            ? ResolveIconName(executor.Source.Icon)
            : string.Empty;

    private static string ResolveSourceMarkerLabel(WorkflowExecutorDescriptor? executor)
        => executor is not null &&
           executor.Source.Kind != WorkflowExecutorSourceKind.BuiltIn &&
           !string.IsNullOrWhiteSpace(executor.Source.PluginId)
            ? string.IsNullOrWhiteSpace(executor.Source.DisplayName) ? executor.Source.PluginId : executor.Source.DisplayName
            : string.Empty;

    private static string ResolveSourceMarkerTone(WorkflowExecutorDescriptor? executor)
        => executor is not null &&
           executor.Source.Kind != WorkflowExecutorSourceKind.BuiltIn &&
           !string.IsNullOrWhiteSpace(executor.Source.PluginId)
            ? "accent"
            : string.Empty;

    private static string ResolveIconName(UiIconDescriptor? icon)
        => icon?.Kind == UiIconKind.MaterialIcon && !string.IsNullOrWhiteSpace(icon.Value)
            ? icon.Value
            : "extension";

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

internal enum WorkflowDecisionBlockKind
{
    IfElse,
    Switch,
    FanOut
}

internal static class WorkflowCanvasDecisionCatalog
{
    private const string CreateDecisionActionPrefix = "workflow-decision:create:";

    public static IReadOnlyList<WorkflowDecisionBlockKind> DecisionBlockKinds { get; } =
    [
        WorkflowDecisionBlockKind.IfElse,
        WorkflowDecisionBlockKind.Switch,
        WorkflowDecisionBlockKind.FanOut
    ];

    public static IReadOnlyList<CanvasWorkbenchAction> BuildQuickCreateActions()
        =>
        [
            new CanvasWorkbenchAction
            {
                ActionId = "workflow-decision:menu",
                Label = "Decisions",
                MenuLabel = "Decisions",
                Description = "Split execution with IF/ELSE predicates, SWITCH/default branches, or fan-out selectors.",
                Icon = "call_split",
                Tone = "accent",
                SubmenuLayout = "toolbox",
                Children = DecisionBlockKinds.Select(BuildCreateAction).ToList()
            }
        ];

    public static CanvasWorkbenchAction BuildCreateAction(WorkflowDecisionBlockKind kind)
        => kind switch
        {
            WorkflowDecisionBlockKind.IfElse => new CanvasWorkbenchAction
            {
                ActionId = BuildCreateActionId(kind),
                Label = "IF",
                MenuLabel = "IF / ELSE",
                Description = "Create a binary decision with true and else branches.",
                Icon = "call_split",
                Tone = "success",
                SetupRendererKey = "workflow-decision-if-else",
                RequiresInput = true,
                CreateMode = "dialog",
                ObjectSubtype = kind.ToString(),
                TitlePlaceholder = "IF",
                NotesPlaceholder = "Route based on a deterministic JSON predicate.",
                SubmitLabel = "Add decision",
                DefaultInputValues =
                [
                    new CanvasWorkbenchInputValue { Key = "jsonPath", Value = "$.status" },
                    new CanvasWorkbenchInputValue { Key = "expectedValue", Value = "approved" },
                    new CanvasWorkbenchInputValue { Key = "trueLabel", Value = "IF" },
                    new CanvasWorkbenchInputValue { Key = "falseLabel", Value = "ELSE" }
                ],
                InputFields =
                [
                    RouteJsonPathField("Predicate", "$.status"),
                    new CanvasWorkbenchInputField
                    {
                        Key = "expectedValue",
                        SectionKey = "predicate",
                        SectionTitle = "Predicate",
                        SectionDescription = "The IF branch runs when this value matches.",
                        Label = "Expected value",
                        Placeholder = "approved",
                        IsRequired = true
                    },
                    BranchLabelField("trueLabel", "IF branch", "IF"),
                    BranchLabelField("falseLabel", "Else branch", "ELSE")
                ]
            },
            WorkflowDecisionBlockKind.Switch => new CanvasWorkbenchAction
            {
                ActionId = BuildCreateActionId(kind),
                Label = "SWITCH",
                MenuLabel = "SWITCH / DEFAULT",
                Description = "Create switch cases plus a default branch.",
                Icon = "alt_route",
                Tone = "info",
                SetupRendererKey = "workflow-decision-switch",
                RequiresInput = true,
                CreateMode = "dialog",
                ObjectSubtype = kind.ToString(),
                TitlePlaceholder = "SWITCH",
                NotesPlaceholder = "Route based on a JSON discriminator value.",
                SubmitLabel = "Add switch",
                DefaultInputValues =
                [
                    new CanvasWorkbenchInputValue { Key = "jsonPath", Value = "$.category" },
                    new CanvasWorkbenchInputValue { Key = "caseValues", Value = "high, medium, low" },
                    new CanvasWorkbenchInputValue { Key = "defaultLabel", Value = "DEFAULT" }
                ],
                InputFields =
                [
                    RouteJsonPathField("Discriminator", "$.category"),
                    new CanvasWorkbenchInputField
                    {
                        Key = "caseValues",
                        SectionKey = "branches",
                        SectionTitle = "Branches",
                        SectionDescription = "Comma, semicolon, or line-separated switch case values.",
                        Label = "Case values",
                        Placeholder = "high, medium, low",
                        InputMode = "textarea",
                        IsRequired = true
                    },
                    BranchLabelField("defaultLabel", "Default branch", "DEFAULT")
                ]
            },
            WorkflowDecisionBlockKind.FanOut => new CanvasWorkbenchAction
            {
                ActionId = BuildCreateActionId(kind),
                Label = "FAN-OUT",
                MenuLabel = "Fan-out",
                Description = "Create parallel branch selectors for multi-target routing.",
                Icon = "hub",
                Tone = "accent",
                SetupRendererKey = "workflow-decision-fan-out",
                RequiresInput = true,
                CreateMode = "dialog",
                ObjectSubtype = kind.ToString(),
                TitlePlaceholder = "FAN-OUT",
                NotesPlaceholder = "Select one or more downstream branches from an array or text field.",
                SubmitLabel = "Add fan-out",
                DefaultInputValues =
                [
                    new CanvasWorkbenchInputValue { Key = "jsonPath", Value = "$.targets" },
                    new CanvasWorkbenchInputValue { Key = "branchLabels", Value = "validate payment, check inventory, reserve shipment, send confirmation" }
                ],
                InputFields =
                [
                    RouteJsonPathField("Selector path", "$.targets"),
                    new CanvasWorkbenchInputField
                    {
                        Key = "branchLabels",
                        SectionKey = "branches",
                        SectionTitle = "Branches",
                        SectionDescription = "Comma, semicolon, or line-separated branch names.",
                        Label = "Branch labels",
                        Placeholder = "validate payment, check inventory, reserve shipment, send confirmation",
                        InputMode = "textarea",
                        IsRequired = true
                    }
                ]
            },
            _ => throw new InvalidOperationException($"Unsupported decision block kind '{kind}'.")
        };

    public static string BuildCreateActionId(WorkflowDecisionBlockKind kind)
        => $"{CreateDecisionActionPrefix}{kind}";

    public static bool TryParseCreateActionId(string actionId, out WorkflowDecisionBlockKind kind)
    {
        if (actionId.StartsWith(CreateDecisionActionPrefix, StringComparison.Ordinal) &&
            Enum.TryParse(actionId[CreateDecisionActionPrefix.Length..], ignoreCase: false, out kind))
        {
            return true;
        }

        kind = default;
        return false;
    }

    public static string ResolveLabel(WorkflowDecisionBlockKind kind)
        => kind switch
        {
            WorkflowDecisionBlockKind.IfElse => "IF",
            WorkflowDecisionBlockKind.Switch => "SWITCH",
            WorkflowDecisionBlockKind.FanOut => "FAN-OUT",
            _ => kind.ToString()
        };

    private static CanvasWorkbenchInputField RouteJsonPathField(string sectionTitle, string placeholder)
        => new()
        {
            Key = "jsonPath",
            SectionKey = "predicate",
            SectionTitle = sectionTitle,
            SectionDescription = "Routes evaluate against the current workflow payload JSON.",
            Label = "JSON path",
            Placeholder = placeholder,
            IsRequired = true
        };

    private static CanvasWorkbenchInputField BranchLabelField(string key, string label, string placeholder)
        => new()
        {
            Key = key,
            SectionKey = "branches",
            SectionTitle = "Branches",
            SectionDescription = "These labels become visible branch chips on the connector.",
            Label = label,
            Placeholder = placeholder,
            IsRequired = true
        };
}
