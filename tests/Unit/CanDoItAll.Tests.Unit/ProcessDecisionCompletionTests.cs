using System.Text.Json.Nodes;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Tests.Unit;

public sealed class ProcessDecisionCompletionTests {
    [Theory]
    [InlineData(ProcessDecisionPhase.Active, ProcessDecisionCompletion.Complete, false)]
    [InlineData(ProcessDecisionPhase.Active, ProcessDecisionCompletion.Complete, true)]
    [InlineData(ProcessDecisionPhase.WaitingForReview, ProcessDecisionCompletion.Complete, false)]
    [InlineData(ProcessDecisionPhase.WaitingForReview, ProcessDecisionCompletion.Complete, true)]
    [InlineData(ProcessDecisionPhase.Reconciling, ProcessDecisionCompletion.Cancel, false)]
    [InlineData(ProcessDecisionPhase.Reconciling, ProcessDecisionCompletion.Cancel, true)]
    public void Last_child_and_parent_complete_in_one_revision_without_losing_original_effect(
        ProcessDecisionPhase phase, ProcessDecisionCompletion completion, bool accepted) {
        var original = Enrolled(phase);
        var closure = Closure(accepted);
        var changed = ProcessDecisionTransitions.CloseChild(original.Generation, original.Generation.Expectation,
            original.Child, closure, completion);
        changed.Generation.Validate();
        Assert.True(changed.Changed);
        Assert.Equal(original.Generation.Revision + 1, changed.Generation.Revision);
        Assert.Equal(completion == ProcessDecisionCompletion.Complete ? ProcessDecisionPhase.Completed : ProcessDecisionPhase.Cancelled,
            changed.Generation.Phase);
        Assert.Equal(0, changed.Generation.UnclosedChildCount);
        Assert.Equal(original.Generation.MaterialId, changed.Generation.MaterialId);
        Assert.Equal(original.Generation.BudgetId, changed.Generation.BudgetId);
        Assert.Equal(original.Generation.Reference, changed.Generation.Reference);
        Assert.Equal(original.Child with { Closure = closure }, changed.Child);
    }

    [Theory]
    [InlineData(ProcessDecisionPhase.Active, ProcessDecisionCompletion.Cancel)]
    [InlineData(ProcessDecisionPhase.WaitingForReview, ProcessDecisionCompletion.Cancel)]
    [InlineData(ProcessDecisionPhase.Reconciling, ProcessDecisionCompletion.Complete)]
    public void Completion_requires_its_original_phase(ProcessDecisionPhase phase, ProcessDecisionCompletion completion) {
        var original = Enrolled(phase);
        var failure = Assert.Throws<ProcessDecisionException>(() => ProcessDecisionTransitions.CloseChild(
            original.Generation, original.Generation.Expectation, original.Child, Closure(false), completion));
        Assert.Equal(ProcessDecisionRefusal.InvalidPhase, failure.Reason);
        Assert.Null(original.Child.Closure);
    }

    [Fact]
    public void Another_open_child_blocks_parent_completion() {
        var first = Enrolled();
        var sibling = first.Child with { ChildId = new(Guid.NewGuid()), AllocationId = new(Guid.NewGuid()) };
        var current = ProcessDecisionTransitions.Enroll(first.Generation, first.Generation.Expectation, sibling);
        var failure = Assert.Throws<ProcessDecisionException>(() => ProcessDecisionTransitions.CloseChild(
            current.Generation, current.Generation.Expectation, first.Child, Closure(false), ProcessDecisionCompletion.Complete));
        Assert.Equal(ProcessDecisionRefusal.UnclosedChildren, failure.Reason);
        Assert.Equal(2, current.Generation.UnclosedChildCount);
    }

    [Theory]
    [InlineData(ProcessDecisionCompletion.Complete)]
    [InlineData(ProcessDecisionCompletion.Cancel)]
    public void Exact_completion_replay_does_not_advance_revision_or_replace_receipt(ProcessDecisionCompletion completion) {
        var receipt = Receipt(completion);
        var replay = ProcessDecisionTransitions.CloseChild(receipt.Current, receipt.Current.Expectation,
            receipt.Child!, receipt.Child!.Closure!, completion);
        Assert.False(replay.Changed);
        Assert.Same(receipt.Current, replay.Generation);
        Assert.Same(receipt.Child, replay.Child);
        var failure = Assert.Throws<ProcessDecisionException>(() => ProcessDecisionTransitions.CloseChild(
            receipt.Current, receipt.Current.Expectation, receipt.Child, receipt.Child.Closure! with { OwnerRevision = 8 }, completion));
        Assert.Equal(ProcessDecisionRefusal.OriginalClosureChanged, failure.Reason);
    }

