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
            join parent in context.DecisionBudgets.AsNoTracking().Select(item => new {
                BudgetId = (Guid?)item.BudgetId, RootRunId = (Guid?)item.RootRunId, RunId = (Guid?)item.RunId,
                item.StepInstanceId, OverrunDetected = (bool?)item.OverrunDetected
            }) on budget.ParentBudgetId equals parent.BudgetId into parents
            from parent in parents.DefaultIfEmpty()
            join parentAllocation in context.DecisionBudgetAllocations.AsNoTracking()
                on new { BudgetId = allocation.FundingBudgetId, AllocationId = allocation.FundingAllocationId }
                equals new { BudgetId = (Guid?)parentAllocation.BudgetId, AllocationId = (Guid?)parentAllocation.AllocationId } into parentAllocations
            from parentAllocation in parentAllocations.DefaultIfEmpty()
            join state in context.RuntimeStates.AsNoTracking() on head.RunId equals state.RunId
            join rootState in context.RuntimeStates.AsNoTracking() on head.RootRunId equals rootState.RunId
            join step in context.RuntimeSteps.AsNoTracking()
                on new { head.RunId, head.StepInstanceId } equals new { step.RunId, step.StepInstanceId }
            where head.RootRunId == address.RootRunId.Value && head.RunId == address.RunId.Value && head.StepInstanceId == address.StepInstanceId.Value &&
                head.DecisionId == address.Reference.DecisionId.Value && generation.Generation == address.Reference.Generation && child.ChildId == address.ChildId.Value
            select new { Head = head, Generation = generation, Child = child, Allocation = allocation, Budget = budget,
                Parent = parent, ParentAllocation = parentAllocation, RootStatus = rootState.Status,
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
        if (row.Budget.ParentBudgetId is { } parentId) {
            if (row.Parent is null || row.ParentAllocation is null || row.Parent.BudgetId != parentId ||
                row.Parent.RunId != address.RootRunId.Value || row.Parent.RootRunId != address.RootRunId.Value || row.Parent.StepInstanceId is not null ||
                row.Allocation.FundingBudgetId != parentId || row.ParentAllocation.BudgetId != parentId ||
                row.ParentAllocation.AllocationId != ProcessDecisionFundingIdentity.Derive(new(parentId), balance.BudgetId, allocationValue.Id).Value ||
                row.ParentAllocation.FundingBudgetId is not null || row.ParentAllocation.FundingAllocationId is not null ||
                ProcessDecisionRows.ReadAllocation(row.ParentAllocation).Grant != allocationValue.Grant ||
                ProcessDecisionRows.ReadAllocation(row.ParentAllocation).Settlement != allocationValue.Settlement) {
                throw new InvalidDataException("The Process child has no matching original root funding.");
            }
        } else if (row.Allocation.FundingBudgetId is not null || row.Allocation.FundingAllocationId is not null) {
            throw new InvalidDataException("The Process child has funding outside its original budget.");
        }
        if ((childValue.Closure is null) != (allocationValue.Settlement is null) ||
                childValue.Closure?.OwnerRevision != allocationValue.Settlement?.OwnerWorkRevision) {
            throw new InvalidDataException("The retained Process child and allocation closure disagree.");
        }
        var admission = new ProcessDecisionChildAdmission(address, generationValue.MaterialId, childValue.Owner,
            allocationValue.BudgetId, allocationValue.Id, allocationValue.Grant);
        admission.Validate();
        return new(new(admission, generationValue, childValue.Closure, allocationValue.Settlement, current),
            row.RunStatus, row.RootStatus, row.StepStatus, row.ActiveClaimToken, row.HasLiveClaim, balance.OverrunDetected || row.Parent?.OverrunDetected == true);
    }
}

internal sealed record ProcessDecisionChildRead(ProcessDecisionChildObservation Observation, ProcessRuntimeStatus RunStatus,
    ProcessRuntimeStatus RootStatus, ProcessRuntimeStepStatus StepStatus, Guid? ActiveClaimToken, bool HasLiveClaim, bool BudgetOverrun);
