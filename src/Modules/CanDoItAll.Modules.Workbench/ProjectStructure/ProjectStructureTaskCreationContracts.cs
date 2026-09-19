using CanDoItAll.Modules.Projects;

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

/// <summary>
/// Task creation request: a canonical task with a title, a planned interval and optionally an estimate, a resource and
/// its position in the Gantt row order. The task is created in the project's main backlog block.
/// </summary>
/// <param name="Title">Task title; required, trimmed, at most 200 characters.</param>
/// <param name="StartUtc">
/// Planned start as an instant with offset; required. Stored in UTC.
/// </param>
/// <param name="EndUtc">
/// Planned end as an instant with offset; required and later than <c>startUtc</c>. Stored in UTC.
/// </param>
/// <param name="AfterTaskNodeId">
/// Identifier of the canonical task after which the new task is placed in the Gantt row order; null or blank appends
/// it at the end. An identifier that is not a canonical task of the project makes the creation fail and be undone
/// (HTTP 409 <c>RowOrderingFailed</c>).
/// </param>
/// <param name="Resource">
/// Optional resource to attach to the new task: a person or agent as direct assignee, or a workflow (with its exact
/// <c>versionId</c>) or process definition. Null or omitted creates the task without a resource.
/// </param>
/// <param name="Estimate">
/// Optional estimate; null or omitted starts with an empty estimate. With a resource, the owner prices the task from
/// the resource and replaces the cost amount and currency when a price is available.
/// </param>
public sealed record ProjectStructureTaskCreateRequest(
    string Title,
    DateTimeOffset StartUtc,
    DateTimeOffset EndUtc,
    string? AfterTaskNodeId = null,
    ProjectStructureTaskResourceSelection? Resource = null,
    ProjectTaskEstimate? Estimate = null)
{
    /// <summary>
    /// Project write admission returned as <c>expectedProjectAdmission</c> by the structure read, sent back unchanged.
    /// Required: when it is omitted, null or names another project, the request is rejected with HTTP 409
    /// <c>ProjectLifetimeRefreshRequired</c> and nothing is created.
    /// </summary>
    public ProjectWriteAdmission? ExpectedProjectAdmission { get; init; }

    /// <summary>
    /// Planned duration computed from <c>startUtc</c> and <c>endUtc</c>, as a time span such as <c>"2.08:00:00"</c>.
    /// Read-only: a value sent in the request is ignored.
    /// </summary>
    public TimeSpan Duration => EndUtc - StartUtc;
}

/// <summary>Result of a committed task creation.</summary>
/// <param name="TaskNodeId">
/// Identifier of the new canonical task, for example <c>custom:3f2504e04f8911d39a0c0305e82c3301</c>; use it with
/// <c>PUT /api/project-structure/projects/{projectId}/tasks/{taskId}</c>.
/// </param>
/// <param name="BacklogNodeId">Identifier of the main backlog block that contains the task.</param>
/// <param name="AttachedResource">The resource attached to the task; null when none was requested.</param>
/// <param name="Pricing">How the task's estimate was priced when it was created.</param>
public sealed record ProjectStructureTaskCreateResult(
    string TaskNodeId,
    string BacklogNodeId,
    ProjectStructureTaskResourceSelection? AttachedResource,
    ProjectStructureTaskEstimateRefreshResult Pricing);

public enum ProjectStructureTaskCreationFailureStage
{
    ResourceAttachment,
    RowOrdering
}

public enum ProjectStructureTaskCreationErrorCode
{
    ResourceAttachmentFailed,
    RowOrderingFailed,
    ResourceAttachmentCompensationFailed,
    RowOrderingCompensationFailed,
    CreationFailed
}

public sealed class ProjectStructureTaskCreationException : Exception
{
    public ProjectStructureTaskCreationException(
        ProjectStructureTaskCreationFailureStage stage,
        string taskNodeId,
        bool compensationSucceeded,
        Exception failure,
        Exception? compensationFailure = null)
        : base(
            BuildMessage(stage, taskNodeId, compensationSucceeded),
            compensationFailure is null ? failure : new AggregateException(failure, compensationFailure))
    {
        Stage = stage;
        TaskNodeId = taskNodeId;
        CompensationSucceeded = compensationSucceeded;
        CompensationFailure = compensationFailure;
        Code = (stage, compensationSucceeded) switch
        {
            (ProjectStructureTaskCreationFailureStage.ResourceAttachment, true) => ProjectStructureTaskCreationErrorCode.ResourceAttachmentFailed,
            (ProjectStructureTaskCreationFailureStage.RowOrdering, true) => ProjectStructureTaskCreationErrorCode.RowOrderingFailed,
            (ProjectStructureTaskCreationFailureStage.ResourceAttachment, false) => ProjectStructureTaskCreationErrorCode.ResourceAttachmentCompensationFailed,
            (ProjectStructureTaskCreationFailureStage.RowOrdering, false) => ProjectStructureTaskCreationErrorCode.RowOrderingCompensationFailed,
            _ => ProjectStructureTaskCreationErrorCode.CreationFailed
        };
    }

    public ProjectStructureTaskCreationFailureStage Stage { get; }

    public ProjectStructureTaskCreationErrorCode Code { get; }

    public string TaskNodeId { get; }

    public bool CompensationSucceeded { get; }

    public Exception? CompensationFailure { get; }

    private static string BuildMessage(
        ProjectStructureTaskCreationFailureStage stage,
        string taskNodeId,
        bool compensationSucceeded)
    {
        var operation = stage switch
        {
            ProjectStructureTaskCreationFailureStage.ResourceAttachment => "resource attachment",
            ProjectStructureTaskCreationFailureStage.RowOrdering => "Gantt row ordering",
            _ => "task creation"
        };
        var compensation = compensationSucceeded
            ? "The partially created task was removed."
            : "The partially created task could not be removed and requires attention.";
        return $"Task '{taskNodeId}' failed during {operation}. {compensation}";
    }
}
