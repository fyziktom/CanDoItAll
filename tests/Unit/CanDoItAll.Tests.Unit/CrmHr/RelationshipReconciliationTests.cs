using CanDoItAll.CrmHr.UI.Parties;
using CanDoItAll.Editing;
using CanDoItAll.Modules.CrmHr;

namespace CanDoItAll.Tests.Unit.CrmHr;

public sealed class RelationshipReconciliationTests {
    private static readonly Guid Subject = Guid.NewGuid();
    private static readonly Guid Related = Guid.NewGuid();

    [Fact]
    public void Raw_notes_and_row_instances_survive_a_peer_change_until_explicit_local_apply() {
        var original = Saved();
        var state = State(original);
        var row = state.Draft[0];
        row.Notes = "  Local raw notes  ";
        state.InputChanged(new(row.Id!.Value, RelationshipField.Notes));
        var peer = original with { IsPrimary = true, Version = 2 };
        state.Observe([peer]);
        Assert.True(state.IsOutdated);
        Assert.Equal("  Local raw notes  ", row.Notes);
        state.Compare([peer], state.BeginReview());
        var merged = row.Snapshot();
        merged.IsPrimary = true;
        Assert.Single(state.Review!.Rows).Field.SetResult(MergeEntry<PartyRelationshipEditorModel>.Present(merged));

        Assert.True(state.Apply());

        Assert.Same(row, Assert.Single(state.Draft));
        Assert.Equal("  Local raw notes  ", row.Notes);
        Assert.True(row.IsPrimary);
        Assert.Equal(2u, row.ExpectedVersion);
        Assert.True(state.IsDirty);
        Assert.Equal(original.Id, Assert.Single(state.Original.Rows).Id);
    }

    [Fact]
    public void Accepted_canonical_notes_do_not_replace_later_ABA_input_or_pending_date_state() {
        var original = Saved();
        var state = State(original);
        var row = state.Draft[0];
        row.Notes = "  Submitted  ";
        var submitted = state.BeginSave();
        row.Notes = "Typed later";
        state.InputChanged(new(original.Id, RelationshipField.Notes));
        row.Notes = "  Submitted  ";
        state.InputChanged(new(original.Id, RelationshipField.Notes));
        state.InputChanged(new(original.Id, RelationshipField.Start, PendingDate: true));

        state.Accepted(submitted, [original with { Notes = "Submitted", Version = 2 }]);

        Assert.Same(row, state.Draft[0]);
        Assert.Equal("  Submitted  ", row.Notes);
        Assert.Equal(2u, row.ExpectedVersion);
        Assert.True(state.HasPendingInput);
        Assert.True(state.IsDirty);
        Assert.Throws<InvalidOperationException>(() => state.BeginSave());
    }

    [Fact]
    public void Acceptance_preserves_a_row_removed_and_a_new_row_added_after_submission() {
        var first = Saved();
        var second = Saved(related: Guid.NewGuid());
        var state = State(first, second);
        state.Draft[0].Notes = "Submitted edit";
        var submitted = state.BeginSave();
        state.Draft.RemoveAt(0);
        var added = RelationshipValues.FromOwner(Saved(related: Guid.NewGuid()));
        added.ExpectedVersion = 0;
        state.Draft.Add(added);

        state.Accepted(submitted, [first with { Notes = "Submitted edit", Version = 2 }, second]);

        Assert.DoesNotContain(state.Draft, row => row.Id == first.Id);
        Assert.Contains(state.Draft, row => ReferenceEquals(row, added));
        Assert.Equal(2, state.Original.Rows.Count);
        Assert.True(state.IsDirty);
    }

    [Fact]
    public void Current_values_that_match_an_unknown_submission_cannot_confirm_it_or_unlock_resubmission() {
        var original = Saved();
        var state = State(original);
        state.Draft[0].Notes = "Submitted note";
        var submitted = state.BeginSave();
        state.Unknown(submitted);
        submitted[0].Notes = "Mutated by caller";
        state.Observe([original with { Notes = "Submitted note", Version = 2 }]);
        state.Compare([original with { Notes = "Submitted note", Version = 2 }], state.BeginReview());

        Assert.True(state.IsUnknown);
        Assert.Equal("Submitted note", Assert.Single(state.UnknownSubmission!).Notes);
        Assert.False(state.Apply());
        Assert.Throws<InvalidOperationException>(() => state.BeginSave());
    }

    [Fact]
    public void A_third_writer_keeps_custom_choices_but_new_parent_input_fences_apply() {
        var original = Saved();
        var state = State(original);
        state.Draft[0].Notes = "Local notes";
        state.Compare([original with { Notes = "Peer notes", Version = 2 }], state.BeginReview());
        var custom = state.Draft[0].Snapshot();
        custom.Notes = "Custom merged notes";
        Assert.Single(state.Review!.Rows).Field.SetResult(MergeEntry<PartyRelationshipEditorModel>.Present(custom));
        state.Hint();
        Assert.False(state.Apply());

        state.Compare([original with { Notes = "Third notes", Version = 3 }], state.BeginReview());

        Assert.Equal("Custom merged notes", Assert.Single(state.Review!.Rows).Field.Local.Value!.Notes);
        Assert.Single(state.Review.Rows).Field.UseLocal();
        state.InputChanged(new(original.Id, RelationshipField.Notes));
        Assert.False(state.Apply());
        Assert.Equal("Local notes", state.Draft[0].Notes);
    }

