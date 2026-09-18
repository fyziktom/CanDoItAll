using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.CrmHr.Components;

namespace CanDoItAll.Tests.Unit.CrmHr;

// What a read-back does to the editor drafts of one host. A dirty draft of the same target survives a reload another
// editor caused: touched fields keep the operator's value, untouched fields take the owner's newer value, the instance
// survives. A clean draft and every draft of a new target take the owner's values.
//
// The editor's own commit is reconciled against the submission it dispatched. With no later edits the owner's accepted
// values start a fresh draft. With later edits the draft instance survives, the fields typed after the dispatch keep
// the operator's text and every other field takes what the owner accepted, including the identity it assigned. A
// rejected or retired write leaves no submission behind, so it can never be mistaken for a commit.
public sealed class CrmHrDraftReconcilerTests
{
    private const string Profile = "profile";
    private const string Connections = "connections";

    [Fact]
    public void A_dirty_draft_keeps_the_touched_field_and_takes_the_owners_newer_value_of_every_untouched_field()
    {
        var reconciler = new CrmHrDraftReconciler();
        var draft = reconciler.Replace(Profile, ProfileDraft("owner note", CrmAccountRelationshipStage.Prospect));
        draft.CommercialNotes = "typed and not saved";

        // Another editor's commit moved the account to an active customer and the owner rewrote a second note.
        var reconciled = reconciler.Reconcile(
            Profile,
            draft,
            () => ProfileDraft("owner note", CrmAccountRelationshipStage.ActiveCustomer, constraintNotes: "owner constraint"),
            targetChanged: false,
            committed: false);

        Assert.Same(draft, reconciled);
        Assert.Equal("typed and not saved", reconciled.CommercialNotes);
        Assert.Equal(CrmAccountRelationshipStage.ActiveCustomer, reconciled.RelationshipStage);
        Assert.Equal("owner constraint", reconciled.ConstraintNotes);
        Assert.True(reconciler.IsDirty(Profile, reconciled));
    }

    [Fact]
    public void A_touched_field_wins_over_a_concurrent_owner_change_of_the_same_field_and_is_clean_once_they_agree()
    {
        var reconciler = new CrmHrDraftReconciler();
        var draft = reconciler.Replace(Profile, ProfileDraft("owner note", CrmAccountRelationshipStage.Prospect));
        draft.RelationshipStage = CrmAccountRelationshipStage.DormantCustomer;

        var reconciled = reconciler.Reconcile(
            Profile, draft, () => ProfileDraft("owner note", CrmAccountRelationshipStage.ActiveCustomer), targetChanged: false, committed: false);

        Assert.Same(draft, reconciled);
        Assert.Equal(CrmAccountRelationshipStage.DormantCustomer, reconciled.RelationshipStage);
        Assert.True(reconciler.IsDirty(Profile, reconciled));

        // The operator picks the value the owner holds: nothing is unsaved any more.
        reconciled.RelationshipStage = CrmAccountRelationshipStage.ActiveCustomer;
        Assert.False(reconciler.IsDirty(Profile, reconciled));
    }

    [Fact]
    public void A_clean_draft_takes_the_owners_newer_values()
    {
        var reconciler = new CrmHrDraftReconciler();
        var draft = reconciler.Replace(Profile, ProfileDraft("owner note", CrmAccountRelationshipStage.Prospect));

        var reconciled = reconciler.Reconcile(
            Profile, draft, () => ProfileDraft("owner note after another save", CrmAccountRelationshipStage.Prospect), targetChanged: false, committed: false);

        Assert.NotSame(draft, reconciled);
        Assert.Equal("owner note after another save", reconciled.CommercialNotes);
        Assert.False(reconciler.IsDirty(Profile, reconciled));
    }

    [Fact]
    public void An_own_commit_with_no_later_edits_takes_the_owners_accepted_values_into_a_fresh_draft()
    {
        var reconciler = new CrmHrDraftReconciler();
        var draft = reconciler.Replace(Profile, ProfileDraft("owner note", CrmAccountRelationshipStage.Prospect));
        draft.CommercialNotes = "typed before the save";
        var submission = reconciler.Submit(Profile, draft, static editor => editor.Snapshot());

        // The owner trimmed the note and assigned the profile identity it created.
        var reconciled = reconciler.Reconcile(
            Profile,
            draft,
            () => ProfileDraft("typed before the save", CrmAccountRelationshipStage.Prospect, id: OwnerProfileId),
            targetChanged: false,
            committed: true);

        Assert.Equal("typed before the save", submission.CommercialNotes);
        Assert.NotSame(draft, reconciled);
        Assert.Equal(OwnerProfileId, reconciled.Id);
        Assert.Equal("typed before the save", reconciled.CommercialNotes);
        Assert.False(reconciler.IsDirty(Profile, reconciled));
        Assert.False(reconciler.ChangedAfterSubmission(Profile, reconciled));
    }

