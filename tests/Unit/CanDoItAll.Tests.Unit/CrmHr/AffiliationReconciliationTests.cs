using CanDoItAll.CrmHr.UI.Parties;
using CanDoItAll.Editing;
using CanDoItAll.Modules.CrmHr;

namespace CanDoItAll.Tests.Unit.CrmHr;

public sealed class AffiliationReconciliationTests {
    private static readonly DateOnly Today = new(2030, 1, 2);
    private static readonly Guid Person = Guid.NewGuid();
    private static readonly Guid Organization = Guid.NewGuid();
    private static readonly DateTimeOffset Version = new(2030, 1, 2, 3, 4, 5, TimeSpan.Zero);

    [Fact]
    public void Future_primary_dates_and_oversized_merged_text_block_apply_without_truncating_the_draft() {
        var original = AffiliationValues.FromOwner(Saved() with { IsPrimary = true });
        var local = original.Snapshot();
        local.ValidFrom = Today.AddDays(1);
        var review = new AffiliationMergeReview([original], [local], [original], Today);
        Assert.False(review.CanApply);
        Assert.Contains("current affiliation", review.GroupError, StringComparison.Ordinal);
        var result = local.Snapshot();
        result.IsPrimary = false;
        Assert.Single(review.Rows).Field.SetResult(MergeEntry<PartyOrganizationAffiliationEditorModel>.Present(result));
        Assert.True(review.CanApply);
        result.EmployeeCode = new string('x', PartyOrganizationAffiliationLimits.MaximumEmployeeCodeLength + 1);
        Assert.False(review.CanApply);
        Assert.Equal(81, Assert.Single(review.CaptureCandidate()).EmployeeCode.Length);
    }

    [Fact]
    public void A_pending_picker_retains_the_original_draft_when_a_foreign_change_arrives() {
        var original = Saved();
        var state = new AffiliationReconciliation();
        state.Reset([original]);
        state.ReportPicker(true);
        var current = original with { JobTitle = "Peer title", UpdatedAtUtc = Version.AddTicks(10) };
        state.Observe([current]);

        Assert.True(state.IsOutdated);
        Assert.True(state.IsDirty);
        Assert.Equal(original.JobTitle, state.Draft[0].JobTitle);
        Assert.Throws<InvalidOperationException>(() => state.BeginSave());
        state.ReportPicker(false);
        state.Compare([current], state.BeginReview(), Today);
        Assert.True(state.Apply());
        Assert.Equal(current.JobTitle, state.Draft[0].JobTitle);
    }

    [Fact]
    public void Different_rows_merge_locally_and_keep_the_root_and_child_instances_alive() {
        var first = Saved();
        var second = Saved(organization: Guid.NewGuid());
        var state = new AffiliationReconciliation();
        state.Reset([first, second]);
        var draft = state.Draft;
        var child = draft[0];
        child.JobTitle = "  My raw title  ";
        var current = second with { Notes = "Peer notes", UpdatedAtUtc = Version.AddTicks(10) };
        state.Observe([first, current]);
        Assert.True(state.IsOutdated);
        Assert.Empty(draft[1].Notes);
        state.Compare([first, current], state.BeginReview(), Today);

        Assert.True(state.Apply());

        Assert.Same(draft, state.Draft);
        Assert.Same(child, draft[0]);
        Assert.Equal("  My raw title  ", child.JobTitle);
        Assert.Equal("Peer notes", draft[1].Notes);
        Assert.Equal(current.UpdatedAtUtc, draft[1].ExpectedUpdatedAtUtc);
        state.Undo();
        Assert.Empty(draft[1].Notes);
        Assert.Equal(current.UpdatedAtUtc, draft[1].ExpectedUpdatedAtUtc);
        Assert.True(state.IsDirty);
    }

    [Fact]
    public void Save_acknowledgement_uses_submitted_local_and_accepted_values_with_field_revisions() {
        var original = Saved();
        var state = new AffiliationReconciliation();
        state.Reset([original]);
        var row = state.Draft[0];
        row.JobTitle = "  Submitted title  ";
        state.InputChanged(new(original.Id, AffiliationField.Title));
        var submitted = state.BeginSave();
        row.JobTitle = "Later title";
        state.InputChanged(new(original.Id, AffiliationField.Title));
        row.JobTitle = "  Submitted title  ";
        state.InputChanged(new(original.Id, AffiliationField.Title));
        var later = AffiliationValues.FromOwner(Saved(organization: Guid.NewGuid()));
        later.IsNew = true;
        later.ExpectedUpdatedAtUtc = null;
        state.Draft.Add(later);
        var accepted = original with { JobTitle = "Submitted title", EmployeeCode = "Canonical code", UpdatedAtUtc = Version.AddTicks(10) };

        state.Accepted(submitted, [accepted]);

        Assert.Same(row, state.Draft[0]);
        Assert.Equal("  Submitted title  ", row.JobTitle);
        Assert.Equal("Canonical code", row.EmployeeCode);
        Assert.Equal(accepted.UpdatedAtUtc, row.ExpectedUpdatedAtUtc);
        Assert.Same(later, state.Draft[1]);
        Assert.True(state.Draft[1].IsNew);
        Assert.True(state.IsDirty);
        Assert.False(state.IsSaving);
    }

