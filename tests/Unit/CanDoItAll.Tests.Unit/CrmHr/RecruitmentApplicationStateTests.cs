using CanDoItAll.CrmHr.UI.Recruiting;
using CanDoItAll.Modules.CrmHr;

namespace CanDoItAll.Tests.Unit.CrmHr;

public sealed class RecruitmentApplicationStateTests {
    [Fact]
    public void An_older_read_cannot_replace_an_exact_accepted_version_or_open_a_reverse_comparison() {
        var original = Saved();
        var state = Open(original);
        state.Draft.Notes = "Accepted notes";
        var submission = state.Begin()!;
        var accepted = submission.Model.Snapshot();
        Stamp(accepted);
        state.Accept(submission, new(accepted.Id!.Value, accepted));
        state.Complete(submission);

        state.Observe(original, state.HintRevision, explicitPull: true);
        state.Compare(original, state.HintRevision);

        Assert.Equal("Accepted notes", state.Draft.Notes);
        Assert.Equal(accepted.Original, state.Draft.Original);
        Assert.Null(state.Review);
        Assert.True(state.IsOutdated);
        Assert.False(state.CanSave);
    }

    [Fact]
    public void A_clean_foreign_change_waits_for_explicit_pull_and_keeps_the_same_draft() {
        var original = Saved();
        var state = Open(original);
        var draft = state.Draft;
        var current = Changed(original, "Current role");
        state.Hint();
        state.Observe(current, state.HintRevision, explicitPull: false);
        Assert.Same(draft, state.Draft);
        Assert.Equal("Original role", draft.DesiredRole);
        Assert.False(state.CanSave);

        state.Observe(current, state.HintRevision, explicitPull: true);

        Assert.Same(draft, state.Draft);
        Assert.Equal("Current role", draft.DesiredRole);
        Assert.Equal(current.Original, draft.Original);
        Assert.True(state.CanSave);
        Assert.False(state.IsDirty);
    }

    [Fact]
    public void Review_applies_only_a_local_candidate_and_undo_restores_the_old_basis() {
        var original = Saved();
        var state = Open(original);
        state.Draft.Notes = "My local notes";
        state.Input(new(RecruitmentApplicationField.Notes));
        var current = Changed(original, "Peer role");
        state.Hint();
        state.Compare(current, state.HintRevision);
        Assert.True(state.CanApply);

        Assert.True(state.Apply());

        Assert.Equal("Peer role", state.Draft.DesiredRole);
        Assert.Equal("My local notes", state.Draft.Notes);
        Assert.Equal(current.Original, state.Draft.Original);
        Assert.True(state.IsDirty);
        Assert.Null(state.Accepted);
        Assert.True(state.CanSave);
        state.Undo();
        Assert.Equal("Original role", state.Draft.DesiredRole);
        Assert.Equal("My local notes", state.Draft.Notes);
        Assert.Equal(original.Original, state.Draft.Original);
        Assert.False(state.CanSave);
    }

    [Fact]
    public void A_decision_pair_requires_a_choice_and_a_third_change_invalidates_the_review() {
        var original = Saved();
        var state = Open(original);
        state.Draft.Stage = RecruitmentStage.Interviewing;
        state.Input(new(RecruitmentApplicationField.Stage));
        var current = Changed(original, "Original role");
        current.Decision = RecruitmentDecision.Approved;
        Stamp(current);
        state.Hint();
        state.Compare(current, state.HintRevision);
        Assert.False(state.CanApply);
        state.Review!.Decision.UseCurrent();
        Assert.True(state.CanApply);
        state.Hint();
        Assert.False(state.CanApply);
        current.Stage = RecruitmentStage.Hired;
        Stamp(current);
        state.Compare(current, state.HintRevision);
        Assert.True(state.CanApply);
        Assert.True(state.Apply());
        Assert.Equal(RecruitmentStage.Hired, state.Draft.Stage);
        Assert.Equal(RecruitmentDecision.Approved, state.Draft.Decision);
    }

    [Fact]
    public void Acceptance_keeps_later_ABA_intent_and_pending_input_over_the_accepted_original() {
        var state = Open(Saved());
        state.Draft.Notes = "Submitted notes ";
        state.Input(new(RecruitmentApplicationField.Notes));
        var submission = state.Begin()!;
        state.Draft.Notes = "Later notes";
        state.Input(new(RecruitmentApplicationField.Notes));
        state.Draft.Notes = "Submitted notes ";
        state.Input(new(RecruitmentApplicationField.Notes));
        state.Input(new(RecruitmentApplicationField.AvailableFrom, true));
        var accepted = Changed(submission.Model, "Canonical accepted role");
        accepted.Notes = "Submitted notes";
        Stamp(accepted);

        state.Accept(submission, new(accepted.Id!.Value, accepted));
        state.Complete(submission);

        Assert.Equal("Canonical accepted role", state.Draft.DesiredRole);
        Assert.Equal("Submitted notes ", state.Draft.Notes);
        Assert.Equal(accepted.Original, state.Draft.Original);
        Assert.True(state.IsDirty);
        Assert.True(state.HasPendingInput);
        Assert.False(state.CanSave);
    }