    [Fact]
    public void An_own_commit_keeps_what_was_typed_after_the_dispatch_and_adopts_the_owners_identity_for_every_other_field()
    {
        var reconciler = new CrmHrDraftReconciler();
        var draft = reconciler.Replace(Profile, ProfileDraft("first note", CrmAccountRelationshipStage.Prospect));
        draft.CommercialNotes = "submitted note";
        var submission = reconciler.Submit(Profile, draft, static editor => editor.Snapshot());

        // The save is in flight and the operator keeps typing into the same field.
        draft.CommercialNotes = "typed after the save was dispatched";
        Assert.True(reconciler.ChangedAfterSubmission(Profile, draft));

        var reconciled = reconciler.Reconcile(
            Profile,
            draft,
            () => ProfileDraft("submitted note", CrmAccountRelationshipStage.Prospect, id: OwnerProfileId, lastChangedBy: "owner"),
            targetChanged: false,
            committed: true);

        // The submission carried what was dispatched; the live draft keeps what came after it, in the same instance.
        Assert.Equal("submitted note", submission.CommercialNotes);
        Assert.Same(draft, reconciled);
        Assert.Equal("typed after the save was dispatched", reconciled.CommercialNotes);
        // Fields nobody touched after the dispatch take what the owner accepted, including the identity it assigned.
        Assert.Equal(OwnerProfileId, reconciled.Id);
        Assert.Equal("owner", reconciled.LastChangedBy);
        Assert.True(reconciler.IsDirty(Profile, reconciled));
        // The submission is consumed exactly once: the next read-back is an ordinary reload again.
        Assert.False(reconciler.ChangedAfterSubmission(Profile, reconciled));
    }

    [Fact]
    public void A_rejected_write_leaves_the_draft_owning_every_field_and_is_never_read_as_a_commit()
    {
        var reconciler = new CrmHrDraftReconciler();
        var draft = reconciler.Replace(Profile, ProfileDraft("owner note", CrmAccountRelationshipStage.Prospect));
        draft.CommercialNotes = "rejected note";
        reconciler.Submit(Profile, draft, static editor => editor.Snapshot());

        // The owner refused the write. Nothing was persisted, so the draft is only dirty against its baseline.
        reconciler.Retire(Profile);
        Assert.False(reconciler.ChangedAfterSubmission(Profile, draft));

        var reconciled = reconciler.Reconcile(
            Profile,
            draft,
            () => ProfileDraft("owner note", CrmAccountRelationshipStage.Prospect, constraintNotes: "owner constraint"),
            targetChanged: false,
            committed: false);

        Assert.Same(draft, reconciled);
        Assert.Equal("rejected note", reconciled.CommercialNotes);
        Assert.Equal("owner constraint", reconciled.ConstraintNotes);
        Assert.Null(reconciled.Id);
    }

    [Fact]
    public void A_retired_operation_and_another_target_both_take_the_owners_values()
    {
        var reconciler = new CrmHrDraftReconciler();
        var retired = reconciler.Replace(Profile, ProfileDraft("owner note", CrmAccountRelationshipStage.Prospect));
        retired.CommercialNotes = "typed";
        reconciler.Submit(Profile, retired, static editor => editor.Snapshot());
        retired.CommercialNotes = "typed after the dispatch";

        // The operation was retired without a read-back; a later reload of the same target is an ordinary one, so the
        // commit path finds no submission to reconcile against and starts from the owner.
        reconciler.Retire(Profile);
        var afterRetirement = reconciler.Reconcile(
            Profile, retired, () => ProfileDraft("accepted by the owner", CrmAccountRelationshipStage.Prospect), targetChanged: false, committed: true);
        Assert.NotSame(retired, afterRetirement);
        Assert.Equal("accepted by the owner", afterRetirement.CommercialNotes);

        // A different account discards the draft even while its own write is still recorded.
        afterRetirement.CommercialNotes = "typed for this account only";
        reconciler.Submit(Profile, afterRetirement, static editor => editor.Snapshot());
        var otherTarget = reconciler.Reconcile(
            Profile, afterRetirement, () => ProfileDraft("the other account", CrmAccountRelationshipStage.Prospect), targetChanged: true, committed: false);
        Assert.NotSame(afterRetirement, otherTarget);
        Assert.Equal("the other account", otherTarget.CommercialNotes);
        Assert.False(reconciler.ChangedAfterSubmission(Profile, otherTarget));
    }

