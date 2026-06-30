using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Builder;

namespace CanDoItAll.AgentFramework.Workflows.Templates;

internal static class WorkflowTemplateGraphMaterializer
{
    private static readonly WorkflowPortId WorkflowInputPortId = new("workflow:input");
    private static readonly WorkflowPortId WorkflowOutputPortId = new("workflow:output");

    public static WorkflowGraph CreateGraph(
        WorkflowTemplatePack pack,
        WorkflowTemplateDefinition template,
        WorkflowComponentId componentId)
    {
        ArgumentNullException.ThrowIfNull(pack);
        ArgumentNullException.ThrowIfNull(template);

        var context = WorkflowTemplatePack.CreateContext(template);
        if (template.Graph.Nodes.Count == 0)
        {
            throw WorkflowTemplateDiagnostics.CreateException(
                WorkflowTemplateFailureKind.GraphMaterializationFailed,
                "Workflow template must define at least one node.",
                context.WithYamlPath("graph.nodes"),
                "Add graph.nodes with at least a start node and an end node.");
        }

        return new WorkflowGraph(
            new WorkflowNodeId(WorkflowTemplateDiagnostics.Require(
                template.Graph.StartNodeId,
                "graph.startNodeId",
                context.WithYamlPath("graph.startNodeId"),
                "Set graph.startNodeId to the id of an existing start node.")),
            template.Graph.Nodes
                .Select((node, index) => CreateNode(pack, node, componentId, context.WithYamlPath($"graph.nodes[{index}]")))
                .ToArray(),
            template.Graph.Edges
                .Select((edge, index) => CreateEdge(edge, context.WithYamlPath($"graph.edges[{index}]")))
                .ToArray());
    }

    public static WorkflowDefinition CreateDefinition(
        WorkflowTemplatePack pack,
        WorkflowTemplateDefinition template,
        LlmCallComponent component)
    {
        var graph = CreateGraph(pack, template, component.Id);
        var builder = WorkflowDefinitionBuilder
            .Create(string.IsNullOrWhiteSpace(template.Name) ? template.Key : template.Name)
            .WithDescription(template.Description)
            .WithStatus(WorkflowLifecycleStatus.Active)
            .WithRuntimePolicy(pack.RuntimePolicy)
            .WithStartNode(graph.StartNodeId);

        foreach (var inputParameter in pack.CreateInputParameters(template))
        {
            builder.AddInputParameter(inputParameter);
        }

        foreach (var node in graph.Nodes)
        {
            builder.AddNode(node);
        }

        foreach (var edge in graph.Edges)
        {
            builder.AddEdge(edge);
        }

        return builder.Build();
    }

