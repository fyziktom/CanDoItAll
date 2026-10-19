using CanDoItAll.Processes.Runtime;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Tests.Unit.Processes;

public sealed class ProcessDecisionBudgetTests {
    [Fact]
    public void Multiple_allocations_share_one_remaining_allowance() {
        var original = Balance();
        var first = Reserve(original, 60, 2);
        var second = Reserve(first.Balance, 40, 2);
        Assert.True(first.Changed);
        Assert.True(second.Changed);
        Assert.Equal(100, second.Balance.ReservedWorkTicks);
        Assert.Equal(4, second.Balance.ReservedReplans);
        Assert.Equal(2, second.Balance.AllocationCount);
        Assert.Equal(original.Revision + 2, second.Balance.Revision);
        Refused(ProcessDecisionBudgetRefusal.WorkExhausted, () => Reserve(second.Balance, 1, 0));
        Refused(ProcessDecisionBudgetRefusal.ReplansExhausted, () => Reserve(second.Balance, 0, 1));
        Assert.Equal(0, original.AllocationCount);
    }

    [Fact]
    public void Partial_settlement_consumes_actual_work_and_releases_only_unused_allowance() {
        var first = Reserve(Balance(), 60, 2);
        var second = Reserve(first.Balance, 30, 1);
        var settled = ProcessDecisionBudgetTransitions.Settle(second.Balance, first.Allocation, new(7, new(25, 1)));
        Assert.True(settled.Changed);
        Assert.Equal(25, settled.Balance.ConsumedWorkTicks);
        Assert.Equal(30, settled.Balance.ReservedWorkTicks);
        Assert.Equal(1, settled.Balance.ConsumedReplans);
        Assert.Equal(1, settled.Balance.ReservedReplans);
        Assert.Equal(2, settled.Balance.AllocationCount);
        Assert.Equal(first.Allocation.Grant, settled.Allocation.Grant);
        var remaining = Reserve(settled.Balance, 45, 2);
        Refused(ProcessDecisionBudgetRefusal.WorkExhausted, () => Reserve(remaining.Balance, 1, 0));
        Refused(ProcessDecisionBudgetRefusal.ReplansExhausted, () => Reserve(remaining.Balance, 0, 1));
    }

