using CanDoItAll.CrmHr.UI.Crm;
using CanDoItAll.Modules.CrmHr;

namespace CanDoItAll.Tests.Unit.CrmHr;

public sealed class OpportunityConversionStateTests {
    [Fact]
    public void Submission_is_frozen_and_known_acceptance_keeps_later_ABA_input_without_replay() {
        var state = Open();
        var draft = state.Draft;
        var submission = Assert.IsType<OpportunityConversionSubmission>(state.Begin(draft));
        draft.ProjectName = "Later input";
        state.Input(state.Opening);
        draft.ProjectName = "Original target";
        state.Input(state.Opening);

        state.Accept(submission, new(draft.OpportunityId, Guid.NewGuid(), true));
        state.Complete(submission);

        Assert.Equal("Original target", submission.Model.ProjectName);
        Assert.Same(draft, state.Draft);
        Assert.True(state.HasLaterInput(submission));
        Assert.True(state.HasChanges);
        Assert.False(state.CanSubmit);
        Assert.Null(state.Begin(draft));
    }

    [Fact]
    public void Unknown_outcome_remains_blocked_through_more_input_and_completion() {
        var state = Open();
        var submission = Assert.IsType<OpportunityConversionSubmission>(state.Begin(state.Draft));
        state.Fail(submission);
        state.Draft.ProjectDescription = "Retained after uncertain conversion";
        state.Input(state.Opening);
        state.Complete(submission);

        Assert.True(state.IsUnknown);
        Assert.False(state.CanSubmit);
        Assert.Equal("Retained after uncertain conversion", state.Draft.ProjectDescription);
    }

    [Fact]
    public void Retired_submission_and_picker_callbacks_cannot_change_a_reopened_same_target() {
        var state = Open();
        var submission = Assert.IsType<OpportunityConversionSubmission>(state.Begin(state.Draft));
        state.Open(state.Draft);
        var current = state.Draft;
        state.Input(submission.Opening);
        state.Picker(submission.Opening, true);
        state.Accept(submission, new(current.OpportunityId, Guid.NewGuid(), true));
        state.Fail(submission);
        state.Complete(submission);

        Assert.Same(current, state.Draft);
        Assert.Null(state.Accepted);
        Assert.False(state.IsUnknown);
        Assert.False(state.PickerPending);
        Assert.Equal(0, state.InputRevision);
        Assert.True(state.CanSubmit);
    }

    [Fact]
    public void A_wrong_target_receipt_is_unknown_and_does_not_authorize_replay() {
        var state = Open();
        var submission = Assert.IsType<OpportunityConversionSubmission>(state.Begin(state.Draft));

        Assert.Throws<InvalidOperationException>(() => state.Accept(submission, new(Guid.NewGuid(), Guid.NewGuid(), true)));
        state.Complete(submission);

        Assert.True(state.IsUnknown);
        Assert.Null(state.Accepted);
        Assert.False(state.CanSubmit);
    }

    [Fact]
    public void A_pending_picker_blocks_submission_and_accepted_input_is_the_new_close_baseline() {
        var state = Open();
        state.Picker(state.Opening, true);
        Assert.Null(state.Begin(state.Draft));
        state.Picker(state.Opening, false);
        state.Draft.ProjectObjective = "Accepted objective";
        state.Input(state.Opening);
        var submission = Assert.IsType<OpportunityConversionSubmission>(state.Begin(state.Draft));
        state.Accept(submission, new(state.Draft.OpportunityId, Guid.NewGuid(), true));
        state.Complete(submission);

        Assert.False(state.HasChanges);
        Assert.False(state.HasLaterInput(submission));
    }

    private static OpportunityConversionState Open() {
        var state = new OpportunityConversionState();
        state.Open(new() { OpportunityId = Guid.NewGuid(), ProjectName = "Original target", ExpectedUpdatedAtUtc = DateTimeOffset.UtcNow });
        return state;
    }
}
