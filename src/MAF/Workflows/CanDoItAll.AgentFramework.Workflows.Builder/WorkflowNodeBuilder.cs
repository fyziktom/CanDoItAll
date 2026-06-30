using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Workflows.Builder;

public sealed class WorkflowNodeBuilder
{
    private readonly WorkflowNodeId id;
    private readonly WorkflowNodeKind kind;
    private readonly List<WorkflowPort> ports = [];
    private string name;
    private WorkflowComponentId? componentId;
    private Guid? agentId;
    private WorkflowId? subworkflowId;
    private WorkflowExternalRequestKind? externalRequestKind;
    private string instructions = string.Empty;
    private WorkflowValueShape? inputShape = WorkflowValueShape.Text;
    private WorkflowValueShape? resultShape = WorkflowValueShape.Text;
    private WorkflowExecutorId? executorId;
    private string executorSettingsJson = string.Empty;
    private WorkflowExecutorExecutionPolicy? executionPolicy;
    private double canvasX;
    private double canvasY;

    private WorkflowNodeBuilder(string id, WorkflowNodeKind kind)
    {
        this.id = new WorkflowNodeId(id);
        this.kind = kind;
        name = this.id.Value;
    }

    public static WorkflowNode Start(string id = "start") => For(id, WorkflowNodeKind.Start).Build();

    public static WorkflowNode End(string id = "end") => For(id, WorkflowNodeKind.End).Build();

    public static WorkflowNode Llm(
        string id,
        WorkflowComponentId componentId)
        => For(id, WorkflowNodeKind.LlmCall)
            .WithComponent(componentId)
            .Build();

    public static WorkflowNode Executor(
        string id,
        WorkflowExecutorId executorId,
        string settingsJson = "{}",
        WorkflowValueShape? inputShape = null,
        WorkflowValueShape? resultShape = null)
        => For(id, WorkflowNodeKind.Executor)
            .WithExecutor(executorId, settingsJson)
            .WithInputShape(inputShape ?? WorkflowValueShape.Text)
            .WithResultShape(resultShape ?? WorkflowValueShape.Text)
            .Build();

    public static WorkflowNode HumanInput(
        string id,
        WorkflowExternalRequestKind requestKind = WorkflowExternalRequestKind.HumanInput)
        => For(id, WorkflowNodeKind.HumanInput)
            .WithExternalRequestKind(requestKind)
            .Build();

    public static WorkflowNodeBuilder For(string id, WorkflowNodeKind kind) => new(id, kind);

    public WorkflowNodeBuilder WithName(string value)
    {
        name = RequireText(value, nameof(value));
        return this;
    }

    public WorkflowNodeBuilder WithComponent(WorkflowComponentId value)
    {
        componentId = value;
        return this;
    }

    public WorkflowNodeBuilder WithAgent(Guid value)
    {
        agentId = value;
        return this;
    }

    public WorkflowNodeBuilder WithSubworkflow(WorkflowId value)
    {
        subworkflowId = value;
        return this;
    }

    public WorkflowNodeBuilder WithExternalRequestKind(WorkflowExternalRequestKind value)
    {
        externalRequestKind = value;
        return this;
    }

    public WorkflowNodeBuilder WithInstructions(string value)
    {
        instructions = string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        return this;
    }

    public WorkflowNodeBuilder WithInputShape(WorkflowValueShape? value)
    {
        inputShape = value;
        return this;
    }

    public WorkflowNodeBuilder WithResultShape(WorkflowValueShape? value)
    {
        resultShape = value;
        return this;
    }

    public WorkflowNodeBuilder WithExecutor(
        WorkflowExecutorId value,
        string settingsJson = "{}",
        WorkflowExecutorExecutionPolicy? policy = null)
    {
        executorId = value;
        executorSettingsJson = RequireText(settingsJson, nameof(settingsJson));
        executionPolicy = policy;
        return this;
    }

    public WorkflowNodeBuilder AddPort(WorkflowPort port)
    {
        ArgumentNullException.ThrowIfNull(port);
        ports.Add(port);
        return this;
    }

    public WorkflowNodeBuilder At(double x, double y)
    {
        canvasX = x;
        canvasY = y;
        return this;
    }

    public WorkflowNode Build()
    {
        if (kind == WorkflowNodeKind.Executor && executorId is null)
        {
            throw new InvalidOperationException("Executor nodes must declare an executor id.");
        }

        var settings = new WorkflowNodeSettings(
            componentId,
            agentId,
            subworkflowId,
            externalRequestKind,
            instructions,
            inputShape,
            resultShape)
        {
            ExecutorId = executorId,
            ExecutorSettingsJson = executorSettingsJson,
            ExecutionPolicy = executionPolicy
        };

        return new WorkflowNode(id, kind, name, ports.ToArray(), settings, canvasX, canvasY);
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
