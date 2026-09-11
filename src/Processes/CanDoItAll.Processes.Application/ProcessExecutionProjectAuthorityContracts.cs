using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Contracts;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Processes.Application;

public enum ProcessExecutionAuthorityDisposition {
    Bound,
    ExecutionNotFound,
    UnsupportedSource,
    AdmissionReconciliationRequired,
    NoProjectScope,
    DispatchBindingChanged
}

public sealed record ProcessExecutionClaimEvidence(
    Guid ExecutionRunId,
    Guid ExecutorAgentId,
    ProcessRunId RunId,
    ProcessStepInstanceId StepInstanceId,
    Guid DispatchClaimToken,
    string StepKey,
    DateTimeOffset ExecutionCreatedAtUtc,
    bool ExecutionMayDispatch);

public sealed record ProcessExecutionProjectAuthoritySnapshot(
    Guid ExecutionRunId,
    Guid ExecutorAgentId,
    Guid DispatchClaimToken,
    ProcessExecutionProjectAuthority Reference,
    ProcessLaunchAuthority SourceAuthority,
    IReadOnlyList<string> AllowedOperations,
    string OperationTargetScope,
    ProcessCapabilityScope CapabilityScope,
    bool ObservedCurrentDispatch,
    DateTimeOffset ObservedAtUtc) {
    public ProcessExecutionDispatchAuthority? DispatchAuthority { get; init; }
}

public sealed record ProcessExecutionProjectAuthorityResult(
    ProcessExecutionAuthorityDisposition Disposition,
    ProcessExecutionProjectAuthoritySnapshot? Snapshot = null);

public interface IProcessExecutionProjectAuthorityReader {
    Task<ProcessExecutionProjectAuthorityResult> ReadAsync(Guid executionRunId, CancellationToken cancellationToken = default);
}

public sealed class ProcessExecutionAuthorityMismatchException(string message) : InvalidOperationException(message);