    [Fact]
    public void Closed_zero_work_releases_reservation_but_never_erases_allocation_history() {
        var current = Balance();
        for (var index = 0; index < ProcessDecisionBudgetBalance.MaximumAllocations; index++) {
            var reserved = Reserve(current, 100, 4);
            current = ProcessDecisionBudgetTransitions.Settle(reserved.Balance, reserved.Allocation, new(1, new(0, 0))).Balance;
        }
        Assert.Equal(0, current.ConsumedWorkTicks);
        Assert.Equal(0, current.ReservedWorkTicks);
        Assert.Equal(0, current.ConsumedReplans);
        Assert.Equal(0, current.ReservedReplans);
        Assert.Equal(ProcessDecisionBudgetBalance.MaximumAllocations, current.AllocationCount);
        Refused(ProcessDecisionBudgetRefusal.AllocationCapacity, () => Reserve(current, 1, 0));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Exact_reservation_replay_retains_original_open_or_settled_allocation(bool settle) {
        var original = Reserve(Balance(), 30, 2);
        if (settle) {
            original = ProcessDecisionBudgetTransitions.Settle(original.Balance, original.Allocation, new(3, new(12, 1)));
        }
        var later = Reserve(original.Balance, 20, 1);
        var replay = ProcessDecisionBudgetTransitions.Reserve(later.Balance, original.Allocation.Id, original.Allocation.Grant, original.Allocation);
        Assert.False(replay.Changed);
        Assert.Same(later.Balance, replay.Balance);
        Assert.Same(original.Allocation, replay.Allocation);
    }

    [Theory]
    [InlineData(ChangedAllocation.Work)]
    [InlineData(ChangedAllocation.Replans)]
    [InlineData(ChangedAllocation.Identity)]
    public void Reservation_identity_cannot_be_reused_for_changed_intent(ChangedAllocation change) {
        var original = Reserve(Balance(), 30, 2);
        var request = change switch {
            ChangedAllocation.Work => new ProcessDecisionBudgetAmount(31, 2),
            ChangedAllocation.Replans => new(30, 3),
            _ => original.Allocation.Grant
        };
        var identity = change == ChangedAllocation.Identity ? AllocationId() : original.Allocation.Id;
        Refused(ProcessDecisionBudgetRefusal.OriginalAllocationChanged,
            () => ProcessDecisionBudgetTransitions.Reserve(original.Balance, identity, request, original.Allocation));
    }

    [Fact]
    public void Settlement_replay_after_other_work_does_not_credit_unused_allowance_twice() {
        var reserved = Reserve(Balance(), 50, 3);
        var evidence = new ProcessDecisionBudgetSettlement(8, new(20, 1));
        var settled = ProcessDecisionBudgetTransitions.Settle(reserved.Balance, reserved.Allocation, evidence);
        var later = Reserve(settled.Balance, 80, 3);
        var replay = ProcessDecisionBudgetTransitions.Settle(later.Balance, settled.Allocation, evidence);
        Assert.False(replay.Changed);
        Assert.Same(later.Balance, replay.Balance);
        Assert.Same(settled.Allocation, replay.Allocation);
        Refused(ProcessDecisionBudgetRefusal.WorkExhausted, () => Reserve(replay.Balance, 1, 0));
    }

    [Theory]
    [InlineData(9, 20, 1)]
    [InlineData(8, 21, 1)]
    [InlineData(8, 20, 2)]
    public void Original_closed_evidence_cannot_be_replaced(long ownerRevision, long work, int replans) {
        var reserved = Reserve(Balance(), 50, 3);
        var settled = ProcessDecisionBudgetTransitions.Settle(reserved.Balance, reserved.Allocation, new(8, new(20, 1)));
        Refused(ProcessDecisionBudgetRefusal.OriginalSettlementChanged,
            () => ProcessDecisionBudgetTransitions.Settle(settled.Balance, settled.Allocation, new(ownerRevision, new(work, replans))));
    }

    [Theory]
    [InlineData(21, 1)]
    [InlineData(20, 2)]
    [InlineData(120, 7)]
    public void Grant_overrun_is_retained_and_blocks_new_work_while_other_allocations_can_settle(long work, int replans) {
        var first = Reserve(Balance(), 20, 1);
        var second = Reserve(first.Balance, 30, 2);
        var firstEvidence = new ProcessDecisionBudgetSettlement(4, new(work, replans));
        var overrun = ProcessDecisionBudgetTransitions.Settle(second.Balance, first.Allocation, firstEvidence);
        Assert.True(overrun.Balance.OverrunDetected);
        Assert.Equal(work, overrun.Balance.ConsumedWorkTicks);
        Assert.Equal(replans, overrun.Balance.ConsumedReplans);
        Assert.Equal(30, overrun.Balance.ReservedWorkTicks);
        Refused(ProcessDecisionBudgetRefusal.Overrun, () => Reserve(overrun.Balance, 1, 0));
        var closed = ProcessDecisionBudgetTransitions.Settle(overrun.Balance, second.Allocation, new(2, new(10, 1)));
        Assert.True(closed.Balance.OverrunDetected);
        Assert.Equal(work + 10, closed.Balance.ConsumedWorkTicks);
        Assert.Equal(replans + 1, closed.Balance.ConsumedReplans);
        Assert.Equal(0, closed.Balance.ReservedWorkTicks);
        Assert.Equal(0, closed.Balance.ReservedReplans);
        var historical = ProcessDecisionBudgetTransitions.Reserve(closed.Balance, overrun.Allocation.Id, overrun.Allocation.Grant, overrun.Allocation);
        Assert.False(historical.Changed);
        Assert.Equal(firstEvidence, historical.Allocation.Settlement);
        Assert.False(ProcessDecisionBudgetTransitions.Settle(closed.Balance, overrun.Allocation, firstEvidence).Changed);
    }

    [Fact]
    public void Foreign_and_unbacked_allocations_cannot_release_another_decisions_allowance() {
        var original = Reserve(Balance(), 20, 1);
        var foreign = Reserve(Balance(), 20, 1);
        Assert.Throws<InvalidDataException>(() => ProcessDecisionBudgetTransitions.Settle(original.Balance, foreign.Allocation, new(1, new(0, 0))));
        Assert.Throws<InvalidDataException>(() => ProcessDecisionBudgetTransitions.Settle(original.Balance,
            original.Allocation with { Grant = new(21, 1) }, new(1, new(0, 0))));
        Assert.Throws<InvalidDataException>(() => ProcessDecisionBudgetTransitions.Settle(original.Balance,
            original.Allocation with { Settlement = new(1, new(1, 0)) }, new(1, new(1, 0))));
    }

    [Fact]
    public void Exhausted_and_extreme_values_do_not_wrap_or_silently_reset_work() {
        var first = Reserve(Balance(long.MaxValue, int.MaxValue), long.MaxValue - 1, int.MaxValue);
        var consumed = ProcessDecisionBudgetTransitions.Settle(first.Balance, first.Allocation, new(1, first.Allocation.Grant));
        var last = Reserve(consumed.Balance, 1, 0);
        Assert.Throws<OverflowException>(() => ProcessDecisionBudgetTransitions.Settle(last.Balance, last.Allocation, new(1, new(2, 0))));
        Assert.Equal(long.MaxValue - 1, last.Balance.ConsumedWorkTicks);
        Assert.Equal(1, last.Balance.ReservedWorkTicks);
        Assert.Null(last.Allocation.Settlement);
        Assert.Throws<OverflowException>(() => Reserve(Balance() with { Revision = long.MaxValue }, 1, 0));
    }

    [Theory]
    [InlineData(InvalidBalance.NegativeConsumed)]
    [InlineData(InvalidBalance.NegativeReserved)]
    [InlineData(InvalidBalance.UnrecordedOverrun)]
    [InlineData(InvalidBalance.ImpossibleCount)]
    [InlineData(InvalidBalance.ImpossibleRevision)]
    [InlineData(InvalidBalance.Overflow)]
    [InlineData(InvalidBalance.NoAllocations)]
    public void Malformed_retained_balances_fail_before_arithmetic(InvalidBalance change) {
        var current = Reserve(Balance(), 20, 1).Balance;
        current = change switch {
            InvalidBalance.NegativeConsumed => current with { ConsumedWorkTicks = -1 },
            InvalidBalance.NegativeReserved => current with { ReservedReplans = -1 },
            InvalidBalance.UnrecordedOverrun => current with { ConsumedWorkTicks = 101 },
            InvalidBalance.ImpossibleCount => current with { AllocationCount = ProcessDecisionBudgetBalance.MaximumAllocations + 1 },
            InvalidBalance.ImpossibleRevision => current with { Revision = 1 },
            InvalidBalance.Overflow => current with { ConsumedWorkTicks = long.MaxValue, OverrunDetected = true },
            InvalidBalance.NoAllocations => current with { AllocationCount = 0 },
            _ => throw new ArgumentOutOfRangeException(nameof(change))
        };
        Assert.Throws<InvalidDataException>(() => Reserve(current, 1, 0));
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, -1)]
    [InlineData(0, 0)]
    public void Negative_or_empty_reservations_are_not_admitted(long work, int replans)
        => Assert.Throws<InvalidDataException>(() => Reserve(Balance(), work, replans));