    [Fact]
    public void Acceptance_also_preserves_a_later_direct_model_edit() {
        var state = Open(Saved());
        var submission = state.Begin()!;
        state.Draft.Notes = "Later direct input";
        var accepted = Changed(submission.Model, "Canonical role");
        state.Accept(submission, new(accepted.Id!.Value, accepted));
        state.Complete(submission);
        Assert.Equal("Later direct input", state.Draft.Notes);
        Assert.Equal("Canonical role", state.Draft.DesiredRole);
        Assert.True(state.IsDirty);
    }

    [Fact]
    public void An_unknown_outcome_keeps_submission_and_later_values_after_an_explicit_current_read() {
        var state = Open(Saved());
        state.Draft.Notes = "Submitted notes";
        var submission = state.Begin()!;
        state.Draft.Notes = "Later notes";
        state.Input(new(RecruitmentApplicationField.Notes));
        state.Unknown(submission);
        state.Complete(submission);
        var current = Changed(submission.Model, "Current role");
        state.Observe(current, state.HintRevision, explicitPull: true);
        state.Compare(current, state.HintRevision);
        Assert.Equal("Submitted notes", state.UnknownSubmission!.Model.Notes);
        Assert.Equal("Later notes", state.Draft.Notes);
        Assert.False(state.CanSave);
        Assert.False(state.CanApply);
        Assert.True(state.IsUnknown);
    }

    [Fact]
    public void A_callback_from_a_retired_opening_cannot_change_the_next_record() {
        var original = Saved();
        var state = Open(original);
        var submission = state.Begin()!;
        var other = Saved();
        state.Open(other);
        state.Accept(submission, new(original.Id!.Value, Changed(original, "Late acceptance")));
        Assert.Equal(other.Id, state.Draft.Id);
        Assert.Equal("Original role", state.Draft.DesiredRole);
        Assert.Null(state.Accepted);
    }

    [Fact]
    public void A_new_candidate_acceptance_retains_later_identity_input_until_an_explicit_decision() {
        var state = new RecruitmentApplicationState();
        state.Draft.CandidateName = "Submitted candidate";
        state.Draft.DesiredRole = "Submitted role";
        var submission = state.Begin()!;
        state.Draft.CandidateName = "Later candidate details";
        state.Input(new(RecruitmentApplicationField.CandidateName));
        var accepted = Saved();
        accepted.CandidateName = "Submitted candidate";
        accepted.DesiredRole = "Submitted role";
        Stamp(accepted);
        state.Accept(submission, new(accepted.Id!.Value, accepted));
        state.Complete(submission);
        Assert.Equal(accepted.Id, state.Draft.Id);
        Assert.Equal("Later candidate details", state.Draft.CandidateName);
        Assert.True(state.CandidateInputRetained);
        Assert.False(state.CanSave);
        state.UseSavedCandidateDetails();
        Assert.Equal("Submitted candidate", state.Draft.CandidateName);
        Assert.False(state.CandidateInputRetained);
        Assert.True(state.CanSave);
    }

    [Fact]
    public void A_reassigned_candidate_requires_a_new_opening_and_cannot_be_merged_into_the_old_one() {
        var state = Open(Saved());
        var current = state.Draft.Snapshot();
        current.PartyId = Guid.NewGuid();
        Stamp(current);
        state.Observe(current, state.HintRevision, explicitPull: true);
        Assert.True(state.Missing);
        Assert.False(state.CanSave);
        Assert.NotEqual(current.PartyId, state.Draft.PartyId);
    }

    [Fact]
    public void A_receipt_for_a_different_candidate_is_rejected() {
        var state = Open(Saved());
        var submission = state.Begin()!;
        var accepted = submission.Model.Snapshot();
        accepted.PartyId = Guid.NewGuid();
        Stamp(accepted);
        Assert.Throws<InvalidOperationException>(() => state.Accept(submission, new(accepted.Id!.Value, accepted)));
    }

    private static RecruitmentApplicationState Open(RecruitmentApplicationEditorModel model) {
        var state = new RecruitmentApplicationState();
        state.Open(model);
        return state;
    }
    private static RecruitmentApplicationEditorModel Saved() {
        var model = new RecruitmentApplicationEditorModel {
            Id = Guid.NewGuid(), PartyId = Guid.NewGuid(), CandidateName = "Candidate",
            DesiredRole = "Original role", Stage = RecruitmentStage.Offer
        };
        Stamp(model);
        return model;
    }
    private static RecruitmentApplicationEditorModel Changed(RecruitmentApplicationEditorModel model, string role) {
        var current = model.Snapshot();
        current.DesiredRole = role;
        Stamp(current);
        return current;
    }
    private static void Stamp(RecruitmentApplicationEditorModel model) => model.Original = new(model.Id!.Value, model.PartyId!.Value,
        model.TargetUnitPartyId, model.RecruiterPartyId, model.HiringManagerPartyId, model.DesiredRole, model.Source, model.Stage,
        model.AvailableFrom is { } date ? new(date.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero) : null, model.Decision, model.Notes,
        (model.Original?.UpdatedAtUtc ?? new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero)).AddTicks(10));
}