    [Fact]
    public void A_saved_relationship_removed_by_a_peer_cannot_be_recreated_by_apply_or_undo() {
        var original = Saved();
        var state = State(original);
        state.Draft[0].Notes = "Local edit of deleted row";
        state.Compare([], state.BeginReview());
        var row = Assert.Single(state.Review!.Rows);
        row.Field.UseLocal();
        Assert.False(state.Review.CanApply);
        Assert.Contains("removed", state.Review.GroupError);
        row.Field.UseCurrent();

        Assert.True(state.Apply());

        Assert.Empty(state.Draft);
        Assert.False(state.CanUndo);
        state.Undo();
        Assert.Empty(state.Draft);
    }

    [Fact]
    public void A_combined_identity_budget_uses_complete_snapshot_choices_without_losing_unrendered_rows() {
        var original = Enumerable.Range(0, PartyMergeReview.DetailedRowLimit - 1).Select(_ => RelationshipValues.FromOwner(Saved(related: Guid.NewGuid()))).ToList();
        var local = RelationshipValues.Snapshot(original);
        var localAdded = RelationshipValues.FromOwner(Saved(related: Guid.NewGuid()));
        localAdded.ExpectedVersion = 0;
        local.Add(localAdded);
        var current = RelationshipValues.Snapshot(original);
        var peerAdded = RelationshipValues.FromOwner(Saved(related: Guid.NewGuid()));
        current.Add(peerAdded);

        var review = new RelationshipMergeReview(Subject, original, local, current);

        Assert.Empty(review.Rows);
        Assert.NotNull(review.WholeCollection);
        Assert.False(review.CanApply);
        review.WholeCollection.UseCurrent();
        var result = review.Result();
        Assert.Equal(PartyMergeReview.DetailedRowLimit, result.Count);
        Assert.Contains(result, row => row.Id == peerAdded.Id);
        Assert.DoesNotContain(result, row => row.Id == localAdded.Id);
        Assert.Equal(peerAdded.ExpectedVersion, result.Single(row => row.Id == peerAdded.Id).ExpectedVersion);
    }

    [Fact]
    public void Combined_duplicate_roles_block_apply_while_buddy_and_mentor_are_distinct() {
        var buddy = RelationshipValues.FromOwner(Saved() with { RelationshipKind = PartyRelationshipKind.Supports, Notes = "Buddy" });
        buddy.ExpectedVersion = 0;
        buddy.IsOutgoing = false;
        var mentor = buddy.Snapshot();
        mentor.Id = Guid.NewGuid();
        mentor.ExpectedVersion = 1;
        mentor.Notes = "Mentor";
        var valid = new RelationshipMergeReview(Subject, [], [buddy], [mentor]);
        Assert.True(valid.CanApply);
        Assert.Equal(2, valid.Result().Count);
        mentor.Notes = "Buddy";

        var duplicate = new RelationshipMergeReview(Subject, [], [buddy], [mentor]);

        Assert.False(duplicate.CanApply);
        Assert.Contains("same relationship", duplicate.GroupError);
    }

    [Fact]
    public void Pending_picker_and_native_date_input_block_refresh_adoption_save_and_apply() {
        var original = Saved();
        var state = State(original);
        state.ReportPicker(true);
        state.Observe([original with { Notes = "Peer note", Version = 2 }]);
        Assert.True(state.IsOutdated);
        Assert.Equal("Original notes", state.Draft[0].Notes);
        Assert.Throws<InvalidOperationException>(() => state.BeginSave());
        state.InputChanged(new(original.Id, RelationshipField.Start, PendingDate: true));
        state.ReportPicker(false);
        state.Compare([original], state.BeginReview());
        Assert.False(state.Apply());
        state.ReportValidation(false);

        Assert.True(state.Apply());
        Assert.False(state.IsDirty);
    }

    [Fact]
    public void Undo_keeps_local_raw_values_and_the_new_current_row_observation() {
        var original = Saved();
        var state = State(original);
        state.Draft[0].Notes = "  Local draft  ";
        state.Compare([original with { Notes = "Peer draft", Version = 2 }], state.BeginReview());
        Assert.Single(state.Review!.Rows).Field.UseCurrent();
        Assert.True(state.Apply());

        state.Undo();

        Assert.Equal("  Local draft  ", Assert.Single(state.Draft).Notes);
        Assert.Equal(2u, state.Draft[0].ExpectedVersion);
        Assert.Equal(2u, Assert.Single(state.Original.Rows).Version);
        Assert.True(state.IsDirty);
    }

    private static RelationshipReconciliation State(params PartyRelationshipListItemModel[] rows) {
        var state = new RelationshipReconciliation();
        state.Reset(Subject, rows);
        return state;
    }
    private static PartyRelationshipListItemModel Saved(Guid? related = null) => new(Guid.NewGuid(), related ?? Related,
        "Related party", PartyType.Person, PartyRelationshipKind.MemberOf, true, false, null, null, "Original notes") { Version = 1 };
}