    [Fact]
    public void Refreshing_a_third_writer_keeps_the_result_being_edited_and_parent_input_fences_apply() {
        var original = Saved();
        var state = new AffiliationReconciliation();
        state.Reset([original]);
        state.Draft[0].JobTitle = "Local title";
        var current = original with { JobTitle = "Peer title", UpdatedAtUtc = Version.AddTicks(10) };
        state.Compare([current], state.BeginReview(), Today);
        var custom = state.Draft[0].Snapshot();
        custom.JobTitle = "Custom comparison title";
        Assert.Single(state.Review!.Rows).Field.SetResult(MergeEntry<PartyOrganizationAffiliationEditorModel>.Present(custom));
        state.Hint();
        Assert.False(state.Apply());
        state.Compare([current with { JobTitle = "Third title", UpdatedAtUtc = Version.AddTicks(20) }], state.BeginReview(), Today);

        Assert.Equal("Custom comparison title", Assert.Single(state.Review!.Rows).Field.Local.Value!.JobTitle);
        Assert.Equal("Local title", state.Draft[0].JobTitle);
        Assert.Single(state.Review.Rows).Field.UseLocal();
        state.InputChanged(new(original.Id, AffiliationField.Title));
        Assert.False(state.Apply());
    }

    [Fact]
    public void Independently_added_primary_affiliations_require_a_group_decision_without_dropping_a_row() {
        var local = AffiliationValues.FromOwner(Saved());
        local.IsNew = true;
        local.ExpectedUpdatedAtUtc = null;
        local.IsPrimary = true;
        var current = Saved(organization: Guid.NewGuid()) with { IsPrimary = true };
        var review = new AffiliationMergeReview([], [local], [AffiliationValues.FromOwner(current)], Today);
        Assert.False(review.CanApply);
        Assert.Contains("primary", review.GroupError, StringComparison.Ordinal);
        var demoted = local.Snapshot();
        demoted.IsPrimary = false;
        review.Rows.Single(row => row.Key == local.Id).Field.SetResult(MergeEntry<PartyOrganizationAffiliationEditorModel>.Present(demoted));

        Assert.True(review.CanApply);
        var values = review.Result();
        Assert.Equal(2, values.Count);
        Assert.True(values.Single(row => row.Id == local.Id).IsNew);
        Assert.False(values.Single(row => row.Id == current.Id).IsNew);
        Assert.Single(values.Where(row => row.IsPrimary));
    }

    [Fact]
    public void Historical_rows_cannot_be_deleted_or_resurrected_by_a_comparison() {
        var original = AffiliationValues.FromOwner(Saved());
        var edited = original.Snapshot();
        edited.JobTitle = "Edited title";
        var deletion = new AffiliationMergeReview([original], [], [edited], Today);
        Assert.Single(deletion.Rows).Field.UseLocal();
        Assert.False(deletion.CanApply);
        Assert.Contains("cannot be deleted", deletion.GroupError, StringComparison.Ordinal);
        Assert.Single(deletion.Rows).Field.UseCurrent();
        Assert.True(deletion.CanApply);
        var resurrection = new AffiliationMergeReview([original], [edited], [], Today);
        Assert.Single(resurrection.Rows).Field.UseLocal();
        Assert.False(resurrection.CanApply);
        Assert.Contains("cannot recreate", resurrection.GroupError, StringComparison.Ordinal);
        Assert.Single(resurrection.Rows).Field.UseCurrent();
        Assert.True(resurrection.CanApply);
        Assert.Empty(resurrection.Result());
    }

    [Fact]
    public void Pending_native_date_input_is_dirty_and_prevents_save_and_apply_until_validated() {
        var original = Saved();
        var state = new AffiliationReconciliation();
        state.Reset([original]);
        state.InputChanged(new(original.Id, AffiliationField.Start, PendingDate: true));
        Assert.True(state.IsDirty);
        Assert.Throws<InvalidOperationException>(() => state.BeginSave());
        state.Compare([original], state.BeginReview(), Today);
        Assert.False(state.Apply());
        state.ReportValidation(false);
        Assert.True(state.Apply());
        Assert.False(state.IsDirty);
    }

    [Fact]
    public void Unknown_retains_an_independent_submission_and_current_reads_or_merge_cannot_confirm_it() {
        var original = Saved();
        var state = new AffiliationReconciliation();
        state.Reset([original]);
        state.Draft[0].Notes = "Submitted note";
        var submitted = state.BeginSave();
        state.Draft[0].Notes = "Typed later";
        state.Unknown(submitted);
        submitted[0].Notes = "Mutated outside";
        state.Observe([original with { Notes = "Submitted note", UpdatedAtUtc = Version.AddTicks(10) }]);
        state.Compare([original], state.BeginReview(), Today);

        Assert.True(state.IsUnknown);
        Assert.Equal("Submitted note", Assert.Single(state.UnknownSubmission!).Notes);
        Assert.Equal("Typed later", state.Draft[0].Notes);
        Assert.False(state.Apply());
        Assert.Throws<InvalidOperationException>(() => state.BeginSave());
    }

    private static PartyOrganizationAffiliationListItemModel Saved(Guid? organization = null) => new(
        Guid.NewGuid(), Person, "Person", organization ?? Organization, "Organization", PartyOrganizationAffiliationKind.ExternalContact,
        false, "Original title", "Original code", null, string.Empty, null, string.Empty, null, null, string.Empty,
        "affiliation-test", Version, Version, true);
}
