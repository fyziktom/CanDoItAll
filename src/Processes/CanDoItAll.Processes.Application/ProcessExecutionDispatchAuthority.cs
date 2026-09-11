using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Contracts;
using CanDoItAll.Processes.Runtime;

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
    ProcessExecutionDispatchAuthority? Snapshot = null);

public sealed record ProcessSourceAuthorityObservation(bool ReadAllowed, bool DispatchAllowed);

public interface IProcessSourceAuthorityObservationPolicy {
    Task<ProcessSourceAuthorityObservation> ObserveAsync(ProcessLaunchAuthority authority,
        CancellationToken cancellationToken = default);
}

public interface IProcessExecutionDispatchAuthorityReader {
    Task<ProcessExecutionDispatchAuthorityResult> ReadAsync(Guid executionRunId, CancellationToken cancellationToken = default);
}

public interface IProcessExecutionMutationGuard {
    Task RequireForMutationAsync(ProcessExecutionDispatchAuthority expected, CancellationToken cancellationToken = default);
}
