using CanDoItAll.CrmHr.UI.Parties;
using CanDoItAll.Modules.CrmHr;

namespace CanDoItAll.Tests.Unit.CrmHr;

public sealed class PartyConcurrentReviewTests {
    [Fact]
    public void Disjoint_additions_use_the_complete_collection_mode_when_the_combined_identity_budget_is_exceeded() {
        var original = Party();
        var local = original.Snapshot();
        var current = original.Snapshot();
        local.Addresses = Enumerable.Range(0, 600).Select(index => new PartyAddressEditorModel { Id = Guid.NewGuid(), Line1 = $"Local {index}" }).ToList();
        current.Addresses = Enumerable.Range(0, 600).Select(index => new PartyAddressEditorModel { Id = Guid.NewGuid(), Line1 = $"Current {index}" }).ToList();

        var review = new PartyMergeReview(original, local, current);

        Assert.Empty(review.Addresses);
        Assert.NotNull(review.LargeAddresses);
        Assert.False(review.CanApply);
        review.LargeAddresses.UseLocal();
        Assert.True(review.CanApply);
        Assert.Equal(local.Addresses.Select(row => row.Id), review.Result().Addresses.Select(row => row.Id));
    }

    [Fact]
    public void Collections_above_the_detail_budget_require_a_whole_collection_choice_and_keep_every_row() {
        var original = LargeParty();
        var local = original.Snapshot();
        var current = original.Snapshot();
        local.Addresses[0].Line1 = "Local address";
        current.Addresses[1000].Line1 = "Current last address";

        var review = new PartyMergeReview(original, local, current);

        Assert.Empty(review.Addresses);
        Assert.NotNull(review.LargeAddresses);
        Assert.False(review.CanApply);
        review.LargeAddresses.UseCurrent();
        Assert.True(review.CanApply);
        var result = review.Result();
        Assert.Equal(1001, result.Addresses.Count);
        Assert.Equal("Current last address", result.Addresses[1000].Line1);
        Assert.Equal("Address 0", result.Addresses[0].Line1);
        Assert.Equal("Local address", local.Addresses[0].Line1);
    }

    [Fact]
    public void Refreshing_an_oversized_comparison_retains_the_previously_chosen_complete_local_collection() {
        var original = LargeParty();
        var draft = PartyEditorViewModel.FromEditorModel(original);
        var state = new PartyEditorReconciliation();
        state.Reset(draft);
        draft.Addresses[0].Line1 = "Local address";
        var current = original.Snapshot();
        current.Addresses[1000].Line1 = "Second writer";
        state.Compare(draft, current, state.BeginReview());
        state.Review!.LargeAddresses!.UseLocal();
        state.Hint();
        Assert.False(state.Apply(draft));
        current.Addresses[1000].Line1 = "Third writer";

        state.Compare(draft, current, state.BeginReview());

        Assert.Equal(1001, state.Review!.LargeAddresses!.Local.Count);
        Assert.Equal("Local address", state.Review.LargeAddresses.Local[0].Line1);
        Assert.Equal("Third writer", state.Review.LargeAddresses.Current[1000].Line1);
        Assert.False(state.Review.CanApply);
        Assert.Equal("Local address", draft.Addresses[0].Line1);
    }

    [Fact]
    public void Oversized_snapshots_with_missing_empty_or_duplicate_row_ids_fail_without_silent_key_repair() {
        var original = LargeParty();
        var local = original.Snapshot();
        foreach (var invalid in new Guid?[] { null, Guid.Empty, original.Addresses[0].Id }) {
            local.Addresses[1000].Id = invalid;

            Assert.Throws<ArgumentException>(() => new PartyMergeReview(original, local, original));

            Assert.Equal(invalid, local.Addresses[1000].Id);
            Assert.Equal(1001, local.Addresses.Count);
        }
    }

