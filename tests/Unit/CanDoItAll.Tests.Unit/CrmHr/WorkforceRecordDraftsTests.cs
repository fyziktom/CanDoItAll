using CanDoItAll.CrmHr.UI.Workforce;
using CanDoItAll.Modules.CrmHr;

namespace CanDoItAll.Tests.Unit.CrmHr;

public sealed class WorkforceRecordDraftsTests {
    [Fact]
    public void Skill_review_merges_one_key_without_saving_and_undo_keeps_the_new_original() {
        var state = new WorkforceRecordDrafts();
        var original = new PartySkillEditorModel { Id = Guid.NewGuid(), PartyId = Guid.NewGuid(), SkillId = Guid.NewGuid(), YearsExperience = 1, Notes = "Original" };
        original.Original = Original(original);
        state.Skill.Open(original);
        var draft = state.Skill.Draft;
        draft.Notes = "Local notes";
        state.Skill.Input(new(WorkforceSkillField.Notes));
        var current = original.Snapshot();
        current.YearsExperience = 5;
        current.Original = Original(current);
        state.HintSkills();
        state.CompareSkill([Row(current)], state.BeginSkillReview());

        Assert.True(state.ApplySkill());
        Assert.Same(draft, state.Skill.Draft);
        Assert.Equal(5, draft.YearsExperience);
        Assert.Equal("Local notes", draft.Notes);
        Assert.Equal(current.Original, draft.Original);
        Assert.Null(state.Skill.Accepted);

        state.UndoSkill();

        Assert.Equal(1, draft.YearsExperience);
        Assert.Equal("Local notes", draft.Notes);
        Assert.Equal(current.Original, draft.Original);
    }

    [Fact]
    public void A_third_skill_writer_invalidates_apply_and_refresh_keeps_an_explicit_choice() {
        var state = new WorkforceRecordDrafts();
        var original = new PartySkillEditorModel { Id = Guid.NewGuid(), PartyId = Guid.NewGuid(), SkillId = Guid.NewGuid(), Notes = "Original" };
        original.Original = Original(original);
        state.Skill.Open(original);
        state.Skill.Draft.Notes = "Local notes";
        state.Skill.Input(new(WorkforceSkillField.Notes));
        var current = original.Snapshot();
        current.Notes = "Peer notes";
        current.Original = Original(current);
        state.HintSkills();
        state.CompareSkill([Row(current)], state.BeginSkillReview());
        state.SkillReview!.Notes.UseLocal();
        state.HintSkills();
        Assert.False(state.ApplySkill());
        current.Notes = "Third writer notes";
        current.Original = Original(current);

        state.CompareSkill([Row(current)], state.BeginSkillReview());

        Assert.Equal("Local notes", state.SkillReview!.Notes.Result);
        Assert.True(state.ApplySkill());
        Assert.Equal("Local notes", state.Skill.Draft.Notes);
        Assert.Equal(current.Original, state.Skill.Draft.Original);
    }

    [Fact]
    public void A_removed_skill_row_or_replacement_of_the_same_key_requires_explicit_reselection() {
        var state = new WorkforceRecordDrafts();
        var original = new PartySkillEditorModel { Id = Guid.NewGuid(), PartyId = Guid.NewGuid(), SkillId = Guid.NewGuid(), Notes = "Original" };
        original.Original = Original(original);
        state.Skill.Open(original);
        var replacement = original.Snapshot();
        replacement.Id = Guid.NewGuid();
        replacement.Original = Original(replacement);

        state.CompareSkill([Row(replacement)], state.BeginSkillReview());

        Assert.True(state.SkillMissing);
        Assert.False(state.CanSaveSkill);
        Assert.False(state.ApplySkill());
        Assert.Equal(original.Id, state.Skill.Draft.Id);
    }

    [Fact]
    public void An_accepted_skill_create_rebases_untouched_values_and_preserves_later_raw_and_ABA_input() {
        var state = new WorkforceRecordDrafts();
        var party = Guid.NewGuid();
        state.Open(party);
        var draft = state.Skill.Draft;
        draft.SkillId = Guid.NewGuid();
        draft.CertificationStatus = "  Submitted certificate  ";
        draft.Notes = "  Submitted notes  ";
        var submission = state.Skill.Begin()!;
        state.Skill.Input(new(WorkforceSkillField.Notes));
        draft.Notes = "  Submitted notes  ";
        draft.YearsExperience = 9;
        state.Skill.Input(new(WorkforceSkillField.Years));
        state.Skill.Input(new(WorkforceSkillField.Validated, true));
        var saved = submission.Model.Snapshot();
        saved.Id = Guid.NewGuid();
        saved.CertificationStatus = "Submitted certificate";
        saved.Notes = "Submitted notes";
        saved.Original = Original(saved);

        state.AcceptSkill(submission, new(saved.Id.Value, saved));
        state.Skill.Complete(submission);

        Assert.Same(draft, state.Skill.Draft);
        Assert.Equal(saved.Id, draft.Id);
        Assert.Equal(saved.Original, draft.Original);
        Assert.Equal("Submitted certificate", draft.CertificationStatus);
        Assert.Equal("  Submitted notes  ", draft.Notes);
        Assert.Equal(9, draft.YearsExperience);
        Assert.True(state.Skill.HasPendingInput);
        Assert.False(state.Skill.CanSave);
    }

