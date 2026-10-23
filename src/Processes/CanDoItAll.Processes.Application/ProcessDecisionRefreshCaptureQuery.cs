using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Processes.Application;

public sealed record ProcessDecisionRefreshCaptureSelection(ProcessAcceptedExecutionInput Input,
    ProcessDecisionRefreshReceipt Admission, ProcessDecisionResultSelection Original,
    ProcessDecisionRunAdmission Root, DecisionBudgetBalance RootBudget, ProcessRuntimeStatus RootStatus,
    ProcessDecisionRunScopeState? SharedScope, bool RootWorkPending);

public sealed record ProcessDecisionRefreshCaptureReceipt(ProcessRuntimeTransitionOutcome Outcome,
    ProcessDecisionReceipt? Decision, ProcessDecisionBudgetReceipt? Budget, ProcessDecisionRunScopeReceipt? Scope);

public sealed record ProcessDecisionRefreshCaptureProgress(ProcessDecisionRefreshCaptureReceipt? RootReservation,
    ProcessDecisionRefreshCaptureReceipt? RootCompletion, ProcessDecisionRefreshCaptureReceipt? RootAbort,
    ProcessDecisionRefreshCaptureReceipt? LocalReservation, ProcessDecisionRefreshCaptureReceipt? LocalAbort,
    ProcessDecisionRefreshCaptureReceipt? Successor);

public interface IProcessDecisionRefreshCaptureQuery {
    Task<ProcessDecisionRefreshCaptureProgress> ReadProgressAsync(ProcessRunId runId, RuntimeCommandId admissionId,
        CancellationToken cancellationToken = default);
    Task<ProcessDecisionRefreshCaptureSelection> ReadAsync(ProcessRuntimeStateSnapshot state, RuntimeCommandId admissionId,
        CancellationToken cancellationToken = default);
}
