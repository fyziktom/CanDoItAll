using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Drivers.Abstractions;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Processes.Application;

public static class ProcessDecisionRefreshAccountingPreparation {
    public static ProcessDecisionRefreshIntent RequireOriginal(ProcessRuntimeStateSnapshot state, ProcessAcceptedExecutionInput input,
        ProcessDecisionResultSelection selection, ProcessDecisionRefreshReceipt admission) {
        if (admission.Decision.Command is not ProcessDecisionBeginRefresh begin) {
            throw new InvalidDataException("Refresh accounting requires its original admission command.");
        }
        var intent = admission.RequireOriginal(begin.Intent.Request, begin.Intent.Actor);
        var address = intent.Request.Child;
        selection.RequireOriginal(state, address.StepInstanceId, new ProcessExecutionRunId(address.ChildId.Value));
        var original = selection.Child?.Original ?? throw new InvalidDataException("Refresh accounting lost its original child.");
        ProcessDecisionSingleActivityAdmission.Require(state, input, original.ExecutionContract);
        var policy = input.Assignment.DecisionDataPolicy!;
        if (state.Steps.Count(step => step.IsExecutable) != 1 || original.Child.Address != address ||
                original.Child.MaterialId != intent.OriginalMaterialId || input.InputFingerprint != intent.InputFingerprint ||
                ProcessDecisionChildMaterialBindingCodec.Fingerprint(original) != intent.ChildFingerprint ||
                input.Assignment.StepInstanceId != address.StepInstanceId || policy.SharedRunScopeId != intent.Request.SharedScope?.ScopeId ||
                input.Source.DatabaseProfileId != original.Scope.DatabaseProfileId || policy.WorkspaceNamespaceId != original.Scope.WorkspaceNamespaceId) {
            throw new InvalidDataException("Refresh accounting changed its accepted input, original child, source or material.");
        }
        return intent;
    }

    public static void RequirePending(ProcessRuntimeStateSnapshot state, ProcessDecisionResultSelection selection,
        ProcessDecisionRefreshReceipt admission) {
        var intent = ((ProcessDecisionBeginRefresh)admission.Decision.Command).Intent;
        if (selection.Head != admission.Decision.Current || selection.Budget.BudgetId != intent.Request.Budget.BudgetId ||
                selection.Budget.Revision != intent.Request.Budget.Revision || selection.OtherWorkPending ||
                selection.SharedScope != admission.SharedScope?.State.Expectation ||
                selection.SharedScopePending != (admission.SharedScope is not null) ||
                selection.Child is not { Observation.Closure: null, Observation.Settlement: null } child ||
                child.Observation.Generation != admission.Decision.Current) {
            throw new ProcessDecisionException(ProcessDecisionRefusal.StaleExpectation);
        }
        if (ProcessDecisionReviewCompletion.ValidatePending(state, intent.Request.Child, intent.Request.StrategyId,
                intent.Request.WaitingResultKey) is { } refusal) {
            throw new InvalidOperationException(refusal.Message);
        }
    }

    public static ProcessRuntimeCommitRequest Create(ProcessRuntimeStateSnapshot state, RuntimeCommandContext context,
        ProcessDecisionRefreshReceipt admission, ProcessDecisionResultChild child, ProcessDecisionChildResult.Closed closed) {
        var intent = ((ProcessDecisionBeginRefresh)admission.Decision.Command).Intent;
        ProcessDecisionRefreshAccounting accounting = new(intent.Request.CommandId);
        var ticks = Math.Max(context.OccurredAtUtc.UtcTicks / 10 * 10, checked(state.UpdatedAtUtc.UtcTicks + 10));
        context = context with { CommandId = accounting.CommandId, Actor = intent.Actor, OccurredAtUtc = new(ticks, TimeSpan.Zero) };
        var accepted = closed.Closure.Outcome == ProcessDecisionChildOutcome.Accepted;
        var outcome = closed.Status switch {
            ProcessDecisionChildTerminalStatus.Succeeded => StrategyOutcome.Succeeded,
            ProcessDecisionChildTerminalStatus.Failed => StrategyOutcome.Failed,
            ProcessDecisionChildTerminalStatus.Cancelled => StrategyOutcome.Canceled,
            _ => throw new InvalidDataException("Refresh accounting received an unsupported terminal owner status.")
        };
        var mutation = accepted ? ProcessDecisionReviewCompletion.Prepare(state, context,
            new(intent.Request.Child, intent.Request.StrategyId, intent.Request.WaitingResultKey, accounting.CompletionKey,
                outcome, closed.Closure, closed.Work)) : ProcessRuntimeMutation.Applied(state with { UpdatedAtUtc = context.OccurredAtUtc }, [], []);
        var request = new ProcessRuntimeCommitRequest(accounting.CommandId, state, mutation) {
            RefreshAccounting = accounting,
            Decision = new ProcessDecisionClosure(intent.Request.Child.StepInstanceId, admission.Decision.Current.Expectation,
                intent.Request.Child.ChildId, closed.Closure) {
                Completion = !accepted ? ProcessDecisionCompletion.Reconcile : outcome == StrategyOutcome.Canceled
                    ? ProcessDecisionCompletion.Cancel : ProcessDecisionCompletion.Complete
            },
            DecisionBudget = new ProcessDecisionBudgetSettlement(intent.Request.Budget, child.Original.Child.AllocationId, closed.Work),
            DecisionRunScope = accepted ? ProcessDecisionRunScopeCompletion.Prepare(admission.SharedScope?.State.Expectation, state, mutation) : null
        };
        accounting.RequireMutation(request, intent);
        return request;
    }
}
