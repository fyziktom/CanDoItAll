namespace CanDoItAll.Modules.Workbench;

public enum ProjectStructureTaskResourceKind
{
    Person,
    Agent,
    Workflow,
    Process
}

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

public sealed record ProjectStructureTaskCreateRequest(
    string Title,
    DateTimeOffset StartUtc,
    DateTimeOffset EndUtc,
    string? AfterTaskNodeId = null,
    ProjectStructureTaskResourceSelection? Resource = null)
{
    public TimeSpan Duration => EndUtc - StartUtc;
}

public sealed record ProjectStructureTaskCreateResult(
    string TaskNodeId,
    string BacklogNodeId,
    ProjectStructureTaskResourceSelection? AttachedResource);

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
