using CanDoItAll.CrmHr.UI.Crm;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.Projects;

namespace CanDoItAll.Tests.Unit.CrmHr;

public sealed class OpportunityReconciliationTests {
    [Fact]
    public void Independent_text_edits_merge_but_amount_and_currency_require_one_explicit_choice() {
        var original = Model();
        var local = original.Snapshot();
        local.Notes = "Local notes";
        local.Amount = 300;
        var current = original.Snapshot();
        current.Title = "Peer title";
        current.CurrencyCode = "EUR";
        var review = new OpportunityReview(original, local, current);

        Assert.False(review.CommercialValue.IsResolved);
        Assert.False(review.CanApply);
        review.CommercialValue.UseCurrent();
        Assert.True(review.CanApply);
        var result = review.Candidate();
        Assert.Equal("Peer title", result.Title);
        Assert.Equal("Local notes", result.Notes);
        Assert.Equal(new("EUR", 100), OpportunityValues.CommercialValue(result));
        Assert.Equal("Original title", original.Title);
        Assert.Equal("USD", original.CurrencyCode);
    }

    [Fact]
    public void A_deleted_link_cannot_be_resurrected_by_a_merge_or_undo() {
        var original = Model();
        var row = new CrmOpportunityPartyLinkEditorModel { Id = Guid.NewGuid(), PartyId = Guid.NewGuid() };
        original.Parties.Add(row);
        var state = new OpportunityReconciliation();
        state.Reset(original);
        state.Draft.Parties[0].Role = OpportunityPartyRole.Sponsor;
        var current = original.Snapshot();
        current.Parties.Clear();
        current.ExpectedUpdatedAtUtc = original.ExpectedUpdatedAtUtc!.Value.AddSeconds(1);
        state.Hint();
        state.Compare(current, state.BeginReview());
        var comparison = Assert.Single(state.Review!.Rows).Field;
        comparison.UseLocal();
        Assert.False(state.CanApply);
        comparison.UseCurrent();
        Assert.True(state.Apply());
        Assert.Empty(state.Draft.Parties);
        Assert.False(state.CanUndo);
        state.Undo();
        Assert.Empty(state.Draft.Parties);
    }

    [Fact]
    public void Receipt_rebasing_preserves_ABA_and_pending_date_input_on_the_same_model_and_rows() {
        var original = Model();
        original.Parties.Add(new() { Id = Guid.NewGuid(), PartyId = Guid.NewGuid() });
        var state = new OpportunityReconciliation();
        state.Reset(original);
        state.Draft.Notes = "  Submitted notes  ";
        var draft = state.Draft;
        var row = draft.Parties[0];
        var submitted = state.BeginSave();
        draft.Notes = "Intermediate";
        state.InputChanged(new(OpportunityField.Notes));
        draft.Notes = submitted.Notes;
        state.InputChanged(new(OpportunityField.Notes));
        state.InputChanged(new(OpportunityField.ExpectedClose, Pending: true));
        var accepted = submitted.Snapshot();
        accepted.Notes = "Submitted notes";
        accepted.Title = "Canonical title";
        accepted.ExpectedUpdatedAtUtc = original.ExpectedUpdatedAtUtc!.Value.AddSeconds(1);

        state.Accepted(submitted, accepted);

        Assert.Same(draft, state.Draft);
        Assert.Same(row, draft.Parties[0]);
        Assert.Equal("  Submitted notes  ", draft.Notes);
        Assert.Equal("Canonical title", draft.Title);
        Assert.Equal(accepted.ExpectedUpdatedAtUtc, draft.ExpectedUpdatedAtUtc);
        Assert.True(state.HasPendingInput);
        Assert.True(state.IsDirty);
        Assert.False(state.CanSave);
    }

    [Fact]
    public void An_unknown_submission_remains_sticky_through_current_reads_and_comparison() {
        var state = new OpportunityReconciliation();
        state.Reset(Model());
        state.Draft.Notes = "Unknown submitted notes";
        var submitted = state.BeginSave();
        state.Unknown(submitted);
        submitted.Notes = "External mutation";
        var observed = state.Draft.Snapshot();
        observed.ExpectedUpdatedAtUtc = observed.ExpectedUpdatedAtUtc!.Value.AddSeconds(1);
        state.Observe(observed);
        state.Compare(observed, state.BeginReview());

        Assert.True(state.IsUnknown);
        Assert.Equal("Unknown submitted notes", state.UnknownSubmission!.Notes);
        Assert.False(state.CanApply);
        Assert.False(state.CanSave);
        Assert.False(state.Apply());
        Assert.Throws<InvalidOperationException>(() => state.BeginSave());
    }

