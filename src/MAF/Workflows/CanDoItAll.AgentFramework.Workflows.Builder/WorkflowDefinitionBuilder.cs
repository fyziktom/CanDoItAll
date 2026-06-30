using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Workflows.Builder;

public sealed class WorkflowDefinitionBuilder
{
    private readonly List<WorkflowNode> nodes = [];
    private readonly List<WorkflowEdge> edges = [];
    private readonly List<WorkflowInputParameterDescriptor> inputParameters = [];
    private WorkflowId id = WorkflowId.New();
    private WorkflowVersionId versionId = WorkflowVersionId.New();
    private WorkflowLifecycleStatus status = WorkflowLifecycleStatus.Draft;
    private WorkflowNodeId? startNodeId;
    private string name;
    private string description = string.Empty;
    private WorkflowRuntimePolicy runtimePolicy = WorkflowSettings.Default.DefaultRuntimePolicy;

    private WorkflowDefinitionBuilder(string name)
    {
        this.name = RequireText(name, nameof(name));
    }

    public static WorkflowDefinitionBuilder Create(string name) => new(name);

    public WorkflowDefinitionBuilder WithId(WorkflowId workflowId)
    {
        id = workflowId;
        return this;
    }

    public WorkflowDefinitionBuilder WithVersionId(WorkflowVersionId workflowVersionId)
    {
        versionId = workflowVersionId;
        return this;
    }

    public WorkflowDefinitionBuilder WithName(string workflowName)
    {
        name = RequireText(workflowName, nameof(workflowName));
        return this;
    }

    public WorkflowDefinitionBuilder WithDescription(string workflowDescription)
    {
        description = string.IsNullOrWhiteSpace(workflowDescription) ? string.Empty : workflowDescription.Trim();
        return this;
    }

    public WorkflowDefinitionBuilder WithStatus(WorkflowLifecycleStatus lifecycleStatus)
    {
        status = lifecycleStatus;
        return this;
    }

    public WorkflowDefinitionBuilder WithRuntimePolicy(WorkflowRuntimePolicy policy)
    {
        runtimePolicy = policy;
        return this;
    }

    public WorkflowDefinitionBuilder WithStartNode(WorkflowNodeId nodeId)
    {
        startNodeId = nodeId;
        return this;
    }

    public WorkflowDefinitionBuilder AddNode(WorkflowNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        nodes.Add(node);
        if (node.Kind == WorkflowNodeKind.Start && startNodeId is null)
        {
            startNodeId = node.Id;
        }

        return this;
    }

    public WorkflowDefinitionBuilder AddEdge(WorkflowEdge edge)
    {
        ArgumentNullException.ThrowIfNull(edge);
        edges.Add(edge);
        return this;
    }

    public WorkflowDefinitionBuilder AddInputParameter(WorkflowInputParameterDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        inputParameters.Add(descriptor);
        return this;
    }

    public WorkflowDefinition Build(DateTimeOffset? timestamp = null)
    {
        if (startNodeId is null)
        {
            throw new InvalidOperationException("A workflow start node must be declared explicitly.");
        }

        if (!nodes.Any(node => node.Id == startNodeId))
        {
            throw new InvalidOperationException($"The start node '{startNodeId}' is not present in the workflow graph.");
        }

        return BuildUnchecked(timestamp);
    }

    public WorkflowDefinition BuildUnchecked(DateTimeOffset? timestamp = null)
    {
        var effectiveTimestamp = timestamp ?? DateTimeOffset.UtcNow;
        var graph = new WorkflowGraph(
            startNodeId ?? new WorkflowNodeId("__missing-start__"),
            nodes.ToArray(),
            edges.ToArray());

        return new WorkflowDefinition(
            id,
            versionId,
            name,
            description,
            status,
            graph,
            runtimePolicy,
            effectiveTimestamp,
            effectiveTimestamp)
        {
            InputParameters = inputParameters.ToArray()
        };
    }

    private static string RequireText(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{parameterName} cannot be empty.", parameterName);
        }

        return value.Trim();
    }
}