    [Fact]
    public void Draft_capture_preserves_raw_child_text_and_the_owners_primary_flags() {
        var original = Party();
        original.Roles[0].IsPrimary = false;
        original.Roles.Add(new() { Id = Guid.NewGuid(), RoleKind = PartyRoleKind.Stakeholder, Title = "Secondary" });
        original.ContactPoints[0].IsPrimary = false;
        original.ContactPoints.Add(new() { Id = Guid.NewGuid(), ContactType = PartyContactType.Website, Value = "https://example.test", IsPrimary = true });
        var draft = PartyEditorViewModel.FromEditorModel(original);
        draft.AdditionalRoles[0].Title = "  Raw role title  ";
        draft.AdditionalRoles[0].Notes = "  Raw role notes  ";
        draft.PrimaryEmail = "  raw@example.test  ";
        draft.AdditionalContactPoints[0].Label = "  Raw contact label  ";

        var captured = PartyEditorReconciliation.Capture(draft);

        Assert.Equal("  Raw role title  ", captured.Roles.Single(row => row.Id == original.Roles[1].Id).Title);
        Assert.Equal("  Raw role notes  ", captured.Roles.Single(row => row.Id == original.Roles[1].Id).Notes);
        Assert.False(captured.Roles.Single(row => row.Id == original.Roles[0].Id).IsPrimary);
        Assert.Equal("  raw@example.test  ", captured.ContactPoints.Single(row => row.ContactType == PartyContactType.Email).Value);
        Assert.False(captured.ContactPoints.Single(row => row.ContactType == PartyContactType.Email).IsPrimary);
        Assert.True(captured.ContactPoints.Single(row => row.ContactType == PartyContactType.Website).IsPrimary);
        Assert.Equal("  Raw contact label  ", captured.ContactPoints.Single(row => row.ContactType == PartyContactType.Website).Label);
    }

    [Fact]
    public void Acknowledgement_keeps_raw_contact_input_typed_while_the_owner_was_saving() {
        var draft = PartyEditorViewModel.FromEditorModel(Party());
        var state = new PartyEditorReconciliation();
        state.Reset(draft);
        draft.PrimaryEmail = "submitted@example.test";
        var submitted = state.BeginSave(draft);
        draft.PrimaryEmail = "  later@example.test  ";
        state.InputChanged(new(new(PartyEditableField.Contacts, draft.PrimaryEmailIdentity)));
        var accepted = submitted.Snapshot();
        accepted.UpdatedAtUtc = accepted.UpdatedAtUtc!.Value.AddSeconds(1);

        state.Accepted(draft, submitted, new(accepted.Id!.Value, accepted));

        Assert.Equal("  later@example.test  ", draft.PrimaryEmail);
        Assert.True(state.IsDirty(draft));
        Assert.Equal(accepted.UpdatedAtUtc, draft.UpdatedAtUtc);
    }

    [Fact]
    public void An_owner_acknowledgement_preserves_removed_primary_flags_without_synthetic_promotion() {
        var draft = PartyEditorViewModel.FromEditorModel(Party());
        var state = new PartyEditorReconciliation();
        state.Reset(draft);
        var submitted = state.BeginSave(draft);
        var accepted = submitted.Snapshot();
        accepted.UpdatedAtUtc = accepted.UpdatedAtUtc!.Value.AddSeconds(1);
        accepted.Roles[0].IsPrimary = false;
        accepted.ContactPoints[0].IsPrimary = false;

        state.Accepted(draft, submitted, new(accepted.Id!.Value, accepted));

        Assert.False(Assert.Single(draft.ToEditorModel().Roles).IsPrimary);
        Assert.False(Assert.Single(draft.ToEditorModel().ContactPoints).IsPrimary);
        Assert.False(state.IsDirty(draft));
    }

    [Fact]
    public void Unknown_retains_an_independent_submission_and_neither_a_hint_nor_a_current_read_proves_it_committed() {
        var draft = PartyEditorViewModel.FromEditorModel(Party());
        var state = new PartyEditorReconciliation();
        state.Reset(draft);
        draft.DisplayName = "Submitted name";
        var submitted = state.BeginSave(draft);
        state.Unknown(submitted);
        submitted.DisplayName = "Mutated caller copy";
        draft.DisplayName = "Later local name";
        state.Hint();
        var current = draft.ToEditorModel();
        current.DisplayName = "Third writer";
        current.UpdatedAtUtc = current.UpdatedAtUtc!.Value.AddSeconds(1);
        state.Observe(draft, current);
        state.Compare(draft, current, state.BeginReview());
        state.Review!.DisplayName.UseCurrent();

        Assert.Equal("Submitted name", state.UnknownSubmission!.DisplayName);
        Assert.Equal("Later local name", draft.DisplayName);
        Assert.True(state.IsUnknown);
        Assert.False(state.Apply(draft));
        Assert.Throws<InvalidOperationException>(() => state.BeginSave(draft));
    }

