using System.Collections.Immutable;
using CanDoItAll.CrmHr.UI.Assignments;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.Projects;

namespace CanDoItAll.Tests.Unit;

public sealed class AssignmentComparisonTests {
    private static readonly ProjectWriteAdmission Project = new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

    [Fact]
    public void Assignment_combines_disjoint_edits_and_keeps_the_current_complete_owner_original() {
        var original = Assignment();
        var local = original.Snapshot();
        local.Notes = "Local notes";
        var current = AssignmentReviewModels.From(original.Original! with { AllocationPercent = 55m, PhaseName = "Review", OpportunityId = Guid.NewGuid() }, Project);
        var review = new AssignmentReview(original, local, current);

        var result = review.Candidate();

        Assert.Equal("Local notes", result.Notes);
        Assert.Equal(55m, result.AllocationPercent);
        Assert.Same(current.Original, result.Original);
        Assert.Equal("Review", result.Original!.PhaseName);
        Assert.NotNull(result.Original.OpportunityId);
        Assert.Equal(original.AssignmentId, result.AssignmentId);
    }

    [Fact]
    public void Same_field_overlap_requires_an_explicit_choice_and_does_not_mutate_the_form() {
        var original = Assignment();
        var local = original.Snapshot();
        local.Notes = "My replacement";
        var current = AssignmentReviewModels.From(original.Original! with { Notes = "Peer replacement" }, Project);
        var review = new AssignmentReview(original, local, current);

        Assert.False(review.CanApply);
        Assert.Throws<InvalidOperationException>(review.Candidate);
        review.Notes.UseCurrent();

        Assert.Equal("Peer replacement", review.Candidate().Notes);
        Assert.Equal("My replacement", local.Notes);
        Assert.Equal("Original", original.Notes);
    }

    [Fact]
    public void Staffing_skill_membership_keeps_local_removal_remote_addition_and_unresolved_ids() {
        var removed = Guid.NewGuid();
        var kept = Guid.NewGuid();
        var added = Guid.NewGuid();
        var original = Staffing([removed, kept]);
        var local = original.Snapshot();
        local.SkillIds = [kept];
        local.Notes = "Local demand";
        var current = AssignmentReviewModels.From(original.Original! with { SkillIds = [removed, kept, added], Status = StaffingRequestStatus.Open }, Project);
        var review = new StaffingRequestReview(original, local, current);

        var result = review.Candidate();

        Assert.Equal(new[] { kept, added }.Order(), result.SkillIds.Order());
        Assert.Equal("Local demand", result.Notes);
        Assert.Equal(StaffingRequestStatus.Open, result.Status);
        Assert.Same(current.Original, result.Original);
    }

    [Theory]
    [InlineData(AssignmentEditorKind.Relationship)]
    [InlineData(AssignmentEditorKind.Allocation)]
    [InlineData(AssignmentEditorKind.Staffing)]
    public void Hint_input_or_retired_opening_blocks_a_prepared_comparison(AssignmentEditorKind editor) {
        foreach (var change in new[] { 0, 1, 2 }) {
            var drafts = new AssignmentDrafts();
            if (editor == AssignmentEditorKind.Staffing) {
                drafts.Staffing.Open(Staffing([]));
            } else {
                State(drafts, editor).Open(Assignment());
            }
            var request = drafts.Comparisons.BeginRead(editor);
            Assert.True(editor == AssignmentEditorKind.Staffing
                ? drafts.Comparisons.Observe(request, drafts.Staffing.Draft.Snapshot())
                : drafts.Comparisons.Observe(request, State(drafts, editor).Draft.Snapshot()));
            Assert.True(drafts.Comparisons.CanApply);
            switch (change) {
                case 0:
                    drafts.Comparisons.Hint(editor == AssignmentEditorKind.Staffing);
                    break;
                case 1:
                    if (editor == AssignmentEditorKind.Staffing) {
                        drafts.Staffing.Input(new(StaffingField.Notes));
                    } else {
                        State(drafts, editor).Input(new(AssignmentField.Notes));
                    }
                    break;
                case 2:
                    if (editor == AssignmentEditorKind.Staffing) {
                        drafts.Staffing.Open(Staffing([]));
                    } else {
                        State(drafts, editor).Open(Assignment());
                    }
                    break;
            }
            Assert.False(drafts.Comparisons.CanApply);
            Assert.False(drafts.Comparisons.Apply());
        }
    }

