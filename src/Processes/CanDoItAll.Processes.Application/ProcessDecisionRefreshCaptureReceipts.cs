using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Processes.Application;

internal static class ProcessDecisionRefreshCaptureReceipts {
    internal static void Require(ProcessDecisionRefreshCaptureProgress progress, ProcessRunId root,
        ProcessDecisionRefreshCaptureIdentity identity) {
        foreach (var (receipt, command) in new[] {
            (progress.RootReservation, identity.RootReserve), (progress.RootCompletion, identity.RootComplete),
            (progress.RootAbort, identity.RootAbort), (progress.LocalReservation, identity.LocalReserve),
            (progress.LocalAbort, identity.LocalAbort), (progress.Successor, identity.Successor)
        }) {
            if (receipt is not null) {
                Require(receipt, root, identity, command);
            }
        }
        if (progress.RootCompletion is not null && (progress.RootReservation is null || progress.RootAbort is not null) ||
                progress.RootAbort is not null && progress.RootReservation is null ||
                progress.LocalAbort is not null && progress.LocalReservation is null ||
                progress.Successor is not null && (progress.LocalReservation is null || progress.LocalAbort is not null || progress.RootAbort is not null)) {
            throw Invalid();
        }
        RequirePair(progress.RootReservation, progress.RootCompletion ?? progress.RootAbort);
        RequirePair(progress.LocalReservation, progress.Successor ?? progress.LocalAbort);
        if (progress.Successor?.Decision?.Command.InheritedScope is { } inherited &&
                (progress.RootCompletion?.Scope is not { } rootCapture || rootCapture.State.Expectation != inherited ||
                    rootCapture.State.Material?.Stamp != progress.Successor.Decision.Command.Material)) {
            throw Invalid();
        }
    }

    internal static void Require(ProcessDecisionRefreshCaptureReceipt receipt, ProcessRunId root,
        ProcessDecisionRefreshCaptureIdentity identity, RuntimeCommandId command) {
        var reserve = command == identity.RootReserve || command == identity.LocalReserve;
        var shared = command == identity.RootReserve || command == identity.RootComplete || command == identity.RootAbort;
        if (receipt.Outcome is not (ProcessRuntimeTransitionOutcome.Applied or ProcessRuntimeTransitionOutcome.Duplicate) ||
                receipt.Budget is not { } budget || !budget.Change.Changed ||
                budget.Command.AllocationId != (shared ? identity.RootAllocation : identity.LocalAllocation) ||
                budget.Change.Allocation.Id != budget.Command.AllocationId ||
                budget.Change.Allocation.BudgetId != budget.Command.Expectation.BudgetId ||
                budget.Change.Balance.BudgetId != budget.Command.Expectation.BudgetId ||
                budget.Change.Balance.Revision != checked(budget.Command.Expectation.Revision + 1) ||
                budget.Change.Allocation.Grant is not { WorkTicks: > 0, Replans: 0 } ||
                reserve != (budget.Command is ProcessDecisionBudgetReservation) ||
                shared != (budget.Command.Expectation.StepInstanceId is null) ||
                reserve && budget.Change.Allocation.Settlement is not null ||
                !reserve && (budget.Command is not ProcessDecisionBudgetSettlement settlement ||
                    settlement.Settlement != budget.Change.Allocation.Settlement || settlement.Settlement.OwnerWorkRevision != 1 ||
                    settlement.Settlement.Consumed.Replans != 0)) {
            throw Invalid();
        }
        budget.Command.Validate();
        budget.Change.Allocation.Validate();
        budget.Change.Balance.Validate();
        if (shared) {
            RequireScope(receipt, root, identity, command);
        } else {
            if (receipt.Scope is not null || budget.Funding is not { } funding || funding.RootRunId != root || funding.Receipt.Funding is not null ||
                    funding.Receipt.Command.Expectation.StepInstanceId is not null ||
                    funding.Receipt.Command.AllocationId != ProcessDecisionFundingIdentity.Derive(
                        funding.Receipt.Command.Expectation.BudgetId, budget.Command.Expectation.BudgetId, budget.Command.AllocationId) ||
                    funding.Receipt.Change.Allocation.Grant != budget.Change.Allocation.Grant ||
                    funding.Receipt.Change.Allocation.Settlement != budget.Change.Allocation.Settlement ||
                    command != identity.Successor && receipt.Decision is not null) {
                throw Invalid();
            }
        }
        if (command == identity.Successor && (receipt.Decision is not { Command: ProcessDecisionRefresh refresh, Changed: true, Previous: { } previous } decision ||
                refresh.AdmissionCommandId != identity.AdmissionId || refresh.MaterialId != identity.Material ||
                refresh.Material?.Reference.MaterialId != identity.Material || refresh.StepInstanceId != budget.Command.Expectation.StepInstanceId ||
                previous.RefreshCommandId != identity.AdmissionId || previous.Phase != ProcessDecisionPhase.Superseded ||
                decision.Current.Reference.Generation != checked(previous.Reference.Generation + 1) ||
                decision.Current.Reference.DecisionId != previous.Reference.DecisionId || decision.Current.MaterialId != identity.Material ||
                decision.Current.BudgetId != budget.Command.Expectation.BudgetId || decision.Current.RefreshCommandId is not null ||
                decision.Current.Phase != ProcessDecisionPhase.Active || decision.Current.ChildCount != 0 || decision.Current.UnclosedChildCount != 0)) {
            throw Invalid();
        }
    }

