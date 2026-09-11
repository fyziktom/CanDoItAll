using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Workflows.Abstractions;

public sealed class WorkflowLaunchAdmissionObservationException(
    WorkflowRunId reservedRunId, Exception launchException, Exception observationException)
    : InvalidOperationException("The workflow admission outcome could not be observed. Query the reserved run before retrying this launch.", observationException) {
    public WorkflowRunId ReservedRunId { get; } = reservedRunId;
    public Exception LaunchException { get; } = launchException;
}
