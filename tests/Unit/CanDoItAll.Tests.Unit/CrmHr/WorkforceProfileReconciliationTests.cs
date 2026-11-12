using CanDoItAll.CrmHr.UI.Workforce;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.Projects;

namespace CanDoItAll.Tests.Unit.CrmHr;

public sealed class WorkforceProfileReconciliationTests {
    [Fact]
    public void Clean_peer_adoption_and_local_review_keep_one_draft_and_Undo_keeps_current_identity() {
        var state = Open();
        var draft = state.Draft;
        var peer = Observed(draft.Snapshot());
        peer.Status = "Peer status";
        state.Observe(Observed(peer));
        Assert.Same(draft, state.Draft);
        Assert.Equal("Peer status", draft.Status);
        draft.JobTitle = "  Raw local title  ";
        state.InputChanged(new(WorkforceProfileField.JobTitle));
        peer.Notes = "Peer notes";
        state.Hint();
        Assert.False(state.CanSave);
        state.Compare(Observed(peer), state.BeginReview());

        Assert.True(state.Apply());
        Assert.Same(draft, state.Draft);
        Assert.Equal("  Raw local title  ", draft.JobTitle);
        Assert.Equal("Peer notes", draft.Notes);
        Assert.Equal(peer.Original, draft.Original);
        state.Undo();
        Assert.Equal("  Raw local title  ", draft.JobTitle);
        Assert.Empty(draft.Notes);
        Assert.Equal(peer.Original, draft.Original);
        Assert.True(state.IsDirty);
    }

    [Fact]
    public void Dates_and_rate_unit_currency_are_atomic_choices_instead_of_blind_field_unions() {
        var state = Open();
        var peer = state.Draft.Snapshot();
        state.Draft.EndDate = state.Draft.EndDate!.Value.AddDays(2);
        state.Draft.InternalCostRate = 110m;
        peer.StartDate = peer.StartDate!.Value.AddDays(1);
        peer.RateCurrencyCode = "EUR";
        state.Hint();
        state.Compare(Observed(peer), state.BeginReview());

        Assert.False(state.CanApply);
        state.Review!.Dates.UseLocal();
        state.Review.Rates.UseCurrent();
        Assert.True(state.Apply());
        Assert.Equal(new DateOnly(2026, 11, 2), state.Draft.StartDate);
        Assert.Equal(new DateOnly(2026, 11, 6), state.Draft.EndDate);
        Assert.Equal(100m, state.Draft.InternalCostRate);
        Assert.Equal("EUR", state.Draft.RateCurrencyCode);
        Assert.Equal(ProjectResourceRateUnit.Hour, state.Draft.RateUnit);
    }

    [Fact]
    public void Accepted_values_preserve_later_ABA_and_pending_raw_numeric_input_in_the_same_draft() {
        var state = Open();
        var draft = state.Draft;
        draft.JobTitle = "  Submitted title  ";
        draft.RateCurrencyCode = "usd ";
        state.InputChanged(new(WorkforceProfileField.JobTitle));
        var submitted = state.Begin()!;
        draft.JobTitle = "Later";
        state.InputChanged(new(WorkforceProfileField.JobTitle));
        draft.JobTitle = submitted.Model.JobTitle;
        state.InputChanged(new(WorkforceProfileField.JobTitle));
        state.InputChanged(new(WorkforceProfileField.Rates, Pending: true));
        var accepted = submitted.Model.Snapshot();
        accepted.JobTitle = accepted.JobTitle.Trim();
        accepted.RateCurrencyCode = "USD";
        state.Accept(submitted, new(accepted.Id!.Value, Observed(accepted)));
        state.Complete(submitted);

        Assert.Same(draft, state.Draft);
        Assert.Equal("  Submitted title  ", draft.JobTitle);
        Assert.Equal("usd ", draft.RateCurrencyCode);
        Assert.Equal(accepted.Original, draft.Original);
        Assert.True(state.IsDirty);
        Assert.False(state.CanSave);
        state.InputChanged(new(WorkforceProfileField.Rates));
        Assert.True(state.CanSave);
    }

