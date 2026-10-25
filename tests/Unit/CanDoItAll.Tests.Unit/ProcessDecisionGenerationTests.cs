using CanDoItAll.Processes.Runtime;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Tests.Unit;

public sealed class ProcessDecisionGenerationTests {
    [Fact]
    public void Enrolled_children_share_the_generation_material_and_keep_independent_allocations() {
        var initial = Initial();
        var first = Enroll(initial);
        var second = Enroll(first.Generation, ProcessDecisionChildOwner.Workflow);
        Assert.Equal(initial.Reference, first.Child.Reference);
        Assert.Equal(initial.Reference, second.Child.Reference);
        Assert.NotEqual(first.Child.AllocationId, second.Child.AllocationId);
        Assert.NotEqual(first.Child.ChildId, second.Child.ChildId);
        Assert.Equal(initial.MaterialId, second.Generation.MaterialId);
        Assert.Equal(initial.BudgetId, second.Generation.BudgetId);
        Assert.Equal(2, second.Generation.ChildCount);
        Assert.Equal(2, second.Generation.UnclosedChildCount);
        Assert.Equal(3, second.Generation.Revision);
        Assert.Equal(0, initial.ChildCount);
    }

    [Fact]
    public void Unrelated_enrollment_does_not_revoke_an_active_childs_publication_generation() {
        var first = Enroll(Initial());
        var later = Enroll(first.Generation);
        ProcessDecisionTransitions.RequirePublication(later.Generation, first.Child.Reference, first.Child, ProcessDecisionPublicationKind.Dispatch);
        Refused(ProcessDecisionRefusal.StaleExpectation,
            () => ProcessDecisionTransitions.WaitForReview(later.Generation, first.Generation.Expectation));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Exact_enrollment_replay_is_read_only_after_other_children_or_closure(bool close) {
        var original = Enroll(Initial());
        var retained = close ? Close(original.Generation, original.Child) : original;
        var later = Enroll(retained.Generation);
        var replay = ProcessDecisionTransitions.Enroll(later.Generation, later.Generation.Expectation, original.Child, retained.Child);
        Assert.False(replay.Changed);
        Assert.Same(later.Generation, replay.Generation);
        Assert.Same(retained.Child, replay.Child);
    }

    [Theory]
    [InlineData(ChangedChild.Identity)]
    [InlineData(ChangedChild.Owner)]
    [InlineData(ChangedChild.Allocation)]
    [InlineData(ChangedChild.Generation)]
    [InlineData(ChangedChild.Decision)]
    public void An_existing_occurrence_cannot_be_rebound(ChangedChild changed) {
        var original = Enroll(Initial());
        var request = changed switch {
            ChangedChild.Identity => original.Child with { ChildId = ChildId() },
            ChangedChild.Owner => original.Child with { Owner = ProcessDecisionChildOwner.Workflow },
            ChangedChild.Allocation => original.Child with { AllocationId = AllocationId() },
            ChangedChild.Generation => original.Child with { Reference = original.Child.Reference with { Generation = 2 } },
            _ => original.Child with { Reference = original.Child.Reference with { DecisionId = DecisionId() } }
        };
        Refused(ProcessDecisionRefusal.OriginalChildChanged,
            () => ProcessDecisionTransitions.Enroll(original.Generation, original.Generation.Expectation, request, original.Child));
    }

    [Theory]
    [InlineData(ChangedExpectation.Decision)]
    [InlineData(ChangedExpectation.Generation)]
    [InlineData(ChangedExpectation.Revision)]
    public void Every_mutation_requires_the_exact_decision_generation_and_revision(ChangedExpectation changed) {
        var original = Enroll(Initial());
        var current = original.Generation;
        var expected = changed switch {
            ChangedExpectation.Decision => current.Expectation with { Reference = current.Reference with { DecisionId = DecisionId() } },
            ChangedExpectation.Generation => current.Expectation with { Reference = current.Reference with { Generation = 2 } },
            _ => current.Expectation with { Revision = current.Revision - 1 }
        };
        Refused(ProcessDecisionRefusal.StaleExpectation, () => ProcessDecisionTransitions.Enroll(current, expected, Child(current)));
        Refused(ProcessDecisionRefusal.StaleExpectation, () => ProcessDecisionTransitions.CloseChild(current, expected, original.Child, NoEffect()));
        Refused(ProcessDecisionRefusal.StaleExpectation, () => ProcessDecisionTransitions.WaitForReview(current, expected));
        Refused(ProcessDecisionRefusal.StaleExpectation, () => ProcessDecisionTransitions.BeginReconciliation(current, expected));
        Refused(ProcessDecisionRefusal.StaleExpectation, () => ProcessDecisionTransitions.Complete(current, expected));
        Refused(ProcessDecisionRefusal.StaleExpectation, () => ProcessDecisionTransitions.Cancel(current, expected));
        Refused(ProcessDecisionRefusal.StaleExpectation, () => ProcessDecisionTransitions.Advance(current, expected, MaterialId()));
    }

    [Fact]
    public void Quiescence_blocks_new_work_and_both_publication_paths_until_every_child_is_closed() {
        var first = Enroll(Initial());
        var second = Enroll(first.Generation, ProcessDecisionChildOwner.Workflow);
        var reconciling = ProcessDecisionTransitions.BeginReconciliation(second.Generation, second.Generation.Expectation);
        Refused(ProcessDecisionRefusal.InvalidPhase, () => Enroll(reconciling));
        Refused(ProcessDecisionRefusal.InvalidPhase,
            () => ProcessDecisionTransitions.RequirePublication(reconciling, reconciling.Reference, first.Child, ProcessDecisionPublicationKind.Dispatch));
        Refused(ProcessDecisionRefusal.InvalidPhase,
            () => ProcessDecisionTransitions.RequirePublication(reconciling, reconciling.Reference, second.Child, ProcessDecisionPublicationKind.ManagerReview));
        Refused(ProcessDecisionRefusal.UnclosedChildren, () => ProcessDecisionTransitions.Advance(reconciling, reconciling.Expectation, MaterialId()));
        Refused(ProcessDecisionRefusal.UnclosedChildren, () => ProcessDecisionTransitions.Cancel(reconciling, reconciling.Expectation));
        var closedFirst = Close(reconciling, first.Child);
        Assert.Equal(1, closedFirst.Generation.UnclosedChildCount);
        Refused(ProcessDecisionRefusal.UnclosedChildren,
            () => ProcessDecisionTransitions.Advance(closedFirst.Generation, closedFirst.Generation.Expectation, MaterialId()));
        var closedSecond = Close(closedFirst.Generation, second.Child);
        var advanced = ProcessDecisionTransitions.Advance(closedSecond.Generation, closedSecond.Generation.Expectation, MaterialId());
        Assert.Equal(ProcessDecisionPhase.Superseded, advanced.Previous.Phase);
        Assert.Equal(ProcessDecisionPhase.Active, advanced.Current.Phase);
        Assert.Equal(0, advanced.Current.ChildCount);
    }

    [Fact]
    public void An_effect_completed_before_quiescence_remains_a_closed_historical_receipt() {
        var original = Enroll(Initial());
        ProcessDecisionTransitions.RequirePublication(original.Generation, original.Child.Reference, original.Child, ProcessDecisionPublicationKind.Dispatch);
        var evidence = Accepted();
        var closed = ProcessDecisionTransitions.CloseChild(original.Generation, original.Generation.Expectation, original.Child, evidence);
        var quiesced = ProcessDecisionTransitions.BeginReconciliation(closed.Generation, closed.Generation.Expectation);
        var successor = ProcessDecisionTransitions.Advance(quiesced, quiesced.Expectation, MaterialId());
        Assert.Equal(evidence, closed.Child.Closure);
        Assert.Equal(original.Child.Reference, closed.Child.Reference);
        Assert.Equal(1, successor.Previous.ChildCount);
        Assert.Equal(0, successor.Previous.UnclosedChildCount);
        Assert.Equal(original.Generation.MaterialId, successor.Previous.MaterialId);
        Assert.Equal(original.Generation.BudgetId, successor.Current.BudgetId);
        Assert.Equal(original.Generation.Reference.DecisionId, successor.Current.Reference.DecisionId);
        Assert.Equal(original.Generation.Reference.Generation + 1, successor.Current.Reference.Generation);
        Assert.Equal(quiesced.Revision + 1, successor.Current.Revision);
        Refused(ProcessDecisionRefusal.StaleExpectation,
            () => ProcessDecisionTransitions.RequirePublication(successor.Current, original.Child.Reference, closed.Child, ProcessDecisionPublicationKind.Dispatch));
    }

    [Fact]
    public void Review_blocks_dispatch_without_losing_the_original_child_or_material() {
        var original = Enroll(Initial());
        var waiting = ProcessDecisionTransitions.WaitForReview(original.Generation, original.Generation.Expectation);
        Assert.Equal(original.Generation.MaterialId, waiting.MaterialId);
        Assert.Equal(original.Generation.BudgetId, waiting.BudgetId);
        Assert.Equal(original.Generation.ChildCount, waiting.ChildCount);
        Refused(ProcessDecisionRefusal.InvalidPhase, () => Enroll(waiting));
        Refused(ProcessDecisionRefusal.InvalidPhase,
            () => ProcessDecisionTransitions.RequirePublication(waiting, waiting.Reference, original.Child, ProcessDecisionPublicationKind.Dispatch));
        ProcessDecisionTransitions.RequirePublication(waiting, waiting.Reference, original.Child, ProcessDecisionPublicationKind.ManagerReview);
        Refused(ProcessDecisionRefusal.InvalidPhase,
            () => ProcessDecisionTransitions.RequirePublication(original.Generation, original.Child.Reference, original.Child, ProcessDecisionPublicationKind.ManagerReview));
        Refused(ProcessDecisionRefusal.UnclosedChildren, () => ProcessDecisionTransitions.Complete(waiting, waiting.Expectation));
        var closed = Close(waiting, original.Child);
        var completed = ProcessDecisionTransitions.Complete(closed.Generation, closed.Generation.Expectation);
        Assert.Equal(ProcessDecisionPhase.Completed, completed.Phase);
    }

    [Fact]
    public void Reconciliation_from_review_preserves_pending_work_until_owner_closure() {
        var original = Enroll(Initial());
        var waiting = ProcessDecisionTransitions.WaitForReview(original.Generation, original.Generation.Expectation);
        var reconciling = ProcessDecisionTransitions.BeginReconciliation(waiting, waiting.Expectation);
        Assert.Equal(1, reconciling.UnclosedChildCount);
        var closed = Close(reconciling, original.Child);
        var cancelled = ProcessDecisionTransitions.Cancel(closed.Generation, closed.Generation.Expectation);
        Assert.Equal(ProcessDecisionPhase.Cancelled, cancelled.Phase);
        Assert.Equal(original.Generation.BudgetId, cancelled.BudgetId);
    }

    [Fact]
    public void Exact_closure_replay_cannot_decrement_another_open_child() {
        var original = Enroll(Initial());
        var later = Enroll(original.Generation);
        var closed = Close(later.Generation, original.Child);
        var replay = ProcessDecisionTransitions.CloseChild(closed.Generation, closed.Generation.Expectation, closed.Child, closed.Child.Closure!);
        Assert.False(replay.Changed);
        Assert.Same(closed.Generation, replay.Generation);
        Assert.Same(closed.Child, replay.Child);
        Assert.Equal(1, replay.Generation.UnclosedChildCount);
        Refused(ProcessDecisionRefusal.ChildNotOpen,
            () => ProcessDecisionTransitions.RequirePublication(replay.Generation, replay.Child.Reference, replay.Child, ProcessDecisionPublicationKind.Dispatch));
    }

    [Theory]
    [InlineData(ChangedClosure.OwnerRevision)]
    [InlineData(ChangedClosure.Outcome)]
    [InlineData(ChangedClosure.Receipt)]
    public void Closed_owner_evidence_cannot_be_replaced(ChangedClosure changed) {
        var original = Enroll(Initial());
        var closure = Accepted();
        var closed = ProcessDecisionTransitions.CloseChild(original.Generation, original.Generation.Expectation, original.Child, closure);
        var replacement = changed switch {
            ChangedClosure.OwnerRevision => closure with { OwnerRevision = closure.OwnerRevision + 1 },
            ChangedClosure.Outcome => NoEffect(),
            _ => closure with { AcceptedReceipt = new(Guid.NewGuid()) }
        };
        Refused(ProcessDecisionRefusal.OriginalClosureChanged,
            () => ProcessDecisionTransitions.CloseChild(closed.Generation, closed.Generation.Expectation, closed.Child, replacement));
    }

    [Fact]
    public void Child_capacity_includes_closed_children_and_cannot_be_recovered_by_zero_effect_retries() {
        var current = Initial();
        for (var index = 0; index < ProcessDecisionGeneration.MaximumChildren; index++) {
            var enrolled = Enroll(current);
            current = Close(enrolled.Generation, enrolled.Child).Generation;
        }
        Assert.Equal(ProcessDecisionGeneration.MaximumChildren, current.ChildCount);
        Assert.Equal(0, current.UnclosedChildCount);
        Refused(ProcessDecisionRefusal.ChildCapacity, () => Enroll(current));
    }

    [Fact]
    public void Generation_capacity_and_budget_identity_survive_every_refresh() {
        var original = Initial();
        var current = original;
        for (var index = 1; index < ProcessDecisionGeneration.MaximumGenerations; index++) {
            current = ProcessDecisionTransitions.BeginReconciliation(current, current.Expectation);
            var next = ProcessDecisionTransitions.Advance(current, current.Expectation, MaterialId());
            next.Previous.Validate();
            next.Current.Validate();
            Assert.Equal(current.MaterialId, next.Previous.MaterialId);
            Assert.NotEqual(current.MaterialId, next.Current.MaterialId);
            Assert.Equal(original.BudgetId, next.Current.BudgetId);
            current = next.Current;
        }
        current = ProcessDecisionTransitions.BeginReconciliation(current, current.Expectation);
        Refused(ProcessDecisionRefusal.GenerationCapacity, () => ProcessDecisionTransitions.Advance(current, current.Expectation, MaterialId()));
        Assert.Equal(ProcessDecisionPhase.Cancelled, ProcessDecisionTransitions.Cancel(current, current.Expectation).Phase);
    }

    [Theory]
    [InlineData(ProcessDecisionPhase.Completed)]
    [InlineData(ProcessDecisionPhase.Cancelled)]
    [InlineData(ProcessDecisionPhase.Superseded)]
    public void Terminal_generations_cannot_restart_or_authorize_more_work(ProcessDecisionPhase phase) {
        var original = Initial();
        var current = original with { Phase = phase, Revision = 3 };
        current.Validate();
        Refused(ProcessDecisionRefusal.InvalidPhase, () => Enroll(current));
        Refused(ProcessDecisionRefusal.InvalidPhase, () => ProcessDecisionTransitions.WaitForReview(current, current.Expectation));
        Refused(ProcessDecisionRefusal.InvalidPhase, () => ProcessDecisionTransitions.BeginReconciliation(current, current.Expectation));
        Refused(ProcessDecisionRefusal.InvalidPhase, () => ProcessDecisionTransitions.Complete(current, current.Expectation));
        Refused(ProcessDecisionRefusal.InvalidPhase, () => ProcessDecisionTransitions.Cancel(current, current.Expectation));
        Refused(ProcessDecisionRefusal.InvalidPhase, () => ProcessDecisionTransitions.Advance(current, current.Expectation, MaterialId()));
    }

    [Fact]
    public void Enrollment_does_not_accept_asserted_completion_or_unbacked_retained_children() {
        var original = Initial();
        var child = Child(original);
        Assert.Throws<InvalidDataException>(() => ProcessDecisionTransitions.Enroll(original, original.Expectation, child with { Closure = NoEffect() }));
        Assert.Throws<InvalidDataException>(() => ProcessDecisionTransitions.Enroll(original, original.Expectation, child, child));
        Assert.Throws<InvalidDataException>(() => ProcessDecisionTransitions.CloseChild(original, original.Expectation, child, NoEffect()));
        Assert.Throws<InvalidDataException>(() => ProcessDecisionTransitions.RequirePublication(original, original.Reference, child, ProcessDecisionPublicationKind.Dispatch));
    }

    [Fact]
    public void Invalid_retained_heads_are_rejected_before_transition() {
        var original = Initial();
        ProcessDecisionGeneration[] invalid = [
            original with { Reference = original.Reference with { DecisionId = default } },
            original with { Reference = original.Reference with { Generation = 0 } },
            original with { Reference = original.Reference with { Generation = ProcessDecisionGeneration.MaximumGenerations + 1 } },
            original with { Revision = 0 },
            original with { Reference = original.Reference with { Generation = 2 } },
            original with { Phase = (ProcessDecisionPhase)100 },
            original with { MaterialId = default },
            original with { BudgetId = default },
            original with { ChildCount = -1 },
            original with { Revision = 1000, ChildCount = ProcessDecisionGeneration.MaximumChildren + 1 },
            original with { UnclosedChildCount = 1 },
            original with { UnclosedChildCount = -1 },
            original with { ChildCount = 1 },
            original with { Revision = 2, ChildCount = 1, UnclosedChildCount = 1, Phase = ProcessDecisionPhase.Completed },
            original with { Revision = 1000, Reference = original.Reference with { Generation = ProcessDecisionGeneration.MaximumGenerations }, Phase = ProcessDecisionPhase.Superseded }
        ];
        foreach (var current in invalid) {
            Assert.Throws<InvalidDataException>(() => current.Validate());
            Assert.Throws<InvalidDataException>(() => ProcessDecisionTransitions.BeginReconciliation(current, current.Expectation));
        }
    }

    [Fact]
    public void Invalid_child_and_closure_shapes_are_not_owner_evidence() {
        var original = Enroll(Initial());
        ProcessDecisionChild[] invalid = [
            original.Child with { ChildId = default },
            original.Child with { Owner = (ProcessDecisionChildOwner)100 },
            original.Child with { AllocationId = default }
        ];
        foreach (var child in invalid) {
            Assert.Throws<InvalidDataException>(() => ProcessDecisionTransitions.CloseChild(original.Generation, original.Generation.Expectation, child, NoEffect()));
        }
        ProcessDecisionChildClosure[] closures = [
            new(0, ProcessDecisionChildOutcome.NoEffect, null),
            new(1, (ProcessDecisionChildOutcome)100, null),
            new(1, ProcessDecisionChildOutcome.Accepted, null),
            new(1, ProcessDecisionChildOutcome.Accepted, default(ProcessDecisionEffectReceiptId)),
            new(1, ProcessDecisionChildOutcome.NoEffect, new(Guid.NewGuid()))
        ];
        foreach (var closure in closures) {
            Assert.Throws<InvalidDataException>(() => ProcessDecisionTransitions.CloseChild(original.Generation, original.Generation.Expectation, original.Child, closure));
        }
        Assert.Throws<InvalidDataException>(() => ProcessDecisionTransitions.RequirePublication(original.Generation,
            original.Child.Reference, original.Child, (ProcessDecisionPublicationKind)100));
    }

    [Fact]
    public void Revision_overflow_never_returns_a_changed_generation_or_child() {
        var original = Enroll(Initial());
        var current = original.Generation with { Revision = long.MaxValue };
        Assert.Throws<OverflowException>(() => Enroll(current));
        Assert.Throws<OverflowException>(() => Close(current, original.Child));
        Assert.Throws<OverflowException>(() => ProcessDecisionTransitions.WaitForReview(current, current.Expectation));
        Assert.Throws<OverflowException>(() => ProcessDecisionTransitions.BeginReconciliation(current, current.Expectation));
        var closed = current with { UnclosedChildCount = 0 };
        Assert.Throws<OverflowException>(() => ProcessDecisionTransitions.Complete(closed, closed.Expectation));
        var reconciling = closed with { Phase = ProcessDecisionPhase.Reconciling };
        Assert.Throws<OverflowException>(() => ProcessDecisionTransitions.Cancel(reconciling, reconciling.Expectation));
        Assert.Throws<OverflowException>(() => ProcessDecisionTransitions.Advance(reconciling, reconciling.Expectation, MaterialId()));
        Assert.Equal(1, current.UnclosedChildCount);
        Assert.Null(original.Child.Closure);
    }

    private static ProcessDecisionGeneration Initial() => ProcessDecisionTransitions.Create(DecisionId(), MaterialId(), new(Guid.NewGuid()));
    private static ProcessDecisionId DecisionId() => new(Guid.NewGuid());
    private static ProcessDecisionMaterialId MaterialId() => new(Guid.NewGuid());
    private static ProcessDecisionChildId ChildId() => new(Guid.NewGuid());
    private static DecisionBudgetAllocationId AllocationId() => new(Guid.NewGuid());
    private static ProcessDecisionChild Child(ProcessDecisionGeneration current, ProcessDecisionChildOwner owner = ProcessDecisionChildOwner.Agent)
        => new(current.Reference, ChildId(), owner, AllocationId());
    private static ProcessDecisionChildChange Enroll(ProcessDecisionGeneration current, ProcessDecisionChildOwner owner = ProcessDecisionChildOwner.Agent)
        => ProcessDecisionTransitions.Enroll(current, current.Expectation, Child(current, owner));
    private static ProcessDecisionChildChange Close(ProcessDecisionGeneration current, ProcessDecisionChild child)
        => ProcessDecisionTransitions.CloseChild(current, current.Expectation, child, NoEffect());
    private static ProcessDecisionChildClosure NoEffect() => new(7, ProcessDecisionChildOutcome.NoEffect, null);
    private static ProcessDecisionChildClosure Accepted() => new(9, ProcessDecisionChildOutcome.Accepted, new(Guid.NewGuid()));
    private static void Refused(ProcessDecisionRefusal reason, Action action)
        => Assert.Equal(reason, Assert.Throws<ProcessDecisionException>(action).Reason);

    public enum ChangedChild { Identity, Owner, Allocation, Generation, Decision }
    public enum ChangedExpectation { Decision, Generation, Revision }
    public enum ChangedClosure { OwnerRevision, Outcome, Receipt }
}
