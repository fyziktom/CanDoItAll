namespace CanDoItAll.Processes.Application;

public sealed class ProcessAuthoringProjectUnavailableException(Guid projectId)
    : InvalidOperationException($"The authoring project {projectId:D} no longer exists.") {
    public Guid ProjectId { get; } = projectId;
}