    private static WorkflowNode CreateNode(
        WorkflowTemplatePack pack,
        WorkflowTemplateNode node,
        WorkflowComponentId componentId,
        WorkflowTemplateContext context)
    {
        var nodeId = WorkflowTemplateDiagnostics.Require(
            node.Id,
            "node.id",
            context.WithYamlPath($"{context.YamlPath}.id"),
            "Set each graph.nodes[].id to a stable non-empty node id.");
        var nodeContext = context.WithNode(nodeId, context.YamlPath);
        var kind = WorkflowTemplateModelMaterializer.ParseEnum<WorkflowNodeKind>(
            node.Kind,
            $"node '{nodeId}' kind",
            nodeContext.WithYamlPath($"{context.YamlPath}.kind"),
            WorkflowTemplateFailureKind.GraphMaterializationFailed);
        var instructions = string.IsNullOrWhiteSpace(node.Instructions)
            ? ResolveDefaultInstruction(kind, pack.Manifest.NodeInstructionDefaults)
            : node.Instructions.Trim();

        var builder = WorkflowNodeBuilder
            .For(nodeId, kind)
            .WithName(WorkflowTemplateDiagnostics.Require(
                node.Name,
                $"node '{nodeId}' name",
                nodeContext.WithYamlPath($"{context.YamlPath}.name"),
                "Set each graph.nodes[].name to a readable node label."))
            .WithInstructions(instructions)
            .WithInputShape(kind == WorkflowNodeKind.Start ? null : pack.JsonShape)
            .WithResultShape(kind == WorkflowNodeKind.End ? null : pack.JsonShape)
            .At(node.X, node.Y);

        foreach (var port in BuildPorts(kind, pack.JsonShape))
        {
            builder.AddPort(port);
        }

        if (kind == WorkflowNodeKind.LlmCall)
        {
            builder.WithComponent(componentId);
        }

        if (!string.IsNullOrWhiteSpace(node.ExternalRequestKind))
        {
            builder.WithExternalRequestKind(WorkflowTemplateModelMaterializer.ParseEnum<WorkflowExternalRequestKind>(
                node.ExternalRequestKind,
                $"node '{nodeId}' externalRequestKind",
                nodeContext.WithYamlPath($"{context.YamlPath}.externalRequestKind"),
                WorkflowTemplateFailureKind.GraphMaterializationFailed));
        }

        if (node.Executor is null)
        {
            return builder.Build();
        }

        var executorId = WorkflowTemplateDiagnostics.Require(
            node.Executor.Id,
            $"node '{nodeId}' executor.id",
            nodeContext.WithYamlPath($"{context.YamlPath}.executor.id"),
            "Set executor.id to a registered workflow executor id.");
        var executorContext = nodeContext.WithExecutor(
            nodeId,
            executorId,
            $"{context.YamlPath}.executor.id");
        var settingsJson = WorkflowTemplateModelMaterializer.SerializeSettings(node.Executor.Settings);
        var builderSettingsJson = string.IsNullOrWhiteSpace(settingsJson) ? "{}" : settingsJson;
        var built = builder
            .WithExecutor(
                new WorkflowExecutorId(executorId),
                builderSettingsJson,
                ResolveExecutionPolicy(node.Executor.Policy, pack.Manifest.ExecutorPolicies, executorContext))
            .Build();

        return string.Equals(settingsJson, builderSettingsJson, StringComparison.Ordinal)
            ? built
            : built with
            {
                Settings = built.Settings with
                {
                    ExecutorSettingsJson = settingsJson
                }
            };
    }

    private static WorkflowExecutorExecutionPolicy ResolveExecutionPolicy(
        string policy,
        IReadOnlyDictionary<string, WorkflowTemplateExecutionPolicy> executorPolicies,
        WorkflowTemplateContext context)
    {
        var policyKey = string.IsNullOrWhiteSpace(policy) ? "slow" : policy.Trim();
        if (!executorPolicies.TryGetValue(policyKey, out var templatePolicy))
        {
            throw WorkflowTemplateDiagnostics.CreateException(
                WorkflowTemplateFailureKind.GraphMaterializationFailed,
                $"Workflow template node references unknown executor policy '{policyKey}'.",
                context.WithYamlPath($"{context.YamlPath}.policy"),
                "Add the policy under manifest executorPolicies or change the node executor.policy to a known key.");
        }

        return WorkflowTemplateModelMaterializer.CreateExecutionPolicy(
            templatePolicy,
            context.WithYamlPath($"executorPolicies.{policyKey}"));
    }

    private static WorkflowEdge CreateEdge(
        WorkflowTemplateEdge edge,
        WorkflowTemplateContext context)
    {
        var source = WorkflowTemplateDiagnostics.Require(
            edge.Source,
            "edge.source",
            context.WithYamlPath($"{context.YamlPath}.source"),
            "Set graph.edges[].source to an existing source node id.");
        var target = WorkflowTemplateDiagnostics.Require(
            edge.Target,
            "edge.target",
            context.WithYamlPath($"{context.YamlPath}.target"),
            "Set graph.edges[].target to an existing target node id.");
        var id = string.IsNullOrWhiteSpace(edge.Id)
            ? $"{source}-to-{target}"
            : edge.Id.Trim();
        var kind = string.IsNullOrWhiteSpace(edge.Kind)
            ? WorkflowEdgeKind.Direct
            : WorkflowTemplateModelMaterializer.ParseEnum<WorkflowEdgeKind>(
                edge.Kind,
                $"edge '{id}' kind",
                context.WithYamlPath($"{context.YamlPath}.kind"),
                WorkflowTemplateFailureKind.GraphMaterializationFailed);

        return WorkflowEdgeBuilder.Create(
            id,
            source,
            target,
            kind,
            edge.Routing?.ToModel(context.WithYamlPath($"{context.YamlPath}.routing"), id) ?? WorkflowEdgeRouting.Always,
            WorkflowOutputPortId,
            WorkflowInputPortId);
    }