    [Fact]
    public void Acknowledgement_and_dirty_checks_are_not_limited_by_the_interactive_comparison_row_budget() {
        var model = Party();
        model.Addresses = Enumerable.Range(0, 1001).Select(index => new PartyAddressEditorModel {
            Id = Guid.NewGuid(), Line1 = $"Address {index}"
        }).ToList();
        var draft = PartyEditorViewModel.FromEditorModel(model);
        var state = new PartyEditorReconciliation();
        state.Reset(draft);
        Assert.False(state.IsDirty(draft));
        var submitted = state.BeginSave(draft);
        var accepted = submitted.Snapshot();
        accepted.UpdatedAtUtc = accepted.UpdatedAtUtc!.Value.AddSeconds(1);
        draft.Addresses[1000].Line1 = "Later local address";

        state.Accepted(draft, submitted, new(model.Id!.Value, accepted));

        Assert.Equal(1001, draft.Addresses.Count);
        Assert.Equal("Later local address", draft.Addresses[1000].Line1);
        Assert.True(state.IsDirty(draft));
        Assert.Equal(accepted.UpdatedAtUtc, draft.UpdatedAtUtc);
    }

    [Fact]
    public void Editing_the_parent_and_returning_to_the_same_value_invalidates_an_open_review() {
        var draft = PartyEditorViewModel.FromEditorModel(Party());
        var state = new PartyEditorReconciliation();
        state.Reset(draft);
        var current = draft.ToEditorModel();
        current.Summary = "Peer summary";
        current.UpdatedAtUtc = current.UpdatedAtUtc!.Value.AddSeconds(1);
        state.Compare(draft, current, state.BeginReview());
        state.InputChanged(new(new(PartyEditableField.DisplayName)));
        state.InputChanged(new(new(PartyEditableField.DisplayName)));

        Assert.False(state.Apply(draft));
        Assert.Empty(draft.Summary);
        state.Compare(draft, current, state.BeginReview());
        Assert.True(state.Apply(draft));
    }

    [Fact]
    public void An_edit_changed_back_to_the_submission_still_preserves_its_raw_value_after_acknowledgement() {
        var draft = PartyEditorViewModel.FromEditorModel(Party());
        var state = new PartyEditorReconciliation();
        state.Reset(draft);
        draft.DisplayName = " Submitted raw name ";
        var submitted = state.BeginSave(draft);
        draft.DisplayName = "Intermediate input";
        state.InputChanged(new(new(PartyEditableField.DisplayName)));
        draft.DisplayName = submitted.DisplayName;
        state.InputChanged(new(new(PartyEditableField.DisplayName)));
        var accepted = submitted.Snapshot();
        accepted.DisplayName = accepted.DisplayName.Trim();
        accepted.UpdatedAtUtc = accepted.UpdatedAtUtc!.Value.AddSeconds(1);

        state.Accepted(draft, submitted, new(accepted.Id!.Value, accepted));

        Assert.Equal(" Submitted raw name ", draft.DisplayName);
        Assert.Equal(accepted.UpdatedAtUtc, draft.UpdatedAtUtc);
        Assert.True(state.IsDirty(draft));
    }

    [Fact]
    public void Pending_date_input_is_dirty_even_when_the_last_parsed_value_matches_the_original() {
        var original = Party();
        var draft = PartyEditorViewModel.FromEditorModel(original);
        var state = new PartyEditorReconciliation();
        state.Reset(draft);
        state.InputChanged(new(new(PartyEditableField.Roles, original.Roles[0].Id!.Value), PendingDateInput: true));
        var current = original.Snapshot();
        current.Roles[0].ValidFromUtc = original.UpdatedAtUtc;
        current.UpdatedAtUtc = current.UpdatedAtUtc!.Value.AddSeconds(1);

        state.Observe(draft, current);

        Assert.True(state.IsDirty(draft));
        Assert.True(state.IsOutdated);
        Assert.Null(Assert.Single(draft.ToEditorModel().Roles).ValidFromUtc);
        Assert.Equal(original.UpdatedAtUtc, draft.UpdatedAtUtc);
    }