    [Fact]
    public void Identities_and_closed_owner_evidence_are_required() {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ProcessDecisionId(Guid.Empty));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ProcessDecisionAllocationId(Guid.Empty));
        Assert.Throws<InvalidDataException>(() => ProcessDecisionBudgetTransitions.Create(default, Ceiling()));
        Assert.Throws<InvalidDataException>(() => ProcessDecisionBudgetTransitions.Reserve(Balance(), default, new(1, 0)));
        var reserved = Reserve(Balance(), 20, 1);
        Assert.Throws<InvalidDataException>(() => ProcessDecisionBudgetTransitions.Settle(reserved.Balance, reserved.Allocation, new(0, new(0, 0))));
        Assert.Throws<InvalidDataException>(() => ProcessDecisionBudgetTransitions.Settle(reserved.Balance, reserved.Allocation, new(1, new(-1, 0))));
    }

    private static ProcessDecisionBudgetBalance Balance(long work = 100, int replans = 4)
        => ProcessDecisionBudgetTransitions.Create(new(Guid.NewGuid()), Ceiling(work, replans));

    private static DecisionDataPolicyBudget Ceiling(long work = 100, int replans = 4)
        => new(replans, TimeSpan.FromTicks(work), TimeSpan.FromMinutes(5));

    private static ProcessDecisionAllocationId AllocationId() => new(Guid.NewGuid());

    private static ProcessDecisionBudgetChange Reserve(ProcessDecisionBudgetBalance current, long work, int replans)
        => ProcessDecisionBudgetTransitions.Reserve(current, AllocationId(), new(work, replans));

    private static void Refused(ProcessDecisionBudgetRefusal reason, Func<ProcessDecisionBudgetChange> action)
        => Assert.Equal(reason, Assert.Throws<ProcessDecisionBudgetException>(action).Reason);

    public enum ChangedAllocation { Work, Replans, Identity }
    public enum InvalidBalance { NegativeConsumed, NegativeReserved, UnrecordedOverrun, ImpossibleCount, ImpossibleRevision, Overflow, NoAllocations }
}
