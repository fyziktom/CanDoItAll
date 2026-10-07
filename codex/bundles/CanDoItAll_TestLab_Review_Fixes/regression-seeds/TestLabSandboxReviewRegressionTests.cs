using CanDoItAll.TestLab.UI;
using CanDoItAll.TestLab.UiSandbox;
using Xunit;

namespace CanDoItAll.Tests.Components.TestLab;

// These proposed regression seeds have not been compiled or executed by the reviewer.
public sealed class TestLabSandboxReviewRegressionTests {
    [Fact]
    public async Task Party_change_during_committed_readback_settles_the_same_draft_without_replaying_save() {
        using var view = new TestLabScenarioWorkspace(TestLabScenario.DelayedReadback, () => { });
        var draft = Assert.IsType<TestLabDraft>(view.State.Draft);
        var context = draft.Context;
        var pendingSave = view.SaveAsync(draft);

        Assert.Equal(1, view.Store.Commits);
        Assert.Equal(1, view.PendingCount);
        Assert.NotNull(draft.Pending);
        var committedId = Assert.IsType<Guid>(draft.Model.Id);
        var committedCaseIds = draft.Model.Cases.Select(item => item.Id).ToArray();

        await view.ChangePartyAsync(draft, null);
        // A correct independent lane may still need its normal completion signal.
        view.CompletePending();
        await pendingSave.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Same(draft, view.State.Draft);
        Assert.Same(context, draft.Context);
        Assert.Null(draft.Model.ResponsiblePartyId);
        Assert.Null(draft.Pending);
        Assert.True(draft.CanSave);
        Assert.True(draft.SaveState is TestLabSaveState.Saved or TestLabSaveState.SavedWithWarning);
        Assert.Equal(0, view.PendingCount);
        Assert.Equal(1, view.Store.Commits);
        Assert.Equal(committedId, draft.Model.Id);
        Assert.Equal(committedCaseIds, draft.Model.Cases.Select(item => item.Id).ToArray());

        await view.RetryAsync();
        Assert.Equal(1, view.Store.Commits);
        Assert.Null(draft.Model.ResponsiblePartyId);
        Assert.Equal(committedId, draft.Model.Id);
        Assert.Same(context, draft.Context);
    }

    [Fact]
    public async Task Global_plan_keeps_a_known_saved_party_resolved_without_recapturing_project_admission() {
        using var view = new TestLabScenarioWorkspace(TestLabScenario.Representative, () => { });
        var draft = Assert.IsType<TestLabDraft>(view.State.Draft);
        var partyId = Assert.IsType<Guid>(draft.Model.ResponsiblePartyId);
        Assert.Equal(view.Store.PartyId, partyId);
        Assert.Contains(view.State.Parties, item => item.PartyId == partyId);

        await view.ChangeProjectAsync(draft, null);

        Assert.Same(draft, view.State.Draft);
        Assert.Null(draft.Model.ProjectId);
        Assert.Null(draft.Model.ExpectedProjectAdmission);
        Assert.Equal(partyId, draft.Model.ResponsiblePartyId);
        Assert.Contains(view.State.Parties,
            item => item.PartyId == partyId && !string.IsNullOrWhiteSpace(item.DisplayName));
        Assert.Equal(TestLabReadState.Ready, view.State.PartiesRead);
        Assert.Equal(0, view.Store.Commits);
    }
}
