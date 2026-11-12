using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Tests.Unit;

public sealed class ProcessDecisionRefreshCaptureJournalTests {
    private static readonly ProcessDecisionCaptureInvocationId Invocation = new(ProcessDecisionReviewTestData.Id(20));

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    [InlineData(false, true)]
    public void Captured_work_advances_once_with_original_budget_and_material(bool root, bool shared) {
        var (reservation, payload, budget) = Create(root, shared);
        var reserved = ProcessDecisionRefreshCaptureJournal.Reserve(reservation);
        Assert.True(reserved.Changed);
        Assert.Equal(ProcessDecisionRefreshCapturePhase.Reserved, reserved.State.Phase);
        Assert.False(ProcessDecisionRefreshCaptureJournal.Reserve(reservation, reserved.State).Changed);
        var begun = ProcessDecisionRefreshCaptureJournal.Begin(reserved.State, Invocation);
        Assert.True(begun.Changed);
        Assert.False(ProcessDecisionRefreshCaptureJournal.Begin(begun.State, Invocation).Changed);
        var returned = Captured(reservation, payload);
        var recorded = ProcessDecisionRefreshCaptureJournal.Return(begun.State, Invocation, returned);
        Assert.Equal(ProcessDecisionRunScopeMaterial.From(payload), recorded.State.Outcome!.Material);
        var closed = DecisionBudgetTransitions.Settle(budget.Balance, budget.Allocation, returned.Outcome.Work);
        var settled = ProcessDecisionRefreshCaptureJournal.Settle(recorded.State, closed, ProcessDecisionRefreshCaptureDisposition.Advanced);
        Assert.True(settled.Changed);
        Assert.Equal(4, settled.State.Revision);
        Assert.Equal(ProcessDecisionRefreshCaptureDisposition.Advanced, settled.State.Disposition);
        Assert.Equal(TimeSpan.FromSeconds(4).Ticks, closed.Balance.ConsumedWorkTicks);
        Assert.Equal(0, closed.Balance.ReservedWorkTicks);
        Assert.Equal(1, closed.Balance.AllocationCount);
        var budgetReplay = DecisionBudgetTransitions.Settle(closed.Balance, closed.Allocation, returned.Outcome.Work);
        Assert.False(budgetReplay.Changed);
        Assert.False(ProcessDecisionRefreshCaptureJournal.Settle(settled.State, budgetReplay, ProcessDecisionRefreshCaptureDisposition.Advanced).Changed);
        Assert.False(ProcessDecisionRefreshCaptureJournal.Return(settled.State, Invocation, returned).Changed);
        Assert.False(ProcessDecisionRefreshCaptureJournal.Begin(settled.State, Invocation).Changed);
        Assert.False(ProcessDecisionRefreshCaptureJournal.Reserve(reservation, settled.State).Changed);
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    [InlineData(false, true)]
    public void Cancellation_before_invocation_can_close_zero_work_and_prevents_later_invocation(bool root, bool shared) {
        var (reservation, _, budget) = Create(root, shared);
        var reserved = ProcessDecisionRefreshCaptureJournal.Reserve(reservation).State;
        var returned = ProcessDecisionRefreshCaptureJournal.CloseNeverInvoked(reserved);
        Assert.True(returned.Changed);
        Assert.Null(returned.State.Invocation);
        Assert.Equal(ProcessDecisionRefreshCaptureOutcomeKind.NeverInvoked, returned.State.Outcome!.Kind);
        Assert.Equal(0, returned.State.Outcome.Work.Consumed.WorkTicks);
        Refused(() => ProcessDecisionRefreshCaptureJournal.Begin(returned.State, Invocation), ProcessDecisionRefreshCaptureRefusal.InvalidPhase);
        var closed = DecisionBudgetTransitions.Settle(budget.Balance, budget.Allocation, returned.State.Outcome.Work);
        var settled = ProcessDecisionRefreshCaptureJournal.Settle(returned.State, closed, ProcessDecisionRefreshCaptureDisposition.Aborted);
        Assert.Equal(3, settled.State.Revision);
        Assert.Equal(0, closed.Balance.ConsumedWorkTicks);
        Assert.Equal(0, closed.Balance.ReservedWorkTicks);
        Assert.False(ProcessDecisionRefreshCaptureJournal.CloseNeverInvoked(settled.State).Changed);
        Refused(() => ProcessDecisionRefreshCaptureJournal.Begin(settled.State, Invocation), ProcessDecisionRefreshCaptureRefusal.InvalidPhase);
    }

    [Theory]
    [InlineData(ProcessDecisionRefreshCaptureOutcomeKind.Refused)]
    [InlineData(ProcessDecisionRefreshCaptureOutcomeKind.Expired)]
    [InlineData(ProcessDecisionRefreshCaptureOutcomeKind.DeadlineExceeded)]
    [InlineData(ProcessDecisionRefreshCaptureOutcomeKind.Cancelled)]
    [InlineData(ProcessDecisionRefreshCaptureOutcomeKind.Faulted)]
    public void Known_failures_settle_actual_work_including_overrun_without_publishing(ProcessDecisionRefreshCaptureOutcomeKind kind) {
        var (reservation, _, budget) = Create();
        var invoking = Start(reservation);
        var duration = TimeSpan.FromSeconds(kind == ProcessDecisionRefreshCaptureOutcomeKind.DeadlineExceeded ? 11 : 4);
        ProcessDecisionRefreshCaptureReturned returned = new(new(kind, new(1, new(duration.Ticks, 0))));
        var recorded = ProcessDecisionRefreshCaptureJournal.Return(invoking, Invocation, returned).State;
        Assert.Null(recorded.Outcome!.Material);
        var closed = DecisionBudgetTransitions.Settle(budget.Balance, budget.Allocation, recorded.Outcome.Work);
        Assert.Equal(duration.Ticks, closed.Balance.ConsumedWorkTicks);
        Assert.Equal(duration > ProcessDecisionRefreshCaptureReservation.MaximumWork, closed.Balance.OverrunDetected);
        Refused(() => ProcessDecisionRefreshCaptureJournal.Settle(recorded, closed, ProcessDecisionRefreshCaptureDisposition.Advanced),
            ProcessDecisionRefreshCaptureRefusal.OriginalSettlementChanged);
        var settled = ProcessDecisionRefreshCaptureJournal.Settle(recorded, closed, ProcessDecisionRefreshCaptureDisposition.Aborted);
        Assert.True(settled.Changed);
        Assert.Equal(ProcessDecisionRefreshCaptureDisposition.Aborted, settled.State.Disposition);
        Assert.Equal(0, closed.Balance.ReservedWorkTicks);
    }

    public enum ReservationChange { Run, Admission, Reservation, Budget, Allocation, Grant, Material, Input }

    [Theory]
    [InlineData(ReservationChange.Run)]
    [InlineData(ReservationChange.Admission)]
    [InlineData(ReservationChange.Reservation)]
    [InlineData(ReservationChange.Budget)]
    [InlineData(ReservationChange.Allocation)]
    [InlineData(ReservationChange.Grant)]
    [InlineData(ReservationChange.Material)]
    [InlineData(ReservationChange.Input)]
    public void Reservation_replay_cannot_replace_original_identity_or_grant(ReservationChange change) {
        var (reservation, _, _) = Create(shared: true);
        var retained = ProcessDecisionRefreshCaptureJournal.Reserve(reservation).State;
        var other = ProcessDecisionReviewTestData.Id(30);
        var changed = change switch {
            ReservationChange.Run => reservation with { RunId = new(other) },
            ReservationChange.Admission => reservation with { AdmissionId = new(other) },
            ReservationChange.Reservation => reservation with { ReservationId = new(other) },
            ReservationChange.Budget => reservation with { Budget = reservation.Budget with { Expectation = reservation.Budget.Expectation with { BudgetId = new(other) } } },
            ReservationChange.Allocation => reservation with { Budget = reservation.Budget with { AllocationId = new(other) } },
            ReservationChange.Grant => reservation with { Budget = reservation.Budget with { Grant = new(TimeSpan.FromSeconds(5).Ticks, 0) } },
            ReservationChange.Material => reservation with { Material = reservation.Material with { MaterialId = new(other) } },
            ReservationChange.Input => reservation with { InputFingerprint = ProcessDecisionReviewTestData.Hash('b') },
            _ => throw new ArgumentOutOfRangeException(nameof(change))
        };
        changed.Validate();
        Refused(() => ProcessDecisionRefreshCaptureJournal.Reserve(changed, retained), ProcessDecisionRefreshCaptureRefusal.OriginalReservationChanged);
    }

    public enum StateCorruption { ReservedInvocation, ReservedOutcome, InvokingMissingInvocation, InvokingOutcome, ReturnedMissingOutcome, SettledMissingDisposition, Revision, UnknownPhase }

    [Theory]
    [InlineData(StateCorruption.ReservedInvocation)]
    [InlineData(StateCorruption.ReservedOutcome)]
    [InlineData(StateCorruption.InvokingMissingInvocation)]
    [InlineData(StateCorruption.InvokingOutcome)]
    [InlineData(StateCorruption.ReturnedMissingOutcome)]
    [InlineData(StateCorruption.SettledMissingDisposition)]
    [InlineData(StateCorruption.Revision)]
    [InlineData(StateCorruption.UnknownPhase)]
    public void Invalid_retained_phase_cannot_be_replayed_or_repaired_by_invoking(StateCorruption corruption) {
        var (reservation, payload, _) = Create();
        var reserved = ProcessDecisionRefreshCaptureJournal.Reserve(reservation).State;
        var invoking = ProcessDecisionRefreshCaptureJournal.Begin(reserved, Invocation).State;
        var returned = ProcessDecisionRefreshCaptureJournal.Return(invoking, Invocation, Captured(reservation, payload)).State;
        var broken = corruption switch {
            StateCorruption.ReservedInvocation => reserved with { Invocation = Invocation },
            StateCorruption.ReservedOutcome => reserved with { Outcome = returned.Outcome },
            StateCorruption.InvokingMissingInvocation => invoking with { Invocation = null },
            StateCorruption.InvokingOutcome => invoking with { Outcome = returned.Outcome },
            StateCorruption.ReturnedMissingOutcome => returned with { Outcome = null },
            StateCorruption.SettledMissingDisposition => returned with { Phase = ProcessDecisionRefreshCapturePhase.Settled, Revision = 4 },
            StateCorruption.Revision => returned with { Revision = 4 },
            StateCorruption.UnknownPhase => reserved with { Phase = (ProcessDecisionRefreshCapturePhase)99 },
            _ => throw new ArgumentOutOfRangeException(nameof(corruption))
        };
        Assert.Throws<InvalidDataException>(() => ProcessDecisionRefreshCaptureJournal.Reserve(reservation, broken));
        Assert.Throws<InvalidDataException>(() => ProcessDecisionRefreshCaptureJournal.Begin(broken, Invocation));
    }

    [Fact]
    public void Exact_return_replays_but_a_different_invocation_never_inherits_permission() {
        var (reservation, payload, _) = Create();
        var invoking = Start(reservation);
        var returned = Captured(reservation, payload);
        var recorded = ProcessDecisionRefreshCaptureJournal.Return(invoking, Invocation, returned).State;
        Assert.False(ProcessDecisionRefreshCaptureJournal.Return(recorded, Invocation, Captured(reservation, payload)).Changed);
        ProcessDecisionCaptureInvocationId replacement = new(ProcessDecisionReviewTestData.Id(21));
        Refused(() => ProcessDecisionRefreshCaptureJournal.Begin(invoking, replacement), ProcessDecisionRefreshCaptureRefusal.OriginalInvocationChanged);
        Refused(() => ProcessDecisionRefreshCaptureJournal.Return(recorded, replacement, returned), ProcessDecisionRefreshCaptureRefusal.OriginalInvocationChanged);
    }

    public enum ReturnChange { Work, CaptureTime, Payload, Kind }

    [Theory]
    [InlineData(ReturnChange.Work)]
    [InlineData(ReturnChange.CaptureTime)]
    [InlineData(ReturnChange.Payload)]
    [InlineData(ReturnChange.Kind)]
    public void Returned_material_or_work_cannot_be_replaced_by_a_later_observation(ReturnChange change) {
        var (reservation, payload, _) = Create();
        var original = Captured(reservation, payload);
        var recorded = ProcessDecisionRefreshCaptureJournal.Return(Start(reservation), Invocation, original).State;
        var replacement = change switch {
            ReturnChange.Work => original with { Outcome = original.Outcome with { Work = new(1, new(TimeSpan.FromSeconds(5).Ticks, 0)) } },
            ReturnChange.CaptureTime => Captured(reservation, ProcessDecisionMaterial.FromContent(reservation.Scope, reservation.Material,
                reservation.ContentContract, payload.CapturedAtUtc.AddSeconds(1), payload.PayloadJson)),
            ReturnChange.Payload => Captured(reservation, ProcessDecisionMaterial.FromContent(reservation.Scope, reservation.Material,
                reservation.ContentContract, payload.CapturedAtUtc, "{\"changed\":true}")),
            ReturnChange.Kind => new ProcessDecisionRefreshCaptureReturned(new(ProcessDecisionRefreshCaptureOutcomeKind.Refused, original.Outcome.Work)),
            _ => throw new ArgumentOutOfRangeException(nameof(change))
        };
        replacement.Validate(reservation);
        Refused(() => ProcessDecisionRefreshCaptureJournal.Return(recorded, Invocation, replacement), ProcessDecisionRefreshCaptureRefusal.OriginalOutcomeChanged);
    }

    [Fact]
    public void Never_invoked_evidence_cannot_claim_measured_work() {
        var (reservation, _, _) = Create();
        ProcessDecisionRefreshCaptureOutcome invented = new(ProcessDecisionRefreshCaptureOutcomeKind.NeverInvoked, new(1, new(1, 0)));
        Assert.Throws<InvalidDataException>(() => invented.Validate(reservation));
    }

    [Fact]
    public void Unknown_invocation_keeps_the_grant_and_cannot_be_refunded_or_invoked_again() {
        var (reservation, _, budget) = Create();
        var unknown = Start(reservation);
        Refused(() => ProcessDecisionRefreshCaptureJournal.CloseNeverInvoked(unknown), ProcessDecisionRefreshCaptureRefusal.InvalidPhase);
        var invented = DecisionBudgetTransitions.Settle(budget.Balance, budget.Allocation, new(1, new(0, 0)));
        Refused(() => ProcessDecisionRefreshCaptureJournal.Settle(unknown, invented, ProcessDecisionRefreshCaptureDisposition.Aborted),
            ProcessDecisionRefreshCaptureRefusal.OriginalSettlementChanged);
        Assert.Null(unknown.Outcome);
        Assert.Null(budget.Allocation.Settlement);
        Assert.Equal(reservation.Budget.Grant.WorkTicks, budget.Balance.ReservedWorkTicks);
        Assert.False(ProcessDecisionRefreshCaptureJournal.Begin(unknown, Invocation).Changed);
    }

    [Fact]
    public void Settlement_must_close_the_same_allocation_and_exact_returned_work() {
        var (reservation, payload, budget) = Create();
        var returned = ProcessDecisionRefreshCaptureJournal.Return(Start(reservation), Invocation, Captured(reservation, payload)).State;
        var closed = DecisionBudgetTransitions.Settle(budget.Balance, budget.Allocation, returned.Outcome!.Work);
        foreach (var changed in new[] {
            closed with { Allocation = closed.Allocation with { Id = new(ProcessDecisionReviewTestData.Id(31)) } },
            closed with { Allocation = closed.Allocation with { Grant = new(TimeSpan.FromSeconds(5).Ticks, 0) } },
            DecisionBudgetTransitions.Settle(budget.Balance, budget.Allocation, new(1, new(TimeSpan.FromSeconds(3).Ticks, 0))),
            closed with { Changed = false }
        }) {
            Refused(() => ProcessDecisionRefreshCaptureJournal.Settle(returned, changed, ProcessDecisionRefreshCaptureDisposition.Advanced),
                ProcessDecisionRefreshCaptureRefusal.OriginalSettlementChanged);
        }
        var settled = ProcessDecisionRefreshCaptureJournal.Settle(returned, closed, ProcessDecisionRefreshCaptureDisposition.Advanced).State;
        Refused(() => ProcessDecisionRefreshCaptureJournal.Settle(settled, closed, ProcessDecisionRefreshCaptureDisposition.Aborted),
            ProcessDecisionRefreshCaptureRefusal.OriginalSettlementChanged);
    }

    private static ProcessDecisionRefreshCaptureState Start(ProcessDecisionRefreshCaptureReservation reservation)
        => ProcessDecisionRefreshCaptureJournal.Begin(ProcessDecisionRefreshCaptureJournal.Reserve(reservation).State, Invocation).State;

    private static ProcessDecisionRefreshCaptureReturned Captured(ProcessDecisionRefreshCaptureReservation reservation, ProcessDecisionMaterial material)
        => new(new(ProcessDecisionRefreshCaptureOutcomeKind.Captured, new(1, new(TimeSpan.FromSeconds(4).Ticks, 0)),
            ProcessDecisionRunScopeMaterial.From(material)), reservation.InheritsMaterial ? null : material);

    private static void Refused(Action action, ProcessDecisionRefreshCaptureRefusal reason)
        => Assert.Equal(reason, Assert.Throws<ProcessDecisionRefreshCaptureException>(action).Reason);

    private static (ProcessDecisionRefreshCaptureReservation Reservation, ProcessDecisionMaterial Payload, DecisionBudgetChange Budget) Create(bool root = false, bool shared = false) {
        ProcessRunId rootId = new(ProcessDecisionReviewTestData.Id(1));
        ProcessRunId run = root ? rootId : new(ProcessDecisionReviewTestData.Id(2));
        ProcessStepInstanceId step = new(ProcessDecisionReviewTestData.Id(3));
        ProcessDecisionMaterialScope scope = new(new(ProcessDecisionReviewTestData.Id(4)), ProcessDecisionReviewTestData.Id(5),
            ProcessDecisionReviewTestData.Id(6), rootId, shared ? rootId : run, shared ? null : step);
        ProcessDecisionContentContract content = new(ProcessDecisionReviewTestData.Id(7), 1);
        ProcessDecisionMaterialReference reference = new(scope.Id, 2, new(ProcessDecisionReviewTestData.Id(8)));
        var payload = ProcessDecisionMaterial.FromContent(scope, reference, content, ProcessDecisionReviewTestData.Now, "{}");
        var balance = DecisionBudgetTransitions.Create(new(ProcessDecisionReviewTestData.Id(9)), new(0, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5)));
        ProcessDecisionBudgetReservation command = new(new(root ? null : step, balance.BudgetId, balance.Revision),
            new(ProcessDecisionReviewTestData.Id(10)), new(ProcessDecisionRefreshCaptureReservation.MaximumWork.Ticks, 0));
        var budget = DecisionBudgetTransitions.Reserve(balance, command.AllocationId, command.Grant);
        ProcessDecisionRefreshCaptureReservation reservation = new(run, new(ProcessDecisionReviewTestData.Id(11)), new(ProcessDecisionReviewTestData.Id(12)),
            command, scope, content, reference, ProcessDecisionReviewTestData.Hash('a'));
        reservation.Validate();
        return (reservation, payload, budget);
    }
}
