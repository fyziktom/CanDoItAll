using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.CrmHr.Components;

namespace CanDoItAll.Tests.Unit.CrmHr;

public sealed class RecruitmentSupportObservationTests {
    private const string Editor = nameof(RecruitmentSupportAssignmentsEditorModel);
    private static readonly Guid Party = Guid.NewGuid();
    private static readonly Guid ManagerRow = Guid.NewGuid();

    [Fact]
    public void A_dirty_support_draft_keeps_its_observed_subset_when_another_read_changes_untouched_fields() {
        var drafts = new CrmHrDraftReconciler();
        var manager = Guid.NewGuid();
        var original = Create(manager, 1);
        var draft = drafts.Replace(Editor, original);
        var localManager = Guid.NewGuid();
        draft.ManagerPartyId = localManager;
        var peer = Create(manager, 2);
        peer.BuddyPartyId = Guid.NewGuid();
        peer.SetOriginal(new([new(ManagerRow, manager, 2), new(Guid.NewGuid(), peer.BuddyPartyId.Value, 2)]));

        var retained = drafts.Reconcile(Editor, draft, () => peer, targetChanged: false, committed: false);

        Assert.Same(draft, retained);
        Assert.Equal(localManager, retained.ManagerPartyId);
        Assert.Equal(peer.BuddyPartyId, retained.BuddyPartyId);
        Assert.Equal(1u, Assert.Single(retained.Original!.Rows).Version);
        Assert.True(drafts.IsDirty(Editor, retained));
    }

    [Fact]
    public void Exact_acceptance_advances_the_private_observation_while_preserving_a_later_manager_selection() {
        var drafts = new CrmHrDraftReconciler();
        var manager = Guid.NewGuid();
        var draft = drafts.Replace(Editor, Create(manager, 1));
        draft.ManagerPartyId = Guid.NewGuid();
        var submitted = drafts.Submit(Editor, draft, value => value.Snapshot());
        var accepted = Create(submitted.ManagerPartyId!.Value, 2);
        var later = Guid.NewGuid();
        draft.ManagerPartyId = later;

        var retained = drafts.Reconcile(Editor, draft, () => accepted, targetChanged: false, committed: true,
            adoptOwnerState: (local, owner) => local.SetOriginal(owner.Original!));

        Assert.Same(draft, retained);
        Assert.Equal(later, retained.ManagerPartyId);
        Assert.Equal(2u, Assert.Single(retained.Original!.Rows).Version);
        Assert.Equal(1u, Assert.Single(submitted.Original!.Rows).Version);
        Assert.True(drafts.IsDirty(Editor, retained));
    }

    private static RecruitmentSupportAssignmentsEditorModel Create(Guid manager, uint version) {
        var model = new RecruitmentSupportAssignmentsEditorModel { PartyId = Party, ManagerPartyId = manager };
        model.SetOriginal(new([new(ManagerRow, manager, version)]));
        return model;
    }
}