    [Fact]
    public void A_new_hint_invalidates_an_open_comparison_and_refresh_keeps_chosen_local_values() {
        var state = new OpportunityReconciliation();
        state.Reset(Model());
        state.Draft.Title = "Local title";
        var current = state.Draft.Snapshot();
        current.Title = "Peer title";
        current.ExpectedUpdatedAtUtc = current.ExpectedUpdatedAtUtc!.Value.AddSeconds(1);
        state.Hint();
        state.Compare(current, state.BeginReview());
        state.Review!.Title.UseLocal();
        state.Hint();
        Assert.True(state.ReviewIsStale);
        Assert.False(state.Apply());
        current.Notes = "Third writer notes";
        current.ExpectedUpdatedAtUtc = current.ExpectedUpdatedAtUtc.Value.AddSeconds(1);
        state.Compare(current, state.BeginReview());
        Assert.Equal(CanDoItAll.Editing.MergeChoice.Local, state.Review!.Title.Choice);
        state.Hint();
        current.Title = "Third writer title";
        current.Notes = "Latest automatic notes";
        state.Compare(current, state.BeginReview());
        Assert.False(state.Review!.Title.IsResolved);
        Assert.True(state.Review.Notes.IsResolved);
        Assert.Equal("Latest automatic notes", state.Review.Notes.Result);
        Assert.False(state.Apply());
        state.Review.Title.UseLocal();
        Assert.True(state.Apply());
        Assert.Equal("Local title", state.Draft.Title);
        Assert.Equal("Latest automatic notes", state.Draft.Notes);
        Assert.Equal(current.ExpectedUpdatedAtUtc, state.Draft.ExpectedUpdatedAtUtc);
    }

    [Fact]
    public void Clean_observations_refresh_but_pending_picker_and_date_inputs_freeze_the_baseline() {
        var state = new OpportunityReconciliation();
        state.Reset(Model());
        var current = state.Draft.Snapshot();
        current.Title = "Clean peer title";
        current.ExpectedUpdatedAtUtc = current.ExpectedUpdatedAtUtc!.Value.AddSeconds(1);
        state.Observe(current);
        Assert.Equal(current.Title, state.Draft.Title);
        Assert.False(state.IsOutdated);
        state.ReportPicker(true);
        state.InputChanged(new(OpportunityField.ExpectedClose, Pending: true));
        var later = current.Snapshot();
        later.Title = "Must not overwrite active input";
        later.ExpectedUpdatedAtUtc = current.ExpectedUpdatedAtUtc.Value.AddSeconds(1);
        state.Observe(later);
        Assert.Equal(current.Title, state.Draft.Title);
        Assert.Equal(current.ExpectedUpdatedAtUtc, state.Draft.ExpectedUpdatedAtUtc);
        Assert.True(state.IsOutdated);
        state.ReportPicker(false);
        Assert.True(state.HasPendingInput);
    }

    [Fact]
    public void A_stage_note_cannot_be_replayed_when_the_peer_already_saved_the_selected_stage() {
        var original = Model();
        var local = original.Snapshot();
        local.Stage = OpportunityStage.Qualified;
        local.StageNotes = "My transition note";
        var current = original.Snapshot();
        current.Stage = OpportunityStage.Qualified;
        var review = new OpportunityReview(original, local, current);
        review.StageDecision.UseLocal();
        Assert.False(review.CanApply);
        Assert.Contains("already has this stage", review.GroupError, StringComparison.Ordinal);
        review.StageDecision.UseCurrent();
        Assert.True(review.CanApply);
        Assert.Empty(review.Candidate().StageNotes);
    }

    [Fact]
    public void A_different_target_comparison_is_refused_and_a_new_opening_owns_a_separate_model() {
        var state = new OpportunityReconciliation();
        var first = Model();
        state.Reset(first);
        var retired = state.Draft;
        var next = Model();
        state.Reset(next);
        retired.Title = "Late retired callback";
        Assert.NotSame(retired, state.Draft);
        Assert.Equal(next.Title, state.Draft.Title);
        Assert.Throws<InvalidOperationException>(() => state.Compare(first, state.BeginReview()));
        Assert.Null(state.Review);
    }