    [Fact]
    public void A_previously_closed_ordinary_child_cannot_gain_completion_as_a_read_only_replay() {
        var receipt = Receipt(null);
        var failure = Assert.Throws<ProcessDecisionException>(() => ProcessDecisionTransitions.CloseChild(
            receipt.Current, receipt.Current.Expectation, receipt.Child!, receipt.Child!.Closure!, ProcessDecisionCompletion.Complete));
        Assert.Equal(ProcessDecisionRefusal.InvalidPhase, failure.Reason);
    }

    [Fact]
    public void Completion_still_requires_exact_revision_and_a_supported_command() {
        var original = Enrolled();
        var stale = original.Generation.Expectation with { Revision = original.Generation.Revision - 1 };
        Assert.Equal(ProcessDecisionRefusal.StaleExpectation, Assert.Throws<ProcessDecisionException>(() =>
            ProcessDecisionTransitions.CloseChild(original.Generation, stale, original.Child, Closure(false),
                ProcessDecisionCompletion.Complete)).Reason);
        var invalid = (ProcessDecisionClosure)Receipt(null).Command with { Completion = (ProcessDecisionCompletion)99 };
        Assert.Throws<InvalidDataException>(invalid.Validate);
        Assert.Throws<InvalidDataException>(() => ProcessDecisionTransitions.CloseChild(original.Generation,
            original.Generation.Expectation, original.Child, Closure(false), invalid.Completion));
    }

    [Theory]
    [InlineData(ProcessDecisionCompletion.Complete)]
    [InlineData(ProcessDecisionCompletion.Cancel)]
    public void Version_four_retains_exact_completion_and_rejects_changed_command_replay(ProcessDecisionCompletion completion) {
        var receipt = Receipt(completion);
        var json = ProcessDecisionReceiptCodec.Write(receipt)!;
        var document = JsonNode.Parse(json)!;
        Assert.Equal(4, document["version"]!.GetValue<int>());
        Assert.Equal((int)completion, document["command"]!["completion"]!.GetValue<int>());
        Assert.Equal(receipt, ProcessDecisionReceiptCodec.Read(json));
        Assert.Equal(json, ProcessDecisionReceiptCodec.Write(ProcessDecisionReceiptCodec.Read(json)));
        var removed = (ProcessDecisionClosure)receipt.Command with { Completion = null };
        Assert.Throws<InvalidOperationException>(() => ProcessDecisionReceiptCodec.RequireSameCommand(removed, receipt));
    }

