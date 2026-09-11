using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Workflows.Abstractions;

public interface IWorkflowMappedProcessSourceAuthority {
    Task<WorkflowLaunchOrigin.ProcessDispatchAssignment> CaptureAsync(WorkflowProcessRunId runId,
        WorkflowProcessAssignmentId assignmentId, Guid claimToken, string contractHash, CancellationToken cancellationToken = default);
    Task<IWorkflowStructureSourceAuthorityLease> AcquireForMutationAsync(WorkflowLaunchOrigin.ProcessDispatchAssignment origin,
        CancellationToken cancellationToken = default);
    Task<IWorkflowMappedProcessContinuationLease> AcquireForContinuationAsync(WorkflowRunSnapshot originalChild,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException("The original Process owner cannot verify this retained Workflow continuation.");
}

public interface IWorkflowMappedProcessContinuationLease : IWorkflowStructureSourceAuthorityLease {
    Guid DatabaseProfileId { get; }
    WorkflowDefinitionSelection OriginalSelection { get; }
    string OriginalInputJson { get; }
}

public interface IWorkflowProcessAssignmentRunQuery {
    Task RequireRetainedChildAsync(WorkflowRunSnapshot child, Guid originalProfileId,
        WorkflowDefinitionSelection originalSelection, string originalInputJson, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("The Workflow owner cannot verify this retained mapped child receipt.");
    Task<IReadOnlyList<WorkflowRunSnapshot>> FindAsync(WorkflowProcessRunId runId, WorkflowProcessAssignmentId assignmentId,
        CancellationToken cancellationToken = default);
}
