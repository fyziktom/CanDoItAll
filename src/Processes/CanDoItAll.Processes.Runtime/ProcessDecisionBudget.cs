using CanDoItAll.SharedKernel;

namespace CanDoItAll.Processes.Runtime;

public readonly record struct ProcessDecisionId {
    public ProcessDecisionId(Guid value) {
        ArgumentOutOfRangeException.ThrowIfEqual(value, Guid.Empty);
        Value = value;
    }

    public Guid Value { get; }
}

public readonly record struct ProcessDecisionAllocationId {
    public ProcessDecisionAllocationId(Guid value) {
        ArgumentOutOfRangeException.ThrowIfEqual(value, Guid.Empty);
        Value = value;
    }

    public Guid Value { get; }
}

public sealed record ProcessDecisionBudgetAmount(long WorkTicks, int Replans) {
    public void Validate() {
        if (WorkTicks < 0 || Replans < 0) {
            throw new InvalidDataException("Decision work and replan amounts cannot be negative.");
        }
    }
}

public sealed record ProcessDecisionBudgetSettlement(long OwnerWorkRevision, ProcessDecisionBudgetAmount Consumed) {
    public void Validate() {
        if (OwnerWorkRevision < 1 || Consumed is null) {
            throw new InvalidDataException("Decision settlement requires exact closed owner work evidence.");
        }
        Consumed.Validate();
    }
}

public sealed record ProcessDecisionBudgetAllocation(ProcessDecisionId DecisionId, ProcessDecisionAllocationId Id,
    ProcessDecisionBudgetAmount Grant, ProcessDecisionBudgetSettlement? Settlement = null) {
    public void Validate() {
        if (DecisionId.Value == Guid.Empty || Id.Value == Guid.Empty || Grant is null || Grant is { WorkTicks: 0, Replans: 0 }) {
            throw new InvalidDataException("A decision allocation requires its original identities and nonempty grant.");
        }
        Grant.Validate();
        Settlement?.Validate();
    }
}

public sealed record ProcessDecisionBudgetBalance(ProcessDecisionId DecisionId, long Revision, DecisionDataPolicyBudget Ceiling,
    long ConsumedWorkTicks, long ReservedWorkTicks, long ConsumedReplans, long ReservedReplans, int AllocationCount, bool OverrunDetected) {
    public const int MaximumAllocations = 256;

    public void Validate() {
        if (DecisionId.Value == Guid.Empty || Revision < 1 || Ceiling is null || ConsumedWorkTicks < 0 || ReservedWorkTicks < 0 ||
            ConsumedReplans < 0 || ReservedReplans < 0 || AllocationCount is < 0 or > MaximumAllocations || Revision < AllocationCount + 1 ||
            ConsumedWorkTicks > long.MaxValue - ReservedWorkTicks || ConsumedReplans > long.MaxValue - ReservedReplans ||
            AllocationCount == 0 && (ConsumedWorkTicks != 0 || ReservedWorkTicks != 0 || ConsumedReplans != 0 || ReservedReplans != 0 || OverrunDetected)) {
            throw new InvalidDataException("The retained decision budget balance is invalid.");
        }
        if (!OverrunDetected && (ConsumedWorkTicks + ReservedWorkTicks > Ceiling.MaximumWorkDuration.Ticks ||
                ConsumedReplans + ReservedReplans > Ceiling.MaximumReplans)) {
            throw new InvalidDataException("The retained decision balance exceeds its ceiling without recording an overrun.");
        }
    }
}

public enum ProcessDecisionBudgetRefusal { WorkExhausted, ReplansExhausted, AllocationCapacity, OriginalAllocationChanged, OriginalSettlementChanged, Overrun }

public sealed class ProcessDecisionBudgetException(ProcessDecisionBudgetRefusal reason)
    : InvalidOperationException($"The Process decision budget refused the transition: {reason}.") {
    public ProcessDecisionBudgetRefusal Reason { get; } = reason;
}

public sealed record ProcessDecisionBudgetChange(bool Changed, ProcessDecisionBudgetBalance Balance, ProcessDecisionBudgetAllocation Allocation);
