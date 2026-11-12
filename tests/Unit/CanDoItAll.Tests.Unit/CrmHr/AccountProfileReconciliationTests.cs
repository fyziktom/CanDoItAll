using CanDoItAll.CrmHr.UI.Crm;
using CanDoItAll.Modules.CrmHr;

namespace CanDoItAll.Tests.Unit.CrmHr;

public sealed class AccountProfileReconciliationTests {
    [Fact]
    public void A_first_profile_created_by_a_peer_does_not_replace_the_drafts_expected_absence() {
        var draft = Profile();
        var state = new AccountProfileReconciliation();
        state.Reset(draft);
        draft.CommercialNotes = "Local commercial work";
        var current = draft.Snapshot();
        current.Id = Guid.NewGuid();
        current.Original = new(current.Id, DateTimeOffset.UnixEpoch.AddMinutes(1), DateTimeOffset.UnixEpoch.AddMinutes(1));
        current.ConstraintNotes = "Peer constraint";

        state.Observe(draft, current);

        Assert.True(state.IsOutdated);
        Assert.Null(draft.Original!.ProfileId);
        Assert.Empty(draft.ConstraintNotes);
        state.Compare(draft, current, state.BeginReview());
        Assert.True(state.Apply(draft));
        Assert.Equal(current.Original, draft.Original);
        Assert.Equal("Local commercial work", draft.CommercialNotes);
        Assert.Equal("Peer constraint", draft.ConstraintNotes);
    }

    [Fact]
    public void Accepted_normalization_does_not_replace_later_input_that_was_changed_back_to_the_submission() {
        var draft = Profile();
        var state = new AccountProfileReconciliation();
        state.Reset(draft);
        draft.CommercialNotes = " Submitted raw value ";
        var submitted = state.BeginSave(draft);
        state.InputChanged(AccountProfileField.CommercialNotes);
        state.InputChanged(AccountProfileField.CommercialNotes);
        draft.TimingRiskNotes = "Late timing input";
        var accepted = submitted.Snapshot();
        accepted.CommercialNotes = accepted.CommercialNotes.Trim();
        accepted.Id = Guid.NewGuid();
        accepted.Original = new(accepted.Id, DateTimeOffset.UnixEpoch.AddMinutes(1), DateTimeOffset.UnixEpoch.AddMinutes(1));

        state.Accepted(draft, submitted, new(accepted.AccountPartyId, accepted));

        Assert.Equal(submitted.CommercialNotes, draft.CommercialNotes);
        Assert.Equal("Late timing input", draft.TimingRiskNotes);
        Assert.Equal(accepted.Original, draft.Original);
        Assert.Equal(accepted.Id, draft.Id);
        Assert.True(state.IsDirty(draft));
    }

    [Fact]
    public void A_third_writer_requires_a_new_comparison_and_preserves_the_result_being_edited() {
        var draft = Profile();
        var state = new AccountProfileReconciliation();
        state.Reset(draft);
        draft.CommercialNotes = "Local";
        var current = draft.Snapshot();
        current.CommercialNotes = "Second writer";
        state.Compare(draft, current, state.BeginReview());
        state.Review!.Commercial.SetResult("Custom review work");
        state.Hint();
        Assert.False(state.Apply(draft));
        current.CommercialNotes = "Third writer";

        state.Compare(draft, current, state.BeginReview());

        Assert.Equal("Custom review work", state.Review!.Commercial.Local);
        Assert.Equal("Local", draft.CommercialNotes);
        Assert.False(state.ReviewIsStale);
    }

    [Fact]
    public void Undo_keeps_the_reviewed_owner_original_and_parent_input_revision_fences_apply() {
        var draft = Profile();
        var state = new AccountProfileReconciliation();
        state.Reset(draft);
        draft.CommercialNotes = "Local work";
        var current = draft.Snapshot();
        current.ConstraintNotes = "Current constraint";
        current.Original = new(null, null, DateTimeOffset.UnixEpoch.AddMinutes(1));
        state.Compare(draft, current, state.BeginReview());
        state.InputChanged(AccountProfileField.CommercialNotes);
        Assert.False(state.Apply(draft));
        state.Compare(draft, current, state.BeginReview());
        Assert.True(state.Apply(draft));

        state.Undo(draft);

        Assert.Equal(current.Original, draft.Original);
        Assert.Empty(draft.ConstraintNotes);
        Assert.Equal("Local work", draft.CommercialNotes);
    }

    [Fact]
    public void An_unknown_submission_is_retained_and_neither_current_reads_nor_review_choices_resolve_its_outcome() {
        var draft = Profile();
        var state = new AccountProfileReconciliation();
        state.Reset(draft);
        draft.CommercialNotes = "Submitted";
        var submitted = state.BeginSave(draft);
        state.Unknown(submitted);
        submitted.CommercialNotes = "Changed caller copy";
        draft.CommercialNotes = "Later local input";
        var current = draft.Snapshot();
        current.CommercialNotes = "Unrelated third writer";
        current.Original = new(null, null, DateTimeOffset.UnixEpoch.AddMinutes(1));
        state.Observe(draft, current);
        state.Compare(draft, current, state.BeginReview());
        state.Review!.Commercial.UseCurrent();

        Assert.Equal("Submitted", state.UnknownSubmission!.CommercialNotes);
        Assert.False(state.Apply(draft));
        Assert.Throws<InvalidOperationException>(() => state.BeginSave(draft));
        Assert.Equal("Later local input", draft.CommercialNotes);
    }

    private static CrmAccountProfileEditorModel Profile() => new() {
        AccountPartyId = Guid.NewGuid(), Original = new(null, null, DateTimeOffset.UnixEpoch)
    };
}