    [Fact]
    public void Explicit_raw_retention_combines_other_fields_without_reopening_or_clearing_pending_input() {
        var drafts = new AssignmentDrafts();
        drafts.Staffing.Open(Staffing([]));
        var form = drafts.Staffing.Draft;
        var opening = drafts.Staffing.Opening;
        drafts.Staffing.Input(new(StaffingField.Allocation, Pending: true));
        var current = AssignmentReviewModels.From(form.Original! with { Title = "Peer title", AllocationPercent = 70m }, Project);
        var request = drafts.Comparisons.BeginRead(AssignmentEditorKind.Staffing);
        Assert.True(drafts.Comparisons.Observe(request, current));
        Assert.False(drafts.Comparisons.CanApply);
        drafts.Comparisons.KeepIncompleteInput = true;

        Assert.True(drafts.Comparisons.Apply());

        Assert.Same(form, drafts.Staffing.Draft);
        Assert.Equal(opening, drafts.Staffing.Opening);
        Assert.Equal("Peer title", form.Title);
        Assert.Equal(30m, form.AllocationPercent);
        Assert.Same(current.Original, form.Original);
        Assert.True(drafts.Staffing.IsPending(StaffingField.Allocation));
        Assert.False(drafts.Staffing.CanSave);
        Assert.False(drafts.Comparisons.CanUndo);
    }

    [Fact]
    public void Undo_restores_local_values_but_keeps_the_current_write_expectation() {
        var drafts = new AssignmentDrafts();
        drafts.Relationship.Open(Assignment());
        drafts.Relationship.Draft.Notes = "Local";
        var current = AssignmentReviewModels.From(drafts.Relationship.Draft.Original! with { AllocationPercent = 70m }, Project);
        var request = drafts.Comparisons.BeginRead(AssignmentEditorKind.Relationship);
        Assert.True(drafts.Comparisons.Observe(request, current));
        Assert.True(drafts.Comparisons.Apply());
        Assert.Equal(70m, drafts.Relationship.Draft.AllocationPercent);

        Assert.True(drafts.Comparisons.Undo());

        Assert.Equal(30m, drafts.Relationship.Draft.AllocationPercent);
        Assert.Equal("Local", drafts.Relationship.Draft.Notes);
        Assert.Same(current.Original, drafts.Relationship.Draft.Original);
        Assert.True(drafts.Relationship.IsDirty);
    }

    [Fact]
    public void Refresh_retains_choices_only_for_identical_original_local_and_current_values() {
        var original = Assignment();
        var local = original.Snapshot();
        local.Notes = "Local";
        var current = AssignmentReviewModels.From(original.Original! with { Notes = "Peer one" }, Project);
        var previous = new AssignmentReview(original, local, current);
        previous.Notes.UseLocal();
        var unchanged = new AssignmentReview(original, local, current);
        unchanged.PreserveChoices(previous);
        Assert.True(unchanged.Notes.IsResolved);
        var changed = new AssignmentReview(original, local, AssignmentReviewModels.From(current.Original! with { Notes = "Peer two" }, Project));

        changed.PreserveChoices(previous);

        Assert.False(changed.Notes.IsResolved);
    }

    [Fact]
    public void Different_project_lifetime_cannot_become_a_comparison_original() {
        var drafts = new AssignmentDrafts();
        drafts.Relationship.Open(Assignment());
        var current = drafts.Relationship.Draft.Snapshot();
        current.ExpectedProjectAdmission = new(Project.DatabaseProfileId, Project.ProjectId, Guid.NewGuid());
        var request = drafts.Comparisons.BeginRead(AssignmentEditorKind.Relationship);

        Assert.False(drafts.Comparisons.Observe(request, current));
        Assert.False(drafts.Comparisons.CanApply);
        Assert.NotEqual(current.ExpectedProjectAdmission, drafts.Relationship.Draft.ExpectedProjectAdmission);
    }

    [Fact]
    public void Failed_refresh_cannot_make_an_earlier_stale_comparison_applicable_again() {
        var drafts = new AssignmentDrafts();
        drafts.Relationship.Open(Assignment());
        var request = drafts.Comparisons.BeginRead(AssignmentEditorKind.Relationship);
        Assert.True(drafts.Comparisons.Observe(request, drafts.Relationship.Draft.Snapshot()));
        drafts.Comparisons.Hint(staffing: false);
        Assert.False(drafts.Comparisons.CanApply);
        var retry = drafts.Comparisons.BeginRead(AssignmentEditorKind.Relationship);

        drafts.Comparisons.ReadFailed(retry);

        Assert.True(drafts.Comparisons.IsStale);
        Assert.False(drafts.Comparisons.CanApply);
        Assert.False(drafts.Comparisons.Apply());
    }

    [Fact]
    public void Pending_picker_cannot_be_acknowledged_as_incomplete_number_input() {
        var drafts = new AssignmentDrafts();
        drafts.Staffing.Open(Staffing([]));
        drafts.Staffing.Input(new(StaffingField.Requester, Pending: true));
        var request = drafts.Comparisons.BeginRead(AssignmentEditorKind.Staffing);
        Assert.True(drafts.Comparisons.Observe(request, drafts.Staffing.Draft.Snapshot()));
        drafts.Comparisons.KeepIncompleteInput = true;

        Assert.True(drafts.Comparisons.HasPendingSelection);
        Assert.False(drafts.Comparisons.Apply());
        Assert.True(drafts.Staffing.HasPendingInput);
    }

