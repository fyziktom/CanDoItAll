using Bunit;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.TestLab.UI;
using CanDoItAll.TestLab.UiSandbox;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.TestLab;

public sealed class TestLabSandboxReviewTests : BunitContext {
    public TestLabSandboxReviewTests() {
        Services.AddCanDoItAllBaseLib();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public async Task Party_change_during_real_form_readback_preserves_draft_and_settles_without_replay() {
        var cut = Render<CanDoItAll.TestLab.UiSandbox.Components.Home>();
        await cut.InvokeAsync(() => cut.Find("[data-testid=testlab-scenario]").ChangeAsync(new() { Value = nameof(TestLabScenario.DelayedReadback) }));
        var view = (TestLabScenarioWorkspace)cut.FindComponent<TestLabWorkspaceSurface>().Instance.View;
        var draft = view.State.Draft!;
        var context = draft.Context;
        var party = draft.Model.ResponsiblePartyId;
        var save = cut.InvokeAsync(() => cut.Find("form").SubmitAsync());
        cut.WaitForAssertion(() => Assert.Equal(1, view.PendingCount));
        var id = draft.Model.Id!.Value;
        var childIds = ChildIds(draft);
        Assert.Equal(1, view.Store.Commits);
        await cut.InvokeAsync(() => cut.Find("[data-testid=testlab-responsible-party-select]").ChangeAsync(new() { Value = string.Empty }));
        await cut.InvokeAsync(() => cut.Find("[data-testid=testlab-title-input]").InputAsync(new() { Value = "Newer unblurred title" }));
        await cut.InvokeAsync(() => cut.FindComponent<SecondaryTabs>().FindAll("button").Single(button => button.TextContent.Trim().StartsWith("Cases", StringComparison.Ordinal)).ClickAsync(new()));
        await cut.InvokeAsync(view.CompletePending);
        await save.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Same(draft, view.State.Draft);
        Assert.Same(context, cut.FindComponent<EditForm>().Instance.EditContext);
        Assert.Null(draft.Pending);
        Assert.True(draft.CanSave);
        Assert.True(draft.SaveState is TestLabSaveState.Saved or TestLabSaveState.SavedWithWarning);
        Assert.Null(draft.Model.ResponsiblePartyId);
        Assert.Equal("Newer unblurred title", draft.Model.Title);
        Assert.Equal(TestLabSection.Cases, view.State.Section);
        Assert.Equal(party, view.Store.Read(id)!.ResponsiblePartyId);
        Assert.Equal(childIds, ChildIds(draft));
        Assert.Equal(0, view.PendingCount);
        await cut.InvokeAsync(view.RetryAsync);
        Assert.Equal(1, view.Store.Commits);
        Assert.Null(draft.Model.ResponsiblePartyId);
        var second = cut.InvokeAsync(() => cut.Find("form").SubmitAsync());
        cut.WaitForAssertion(() => Assert.Equal(1, view.PendingCount));
        await cut.InvokeAsync(view.CompletePending);
        await second.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(2, view.Store.Commits);
        Assert.Equal(id, draft.Model.Id);
        Assert.Equal(childIds, ChildIds(draft));
        Assert.Null(view.Store.Read(id)!.ResponsiblePartyId);
        Assert.Equal("Newer unblurred title", view.Store.Read(id)!.Title);
    }

    [Fact]
    public async Task Global_project_control_keeps_the_known_saved_party_named() {
        var cut = Render<CanDoItAll.TestLab.UiSandbox.Components.Home>();
        var view = (TestLabScenarioWorkspace)cut.FindComponent<TestLabWorkspaceSurface>().Instance.View;
        var draft = view.State.Draft!;
        var party = draft.Model.ResponsiblePartyId;
        await cut.InvokeAsync(() => cut.Find("[data-testid=testlab-project-select]").ChangeAsync(new() { Value = string.Empty }));
        Assert.Same(draft, view.State.Draft);
        Assert.Null(draft.Model.ProjectId);
        Assert.Null(draft.Model.ExpectedProjectAdmission);
        Assert.Equal(party, draft.Model.ResponsiblePartyId);
        Assert.Contains(view.State.Parties, item => item.PartyId == party && item.DisplayName == "Delivery reviewer");
        Assert.DoesNotContain("Unavailable responsible party", cut.Markup);
        Assert.Contains("Delivery reviewer", cut.Find("[data-testid=testlab-responsible-party-select]").TextContent);
        Assert.Equal(0, view.Store.Commits);
    }

    [Fact]
    public async Task Project_options_and_saved_party_existence_are_independent_and_unknown_ids_are_not_fabricated() {
        using var view = new TestLabScenarioWorkspace(TestLabScenario.Representative, () => { });
        var draft = view.State.Draft!;
        var project = view.Store.Projects[1];
        Assert.Empty(view.Store.ListParties(project.Id));
        await view.ChangeProjectAsync(draft, project.Id);
        Assert.Equal(view.Store.PartyId, Assert.Single(view.State.Parties).PartyId);
        Assert.Same(project.Admission, draft.Model.ExpectedProjectAdmission);
        var unknown = Guid.NewGuid();
        await view.ChangePartyAsync(draft, unknown);
        Assert.Empty(view.State.Parties);
        Assert.Equal(TestLabReadState.Empty, view.State.PartiesRead);
        Assert.Equal(unknown, draft.Model.ResponsiblePartyId);
        var cut = Render<TestLabWorkspaceSurface>(parameters => parameters.Add(item => item.View, view));
        Assert.Contains("Unavailable responsible party", cut.Markup);
    }

    [Fact]
    public async Task Failed_global_lookup_preserves_identity_and_reports_failure_until_read_only_recovery() {
        using var view = new TestLabScenarioWorkspace(TestLabScenario.ReferenceFailure, () => { });
        var draft = view.State.Draft!;
        Assert.Null(draft.Model.ProjectId);
        Assert.Equal(view.Store.PartyId, draft.Model.ResponsiblePartyId);
        Assert.Equal(TestLabReadState.Unavailable, view.State.PartiesRead);
        view.Store.FailPartyLookup = false;
        await view.RetryAsync();
        Assert.Equal("Delivery reviewer", Assert.Single(view.State.Parties).DisplayName);
        Assert.Equal(TestLabReadState.Ready, view.State.PartiesRead);
        Assert.Equal(view.Store.PartyId, draft.Model.ResponsiblePartyId);
        Assert.Null(draft.Model.ExpectedProjectAdmission);
        Assert.Equal(0, view.Store.Commits);
    }

    [Theory]
    [InlineData(TestLabScenario.DelayedReferences, false)]
    [InlineData(TestLabScenario.DelayedReferences, true)]
    [InlineData(TestLabScenario.DelayedPartyLookup, false)]
    [InlineData(TestLabScenario.DelayedPartyLookup, true)]
    public async Task Stale_reference_success_or_failure_cannot_replace_a_newer_party_choice(TestLabScenario scenario, bool fail) {
        var cut = Render<CanDoItAll.TestLab.UiSandbox.Components.Home>();
        await cut.InvokeAsync(() => cut.Find("[data-testid=testlab-scenario]").ChangeAsync(new() { Value = scenario.ToString() }));
        var view = (TestLabScenarioWorkspace)cut.FindComponent<TestLabWorkspaceSurface>().Instance.View;
        var draft = view.State.Draft!;
        var old = cut.InvokeAsync(() => view.ChangeProjectAsync(draft, null));
        cut.WaitForAssertion(() => Assert.Equal(1, view.PendingCount));
        Task replacement = Task.CompletedTask;
        await cut.InvokeAsync(() => {
            if (fail) {
                view.FailPendingReferenceRead();
            } else {
                view.CompletePending();
            }
            replacement = view.ChangePartyAsync(draft, null);
        });
        await old.WaitAsync(TimeSpan.FromSeconds(5));
        if (scenario == TestLabScenario.DelayedReferences) {
            Assert.Equal(TestLabReadState.Loading, view.State.PartiesRead);
            await cut.InvokeAsync(view.CompletePending);
        }
        await replacement.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Null(draft.Model.ResponsiblePartyId);
        Assert.Empty(view.State.Parties);
        Assert.Equal(TestLabReadState.Empty, view.State.PartiesRead);
        Assert.Equal(0, view.PendingCount);
        Assert.Equal(TestLabSaveState.Ready, draft.SaveState);
    }

    [Theory]
    [InlineData(TestLabScenario.DelayedReferences)]
    [InlineData(TestLabScenario.DelayedPartyLookup)]
    public async Task Project_A_B_A_retires_old_reference_waits_without_finishing_the_current_request(TestLabScenario scenario) {
        using var view = new TestLabScenarioWorkspace(scenario, () => { });
        var draft = view.State.Draft!;
        var a = view.Store.Projects[1];
        var first = view.ChangeProjectAsync(draft, a.Id);
        var middle = view.ChangeProjectAsync(draft, null);
        var current = view.ChangeProjectAsync(draft, a.Id);
        await Task.WhenAll(first, middle).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(1, view.PendingCount);
        Assert.Equal(TestLabReadState.Loading, view.State.PartiesRead);
        Assert.Same(a.Admission, draft.Model.ExpectedProjectAdmission);
        view.CompletePending();
        await current.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(view.Store.PartyId, Assert.Single(view.State.Parties).PartyId);
        Assert.Equal(TestLabReadState.Ready, view.State.PartiesRead);
        Assert.Equal(0, view.PendingCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Retired_readback_cannot_settle_an_independent_successor_submission(bool rebindProject) {
        using var view = new TestLabScenarioWorkspace(TestLabScenario.DelayedReadback, () => { });
        var original = view.State.Draft!;
        var a = original.Model.Id!.Value;
        var first = view.SaveAsync(original);
        Assert.Equal(1, view.Store.Commits);
        if (rebindProject) {
            var project = original.Model.ProjectId;
            await view.ChangeProjectAsync(original, null);
            await view.ChangeProjectAsync(original, project);
        } else {
            await view.SelectAsync(view.State.Plans[1].Id);
            await view.SelectAsync(a);
        }
        var successor = view.State.Draft!;
        successor.Model.Title = "Successor submission";
        var second = view.SaveAsync(successor);
        await first.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.NotNull(successor.Pending);
        Assert.False(successor.CanSave);
        Assert.Equal(TestLabSaveState.Pending, successor.SaveState);
        Assert.Equal(1, view.PendingCount);
        await view.ChangePartyAsync(successor, null);
        Assert.NotNull(successor.Pending);
        view.CompletePending();
        await second.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(2, view.Store.Commits);
        Assert.Equal("Successor submission", view.Store.Read(a)!.Title);
        Assert.Equal(TestLabSaveState.Saved, successor.SaveState);
        Assert.Null(successor.Model.ResponsiblePartyId);
    }

    [Theory]
    [InlineData(TestLabScenario.DelayedRead)]
    [InlineData(TestLabScenario.DelayedReferences)]
    [InlineData(TestLabScenario.DelayedSave)]
    [InlineData(TestLabScenario.DelayedReadback)]
    public async Task Reset_ends_retired_reads_but_an_admitted_write_still_commits(TestLabScenario scenario) {
        using var view = new TestLabScenarioWorkspace(scenario, () => { });
        var original = view.State.Draft!;
        var operation = scenario switch {
            TestLabScenario.DelayedRead => view.SelectAsync(view.State.Plans[1].Id),
            TestLabScenario.DelayedReferences => view.ChangeProjectAsync(original, null),
            _ => view.SaveAsync(original)
        };
        Assert.Equal(1, view.PendingCount);
        await view.NewAsync();
        var replacement = view.State.Draft!;
        view.CompletePending();
        await operation.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Same(replacement, view.State.Draft);
        Assert.Null(replacement.Pending);
        Assert.Null(replacement.Model.Id);
        Assert.Equal(TestLabSaveState.Ready, replacement.SaveState);
        Assert.Equal(0, view.PendingCount);
        Assert.Equal(scenario is TestLabScenario.DelayedSave or TestLabScenario.DelayedReadback ? 1 : 0, view.Store.Commits);
    }

    private static Guid?[] ChildIds(TestLabDraft draft) => draft.Model.Cases.Select(item => item.Id)
        .Concat(draft.Model.Evidence.Select(item => item.Id)).Concat(draft.Model.Runs.Select(item => item.Id)).ToArray();
}