    private static void RequirePair(ProcessDecisionRefreshCaptureReceipt? reservation, ProcessDecisionRefreshCaptureReceipt? closed) {
        if (reservation is null || closed is null) {
            return;
        }
        var original = reservation.Budget!;
        var settled = closed.Budget!;
        if (settled.Command.Expectation.BudgetId != original.Command.Expectation.BudgetId ||
                settled.Command.Expectation.StepInstanceId != original.Command.Expectation.StepInstanceId ||
                settled.Command.Expectation.Revision < original.Change.Balance.Revision ||
                settled.Change.Allocation.Grant != original.Change.Allocation.Grant ||
                reservation.Scope is { } scope && closed.Scope?.Command.Expectation != scope.State.Expectation) {
            throw Invalid();
        }
    }

    private static void RequireScope(ProcessDecisionRefreshCaptureReceipt receipt, ProcessRunId root,
        ProcessDecisionRefreshCaptureIdentity identity, RuntimeCommandId command) {
        if (receipt.Scope is not { } scope || receipt.Decision is not null || receipt.Budget!.Funding is not null ||
                scope.State.Admission.Funding.RootRunId != root || scope.State.Admission.Funding.BudgetId != receipt.Budget.Command.Expectation.BudgetId ||
                scope.State.Revision != checked(scope.Command.Expectation.Revision + 1) ||
                scope.State.Admission.ScopeId != scope.Command.Expectation.ScopeId) {
            throw Invalid();
        }
        scope.Command.Validate();
        scope.State.Validate();
        var valid = command == identity.RootReserve
            ? scope.Command.Kind == ProcessDecisionRunScopeCommandKind.BeginCapture && scope.State.Capture == scope.Command.Capture &&
                scope.State.Capture == new ProcessDecisionRunScopeCapture(identity.RootAllocation, identity.Material, ProcessDecisionRunCaptureKind.ManagerRefresh)
            : command == identity.RootComplete
                ? scope.Command.Kind == ProcessDecisionRunScopeCommandKind.CompleteCapture && scope.State.Phase == ProcessDecisionRunScopePhase.Active &&
                    scope.State.Material?.Stamp == scope.Command.Material && scope.Command.Material?.Reference.MaterialId == identity.Material
                : command == identity.RootAbort && scope.Command.Kind == ProcessDecisionRunScopeCommandKind.AbortCapture &&
                    scope.State.Phase == ProcessDecisionRunScopePhase.Reconciling && scope.State.Capture is null;
        if (!valid) {
            throw Invalid();
        }
    }

    private static InvalidDataException Invalid() => new("Refresh capture progress does not retain its original action, allocation, scope or settlement.");
}
