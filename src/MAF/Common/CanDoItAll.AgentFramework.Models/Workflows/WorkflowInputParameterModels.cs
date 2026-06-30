namespace CanDoItAll.AgentFramework.Models;

public enum WorkflowInputParameterKind
{
    Text,
    EmailAddress,
    CrmContactEmail,
    ProjectId,
    ProjectNodeId,
    Category,
    Integer,
    TimeZone,
    DurationMinutes,
    ExternalConnectionId
}

public enum WorkflowInputParameterOptionSourceKind
{
    None,
    Static,
    CrmContacts,
    ProjectStructureProjects,
    ProjectStructureNodes,
    Office365Connections
}

public sealed record WorkflowInputParameterOption(
    string Value,
    string Label,
    string Description);

public sealed record WorkflowInputParameterOptionSource(
    WorkflowInputParameterOptionSourceKind Kind,
    string DependsOnParameterKey,
    IReadOnlyList<WorkflowInputParameterOption> StaticOptions)
{
    public static WorkflowInputParameterOptionSource None { get; } = new(
        WorkflowInputParameterOptionSourceKind.None,
        string.Empty,
        Array.Empty<WorkflowInputParameterOption>());
}

public sealed record WorkflowInputParameterDescriptor(
    string Key,
    string Label,
    WorkflowInputParameterKind Kind,
    bool IsRequired,
    string Description,
    string JsonPath,
    string DefaultValue,
    WorkflowInputParameterOptionSource OptionSource,
    int? MinimumValue,
    int? MaximumValue,
    string Placeholder);