    [Fact]
    public void A_list_commit_adopts_the_owner_identity_of_an_untouched_row_and_keeps_a_row_added_after_the_dispatch()
    {
        var reconciler = new CrmHrDraftReconciler();
        var firstParty = Guid.Parse("71000000-0000-0000-0000-000000000011");
        var secondParty = Guid.Parse("71000000-0000-0000-0000-000000000012");
        var connections = reconciler.Replace(Connections, new List<CrmAccountConnectionEditorModel>());
        connections.Add(new CrmAccountConnectionEditorModel { RelatedPartyId = firstParty, Notes = "submitted" });
        reconciler.Submit(Connections, connections, static rows => rows.Select(row => row.Snapshot()).ToList());

        // While the owner writes, the operator edits the submitted row and starts a second one.
        connections[0].Notes = "typed after the save was dispatched";
        connections.Add(new CrmAccountConnectionEditorModel { RelatedPartyId = secondParty, Notes = "started after the save" });

        var createdId = Guid.Parse("71000000-0000-0000-0000-0000000000a1");
        var reconciled = reconciler.ReconcileList(
            Connections,
            connections,
            () =>
            [
                new CrmAccountConnectionEditorModel { Id = createdId, RelatedPartyId = firstParty, Notes = "submitted", IsPrimary = true }
            ],
            targetChanged: false,
            committed: true,
            nameof(CrmAccountConnectionEditorModel.RelatedPartyId),
            nameof(CrmAccountConnectionEditorModel.Role));

        Assert.Same(connections, reconciled);
        Assert.Equal(2, reconciled.Count);
        // The row this commit created adopts the identity and normalization the owner accepted, and keeps the note
        // the operator typed after the dispatch, so the next save updates that row instead of recreating it.
        Assert.Equal(createdId, reconciled[0].Id);
        Assert.True(reconciled[0].IsPrimary);
        Assert.Equal("typed after the save was dispatched", reconciled[0].Notes);
        // The row started after the dispatch is the operator's next record and the owner has never seen it.
        Assert.Null(reconciled[1].Id);
        Assert.Equal("started after the save", reconciled[1].Notes);
        // The list is based on the owner's rows again, so it is unsaved work and an ordinary reload keeps it whole.
        Assert.True(reconciler.IsDirty(Connections, reconciled));
        Assert.Same(
            reconciled,
            reconciler.ReconcileList(
                Connections,
                reconciled,
                () => [new CrmAccountConnectionEditorModel { Id = createdId, RelatedPartyId = firstParty, Notes = "submitted", IsPrimary = true }],
                targetChanged: false,
                committed: false,
                nameof(CrmAccountConnectionEditorModel.RelatedPartyId),
                nameof(CrmAccountConnectionEditorModel.Role)));
        Assert.Equal(2, reconciled.Count);
    }

    [Fact]
    public void Editors_are_tracked_independently_and_a_dirty_list_draft_is_kept_whole()
    {
        var reconciler = new CrmHrDraftReconciler();
        var profile = reconciler.Replace(Profile, ProfileDraft("owner note", CrmAccountRelationshipStage.Prospect));
        var connections = reconciler.Replace(Connections, new List<CrmAccountConnectionEditorModel> { new() { Notes = "first" } });
        connections[0].ProjectIds.Add(Guid.NewGuid());

        Assert.False(reconciler.IsDirty(Profile, profile));
        Assert.True(reconciler.IsDirty(Connections, connections));
        Assert.Same(connections, reconciler.Reconcile(Connections, connections, () => [], targetChanged: false, committed: false));
        Assert.Single(connections);
        Assert.NotSame(profile, reconciler.Reconcile(Profile, profile, () => ProfileDraft("fresh", CrmAccountRelationshipStage.Prospect), targetChanged: false, committed: false));
    }

    [Fact]
    public void An_untracked_editor_or_a_reset_takes_the_owners_values()
    {
        var reconciler = new CrmHrDraftReconciler();
        var untracked = ProfileDraft("never tracked", CrmAccountRelationshipStage.Prospect);

        Assert.False(reconciler.IsDirty(Profile, untracked));
        var first = reconciler.Reconcile(Profile, untracked, () => ProfileDraft("owner note", CrmAccountRelationshipStage.Prospect), targetChanged: false, committed: false);
        Assert.Equal("owner note", first.CommercialNotes);

        first.CommercialNotes = "typed";
        reconciler.Submit(Profile, first, static editor => editor.Snapshot());
        first.CommercialNotes = "typed after the dispatch";
        reconciler.Reset();
        Assert.False(reconciler.ChangedAfterSubmission(Profile, first));
        var afterReset = reconciler.Reconcile(Profile, first, () => ProfileDraft("owner note again", CrmAccountRelationshipStage.Prospect), targetChanged: false, committed: true);
        Assert.Equal("owner note again", afterReset.CommercialNotes);
    }

    private static readonly Guid OwnerProfileId = Guid.Parse("71000000-0000-0000-0000-0000000000f1");

    private static CrmAccountProfileEditorModel ProfileDraft(
        string commercialNotes,
        CrmAccountRelationshipStage stage,
        string constraintNotes = "",
        Guid? id = null,
        string lastChangedBy = "crm-hr-ui")
        => new()
        {
            Id = id,
            AccountPartyId = Guid.Parse("71000000-0000-0000-0000-000000000001"),
            RelationshipStage = stage,
            CommercialNotes = commercialNotes,
            ConstraintNotes = constraintNotes,
            LastChangedBy = lastChangedBy
        };
}
