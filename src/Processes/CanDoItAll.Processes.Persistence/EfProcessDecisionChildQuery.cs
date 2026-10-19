using CanDoItAll.Processes.Runtime;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Processes.Persistence;

public sealed class EfProcessDecisionChildQuery(IDbContextFactory<ProcessPersistenceDbContext> factory) {
    public async Task<ProcessDecisionChildObservation?> ReadAsync(ProcessDecisionChildAddress address, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(address);
        address.Validate();
        await using var context = await factory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return (await ReadCoreAsync(context, address, cancellationToken).ConfigureAwait(false))?.Observation;
    }

    internal static async Task<ProcessDecisionChildRead?> ReadCoreAsync(ProcessPersistenceDbContext context, ProcessDecisionChildAddress address,
        CancellationToken cancellationToken) {
        var row = await (from head in context.DecisionHeads.AsNoTracking()
            join generation in context.DecisionGenerations.AsNoTracking() on head.DecisionId equals generation.DecisionId
            join child in context.DecisionChildren.AsNoTracking()
                on new { generation.DecisionId, generation.Generation } equals new { child.DecisionId, child.Generation }
            join allocation in context.DecisionBudgetAllocations.AsNoTracking()
                on new { child.BudgetId, child.AllocationId } equals new { allocation.BudgetId, allocation.AllocationId }
            join budget in context.DecisionBudgets.AsNoTracking() on allocation.BudgetId equals budget.BudgetId
            join state in context.RuntimeStates.AsNoTracking() on head.RunId equals state.RunId
            join step in context.RuntimeSteps.AsNoTracking()
                on new { head.RunId, head.StepInstanceId } equals new { step.RunId, step.StepInstanceId }
            where head.RootRunId == address.RootRunId.Value && head.RunId == address.RunId.Value && head.StepInstanceId == address.StepInstanceId.Value &&
                head.DecisionId == address.Reference.DecisionId.Value && generation.Generation == address.Reference.Generation && child.ChildId == address.ChildId.Value
            select new { Head = head, Generation = generation, Child = child, Allocation = allocation, Budget = budget,
                StateRoot = state.RootRunId, RunStatus = state.Status, StepStatus = step.Status, step.ActiveClaimToken,
                HasLiveClaim = context.DispatchClaims.Any(claim => claim.RunId == head.RunId && claim.StepInstanceId == head.StepInstanceId &&
                    (claim.Status == DispatchClaimStatus.Claimed || claim.Status == DispatchClaimStatus.LeaseRenewed || claim.Status == DispatchClaimStatus.Reclaimed)) })
            .SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        if (row is null) {
            return null;
        }
        if (row.StateRoot != address.RootRunId.Value || row.Budget.RootRunId != address.RootRunId.Value || row.Budget.RunId != address.RunId.Value ||
                row.Budget.StepInstanceId != address.StepInstanceId.Value || row.Head.BudgetId != row.Generation.BudgetId || row.Child.BudgetId != row.Head.BudgetId ||
                row.Head.CurrentGeneration < row.Generation.Generation || row.Head.Revision < row.Generation.Revision) {
            throw new InvalidDataException("The Process child, head, material and budget do not share their original runtime scope.");
        }
        var current = row.Head.CurrentGeneration == row.Generation.Generation;
        var generationValue = current ? ProcessDecisionRows.RequireHead(row.Head, row.Generation) : ProcessDecisionRows.ReadGeneration(row.Generation);
        var childValue = ProcessDecisionRows.ReadChild(row.Child);
        var allocationValue = ProcessDecisionRows.ReadAllocation(row.Allocation);
        var balance = ProcessDecisionRows.ReadBalance(row.Budget);
        if ((childValue.Closure is null) != (allocationValue.Settlement is null) ||
                childValue.Closure?.OwnerRevision != allocationValue.Settlement?.OwnerWorkRevision) {
            throw new InvalidDataException("The retained Process child and allocation closure disagree.");
        }
        var admission = new ProcessDecisionChildAdmission(address, generationValue.MaterialId, childValue.Owner,
            allocationValue.BudgetId, allocationValue.Id, allocationValue.Grant);
        admission.Validate();
        return new(new(admission, generationValue, childValue.Closure, allocationValue.Settlement, current),
            row.RunStatus, row.StepStatus, row.ActiveClaimToken, row.HasLiveClaim, balance.OverrunDetected);
    }
}

internal sealed record ProcessDecisionChildRead(ProcessDecisionChildObservation Observation, ProcessRuntimeStatus RunStatus,
    ProcessRuntimeStepStatus StepStatus, Guid? ActiveClaimToken, bool HasLiveClaim, bool BudgetOverrun);
