using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Runtime;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Processes.Persistence;

internal static class EfProcessDecisionRefreshSuccessorGuard {
    internal static async Task RequireFreshAsync(ProcessPersistenceDbContext context, ICanonicalRuntimeDatabase? database,
        ProcessRuntimeCommitRequest request, CancellationToken cancellationToken) {
        if (request.Decision is not ProcessDecisionRefresh { AdmissionCommandId: { } id } command) {
            return;
        }
        ProcessDecisionRefreshSuccessor successor = new(id);
        successor.RequireShape(request);
        var state = request.OriginalState;
        var admission = await EfProcessDecisionRefreshReceiptReader.ReadCoreAsync(context, state.RunId, id, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidDataException("The successor lost its original refresh admission.");
        var intent = ((ProcessDecisionBeginRefresh)admission.Decision.Command).Intent;
        var address = intent.Request.Child;
        var selected = await EfProcessDecisionResultQuery.ReadCoreAsync(context, state.RunId, address.StepInstanceId,
            new ProcessExecutionRunId(address.ChildId.Value), cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidDataException("The successor lost its original persisted child.");
        var input = await EfProcessAcceptedExecutionInputReader.ReadCoreAsync(context,
            database?.Profile.Profile.Id ?? throw new InvalidOperationException("A successor requires the canonical database owner."),
            state.RunId, address.StepInstanceId, cancellationToken).ConfigureAwait(false);
        _ = ProcessDecisionRefreshAccountingPreparation.RequireOriginal(state, input, selected, admission);
        await EfProcessDecisionRefreshReconciliation.RequireAsync(context, state, admission, selected, cancellationToken).ConfigureAwait(false);
        if (selected.Head!.Expectation != command.Expectation) {
            throw new InvalidOperationException("The successor changed its reconciled decision expectation.");
        }
        successor.RequireMutation(request, intent);
        await RequireCaptureAsync(context, request, selected, cancellationToken).ConfigureAwait(false);
    }

    private static async Task RequireCaptureAsync(ProcessPersistenceDbContext context, ProcessRuntimeCommitRequest request,
        ProcessDecisionResultSelection selected, CancellationToken cancellationToken) {
        var settlement = (ProcessDecisionBudgetSettlement)request.DecisionBudget!;
        if (settlement.Expectation.StepInstanceId != selected.StepInstanceId || settlement.Expectation.BudgetId != selected.Budget.BudgetId ||
                settlement.Expectation.Revision != selected.Budget.Revision || settlement.AllocationId == selected.Child!.Original.Child.AllocationId) {
            throw new InvalidOperationException("A successor requires its distinct current capture allocation.");
        }
        var allocation = await context.DecisionBudgetAllocations.AsNoTracking().Where(row =>
                row.BudgetId == selected.Budget.BudgetId.Value && row.AllocationId == settlement.AllocationId.Value)
            .Select(row => new { row.OwnerWorkRevision, row.FundingBudgetId, row.FundingAllocationId })
            .SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidDataException("The successor capture was not reserved before reading material.");
        if (allocation.OwnerWorkRevision is not null) {
            throw new InvalidOperationException("The successor capture allocation is already closed.");
        }
        if (await context.DecisionBudgetAllocations.AsNoTracking().AnyAsync(row =>
                row.BudgetId == selected.Budget.BudgetId.Value && row.AllocationId != settlement.AllocationId.Value &&
                row.OwnerWorkRevision == null, cancellationToken).ConfigureAwait(false)) {
            throw new ProcessDecisionException(ProcessDecisionRefusal.UnclosedWork);
        }
        if (selected.SharedScope is null) {
            return;
        }
        if (allocation.FundingBudgetId is null || allocation.FundingAllocationId is null) {
            throw new InvalidDataException("Shared refresh inheritance requires its original root-funded allocation.");
        }
        if (await context.DecisionBudgetAllocations.AsNoTracking().AnyAsync(row => row.BudgetId == allocation.FundingBudgetId &&
                row.AllocationId != allocation.FundingAllocationId && row.OwnerWorkRevision == null, cancellationToken).ConfigureAwait(false)) {
            throw new ProcessDecisionException(ProcessDecisionRefusal.UnclosedWork);
        }
    }
}
