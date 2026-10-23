using CanDoItAll.Processes.Drivers.Abstractions;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Processes.Application;

public static class ProcessDecisionRefreshAccountingReceipt {
    public static ProcessDecisionRefreshAccountingResult Read(ProcessRuntimeCommitResult result,
        ProcessDecisionRefreshReceipt admission, ProcessDecisionResultChild original) {
        var intent = ((ProcessDecisionBeginRefresh)admission.Decision.Command).Intent;
        _ = admission.RequireOriginal(intent.Request, intent.Actor);
        var request = intent.Request;
        var child = original.Original.Child;
        if (!result.Succeeded || result.State.RootRunId != request.Child.RootRunId || result.State.RunId != request.Child.RunId ||
                result.Decision is not { Command: ProcessDecisionClosure closure, Child: { } retainedChild } decision ||
                result.DecisionBudget is not { Command: ProcessDecisionBudgetSettlement settlement } budget ||
                !decision.Changed || decision.Previous is not null || closure.StepInstanceId != request.Child.StepInstanceId ||
                closure.ChildId != request.Child.ChildId || closure.Expectation != admission.Decision.Current.Expectation ||
                settlement.Expectation != request.Budget || settlement.AllocationId != child.AllocationId ||
                closure.Closure.OwnerRevision != settlement.Settlement.OwnerWorkRevision ||
                retainedChild != new ProcessDecisionChild(request.Child.Reference, request.Child.ChildId, child.Owner, child.AllocationId, closure.Closure) ||
                !budget.Change.Changed || budget.Change.Allocation.BudgetId != child.BudgetId ||
                budget.Change.Allocation.Id != child.AllocationId || budget.Change.Allocation.Grant != child.Grant ||
                budget.Change.Allocation.Settlement != settlement.Settlement || budget.Change.Balance.BudgetId != child.BudgetId ||
                budget.Change.Balance.Revision != checked(request.Budget.Revision + 1) ||
                original.Observation.Closure is { } retained && retained != closure.Closure ||
                original.Observation.Settlement is { } work && work != settlement.Settlement ||
                result.ArtifactLedgerEvents.Count != 0 || result.Diagnostics.Count != 0 ||
                result.OutboxMessages.Count != result.Events.Count || result.OutboxMessages.Where((item, index) =>
                    item.EventId != result.Events[index].EventId || item.PayloadHash != result.Events[index].PayloadHash ||
                    item.SubscriberKind != ProcessOutboxSubscriberKind.RuntimeProjection).Any() ||
                closure.Completion is not { } completion || decision.Current != admission.Decision.Current with {
                    Revision = checked(admission.Decision.Current.Revision + 1), UnclosedChildCount = 0,
                    Phase = ProcessDecisionTransitions.CompletionPhase(completion)
                }) {
            throw InvalidReceipt();
        }
        closure.Validate();
        budget.Change.Allocation.Validate();
        budget.Change.Balance.Validate();
        decision.Current.Validate();
        if (closure.Closure.Outcome == ProcessDecisionChildOutcome.NoEffect) {
            if (completion != ProcessDecisionCompletion.Reconcile || result.DecisionRunScope is not null || result.Events.Count != 0) {
                throw InvalidReceipt();
            }
            return new ProcessDecisionRefreshAccountingResult.Reconciled(result);
        }
        ProcessDecisionRefreshAccounting accounting = new(request.CommandId);
        var completed = result.State.AppliedResults.SingleOrDefault(item => item.StepInstanceId == request.Child.StepInstanceId &&
            item.StrategyId == request.StrategyId && item.IdempotencyKey == accounting.CompletionKey);
        if (completed is null || result.Events.Count is not (0 or 2) || result.Events.Any(item => item.Actor != intent.Actor) ||
                completion != (completed.Outcome == StrategyOutcome.Canceled ? ProcessDecisionCompletion.Cancel : ProcessDecisionCompletion.Complete)) {
            throw InvalidReceipt();
        }
        var verification = ProcessDecisionReviewCompletion.Prepare(result.State,
            new(accounting.CommandId, intent.Actor, new("process-refresh-accounting-replay"), result.State.UpdatedAtUtc),
            new(request.Child, request.StrategyId, request.WaitingResultKey, accounting.CompletionKey,
                completed.Outcome, closure.Closure, settlement.Settlement));
        if (verification.Outcome != ProcessRuntimeTransitionOutcome.Duplicate ||
                (result.DecisionRunScope is null) != (admission.SharedScope is null)) {
            throw InvalidReceipt();
        }
        ProcessDecisionRunScopeCompletion.RequireReceipt(result, request.SharedScope?.ScopeId);
        if (result.DecisionRunScope is { } scope && (scope.Command.Expectation != admission.SharedScope!.State.Expectation ||
                scope.State != admission.SharedScope.State with {
                    Revision = checked(admission.SharedScope.State.Revision + 1),
                    Phase = result.State.Status == ProcessRuntimeStatus.Completed
                        ? ProcessDecisionRunScopePhase.Completed : ProcessDecisionRunScopePhase.Cancelled
                })) {
            throw InvalidReceipt();
        }
        return new ProcessDecisionRefreshAccountingResult.CompletedOriginal(result);
    }

    private static InvalidDataException InvalidReceipt()
        => new("The refresh accounting receipt changed its original closure, result or measured work.");
}
