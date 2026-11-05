using CanDoItAll.CrmHr.UI.Assignments;
using CanDoItAll.Editing;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.Projects;

namespace CanDoItAll.Tests.Unit;

public sealed class AssignmentDraftsTests {
    [Fact]
    public void Accepted_identity_and_normalization_preserve_later_notes_and_invalid_capacity_in_the_same_draft() {
        var state = new AssignmentDrafts().Relationship;
        state.Open(new() { PartyId = Guid.NewGuid(), Notes = " submitted ", Source = " source ", AllocationPercent = 30m });
        var draft = state.Draft;
        var sent = Assert.IsType<RecordSubmission<ProjectPartyAssignmentUpsertRequest, AssignmentField>>(state.Begin());
        draft.Notes = "Still typing after dispatch";
        state.Input(new(AssignmentField.Notes));
        state.Input(new(AssignmentField.Allocation, Pending: true));
        var accepted = sent.Model.Snapshot();
        accepted.AssignmentId = Guid.NewGuid();
        accepted.Notes = "submitted";
        accepted.Source = "source";

        AssignmentDrafts.Accept(state, sent, accepted);
        state.Complete(sent);

        Assert.Same(draft, state.Draft);
        Assert.Equal(accepted.AssignmentId, draft.AssignmentId);
        Assert.Equal("Still typing after dispatch", draft.Notes);
        Assert.Equal("source", draft.Source);
        Assert.True(state.IsDirty);
        Assert.True(state.HasPendingInput);
        Assert.False(state.CanSave);
    }

    [Fact]
    public void Staffing_submission_freezes_skills_and_accepted_state_keeps_later_selection() {
        var state = new AssignmentDrafts().Staffing;
        var first = Guid.NewGuid();
        var later = Guid.NewGuid();
        state.Open(new() { Title = " demand ", NeededRole = "Engineer", SkillIds = [first] });
        var sent = Assert.IsType<RecordSubmission<StaffingRequestEditorModel, StaffingField>>(state.Begin());
        state.Draft.SkillIds.Add(later);
        state.Input(new(StaffingField.Skills));
        var accepted = sent.Model.Snapshot();
        accepted.Id = Guid.NewGuid();
        accepted.Title = "demand";

        AssignmentDrafts.Accept(state, sent, accepted);
        state.Complete(sent);

        Assert.Equal(new[] { first }, sent.Model.SkillIds);
        Assert.Equal(new[] { first, later }, state.Draft.SkillIds);
        Assert.Equal("demand", state.Draft.Title);
        Assert.Equal(accepted.Id, state.Draft.Id);
        Assert.True(state.IsDirty);
    }

    [Fact]
    public void Unknown_outcome_retains_the_frozen_submission_and_blocks_a_second_dispatch() {
        var state = new AssignmentDrafts().Allocation;
        state.Open(new() { PartyId = Guid.NewGuid(), AllocationPercent = 40m });
        var sent = Assert.IsType<RecordSubmission<ProjectPartyAssignmentUpsertRequest, AssignmentField>>(state.Begin());
        state.Draft.AllocationPercent = 60m;
        state.Input(new(AssignmentField.Allocation));

        state.Unknown(sent);
        state.Complete(sent);

        Assert.Same(sent, state.UnknownSubmission);
        Assert.Equal(40m, sent.Model.AllocationPercent);
        Assert.Equal(60m, state.Draft.AllocationPercent);
        Assert.Null(state.Begin());
    }

    [Fact]
    public void An_acknowledgement_from_a_retired_opening_cannot_retarget_the_current_draft() {
        var state = new AssignmentDrafts().Relationship;
        state.Open(new() { ProjectId = Guid.NewGuid(), PartyId = Guid.NewGuid(), Notes = "Retired submission" });
        var sent = Assert.IsType<RecordSubmission<ProjectPartyAssignmentUpsertRequest, AssignmentField>>(state.Begin());
        var accepted = sent.Model.Snapshot();
        accepted.AssignmentId = Guid.NewGuid();
        state.Open(new() { ProjectId = Guid.NewGuid(), Notes = "Current project draft" });
        var current = state.Draft;

        AssignmentDrafts.Accept(state, sent, accepted);
        state.Complete(sent);

        Assert.Same(current, state.Draft);
        Assert.Null(current.AssignmentId);
        Assert.Equal("Current project draft", current.Notes);
        Assert.Null(state.Accepted);
    }
}