    [Fact]
    public void An_accepted_skill_create_clears_its_clean_draft_in_place_and_can_start_the_next_skill() {
        var state = new WorkforceRecordDrafts();
        state.Open(Guid.NewGuid());
        var draft = state.Skill.Draft;
        draft.SkillId = Guid.NewGuid();
        var submission = state.Skill.Begin()!;
        var saved = submission.Model.Snapshot();
        saved.Id = Guid.NewGuid();
        saved.Original = Original(saved);

        state.AcceptSkill(submission, new(saved.Id.Value, saved));
        state.Skill.Complete(submission);

        Assert.Same(draft, state.Skill.Draft);
        Assert.Equal(Guid.Empty, draft.SkillId);
        Assert.Null(draft.Id);
        Assert.False(state.Skill.IsDirty);
        Assert.True(state.Skill.CanSave);
    }

    [Fact]
    public void Selecting_a_different_skill_during_a_save_keeps_its_already_displayed_original() {
        var state = new WorkforceRecordDrafts();
        state.Open(Guid.NewGuid());
        state.Skill.Draft.SkillId = Guid.NewGuid();
        var submission = state.Skill.Begin()!;
        var next = new PartySkillObservation(Guid.NewGuid(), state.Skill.Draft.PartyId, Guid.NewGuid(), SkillProficiencyLevel.Strong, 4, "Current certificate", null, "Current notes");
        state.SelectSkill(next.SkillId, next);
        var saved = submission.Model.Snapshot();
        saved.Id = Guid.NewGuid();
        saved.Original = Original(saved);

        state.AcceptSkill(submission, new(saved.Id.Value, saved));

        Assert.Equal(next.SkillId, state.Skill.Draft.SkillId);
        Assert.Equal(next.Id, state.Skill.Draft.Id);
        Assert.Equal(next, state.Skill.Draft.Original);
    }

    [Fact]
    public void Unknown_submissions_remain_frozen_and_a_read_or_completion_cannot_enable_replay() {
        var state = new WorkforceRecordDrafts();
        state.Open(Guid.NewGuid());
        state.Definition.Draft.Name = "Submitted catalog name";
        var submission = state.Definition.Begin()!;
        state.Definition.Draft.Name = "Later catalog name";
        state.Definition.Input(new(WorkforceDefinitionField.Name));

        state.Definition.Unknown(submission);
        state.Definition.Complete(submission);

        Assert.Equal("Submitted catalog name", state.Definition.UnknownSubmission!.Model.Name);
        Assert.Equal("Later catalog name", state.Definition.Draft.Name);
        Assert.False(state.Definition.CanSave);
        Assert.Null(state.Definition.Begin());
    }

    [Fact]
    public void A_definition_create_keeps_later_input_as_a_separate_new_definition() {
        var state = new WorkforceRecordDrafts();
        state.Definition.Draft.Name = "Submitted";
        var submission = state.Definition.Begin()!;
        state.Definition.Draft.Name = "Next definition";
        state.Definition.Input(new(WorkforceDefinitionField.Name));
        var id = Guid.NewGuid();
        var saved = new SkillDefinitionEditorModel { Id = id, Name = "Submitted", Original = new(id, "Submitted", "", "", true) };

        state.AcceptDefinition(submission, new(id, saved));
        state.Definition.Complete(submission);

        Assert.Equal("Next definition", state.Definition.Draft.Name);
        Assert.Null(state.Definition.Draft.Id);
        Assert.Null(state.Definition.Draft.Original);
        Assert.Equal(id, state.Definition.Accepted!.Id);
        Assert.True(state.Definition.IsDirty);
    }

    [Fact]
    public void A_capacity_create_keeps_later_input_and_pending_date_without_attaching_it_to_the_accepted_row() {
        var state = new WorkforceRecordDrafts();
        state.Open(Guid.NewGuid());
        var draft = state.Capacity.Draft;
        var submission = state.Capacity.Begin()!;
        draft.Notes = "  Later capacity notes  ";
        state.Capacity.Input(new(WorkforceCapacityField.Notes));
        state.Capacity.Input(new(WorkforceCapacityField.Dates, true));
        var saved = submission.Model.Snapshot();
        saved.Id = Guid.NewGuid();
        saved.Original = new(saved.Id.Value, saved.PartyId, saved.BlockKind, saved.StartDate!.Value, saved.EndDate!.Value, saved.Percentage, null, "");

        state.AcceptCapacity(submission, new(saved.Id.Value, saved));
        state.Capacity.Complete(submission);

        Assert.Same(draft, state.Capacity.Draft);
        Assert.Equal("  Later capacity notes  ", draft.Notes);
        Assert.Null(draft.Id);
        Assert.Null(draft.Original);
        Assert.False(state.Capacity.CanSave);
    }

    [Fact]
    public void A_late_receipt_cannot_reset_the_next_opening() {
        var state = new WorkforceRecordDrafts();
        state.Definition.Draft.Name = "Old opening";
        var submission = state.Definition.Begin()!;
        state.Open(Guid.NewGuid());
        state.Definition.Draft.Name = "Next opening";
        var id = Guid.NewGuid();
        var saved = new SkillDefinitionEditorModel { Id = id, Name = "Old opening", Original = new(id, "Old opening", "", "", true) };

        state.AcceptDefinition(submission, new(id, saved));

        Assert.Equal("Next opening", state.Definition.Draft.Name);
        Assert.Null(state.Definition.Accepted);
    }

    private static PartySkillObservation Original(PartySkillEditorModel model) => new(model.Id!.Value, model.PartyId, model.SkillId,
        model.Proficiency, model.YearsExperience, model.CertificationStatus, model.LastValidatedOn, model.Notes);

    private static PartySkillItemModel Row(PartySkillEditorModel model) => new(model.Id!.Value, model.SkillId, "Catalog skill", "Testing",
        model.Proficiency, model.YearsExperience, model.CertificationStatus, model.LastValidatedOn, model.Notes) { Original = model.Original };
}
