using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Workflows.Builder;

public sealed class WorkflowInputParameterBuilder
{
    private readonly string key;
    private string label;
    private WorkflowInputParameterKind kind = WorkflowInputParameterKind.Text;
    private bool isRequired = true;
    private string description = string.Empty;
    private string jsonPath;
    private string defaultValue = string.Empty;
    private WorkflowInputParameterOptionSource optionSource = WorkflowInputParameterOptionSource.None;
    private int? minimumValue;
    private int? maximumValue;
    private string placeholder = string.Empty;

    private WorkflowInputParameterBuilder(string key)
    {
        this.key = RequireText(key, nameof(key));
        label = this.key;
        jsonPath = $"$.{this.key}";
    }

    public static WorkflowInputParameterBuilder Create(string key) => new(key);

    public WorkflowInputParameterBuilder WithLabel(string value)
    {
        label = RequireText(value, nameof(value));
        return this;
    }

    public WorkflowInputParameterBuilder WithKind(WorkflowInputParameterKind value)
    {
        kind = value;
        return this;
    }

    public WorkflowInputParameterBuilder Optional()
    {
        isRequired = false;
        return this;
    }

    public WorkflowInputParameterBuilder WithDescription(string value)
    {
        description = string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        return this;
    }

    public WorkflowInputParameterBuilder WithJsonPath(string value)
    {
        jsonPath = RequireText(value, nameof(value));
        return this;
    }

    public WorkflowInputParameterBuilder WithDefaultValue(string value)
    {
        defaultValue = string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        return this;
    }

    public WorkflowInputParameterBuilder WithOptionSource(WorkflowInputParameterOptionSource value)
    {
        optionSource = value;
        return this;
    }

    public WorkflowInputParameterBuilder WithRange(int? minimum, int? maximum)
    {
        minimumValue = minimum;
        maximumValue = maximum;
        return this;
    }

    public WorkflowInputParameterBuilder WithPlaceholder(string value)
    {
        placeholder = string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        return this;
    }

    public WorkflowInputParameterDescriptor Build()
        => new(
            key,
            label,
            kind,
            isRequired,
            description,
            jsonPath,
            defaultValue,
            optionSource,
            minimumValue,
            maximumValue,
            placeholder);

    private static string RequireText(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{parameterName} cannot be empty.", parameterName);
        }

        return value.Trim();
    }
}
