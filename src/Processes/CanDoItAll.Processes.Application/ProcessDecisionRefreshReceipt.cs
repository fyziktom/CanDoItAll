using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Core;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Processes.Application;

public sealed record ProcessDecisionRefreshReceipt(ProcessDecisionReceipt Decision, ProcessDecisionRunScopeReceipt? SharedScope) {
    public ProcessDecisionRefreshIntent RequireOriginal(ProcessDecisionRefreshRequest request, ProcessEventActor actor) {
        ArgumentNullException.ThrowIfNull(Decision);
        request.Validate();
        Decision.Current.Validate();
        if (Decision.Command is not ProcessDecisionBeginRefresh begin || begin.Intent.Request != request || begin.Intent.Actor != actor) {
            throw new InvalidOperationException("The refresh action already belongs to a different original request or actor.");
        }
        begin.Validate();
        var current = Decision.Current;
        if (!Decision.Changed || Decision.Previous is not null || Decision.Child is not null ||
                current.Reference != request.Decision.Reference || current.Revision != checked(request.Decision.Revision + 1) ||
                current.BudgetId != request.Budget.BudgetId || current.Phase != ProcessDecisionPhase.Reconciling ||
                current.MaterialId != begin.Intent.OriginalMaterialId ||
                current.ChildCount != 1 || current.UnclosedChildCount != 1 || current.RefreshCommandId != request.CommandId ||
                SharedScope?.Command != ScopeCommand(request) ||
                SharedScope is { } scope && (scope.State.Expectation.ScopeId != request.SharedScope!.ScopeId ||
                    scope.State.Revision != checked(request.SharedScope.Revision + 1) || scope.State.Phase != ProcessDecisionRunScopePhase.Reconciling ||
                    scope.State.Admission.Funding.RootRunId != request.Child.RootRunId ||
                    scope.State.Material?.Stamp.Reference.MaterialId != current.MaterialId)) {
            throw new InvalidDataException("The retained refresh receipt does not prove its original atomic local and shared admission.");
        }
        SharedScope?.Command.Validate();
        SharedScope?.State.Validate();
        return begin.Intent;
    }

    public static ProcessDecisionRunScopeCommand? ScopeCommand(ProcessDecisionRefreshRequest request)
        => request.SharedScope is { } scope ? new(scope, ProcessDecisionRunScopeCommandKind.BeginReconciliation) : null;
}

public interface IProcessDecisionRefreshReceiptReader {
    Task<ProcessDecisionRefreshReceipt?> ReadAsync(ProcessRunId runId, RuntimeCommandId commandId,
        CancellationToken cancellationToken = default);
    Task<ProcessDecisionRefreshReceipt?> ReadCurrentAsync(ProcessRunId runId, ProcessStepInstanceId stepId,
        CancellationToken cancellationToken = default);
}

public sealed record ProcessDecisionRefreshAdmissionResult(ProcessDecisionRefreshReceipt Receipt, bool CommittedNow);
