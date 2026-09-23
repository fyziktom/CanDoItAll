namespace CanDoItAll.AgentFramework.Models;

public sealed class WorkflowUsageCancellationException(
    OperationCanceledException innerException, IReadOnlyList<WorkflowUsageObservation> observations)
    : OperationCanceledException(innerException.Message,
        new WorkflowUsageObservationException(innerException.Message, innerException, observations),
        innerException.CancellationToken) {
    public IReadOnlyList<WorkflowUsageObservation> Observations { get; } = observations;
}
