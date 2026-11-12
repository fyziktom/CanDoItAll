using CanDoItAll.Processes.Runtime;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Processes.Persistence;

internal static class EfProcessDecisionFinalizationGuard {
    internal static async Task RequireAsync(ProcessPersistenceDbContext context, ProcessRuntimeStateSnapshot state,
        ProcessDecisionGeneration current, ProcessDecisionFinalization command, CancellationToken cancellationToken) {
        if (current.BudgetId != command.Budget.BudgetId) {
            throw new InvalidDataException("Terminal decision finalization must retain the original local budget.");
        }
        var row = await (from budget in context.DecisionBudgets.AsNoTracking()
            join step in context.RuntimeSteps.AsNoTracking()
                on new { budget.RunId, budget.StepInstanceId } equals new { step.RunId, StepInstanceId = (Guid?)step.StepInstanceId }
            where budget.BudgetId == command.Budget.BudgetId.Value && budget.RootRunId == state.RootRunId.Value &&
                budget.RunId == state.RunId.Value && step.StepInstanceId == command.StepInstanceId.Value
            select new {
                Budget = budget, step.Status, step.ActiveClaimToken,
                LiveClaim = context.DispatchClaims.Any(claim => claim.RunId == step.RunId && claim.StepInstanceId == step.StepInstanceId &&
                    (claim.Status == DispatchClaimStatus.Claimed || claim.Status == DispatchClaimStatus.LeaseRenewed || claim.Status == DispatchClaimStatus.Reclaimed)),
                Unsettled = context.DecisionBudgetAllocations.Any(allocation => allocation.BudgetId == budget.BudgetId && allocation.OwnerWorkRevision == null),
                InconsistentChild = context.DecisionChildren.Any(child => child.DecisionId == command.Expectation.Reference.DecisionId.Value &&
                    child.Generation == command.Expectation.Reference.Generation && !context.DecisionBudgetAllocations.Any(allocation =>
                        allocation.BudgetId == child.BudgetId && allocation.AllocationId == child.AllocationId &&
                        allocation.OwnerWorkRevision != null && allocation.OwnerWorkRevision == child.OwnerRevision))
            }).SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidDataException("Terminal decision finalization lost its persisted step or local budget.");
        var balance = ProcessDecisionRows.ReadBalance(row.Budget);
        if (row.Status != command.StepStatus || row.ActiveClaimToken is not null || row.LiveClaim) {
            throw new InvalidOperationException("Terminal decision finalization requires the persisted terminal step without live dispatch work.");
        }
        if (balance.Revision != command.Budget.Revision) {
            throw new ProcessDecisionException(ProcessDecisionRefusal.StaleExpectation);
        }
        if (balance.ReservedWorkTicks != 0 || balance.ReservedReplans != 0 || row.Unsettled || row.InconsistentChild) {
            throw new InvalidOperationException("Terminal decision finalization requires complete retained child closures and settled original work.");
        }
    }
}
