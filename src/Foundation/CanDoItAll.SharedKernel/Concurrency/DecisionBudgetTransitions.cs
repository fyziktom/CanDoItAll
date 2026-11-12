namespace CanDoItAll.SharedKernel;

public static class DecisionBudgetTransitions {
    public static DecisionBudgetBalance Create(DecisionBudgetId budgetId, DecisionDataPolicyBudget ceiling) {
        var balance = new DecisionBudgetBalance(budgetId, 1, ceiling, 0, 0, 0, 0, 0, false);
        balance.Validate();
        return balance;
    }

    public static DecisionBudgetChange Reserve(DecisionBudgetBalance current, DecisionBudgetAllocationId allocationId,
        DecisionBudgetAmount requested, DecisionBudgetAllocation? existing = null) {
        ArgumentNullException.ThrowIfNull(current);
        current.Validate();
        var allocation = new DecisionBudgetAllocation(current.BudgetId, allocationId, requested);
        allocation.Validate();
        if (existing is not null) {
            RequireAllocation(current, existing);
            if (existing.Id != allocationId || existing.Grant != requested) {
                throw new DecisionBudgetException(DecisionBudgetRefusal.OriginalAllocationChanged);
            }
            return new(false, current, existing);
        }
        if (current.OverrunDetected) {
            throw new DecisionBudgetException(DecisionBudgetRefusal.Overrun);
        }
        if (current.AllocationCount == DecisionBudgetBalance.MaximumAllocations) {
            throw new DecisionBudgetException(DecisionBudgetRefusal.AllocationCapacity);
        }
        if (requested.WorkTicks > current.Ceiling.MaximumWorkDuration.Ticks - current.ConsumedWorkTicks - current.ReservedWorkTicks) {
            throw new DecisionBudgetException(DecisionBudgetRefusal.WorkExhausted);
        }
        if (requested.Replans > current.Ceiling.MaximumReplans - current.ConsumedReplans - current.ReservedReplans) {
            throw new DecisionBudgetException(DecisionBudgetRefusal.ReplansExhausted);
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

    public static DecisionBudgetChange Settle(DecisionBudgetBalance current, DecisionBudgetAllocation allocation,
        DecisionBudgetSettlement settlement) {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(allocation);
        ArgumentNullException.ThrowIfNull(settlement);
        current.Validate();
        RequireAllocation(current, allocation);
        settlement.Validate();
        if (allocation.Settlement is { } original) {
            if (original != settlement) {
                throw new DecisionBudgetException(DecisionBudgetRefusal.OriginalSettlementChanged);
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

    private static void RequireAllocation(DecisionBudgetBalance balance, DecisionBudgetAllocation allocation) {
        allocation.Validate();
        if (allocation.BudgetId != balance.BudgetId || balance.AllocationCount == 0 ||
            allocation.Grant.WorkTicks > balance.Ceiling.MaximumWorkDuration.Ticks || allocation.Grant.Replans > balance.Ceiling.MaximumReplans ||
            allocation.Settlement is null && (balance.ReservedWorkTicks < allocation.Grant.WorkTicks || balance.ReservedReplans < allocation.Grant.Replans) ||
            allocation.Settlement is { } closed && (balance.ConsumedWorkTicks < closed.Consumed.WorkTicks || balance.ConsumedReplans < closed.Consumed.Replans ||
                !balance.OverrunDetected && (closed.Consumed.WorkTicks > allocation.Grant.WorkTicks || closed.Consumed.Replans > allocation.Grant.Replans))) {
            throw new InvalidDataException("The retained allocation does not belong to this decision budget balance.");
        }
    }
}