    [Fact]
    public void Pending_tags_and_contact_wizard_input_prevent_clean_observation_adoption() {
        var original = Party();
        var draft = PartyEditorViewModel.FromEditorModel(original);
        var state = new PartyEditorReconciliation();
        var current = original.Snapshot();
        current.DisplayName = "Peer name";
        current.UpdatedAtUtc = current.UpdatedAtUtc!.Value.AddSeconds(1);
        state.Reset(draft);
        state.SetPendingTagInput("unfinished-tag");
        state.Observe(draft, current);
        Assert.Equal(original.DisplayName, draft.DisplayName);
        Assert.Equal("unfinished-tag", state.PendingTagInput);
        state.Reset(draft);
        state.ReportChildInput(true);

        state.Observe(draft, current);

        Assert.Equal(original.DisplayName, draft.DisplayName);
        Assert.True(state.IsOutdated);
        Assert.Throws<InvalidOperationException>(() => state.BeginSave(draft));
    }

    [Fact]
    public void Applying_owner_values_keeps_existing_child_models_alive_by_their_ids() {
        var model = Party();
        model.Roles.Add(new() { Id = Guid.NewGuid(), RoleKind = PartyRoleKind.Stakeholder, Title = "Original title" });
        model.ConfidentialNotes.Add(new() { Id = Guid.NewGuid(), NoteText = "Original note" });
        var view = PartyEditorViewModel.FromEditorModel(model);
        var role = Assert.Single(view.AdditionalRoles);
        var note = Assert.Single(view.ConfidentialNotes);
        var notes = view.ConfidentialNotes;
        var current = model.Snapshot();
        current.Roles[1].Title = "New title";
        current.ConfidentialNotes[0].NoteText = "New note";

        view.Apply(current);

        Assert.Same(role, Assert.Single(view.AdditionalRoles));
        Assert.Same(note, Assert.Single(view.ConfidentialNotes));
        Assert.Same(notes, view.ConfidentialNotes);
        Assert.Equal("New title", role.Title);
        Assert.Equal("New note", note.NoteText);
    }

    [Fact]
    public void Disjoint_scalar_and_child_edits_combine_but_delete_against_edit_requires_a_choice() {
        var original = Party();
        var local = original.Snapshot();
        var current = original.Snapshot();
        local.Summary = "Local summary";
        local.ContactPoints.Clear();
        current.DisplayName = "Current name";
        current.ContactPoints[0].Value = "current@example.test";
        var review = new PartyMergeReview(original, local, current);

        Assert.Equal(local.Summary, review.Summary.Result);
        Assert.Equal(current.DisplayName, review.DisplayName.Result);
        var contact = Assert.Single(review.Contacts);
        Assert.False(contact.Field.IsResolved);
        Assert.False(review.CanApply);
        contact.Field.UseCurrent();
        Assert.True(review.CanApply);
        Assert.Equal(current.ContactPoints[0].Id, Assert.Single(review.Result().ContactPoints).Id);
        Assert.Equal("Original party", original.DisplayName);
        Assert.Empty(local.ContactPoints);
    }

    [Fact]
    public void Primary_role_and_contact_ids_remain_stable_across_view_round_trips_and_new_draft_captures() {
        var model = Party();
        var view = PartyEditorViewModel.FromEditorModel(model);
        var first = view.ToEditorModel();
        Assert.Equal(model.Roles[0].Id, Assert.Single(first.Roles).Id);
        Assert.Equal(model.ContactPoints[0].Id, Assert.Single(first.ContactPoints).Id);
        var fresh = PartyEditorViewModel.CreateNew();
        fresh.PrimaryEmail = "new@example.test";
        fresh.PrimaryRoleKindText = nameof(PartyRoleKind.Customer);
        var initial = fresh.ToEditorModel();
        Assert.Equal(initial.Roles[0].Id, fresh.ToEditorModel().Roles[0].Id);
        Assert.Equal(initial.ContactPoints[0].Id, fresh.ToEditorModel().ContactPoints[0].Id);
    }

    [Fact]
    public void Dirty_observation_retains_both_the_original_token_and_the_local_fields() {
        var original = Party();
        var draft = PartyEditorViewModel.FromEditorModel(original);
        var state = new PartyEditorReconciliation();
        state.Reset(draft);
        draft.Summary = "Unsaved local summary";
        var current = original.Snapshot();
        current.DisplayName = "Peer name";
        current.UpdatedAtUtc = current.UpdatedAtUtc!.Value.AddSeconds(1);

        state.Observe(draft, current);

        Assert.True(state.IsOutdated);
        Assert.Equal(original.UpdatedAtUtc, draft.UpdatedAtUtc);
        Assert.Equal(original.DisplayName, draft.DisplayName);
        Assert.Equal("Unsaved local summary", draft.Summary);
    }

