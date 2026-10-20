using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Contracts;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Processes.Application;

public sealed record ProcessExecutionDispatchAuthority(
    ProcessExecutionClaimEvidence Evidence,
    ProcessRunId RootRunId,
    Guid? ProjectId,
    string OwnerFingerprint,
    string ReadinessHash,
    IReadOnlyList<string> AllowedOperations,
    string OperationTargetScope,
    ProcessCapabilityScope CapabilityScope,
    ProcessLaunchAuthority? SourceAuthority,
    ProcessExecutionProjectAuthority? ProjectReference,
    bool ObservedCurrentDispatch,
    DateTimeOffset ObservedAtUtc);

public sealed record ProcessExecutionDispatchAuthorityResult(
    ProcessExecutionAuthorityDisposition Disposition,
    ProcessExecutionDispatchAuthority? Snapshot = null) {
    public DecisionDataPolicy? OriginalDecisionPolicy { get; init; }
}

public sealed record ProcessSourceAuthorityObservation(bool ReadAllowed, bool DispatchAllowed);

public interface IProcessSourceAuthorityObservationPolicy {
    Task<ProcessSourceAuthorityObservation> ObserveAsync(ProcessLaunchAuthority authority,
        CancellationToken cancellationToken = default);
}

public interface IProcessExecutionDispatchAuthorityReader {
    Task<ProcessExecutionDispatchAuthorityResult> ReadAsync(Guid executionRunId, CancellationToken cancellationToken = default);

    Task<ProcessExecutionDispatchAuthorityResult> ObserveOriginalForReceiptReadAsync(ProcessExecutionClaimEvidence original,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException("This Process owner does not provide original-evidence receipt observations.");
}

public interface IProcessExecutionMutationGuard {
    Task RequireForMutationAsync(ProcessExecutionDispatchAuthority expected, CancellationToken cancellationToken = default);
    Task RequireDispatchAsync(ProcessExecutionDispatchAuthority expected, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("This Process owner does not provide dispatch-only transaction fencing.");
}
