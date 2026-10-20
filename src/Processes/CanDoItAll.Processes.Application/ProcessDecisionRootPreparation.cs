using CanDoItAll.Processes.Runtime;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Processes.Application;

public sealed record ProcessDecisionRootSelection(ProcessDecisionRunAdmission Root, DecisionBudgetBalance Budget,
    ProcessDecisionRunScopeState Scope, ProcessRuntimeStatus RootStatus, ProcessLaunchAuthority Source);

public interface IProcessDecisionRootQuery {
    Task<ProcessDecisionRootSelection?> ReadAsync(ProcessRuntimeStateSnapshot state, CancellationToken cancellationToken = default);
}

public sealed class ProcessDecisionSourceProjectionRequiredException()
    : InvalidOperationException("Root capture requires the bounded original accepted source projection.");

public enum ProcessDecisionRootPreparationStatus { Ready, CapturePending, ReviewRequired, Stopped, SourceProjectionRequired, NotRequired }

public sealed record ProcessDecisionRootPreparationResult(ProcessDecisionRootPreparationStatus Status, ProcessRuntimeStateSnapshot State);

public sealed record ProcessDecisionRootCaptureRequest(ProcessLaunchAuthority Source, ProcessDecisionRunScopeAdmission Scope,
    ProcessDecisionMaterialReference Material);

public enum ProcessDecisionRootCaptureRefusal { SourceChanged, SourceMissing, LifetimeChanged, DatasetChanged, InvalidSource, LimitExceeded }

public abstract record ProcessDecisionRootCaptureResult {
    private ProcessDecisionRootCaptureResult() { }

    public sealed record Captured(ProcessDecisionMaterial Material) : ProcessDecisionRootCaptureResult;
    public sealed record Refused(ProcessDecisionRootCaptureRefusal Reason) : ProcessDecisionRootCaptureResult;
}

public interface IProcessDecisionRootCapturer {
    ProcessDecisionContentContract ContentContract { get; }
    Task<ProcessDecisionRootCaptureResult> CaptureAsync(ProcessDecisionRootCaptureRequest request, CancellationToken cancellationToken = default);
}
