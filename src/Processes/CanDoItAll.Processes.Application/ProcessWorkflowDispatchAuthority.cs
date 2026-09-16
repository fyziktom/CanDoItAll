using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Drivers.Abstractions;
using CanDoItAll.Processes.Contracts;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Processes.Application;

public sealed record ProcessWorkflowDispatchRequest(ProcessRunId RunId, ProcessStepInstanceId StepInstanceId,
    ProcessDispatchClaimIdentity Claim, string ContractHash);

public sealed record ProcessWorkflowDispatchAuthority(ProcessWorkflowDispatchRequest Request, ProcessRunId RootRunId,
    ProcessRuntimeStepAssignment Assignment, string OwnerFingerprint, ProcessLaunchAuthority? SourceAuthority,
    bool IsCurrent, DateTimeOffset ObservedAtUtc);

public sealed record ProcessWorkflowContinuationRequest(ProcessRunId RunId, ProcessStepInstanceId StepInstanceId,
    ProcessWorkflowRunId ChildRunId, ProcessDispatchClaimIdentity? OriginalClaim = null, string? OriginalContractHash = null);

public sealed record ProcessWorkflowContinuationAuthority(ProcessWorkflowContinuationRequest Request,
    ProcessWorkflowDispatchAuthority Dispatch, long OriginalClaimSequence, ProcessProjectAdmission? ProjectAdmission);

public interface IProcessWorkflowDispatchAuthorityReader {
    Task<ProcessWorkflowDispatchAuthority> ReadAsync(ProcessWorkflowDispatchRequest request, CancellationToken cancellationToken = default);
    Task<ProcessWorkflowContinuationAuthority> ReadContinuationAsync(ProcessWorkflowContinuationRequest request,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException("The Process owner does not support retained mapped Workflow continuation evidence.");
}

public interface IProcessWorkflowDispatchMutationGuard {
    Task RequireForMutationAsync(ProcessWorkflowDispatchAuthority expected, CancellationToken cancellationToken = default);
    Task RequireForContinuationAsync(ProcessWorkflowContinuationAuthority expected, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("The Process owner does not support retained mapped Workflow continuation fencing.");
}