    [Theory]
    [InlineData(AssignmentEditorKind.Relationship)]
    [InlineData(AssignmentEditorKind.Allocation)]
    [InlineData(AssignmentEditorKind.Staffing)]
    public void A_discarded_saved_rows_conflict_does_not_block_the_next_create_opening(AssignmentEditorKind editor) {
        var drafts = new AssignmentDrafts();
        if (editor == AssignmentEditorKind.Staffing) {
            drafts.Staffing.Open(Staffing([]));
        } else {
            State(drafts, editor).Open(Assignment());
        }
        drafts.Comparisons.MarkOutdated(editor);
        Assert.False(drafts.Comparisons.CanSave(editor));
        if (editor == AssignmentEditorKind.Staffing) {
            drafts.Staffing.Open(new());
        } else {
            State(drafts, editor).Open(new());
        }

        Assert.True(drafts.Comparisons.CanSave(editor));
    }

    [Fact]
    public void Direct_model_changes_after_applying_are_not_silently_overwritten_by_undo() {
        var drafts = new AssignmentDrafts();
        drafts.Relationship.Open(Assignment());
        var request = drafts.Comparisons.BeginRead(AssignmentEditorKind.Relationship);
        Assert.True(drafts.Comparisons.Observe(request, drafts.Relationship.Draft.Snapshot()));
        Assert.True(drafts.Comparisons.Apply());
        drafts.Relationship.Draft.Notes = "Later local values";

        Assert.False(drafts.Comparisons.CanUndo);
        Assert.False(drafts.Comparisons.Undo());
        Assert.Equal("Later local values", drafts.Relationship.Draft.Notes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void A_new_saved_note_does_not_erase_unapplied_comparison_text(bool staffing) {
        const string composed = "My earlier composed comparison text";
        if (staffing) {
            var original = Staffing([]);
            var local = original.Snapshot();
            local.Notes = "Local note";
            var current = AssignmentReviewModels.From(original.Original! with { Notes = "Peer one" }, Project);
            var previous = new StaffingRequestReview(original, local, current);
            previous.Notes.SetResult(composed);
            var next = new StaffingRequestReview(original, local, AssignmentReviewModels.From(current.Original! with { Notes = "Peer two" }, Project));

            next.PreserveChoices(previous);

            Assert.Equal(composed, next.RetainedNotes);
            Assert.NotEqual(CanDoItAll.Editing.MergeChoice.Custom, next.Notes.Choice);
            Assert.Equal("Local note", local.Notes);
            next.Notes.SetResult(next.RetainedNotes!);
            Assert.Equal(composed, next.Candidate().Notes);
        } else {
            var original = Assignment();
            var local = original.Snapshot();
            local.Notes = "Local note";
            var current = AssignmentReviewModels.From(original.Original! with { Notes = "Peer one" }, Project);
            var previous = new AssignmentReview(original, local, current);
            previous.Notes.SetResult(composed);
            var next = new AssignmentReview(original, local, AssignmentReviewModels.From(current.Original! with { Notes = "Peer two" }, Project));

            next.PreserveChoices(previous);

            Assert.Equal(composed, next.RetainedNotes);
            Assert.NotEqual(CanDoItAll.Editing.MergeChoice.Custom, next.Notes.Choice);
            Assert.Equal("Local note", local.Notes);
            next.Notes.SetResult(next.RetainedNotes!);
            Assert.Equal(composed, next.Candidate().Notes);
        }
    }

    private static ProjectPartyAssignmentUpsertRequest Assignment() => AssignmentReviewModels.From(new ProjectAssignmentObservation(
        Guid.NewGuid(), Project.ProjectId, Project.LifetimeId, Guid.NewGuid(), null, ProjectPartyAssignmentRole.TeamMember,
        string.Empty, "Delivery", null, false, 30m, null, null, "UI", "Original"), Project);
    private static StaffingRequestEditorModel Staffing(ImmutableArray<Guid> skills) => AssignmentReviewModels.From(new StaffingRequestObservation(
        Guid.NewGuid(), Project.ProjectId, Project.LifetimeId, null, null, "Original demand", "Engineer", skills,
        null, null, 30m, StaffingRequestStatus.Draft, "Original"), Project);
    private static CanDoItAll.Editing.RecordDraftState<ProjectPartyAssignmentUpsertRequest, AssignmentField> State(AssignmentDrafts drafts, AssignmentEditorKind editor) =>
        editor == AssignmentEditorKind.Relationship ? drafts.Relationship : drafts.Allocation;
}