    [Fact]
    public void Unknown_submission_cannot_be_reset_by_a_read_comparison_or_peer_change() {
        var state = Open();
        state.Draft.Notes = "  Unknown local notes  ";
        var submitted = state.Begin()!;
        state.Unknown(submitted);
        state.Complete(submitted);
        state.Observe(submitted.Model.Snapshot());
        var peer = submitted.Model.Snapshot();
        peer.Status = "Peer status";
        state.Compare(Observed(peer), state.BeginReview());

        Assert.True(state.IsUnknown);
        Assert.False(state.CanApply);
        Assert.Null(state.Begin());
        Assert.Equal("  Unknown local notes  ", state.Draft.Notes);
        Assert.Equal(submitted.Model.Original, state.UnknownSubmission!.Model.Original);
    }

    [Fact]
    public void Third_writer_blocks_old_review_and_refresh_preserves_explicit_custom_text() {
        var state = Open();
        var peer = state.Draft.Snapshot();
        state.Draft.Notes = "Local notes";
        state.InputChanged(new(WorkforceProfileField.Notes));
        peer.Notes = "Peer notes";
        state.Hint();
        state.Compare(Observed(peer), state.BeginReview());
        state.Review!.Notes.SetResult("  Explicit combined notes  ");
        peer.Notes = "Third writer";
        state.Hint();
        Assert.True(state.ReviewIsStale);
        Assert.False(state.Apply());

        state.Compare(Observed(peer), state.BeginReview());

        Assert.Equal("  Explicit combined notes  ", state.Review!.Notes.Result);
        Assert.True(state.Apply());
        Assert.Equal("  Explicit combined notes  ", state.Draft.Notes);
        Assert.Equal(peer.Original, state.Begin()!.Model.Original);
    }

    [Fact]
    public void Replaced_profiles_cannot_be_resurrected_and_retired_receipts_cannot_affect_a_new_opening() {
        var state = Open();
        var oldDraft = state.Draft;
        var submitted = state.Begin()!;
        var replacement = oldDraft.Snapshot();
        replacement.Id = Guid.NewGuid();
        replacement.JobTitle = "Replacement profile";
        state.Observe(Observed(replacement));
        Assert.True(state.Missing);
        Assert.False(state.CanSave);
        state.Complete(submitted);
        state.Open(replacement);
        var newDraft = state.Draft;

        state.Accept(submitted, new(submitted.Model.Id!.Value, submitted.Model));

        Assert.NotSame(oldDraft, newDraft);
        Assert.Same(newDraft, state.Draft);
        Assert.Equal("Replacement profile", state.Draft.JobTitle);
        Assert.False(state.IsUnknown);
        Assert.False(state.IsCurrent(submitted));
    }

    [Fact]
    public void A_later_clean_peer_adoption_retires_Undo_from_an_older_merge() {
        var state = Open();
        var peer = state.Draft.Snapshot();
        peer.Status = "Peer status";
        state.Hint();
        state.Compare(Observed(peer), state.BeginReview());
        Assert.True(state.Apply());
        Assert.True(state.CanUndo);
        Assert.False(state.IsDirty);
        peer.Notes = "Later peer notes";

        state.Observe(Observed(peer));

        Assert.False(state.CanUndo);
        state.Undo();
        Assert.Equal("Later peer notes", state.Draft.Notes);
    }

    private static WorkforceProfileReconciliation Open() {
        var state = new WorkforceProfileReconciliation();
        state.Open(Observed(new() {
            Id = Guid.NewGuid(), PartyId = Guid.NewGuid(), JobTitle = "Original title", Status = "Active",
            StartDate = new(2026, 11, 2), EndDate = new(2026, 11, 4), InternalCostRate = 100m, ExternalBillingRate = 200m
        }));
        return state;
    }

    private static WorkforceProfileEditorModel Observed(WorkforceProfileEditorModel model) {
        model.Original = new(model.PartyId, PartyType.Person, model.Id, WorkforceProfileValues.From(model));
        return model;
    }
}
