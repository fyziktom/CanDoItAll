using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.CrmHr.Components;

namespace CanDoItAll.Tests.Unit.CrmHr;

// What a read-back does to the editor drafts of one host. A dirty draft of the same target survives a reload another
// editor caused: touched fields keep the operator's value, untouched fields take the owner's newer value, the instance
// survives. The committed editor, a clean draft and every draft of a new target take the owner's values.
public sealed class CrmHrDraftReconcilerTests
{
    [Fact]
    public void A_dirty_draft_keeps_the_touched_field_and_takes_the_owners_newer_value_of_every_untouched_field()
    {
        var reconciler = new CrmHrDraftReconciler();
        var draft = reconciler.Replace("profile", Profile("owner note", CrmAccountRelationshipStage.Prospect));
        draft.CommercialNotes = "typed and not saved";

        // Another editor's commit moved the account to an active customer and the owner rewrote a second note.
        var reconciled = reconciler.Reconcile(
            "profile",
            draft,
            () => Profile("owner note", CrmAccountRelationshipStage.ActiveCustomer, constraintNotes: "owner constraint"),
            targetChanged: false,
            committed: false);

        Assert.Same(draft, reconciled);
        Assert.Equal("typed and not saved", reconciled.CommercialNotes);
        Assert.Equal(CrmAccountRelationshipStage.ActiveCustomer, reconciled.RelationshipStage);
        Assert.Equal("owner constraint", reconciled.ConstraintNotes);
        Assert.True(reconciler.IsDirty("profile", reconciled));
    }

    [Fact]
    public void A_touched_field_wins_over_a_concurrent_owner_change_of_the_same_field_and_is_clean_once_they_agree()
    {
        var reconciler = new CrmHrDraftReconciler();
        var draft = reconciler.Replace("profile", Profile("owner note", CrmAccountRelationshipStage.Prospect));
        draft.RelationshipStage = CrmAccountRelationshipStage.DormantCustomer;

        var reconciled = reconciler.Reconcile(
            "profile", draft, () => Profile("owner note", CrmAccountRelationshipStage.ActiveCustomer), targetChanged: false, committed: false);

        Assert.Same(draft, reconciled);
        Assert.Equal(CrmAccountRelationshipStage.DormantCustomer, reconciled.RelationshipStage);
        Assert.True(reconciler.IsDirty("profile", reconciled));

        // The operator picks the value the owner holds: nothing is unsaved any more.
        reconciled.RelationshipStage = CrmAccountRelationshipStage.ActiveCustomer;
        Assert.False(reconciler.IsDirty("profile", reconciled));
    }

    [Fact]
    public void A_clean_draft_takes_the_owners_newer_values()
    {
        var reconciler = new CrmHrDraftReconciler();
        var draft = reconciler.Replace("profile", Profile("owner note", CrmAccountRelationshipStage.Prospect));

        var reconciled = reconciler.Reconcile(
            "profile", draft, () => Profile("owner note after another save", CrmAccountRelationshipStage.Prospect), targetChanged: false, committed: false);

        Assert.NotSame(draft, reconciled);
        Assert.Equal("owner note after another save", reconciled.CommercialNotes);
        Assert.False(reconciler.IsDirty("profile", reconciled));
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void A_new_target_or_the_editors_own_commit_always_takes_the_owners_values(bool targetChanged, bool committed)
    {
        var reconciler = new CrmHrDraftReconciler();
        var draft = reconciler.Replace("profile", Profile("owner note", CrmAccountRelationshipStage.Prospect));
        draft.CommercialNotes = "typed";

        var reconciled = reconciler.Reconcile(
            "profile", draft, () => Profile("accepted by the owner", CrmAccountRelationshipStage.Prospect), targetChanged, committed);

        Assert.NotSame(draft, reconciled);
        Assert.Equal("accepted by the owner", reconciled.CommercialNotes);
        Assert.False(reconciler.IsDirty("profile", reconciled));
    }

    [Fact]
    public void Editors_are_tracked_independently_and_a_dirty_list_draft_is_kept_whole()
    {
        var reconciler = new CrmHrDraftReconciler();
        var profile = reconciler.Replace("profile", Profile("owner note", CrmAccountRelationshipStage.Prospect));
        var connections = reconciler.Replace("connections", new List<CrmAccountConnectionEditorModel> { new() { Notes = "first" } });
        connections[0].ProjectIds.Add(Guid.NewGuid());

        Assert.False(reconciler.IsDirty("profile", profile));
        Assert.True(reconciler.IsDirty("connections", connections));
        Assert.Same(connections, reconciler.Reconcile("connections", connections, () => [], targetChanged: false, committed: false));
        Assert.Single(connections);
        Assert.NotSame(profile, reconciler.Reconcile("profile", profile, () => Profile("fresh", CrmAccountRelationshipStage.Prospect), targetChanged: false, committed: false));
    }

    [Fact]
    public void An_untracked_editor_or_a_reset_takes_the_owners_values()
    {
        var reconciler = new CrmHrDraftReconciler();
        var untracked = Profile("never tracked", CrmAccountRelationshipStage.Prospect);

        Assert.False(reconciler.IsDirty("profile", untracked));
        var first = reconciler.Reconcile("profile", untracked, () => Profile("owner note", CrmAccountRelationshipStage.Prospect), targetChanged: false, committed: false);
        Assert.Equal("owner note", first.CommercialNotes);

        first.CommercialNotes = "typed";
        reconciler.Reset();
        var afterReset = reconciler.Reconcile("profile", first, () => Profile("owner note again", CrmAccountRelationshipStage.Prospect), targetChanged: false, committed: false);
        Assert.Equal("owner note again", afterReset.CommercialNotes);
    }

    private static CrmAccountProfileEditorModel Profile(
        string commercialNotes,
        CrmAccountRelationshipStage stage,
        string constraintNotes = "")
        => new()
        {
            AccountPartyId = Guid.Parse("71000000-0000-0000-0000-000000000001"),
            RelationshipStage = stage,
            CommercialNotes = commercialNotes,
            ConstraintNotes = constraintNotes
        };
}