    private static WorkflowEdgeRouting ToModel(
        this WorkflowTemplateRouting routing,
        WorkflowTemplateContext context,
        string edgeId)
    {
        var kind = WorkflowTemplateModelMaterializer.ParseEnum<WorkflowRouteKind>(
            routing.Kind,
            $"edge '{edgeId}' routing.kind",
            context.WithYamlPath($"{context.YamlPath}.kind"),
            WorkflowTemplateFailureKind.GraphMaterializationFailed);
        var valueKind = string.IsNullOrWhiteSpace(routing.ExpectedValueKind)
            ? WorkflowRouteValueKind.Json
            : WorkflowTemplateModelMaterializer.ParseEnum<WorkflowRouteValueKind>(
                routing.ExpectedValueKind,
                $"edge '{edgeId}' routing.expectedValueKind",
                context.WithYamlPath($"{context.YamlPath}.expectedValueKind"),
                WorkflowTemplateFailureKind.GraphMaterializationFailed);

        return new WorkflowEdgeRouting(
            kind,
            routing.Label.Trim(),
            routing.JsonPath.Trim(),
            string.IsNullOrWhiteSpace(routing.Operator)
                ? WorkflowRouteOperator.Exists
                : WorkflowTemplateModelMaterializer.ParseEnum<WorkflowRouteOperator>(
                    routing.Operator,
                    $"edge '{edgeId}' routing.operator",
                    context.WithYamlPath($"{context.YamlPath}.operator"),
                    WorkflowTemplateFailureKind.GraphMaterializationFailed),
            ResolveExpectedValueJson(routing, valueKind),
            valueKind,
            routing.CaseSensitive,
            routing.FanOutTargetIndex,
            WorkflowRoutingLanguages.BuiltInJsonV1);
    }

    private static string ResolveExpectedValueJson(
        WorkflowTemplateRouting routing,
        WorkflowRouteValueKind valueKind)
    {
        if (!string.IsNullOrWhiteSpace(routing.ExpectedValueJson))
        {
            return routing.ExpectedValueJson.Trim();
        }

        if (string.IsNullOrWhiteSpace(routing.ExpectedValue))
        {
            return string.Empty;
        }

        return valueKind == WorkflowRouteValueKind.String
            ? JsonSerializer.Serialize(routing.ExpectedValue)
            : routing.ExpectedValue.Trim();
    }

    private static string ResolveDefaultInstruction(
        WorkflowNodeKind kind,
        IReadOnlyDictionary<string, string> defaults)
        => defaults.TryGetValue(kind.ToString(), out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : string.Empty;

    private static IReadOnlyList<WorkflowPort> BuildPorts(
        WorkflowNodeKind kind,
        WorkflowValueShape jsonShape)
    {
        var ports = new List<WorkflowPort>();
        if (kind != WorkflowNodeKind.Start)
        {
            ports.Add(WorkflowPortBuilder
                .Input(WorkflowInputPortId.Value)
                .WithName("Input")
                .WithShape(jsonShape)
                .Build());
        }

        if (kind != WorkflowNodeKind.End)
        {
            ports.Add(WorkflowPortBuilder
                .Output(WorkflowOutputPortId.Value)
                .WithName("Output")
                .WithShape(jsonShape)
                .Build());
        }

        return ports;
    }
}
