using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Workflows.Builder;

public static class WorkflowEdgeBuilder
{
    public static WorkflowEdge Direct(
        string id,
        string sourceNodeId,
        string targetNodeId)
        => Create(id, sourceNodeId, targetNodeId, WorkflowEdgeKind.Direct, WorkflowEdgeRouting.Always);

    public static WorkflowEdge Predicate(
        string id,
        string sourceNodeId,
        string targetNodeId,
        string jsonPath,
        WorkflowRouteOperator @operator,
        string expectedValueJson,
        WorkflowRouteValueKind expectedValueKind,
        string label = "")
        => Create(
            id,
            sourceNodeId,
            targetNodeId,
            WorkflowEdgeKind.Conditional,
            WorkflowEdgeRouting.Predicate(jsonPath, @operator, expectedValueJson, expectedValueKind, label));

    public static WorkflowEdge SwitchDefault(
        string id,
        string sourceNodeId,
        string targetNodeId,
        string label = "")
        => Create(
            id,
            sourceNodeId,
            targetNodeId,
            WorkflowEdgeKind.Conditional,
            WorkflowEdgeRouting.SwitchDefault(label));

    public static WorkflowEdge Create(
        string id,
        string sourceNodeId,
        string targetNodeId,
        WorkflowEdgeKind kind,
        WorkflowEdgeRouting routing,
        WorkflowPortId? sourcePortId = null,
        WorkflowPortId? targetPortId = null)
        => new(
            new WorkflowEdgeId(id),
            new WorkflowNodeId(sourceNodeId),
            sourcePortId,
            new WorkflowNodeId(targetNodeId),
            targetPortId,
            kind,
            ConditionExpression: string.Empty)
        {
            Routing = routing
        };
}
