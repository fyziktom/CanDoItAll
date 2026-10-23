using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Runtime;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Processes.Persistence;

internal static class EfProcessDecisionRefreshReconciliation {
    internal static async Task RequireAsync(ProcessPersistenceDbContext context, ProcessRuntimeStateSnapshot state,
        ProcessDecisionRefreshReceipt admission, ProcessDecisionResultSelection selected, CancellationToken cancellationToken) {
        var intent = ((ProcessDecisionBeginRefresh)admission.Decision.Command).Intent;
        var accountingId = new ProcessDecisionRefreshAccounting(intent.Request.CommandId).CommandId.Value;
        var receipt = await context.IdempotencyKeys.AsNoTracking().Where(row => row.RunId == state.RunId.Value && row.CommandId == accountingId)
            .Select(row => new { row.DecisionReceiptJson, row.DecisionBudgetReceiptJson, row.DecisionRunScopeReceiptJson })
            .SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidDataException("Refresh capture requires its retained original reconciliation accounting receipt.");
        ProcessRuntimeCommitResult accounting = new(ProcessRuntimeTransitionOutcome.Duplicate, state, [], [], [], []) {
            Decision = ProcessDecisionReceiptCodec.Read(receipt.DecisionReceiptJson),
            DecisionBudget = ProcessDecisionBudgetReceiptCodec.Read(receipt.DecisionBudgetReceiptJson),
            DecisionRunScope = ProcessDecisionRunScopeReceiptCodec.Read(receipt.DecisionRunScopeReceiptJson)
        };
        if (ProcessDecisionRefreshAccountingReceipt.Read(accounting, admission, selected.Child!) is not ProcessDecisionRefreshAccountingResult.Reconciled ||
                selected.Head != accounting.Decision!.Current || selected.Child!.Observation.Generation != selected.Head ||
                selected.Child.Observation.Closure != accounting.Decision.Child!.Closure ||
                selected.Child.Observation.Settlement != accounting.DecisionBudget!.Change.Allocation.Settlement) {
            throw new InvalidOperationException("Only the exact reconciled no-effect child may acquire refresh material.");
        }
    }
}
