using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Workflows.Builder;

public sealed class WorkflowPortBuilder
{
    private readonly WorkflowPortId id;
    private WorkflowPortDirection direction;
    private string name;
    private WorkflowValueShape shape = WorkflowValueShape.Text;
    private bool required = true;

    private WorkflowPortBuilder(string id, WorkflowPortDirection direction)
    {
        this.id = new WorkflowPortId(id);
        this.direction = direction;
        name = this.id.Value;
    }

    public static WorkflowPortBuilder Input(string id) => new(id, WorkflowPortDirection.Input);

    public static WorkflowPortBuilder Output(string id) => new(id, WorkflowPortDirection.Output);

    public WorkflowPortBuilder WithName(string value)
    {
        name = RequireText(value, nameof(value));
        return this;
    }

    public WorkflowPortBuilder WithDirection(WorkflowPortDirection value)
    {
        direction = value;
        return this;
    }

    public WorkflowPortBuilder WithShape(WorkflowValueShape value)
    {
        ArgumentNullException.ThrowIfNull(value);
        shape = value;
        return this;
    }

    public WorkflowPortBuilder Optional()
    {
        required = false;
        return this;
    }

    public WorkflowPort Build() => new(id, name, direction, shape, required);

    private static string RequireText(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{parameterName} cannot be empty.", parameterName);
        }

        return value.Trim();
    }
}