    [Fact]
    public void Accepted_state_preserves_later_input_and_a_later_foreign_read_cannot_become_the_accepted_state() {
        var draft = PartyEditorViewModel.FromEditorModel(Party());
        var state = new PartyEditorReconciliation();
        state.Reset(draft);
        draft.DisplayName = "Submitted name";
        var submitted = state.BeginSave(draft);
        var accepted = submitted.Snapshot();
        accepted.DisplayName = "Normalized accepted name";
        accepted.UpdatedAtUtc = accepted.UpdatedAtUtc!.Value.AddSeconds(1);
        draft.Summary = "Typed while saving";

        state.Accepted(draft, submitted, new(accepted.Id!.Value, accepted));
        var later = accepted.Snapshot();
        later.DisplayName = "Third writer";
        later.UpdatedAtUtc = later.UpdatedAtUtc!.Value.AddSeconds(1);
        state.Observe(draft, later);

        Assert.Equal(accepted.DisplayName, draft.DisplayName);
        Assert.Equal("Typed while saving", draft.Summary);
        Assert.Equal(accepted.UpdatedAtUtc, draft.UpdatedAtUtc);
        Assert.True(state.IsOutdated);
    }

    [Fact]
    public void A_new_hint_invalidates_apply_and_refresh_preserves_work_done_in_review() {
        var original = Party();
        var draft = PartyEditorViewModel.FromEditorModel(original);
        var state = new PartyEditorReconciliation();
        state.Reset(draft);
        draft.DisplayName = "Local name";
        var current = original.Snapshot();
        current.DisplayName = "Peer name";
        current.UpdatedAtUtc = current.UpdatedAtUtc!.Value.AddSeconds(1);
        state.Compare(draft, current, state.BeginReview());
        state.Review!.DisplayName.SetResult("Work inside the comparison");
        state.Hint();
        Assert.False(state.Apply(draft));
        current.DisplayName = "Third name";
        state.Compare(draft, current, state.BeginReview());

        Assert.Equal("Work inside the comparison", state.Review!.DisplayName.Local);
        Assert.Equal("Local name", draft.DisplayName);
        Assert.False(state.ReviewIsStale);
    }

    [Fact]
    public void Applying_and_undoing_are_local_and_keep_the_adopted_original_while_unknown_outcomes_block_save() {
        var original = Party();
        var draft = PartyEditorViewModel.FromEditorModel(original);
        var state = new PartyEditorReconciliation();
        state.Reset(draft);
        draft.Summary = "Local summary";
        var current = original.Snapshot();
        current.DisplayName = "Peer name";
        current.UpdatedAtUtc = current.UpdatedAtUtc!.Value.AddSeconds(1);
        state.Compare(draft, current, state.BeginReview());
        Assert.True(state.Apply(draft));
        Assert.Equal(current.DisplayName, draft.DisplayName);
        Assert.Equal("Local summary", draft.Summary);
        state.Undo(draft);
        Assert.Equal(original.DisplayName, draft.DisplayName);
        Assert.Equal(current.UpdatedAtUtc, draft.UpdatedAtUtc);
        Assert.Equal("Original party", original.DisplayName);
        state.Unknown(draft.ToEditorModel());
        Assert.Throws<InvalidOperationException>(() => state.BeginSave(draft));
        Assert.True(state.IsUnknown);
    }

    private static PartyEditorModel LargeParty() {
        var model = Party();
        model.Addresses = Enumerable.Range(0, 1001).Select(index => new PartyAddressEditorModel { Id = Guid.NewGuid(), Line1 = $"Address {index}" }).ToList();
        return model;
    }

    private static PartyEditorModel Party() => new() {
        Id = Guid.NewGuid(),
        DisplayName = "Original party",
        UpdatedAtUtc = new DateTimeOffset(2030, 1, 2, 3, 4, 5, TimeSpan.Zero),
        Roles = [new() { Id = Guid.NewGuid(), RoleKind = PartyRoleKind.Customer, IsPrimary = true, Title = "Customer" }],
        ContactPoints = [new() { Id = Guid.NewGuid(), ContactType = PartyContactType.Email, IsPrimary = true,
            Label = "Email", Value = "original@example.test", NormalizedValue = "original@example.test", IsPublic = true }]
    };
}
