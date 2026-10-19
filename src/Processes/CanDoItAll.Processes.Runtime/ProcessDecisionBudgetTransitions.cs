using CanDoItAll.SharedKernel;

namespace CanDoItAll.Processes.Runtime;

public static class ProcessDecisionBudgetTransitions {
    public static ProcessDecisionBudgetBalance Create(ProcessDecisionId decisionId, DecisionDataPolicyBudget ceiling) {
        var balance = new ProcessDecisionBudgetBalance(decisionId, 1, ceiling, 0, 0, 0, 0, 0, false);
        balance.Validate();
        return balance;
    }

    public static ProcessDecisionBudgetChange Reserve(ProcessDecisionBudgetBalance current, ProcessDecisionAllocationId allocationId,
        ProcessDecisionBudgetAmount requested, ProcessDecisionBudgetAllocation? existing = null) {
        ArgumentNullException.ThrowIfNull(current);
        current.Validate();
        var allocation = new ProcessDecisionBudgetAllocation(current.DecisionId, allocationId, requested);
        allocation.Validate();
        if (existing is not null) {
            RequireAllocation(current, existing);
            if (existing.Id != allocationId || existing.Grant != requested) {
                throw new ProcessDecisionBudgetException(ProcessDecisionBudgetRefusal.OriginalAllocationChanged);
            }
            return new(false, current, existing);
        }
        if (current.OverrunDetected) {
            throw new ProcessDecisionBudgetException(ProcessDecisionBudgetRefusal.Overrun);
        }
        if (current.AllocationCount == ProcessDecisionBudgetBalance.MaximumAllocations) {
            throw new ProcessDecisionBudgetException(ProcessDecisionBudgetRefusal.AllocationCapacity);
        }
        if (requested.WorkTicks > current.Ceiling.MaximumWorkDuration.Ticks - current.ConsumedWorkTicks - current.ReservedWorkTicks) {
            throw new ProcessDecisionBudgetException(ProcessDecisionBudgetRefusal.WorkExhausted);
        }
        if (requested.Replans > current.Ceiling.MaximumReplans - current.ConsumedReplans - current.ReservedReplans) {
            throw new ProcessDecisionBudgetException(ProcessDecisionBudgetRefusal.ReplansExhausted);
        }
        var next = current with {
            Revision = checked(current.Revision + 1),
            ReservedWorkTicks = checked(current.ReservedWorkTicks + requested.WorkTicks),
            ReservedReplans = checked(current.ReservedReplans + requested.Replans),
            AllocationCount = checked(current.AllocationCount + 1)
        };
        next.Validate();
        return new(true, next, allocation);
    }

    public static ProcessDecisionBudgetChange Settle(ProcessDecisionBudgetBalance current, ProcessDecisionBudgetAllocation allocation,
        ProcessDecisionBudgetSettlement settlement) {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(allocation);
        ArgumentNullException.ThrowIfNull(settlement);
        current.Validate();
        RequireAllocation(current, allocation);
        settlement.Validate();
        if (allocation.Settlement is { } original) {
            if (original != settlement) {
                throw new ProcessDecisionBudgetException(ProcessDecisionBudgetRefusal.OriginalSettlementChanged);
            }
            return new(false, current, allocation);
        }
        var consumedWork = checked(current.ConsumedWorkTicks + settlement.Consumed.WorkTicks);
        var consumedReplans = checked(current.ConsumedReplans + settlement.Consumed.Replans);
        var reservedWork = current.ReservedWorkTicks - allocation.Grant.WorkTicks;
        var reservedReplans = current.ReservedReplans - allocation.Grant.Replans;
        var next = current with {
            Revision = checked(current.Revision + 1),
            ConsumedWorkTicks = consumedWork,
            ConsumedReplans = consumedReplans,
            ReservedWorkTicks = reservedWork,
            ReservedReplans = reservedReplans,
            OverrunDetected = current.OverrunDetected || settlement.Consumed.WorkTicks > allocation.Grant.WorkTicks ||
                settlement.Consumed.Replans > allocation.Grant.Replans ||
                checked(consumedWork + reservedWork) > current.Ceiling.MaximumWorkDuration.Ticks ||
                checked(consumedReplans + reservedReplans) > current.Ceiling.MaximumReplans
        };
        next.Validate();
        return new(true, next, allocation with { Settlement = settlement });
    }

    private static void RequireAllocation(ProcessDecisionBudgetBalance balance, ProcessDecisionBudgetAllocation allocation) {
        allocation.Validate();
        if (allocation.DecisionId != balance.DecisionId || balance.AllocationCount == 0 ||
            allocation.Grant.WorkTicks > balance.Ceiling.MaximumWorkDuration.Ticks || allocation.Grant.Replans > balance.Ceiling.MaximumReplans ||
            allocation.Settlement is null && (balance.ReservedWorkTicks < allocation.Grant.WorkTicks || balance.ReservedReplans < allocation.Grant.Replans) ||
            allocation.Settlement is { } closed && (balance.ConsumedWorkTicks < closed.Consumed.WorkTicks || balance.ConsumedReplans < closed.Consumed.Replans ||
                !balance.OverrunDetected && (closed.Consumed.WorkTicks > allocation.Grant.WorkTicks || closed.Consumed.Replans > allocation.Grant.Replans))) {
            throw new InvalidDataException("The retained allocation does not belong to this decision budget balance.");
        }
    }
}
