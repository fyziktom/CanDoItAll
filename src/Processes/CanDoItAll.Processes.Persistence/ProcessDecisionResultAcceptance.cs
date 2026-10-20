using CanDoItAll.Processes.Drivers.Abstractions;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Processes.Persistence;

internal static class ProcessDecisionResultAcceptance {
    internal static async Task RequireAsync(ProcessPersistenceDbContext context, ProcessRuntimeCommitRequest request,
        CancellationToken cancellationToken) {
        var original = request.OriginalState.AppliedResults.Select(item => (item.StepInstanceId, item.StrategyId, item.IdempotencyKey)).ToHashSet();
        foreach (var result in request.Mutation.State.AppliedResults.Where(item => !original.Contains((item.StepInstanceId, item.StrategyId, item.IdempotencyKey)))) {
            var selection = await EfProcessDecisionResultQuery.ReadStateAsync(context, request.Mutation.State.RunId,
                result.StepInstanceId, result.ExecutionRunId, cancellationToken).ConfigureAwait(false);
            if (selection is null) {
                continue;
            }
            var step = request.Mutation.State.Steps.Single(item => item.StepInstanceId == result.StepInstanceId);
            if (step.Status != result.AppliedStepStatus || step.ActiveClaimToken is not null) {
                throw Refuse();
            }
            if (step.Status is ProcessRuntimeStepStatus.Waiting or ProcessRuntimeStepStatus.WaitingApproval or ProcessRuntimeStepStatus.Blocked) {
                if (selection.Head is { Phase: not (ProcessDecisionPhase.WaitingForReview or ProcessDecisionPhase.Reconciling) }) {
                    throw Refuse();
                }
                continue;
            }
            if (step.Status is not (ProcessRuntimeStepStatus.Completed or ProcessRuntimeStepStatus.Failed or ProcessRuntimeStepStatus.Cancelled) ||
                    selection.OtherWorkPending || selection.SharedScopePending) {
                throw Refuse();
            }
            if (selection.Head is null) {
                if (result.Outcome is not (StrategyOutcome.Failed or StrategyOutcome.Canceled) ||
                        step.Status == ProcessRuntimeStepStatus.Completed || result.ExecutionRunId is not null) {
                    throw Refuse();
                }
                continue;
            }
            var expectedPhase = step.Status == ProcessRuntimeStepStatus.Cancelled ? ProcessDecisionPhase.Cancelled : ProcessDecisionPhase.Completed;
            if (selection.Head.Phase != expectedPhase || selection.Head.UnclosedChildCount != 0 ||
                    selection.ChildGeneration != selection.Head.Reference.Generation || result.ExecutionRunId is not { } execution ||
                    request.Decision is not ProcessDecisionClosure closure || closure.ChildId.Value != execution.Value ||
                    closure.Expectation.Reference != selection.Head.Reference ||
                    closure.Completion is not { } completion || ProcessDecisionTransitions.CompletionPhase(completion) != expectedPhase ||
                    request.DecisionBudget is not ProcessDecisionBudgetSettlement settlement) {
                throw Refuse();
            }
            var address = new ProcessDecisionChildAddress(new(selection.State.RootRunId), request.Mutation.State.RunId, result.StepInstanceId,
                selection.Head.Reference, new(execution.Value));
            var child = await EfProcessDecisionChildQuery.ReadCoreAsync(context, address, cancellationToken).ConfigureAwait(false);
            if (child is null || child.Observation.Closure != closure.Closure || child.Observation.Settlement != settlement.Settlement ||
                    child.Observation.Admission.AllocationId != settlement.AllocationId || settlement.Expectation.BudgetId != selection.Budget.BudgetId ||
                    settlement.Expectation.StepInstanceId != result.StepInstanceId) {
                throw Refuse();
            }
        }
    }

    private static InvalidOperationException Refuse()
        => new("A governed Process result requires an atomic original-child settlement or an explicit reconciliation wait.");
}