    [Theory]
    [InlineData(Malformed.VersionOne)]
    [InlineData(Malformed.VersionTwo)]
    [InlineData(Malformed.VersionThree)]
    [InlineData(Malformed.MissingCompletion)]
    [InlineData(Malformed.NullCompletion)]
    [InlineData(Malformed.UnknownCompletion)]
    [InlineData(Malformed.ActivePhase)]
    [InlineData(Malformed.OpenChild)]
    [InlineData(Malformed.WrongCommand)]
    public void Version_four_rejects_downgrade_incomplete_completion_and_wrong_result(Malformed change) {
        var document = JsonNode.Parse(ProcessDecisionReceiptCodec.Write(Receipt(ProcessDecisionCompletion.Complete))!)!;
        switch (change) {
            case Malformed.VersionOne:
                document["version"] = 1;
                break;
            case Malformed.VersionTwo:
                document["version"] = 2;
                break;
            case Malformed.VersionThree:
                document["version"] = 3;
                break;
            case Malformed.MissingCompletion:
                document["command"]!.AsObject().Remove("completion");
                break;
            case Malformed.NullCompletion:
                document["command"]!["completion"] = null;
                break;
            case Malformed.UnknownCompletion:
                document["command"]!["completion"] = 99;
                break;
            case Malformed.ActivePhase:
                document["current"]!["phase"] = (int)ProcessDecisionPhase.Active;
                break;
            case Malformed.OpenChild:
                document["current"]!["unclosedChildCount"] = 1;
                break;
            case Malformed.WrongCommand:
                document["command"]!["kind"] = 0;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(change));
        }
        Assert.Throws<InvalidDataException>(() => ProcessDecisionReceiptCodec.Read(document.ToJsonString()));
    }

    [Fact]
    public void Original_closure_bytes_remain_exact_without_a_completion_member() {
        const string legacy = """
        {"version":1,"command":{"kind":2,"stepId":"66666666-6666-6666-6666-666666666666","decisionId":null,"expected":{"reference":{"decisionId":{"value":"11111111-1111-1111-1111-111111111111"},"generation":1},"revision":2},"materialId":null,"child":null,"childId":{"value":"44444444-4444-4444-4444-444444444444"},"closure":{"ownerRevision":7,"outcome":1,"acceptedReceipt":null},"phaseChange":null,"refreshKind":null},"changed":true,"current":{"reference":{"decisionId":{"value":"11111111-1111-1111-1111-111111111111"},"generation":1},"revision":3,"phase":0,"materialId":{"value":"22222222-2222-2222-2222-222222222222"},"budgetId":"33333333-3333-3333-3333-333333333333","childCount":1,"unclosedChildCount":0},"previous":null,"child":{"reference":{"decisionId":{"value":"11111111-1111-1111-1111-111111111111"},"generation":1},"childId":{"value":"44444444-4444-4444-4444-444444444444"},"owner":0,"allocationId":"55555555-5555-5555-5555-555555555555","closure":{"ownerRevision":7,"outcome":1,"acceptedReceipt":null}}}
        """;
        var receipt = Receipt(null);
        Assert.Equal(legacy, ProcessDecisionReceiptCodec.Write(receipt));
        Assert.Equal(receipt, ProcessDecisionReceiptCodec.Read(legacy));
        Assert.Equal(legacy, ProcessDecisionReceiptCodec.Write(ProcessDecisionReceiptCodec.Read(legacy)));
    }

    private static ProcessDecisionChildChange Enrolled(ProcessDecisionPhase phase = ProcessDecisionPhase.Active) {
        var initial = ProcessDecisionTransitions.Create(new(Guid.Parse("11111111-1111-1111-1111-111111111111")),
            new(Guid.Parse("22222222-2222-2222-2222-222222222222")), new(Guid.Parse("33333333-3333-3333-3333-333333333333")));
        var child = new ProcessDecisionChild(initial.Reference, new(Guid.Parse("44444444-4444-4444-4444-444444444444")),
            ProcessDecisionChildOwner.Agent, new(Guid.Parse("55555555-5555-5555-5555-555555555555")));
        var enrolled = ProcessDecisionTransitions.Enroll(initial, initial.Expectation, child);
        var generation = phase switch {
            ProcessDecisionPhase.Active => enrolled.Generation,
            ProcessDecisionPhase.WaitingForReview => ProcessDecisionTransitions.WaitForReview(enrolled.Generation, enrolled.Generation.Expectation),
            ProcessDecisionPhase.Reconciling => ProcessDecisionTransitions.BeginReconciliation(enrolled.Generation, enrolled.Generation.Expectation),
            _ => throw new ArgumentOutOfRangeException(nameof(phase))
        };
        return enrolled with { Generation = generation };
    }

    private static ProcessDecisionChildClosure Closure(bool accepted) => new(7,
        accepted ? ProcessDecisionChildOutcome.Accepted : ProcessDecisionChildOutcome.NoEffect,
        accepted ? new(Guid.Parse("77777777-7777-7777-7777-777777777777")) : null);

    private static ProcessDecisionReceipt Receipt(ProcessDecisionCompletion? completion) {
        var original = Enrolled(completion == ProcessDecisionCompletion.Cancel ? ProcessDecisionPhase.Reconciling : ProcessDecisionPhase.Active);
        var closure = Closure(false);
        var command = new ProcessDecisionClosure(new(Guid.Parse("66666666-6666-6666-6666-666666666666")),
            original.Generation.Expectation, original.Child.ChildId, closure) { Completion = completion };
        var changed = ProcessDecisionTransitions.CloseChild(original.Generation, original.Generation.Expectation,
            original.Child, closure, completion);
        return new(command, changed.Changed, changed.Generation, Child: changed.Child);
    }

    public enum Malformed { VersionOne, VersionTwo, VersionThree, MissingCompletion, NullCompletion,
        UnknownCompletion, ActivePhase, OpenChild, WrongCommand }
}
