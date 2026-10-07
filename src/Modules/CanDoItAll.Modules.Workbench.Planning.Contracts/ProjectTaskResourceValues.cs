namespace CanDoItAll.Modules.Workbench;

public static class ProjectStructureTaskActionIds
{
    public const string Create = "add-work-task";
    public const string CreateMode = "project-task-dialog";
}

/// <summary>
/// Kind of resource selected for a task, as a JSON integer: 0 Person (a CRM/HR person party), 1 Agent (an AI agent
/// party), 2 Workflow (a workflow definition), 3 Process (a process definition). Only Person and Agent can be direct
/// task assignees.
/// </summary>
public enum ProjectStructureTaskResourceKind
{
    Person,
    Agent,
    Workflow,
    Process
}

/// <summary>
/// Resource selected for a task: a person or agent that works on it, or a workflow or process definition that
/// executes it.
/// </summary>
/// <param name="Kind">
/// Kind of the selected resource, as a JSON integer: 0 Person, 1 Agent, 2 Workflow, 3 Process. Each operation states
/// which kinds it accepts; a direct task assignee must be a Person or an Agent.
/// </param>
/// <param name="ResourceId">
/// Identifier of the selected resource: the party identifier of a person or agent, or the definition identifier of
/// a workflow or process. Must not be the empty GUID.
/// </param>
/// <param name="VersionId">
/// Exact workflow version for a Workflow selection; must be null or omitted for every other kind.
/// </param>
public sealed record ProjectStructureTaskResourceSelection(
    ProjectStructureTaskResourceKind Kind,
    Guid ResourceId,
    Guid? VersionId = null);

public sealed record ProjectStructureTaskResourceOption(
    ProjectStructureTaskResourceKind Kind,
    Guid ResourceId,
    Guid? VersionId,
    string DisplayName,
    string TypeLabel,
    string Description,
    bool IsFavorite,
    bool IsSensitive);