    [Fact]
    public void Large_link_comparisons_choose_one_complete_collection_without_truncation() {
        var original = Model();
        original.Parties = Enumerable.Range(0, 1001).Select(_ => new CrmOpportunityPartyLinkEditorModel { Id = Guid.NewGuid(), PartyId = Guid.NewGuid() }).ToList();
        var local = original.Snapshot();
        local.Parties[^1].Role = OpportunityPartyRole.Sponsor;
        var current = original.Snapshot();
        current.Parties[0].Role = OpportunityPartyRole.TechnicalContact;
        var review = new OpportunityReview(original, local, current);
        Assert.Empty(review.Rows);
        Assert.NotNull(review.WholeCollection);
        Assert.False(review.CanApply);
        review.WholeCollection.UseLocal();
        Assert.True(review.CanApply);
        Assert.Equal(1001, review.Candidate().Parties.Count);
        Assert.Equal(OpportunityPartyRole.Sponsor, review.Candidate().Parties[^1].Role);
    }

    [Fact]
    public void A_replacement_project_lifetime_cannot_be_adopted_quietly_or_restored_by_undo() {
        var original = Model();
        original.LinkedProjectId = Guid.NewGuid();
        original.LinkedProjectAdmission = new(Guid.NewGuid(), original.LinkedProjectId.Value, Guid.NewGuid());
        var state = new OpportunityReconciliation();
        state.Reset(original);
        var current = original.Snapshot();
        current.LinkedProjectAdmission = new(original.LinkedProjectAdmission.DatabaseProfileId, original.LinkedProjectId.Value, Guid.NewGuid());
        state.Observe(current);
        Assert.True(state.IsOutdated);
        Assert.Equal(original.LinkedProjectAdmission, state.Draft.LinkedProjectAdmission);
        state.Compare(current, state.BeginReview());
        Assert.False(state.Review!.Project.IsResolved);
        Assert.False(state.Apply());
        state.Review.Project.UseLocal();
        Assert.False(state.CanApply);
        Assert.Contains("earlier project instance", state.Review.GroupError, StringComparison.Ordinal);
        state.Review.Project.UseCurrent();
        Assert.True(state.Apply());
        Assert.Equal(current.LinkedProjectAdmission, state.Draft.LinkedProjectAdmission);
        Assert.False(state.CanUndo);
        state.Undo();
        Assert.Equal(current.LinkedProjectAdmission, state.Draft.LinkedProjectAdmission);
    }

    [Fact]
    public void A_later_quiet_refresh_cannot_make_undo_resurrect_a_saved_link() {
        var original = Model();
        original.Parties.Add(new() { Id = Guid.NewGuid(), PartyId = Guid.NewGuid() });
        var state = new OpportunityReconciliation();
        state.Reset(original);
        state.Draft.Title = "Local title before merge";
        var current = original.Snapshot();
        current.Title = "Current saved title";
        current.ExpectedUpdatedAtUtc = current.ExpectedUpdatedAtUtc!.Value.AddSeconds(1);
        state.Hint();
        state.Compare(current, state.BeginReview());
        state.Review!.Title.UseCurrent();
        Assert.True(state.Apply());
        Assert.False(state.IsDirty);
        Assert.True(state.CanUndo);
        var removed = current.Snapshot();
        removed.Parties.Clear();
        var currentRevision = removed.ExpectedUpdatedAtUtc;
        Assert.NotNull(currentRevision);
        removed.ExpectedUpdatedAtUtc = currentRevision.Value.AddSeconds(1);
        state.Observe(removed);
        Assert.False(state.CanUndo);
        state.Undo();
        Assert.Empty(state.Draft.Parties);
        Assert.Equal(current.Title, state.Draft.Title);
    }

    internal static CrmOpportunityEditorModel Model() => new() {
        Id = Guid.NewGuid(), AccountPartyId = Guid.NewGuid(), OwnerPartyId = Guid.NewGuid(), Title = "Original title",
        Amount = 100, CurrencyCode = "USD", ExpectedUpdatedAtUtc = new(2030, 1, 2, 3, 4, 5, TimeSpan.Zero)
    };
}
