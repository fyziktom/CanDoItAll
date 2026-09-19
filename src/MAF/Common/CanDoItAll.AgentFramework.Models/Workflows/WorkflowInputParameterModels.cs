namespace CanDoItAll.AgentFramework.Models;

/// <summary>
/// Kind of value a workflow input parameter takes, as a JSON integer: 0 Text, 1 EmailAddress, 2 CrmContactEmail
/// (e-mail address of a CRM contact), 3 ProjectId (project GUID as a string), 4 ProjectNodeId (Project Structure node
/// identifier), 5 Category, 6 Integer, 7 TimeZone (time zone identifier), 8 DurationMinutes (integer number of
/// minutes), 9 ExternalConnectionId.
/// </summary>
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

/// <summary>
/// Where the selectable values of a workflow input parameter come from, as a JSON integer: 0 None (free entry),
/// 1 Static (the parameter's <c>staticOptions</c>), 2 CrmContacts, 3 ProjectStructureProjects,
/// 4 ProjectStructureNodes, 5 Office365Connections.
/// </summary>
public enum WorkflowInputParameterOptionSourceKind
{
    None,
    Static,
    CrmContacts,
    ProjectStructureProjects,
    ProjectStructureNodes,
    Office365Connections
}

/// <summary>
/// One selectable value of a workflow input parameter.
/// </summary>
/// <param name="Value">Value written into the run input when the option is chosen.</param>
/// <param name="Label">Display label of the option.</param>
/// <param name="Description">Longer description of the option; may be empty.</param>
public sealed record WorkflowInputParameterOption(
    string Value,
    string Label,
    string Description);

/// <summary>
/// Source of the selectable values of a workflow input parameter.
/// </summary>
/// <param name="Kind">
/// Where the values come from, as a JSON integer: 0 None, 1 Static, 2 CrmContacts, 3 ProjectStructureProjects,
/// 4 ProjectStructureNodes, 5 Office365Connections.
/// </param>
/// <param name="DependsOnParameterKey">
/// Key of another parameter whose value narrows these options, for example the project parameter for node options;
/// empty for none.
/// </param>
/// <param name="StaticOptions">The options of a Static source; empty for the other kinds.</param>
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

/// <summary>
/// Declared input parameter of a workflow definition: one member of the run input with its kind, requirement, default
/// and options. Start forms and scheduled runs use these declarations to build and check the run input; the HTTP start
/// operations pass the input through without applying defaults or checking it.
/// </summary>
/// <param name="Key">
/// Key of the parameter; unless <c>jsonPath</c> is set, the value is the run input's root member with this name.
/// </param>
/// <param name="Label">Display label.</param>
/// <param name="Kind">
/// Kind of value, as a JSON integer: 0 Text, 1 EmailAddress, 2 CrmContactEmail, 3 ProjectId, 4 ProjectNodeId,
/// 5 Category, 6 Integer, 7 TimeZone, 8 DurationMinutes, 9 ExternalConnectionId.
/// </param>
/// <param name="IsRequired">True when the run input must contain a value after defaults are applied.</param>
/// <param name="Description">Explanation shown with the parameter; may be empty.</param>
/// <param name="JsonPath">
/// Location of the value in the run input as <c>$.name</c> of a root member, for example <c>$.customerEmail</c>; empty
/// means <c>$.</c> followed by <c>key</c>.
/// </param>
/// <param name="DefaultValue">
/// Default used when the input has no value, as text (an integer for Integer and DurationMinutes); empty for none.
/// </param>
/// <param name="OptionSource">Where selectable values come from.</param>
/// <param name="MinimumValue">Inclusive lower bound for Integer and DurationMinutes values; null for none.</param>
/// <param name="MaximumValue">Inclusive upper bound for Integer and DurationMinutes values; null for none.</param>
/// <param name="Placeholder">Hint text for input forms; may be empty.</param>
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
