using Bunit;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Modules.TestLab;
using CanDoItAll.TestLab.UI;
using CanDoItAll.TestLab.UiSandbox;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.TestLab;

public sealed class TestLabSurfaceTests : BunitContext {
    public TestLabSurfaceTests() {
        Services.AddCanDoItAllBaseLib();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Theory]
    [InlineData(TestLabScenario.Empty, "No saved plans.")]
    [InlineData(TestLabScenario.FilteredEmpty, "No matching plans")]
    [InlineData(TestLabScenario.ReadFailure, "Plans unavailable")]
    [InlineData(TestLabScenario.StaleRefresh, "Previously loaded plans")]
    [InlineData(TestLabScenario.MissingPlan, "Requested plan was not found")]
    [InlineData(TestLabScenario.MissingReferences, "Unavailable responsible party")]
    [InlineData(TestLabScenario.Large, "250 matching plans")]
    public void Real_renderer_exposes_distinct_scenario_states(TestLabScenario scenario, string text) {
        using var view = new TestLabScenarioWorkspace(scenario, () => { });
        var cut = Render<TestLabWorkspaceSurface>(parameters => parameters.Add(item => item.View, view));
        Assert.Contains(text, cut.Markup);
        Assert.Single(cut.FindComponents<ListDetailShell>());
        Assert.Single(cut.FindComponents<SecondaryTabs>());
        Assert.Equal(scenario == TestLabScenario.MissingPlan ? 0 : 1, cut.FindComponents<EditForm>().Count);
    }

    [Fact]
    public async Task Invalid_timestamp_and_context_survive_section_unmount_then_valid_correction_preserves_precision() {
        var cut = Render<CanDoItAll.TestLab.UiSandbox.Components.Home>();
        var view = cut.FindComponent<TestLabWorkspaceSurface>().Instance.View;
        var draft = view.State.Draft!;
        var context = cut.FindComponent<EditForm>().Instance.EditContext;
        await TabAsync(cut, "Runs");
        await cut.InvokeAsync(() => cut.Find("[data-testid=testlab-run-timestamp]").InputAsync(new() { Value = "unfinished" }));
        await cut.InvokeAsync(() => cut.Find("form").SubmitAsync());
        Assert.Contains("Enter a complete ISO 8601", cut.Markup);
        Assert.Equal(0, ((TestLabScenarioWorkspace)view).Store.Commits);
        await TabAsync(cut, "Overview");
        await TabAsync(cut, "Runs");
        Assert.Same(context, cut.FindComponent<EditForm>().Instance.EditContext);
        Assert.Equal("unfinished", cut.Find("[data-testid=testlab-run-timestamp]").GetAttribute("value"));
        const string timestamp = "2026-09-28T12:34:56.1234567-04:00";
        await cut.InvokeAsync(() => cut.Find("[data-testid=testlab-run-timestamp]").InputAsync(new() { Value = timestamp }));
        await cut.InvokeAsync(() => cut.Find("form").SubmitAsync());
        Assert.Equal(TestLabSaveState.Saved, draft.SaveState);
        var stored = ((TestLabScenarioWorkspace)view).Store.Read(draft.Model.Id!.Value)!;
        Assert.Equal(timestamp, Assert.Single(stored.Runs).ExecutedAtUtc.ToOffset(TimeSpan.FromHours(-4)).ToString("O"));
        Assert.Equal(TimeSpan.Zero, stored.Runs[0].ExecutedAtUtc.Offset);
        Assert.Empty(context!.GetValidationMessages());
    }

    [Fact]
    public async Task Input_before_blur_during_readback_keeps_newer_parent_and_nested_text() {
        var cut = Render<CanDoItAll.TestLab.UiSandbox.Components.Home>();
        await ScenarioAsync(cut, TestLabScenario.DelayedReadback);
        var view = (TestLabScenarioWorkspace)cut.FindComponent<TestLabWorkspaceSurface>().Instance.View;
        var draft = view.State.Draft!;
        await cut.InvokeAsync(() => cut.Find("[data-testid=testlab-title-input]").InputAsync(new() { Value = "  Submitted  " }));
        var pending = cut.InvokeAsync(() => cut.Find("form").SubmitAsync());
        cut.WaitForAssertion(() => Assert.Equal(1, view.PendingCount));
        Assert.True(cut.Find("[data-testid=testlab-save-button]").HasAttribute("disabled"));
        await cut.InvokeAsync(() => cut.Find("[data-testid=testlab-title-input]").InputAsync(new() { Value = "Newer title" }));
        await TabAsync(cut, "Cases");
        await cut.InvokeAsync(() => cut.Find("[data-testid=testlab-case-notes]").InputAsync(new() { Value = "Newer notes" }));
        await cut.InvokeAsync(view.CompletePending);
        await pending;
        Assert.Same(draft.Context, cut.FindComponent<EditForm>().Instance.EditContext);
        Assert.Equal("Newer title", draft.Model.Title);
        Assert.Equal("Newer notes", draft.Model.Cases[0].Notes);
        Assert.Equal(TestLabSection.Cases, view.State.Section);
        Assert.Equal("Submitted", view.Store.Read(draft.Model.Id!.Value)!.Title);
    }

    [Fact]
    public async Task Every_section_real_controls_filters_and_reset_work_without_owner_services() {
        var cut = Render<CanDoItAll.TestLab.UiSandbox.Components.Home>();
        var view = cut.FindComponent<TestLabWorkspaceSurface>().Instance.View;
        foreach (var section in new[] { "Cases", "Evidence", "Runs", "Overview" }) {
            await TabAsync(cut, section);
            Assert.Equal(Enum.Parse<TestLabSection>(section), view.State.Section);
        }
        var original = view.State.Draft;
        await cut.InvokeAsync(() => cut.Find("[data-testid=testlab-search]").InputAsync(new() { Value = "no results" }));
        Assert.Empty(view.State.VisiblePlans);
        Assert.Same(original, view.State.Draft);
        await cut.InvokeAsync(() => cut.Find("[data-testid=testlab-project-filter]").ChangeAsync(new() { Value = view.State.Projects[1].Id.ToString() }));
        await cut.InvokeAsync(() => view.State.ResetFilters());
        Assert.Equal(3, view.State.VisiblePlans.Count);
        await TabAsync(cut, "Cases");
        await cut.InvokeAsync(() => cut.FindAll("button").Single(button => button.TextContent.Trim() == "Add case").ClickAsync(new()));
        Assert.Equal(2, view.State.Draft!.Model.Cases.Count);
        await cut.InvokeAsync(() => cut.FindAll("button").First(button => button.TextContent.Trim() == "Remove case").ClickAsync(new()));
        Assert.Single(view.State.Draft.Model.Cases);
    }

    [Fact]
    public async Task Duplicate_real_form_dispatch_is_blocked_and_retired_store_write_does_not_patch_successor() {
        var cut = Render<CanDoItAll.TestLab.UiSandbox.Components.Home>();
        await ScenarioAsync(cut, TestLabScenario.DelayedSave);
        var view = (TestLabScenarioWorkspace)cut.FindComponent<TestLabWorkspaceSurface>().Instance.View;
        await cut.InvokeAsync(view.NewAsync);
        await cut.InvokeAsync(() => cut.Find("[data-testid=testlab-title-input]").InputAsync(new() { Value = "Admitted new plan" }));
        var first = cut.InvokeAsync(() => cut.Find("form").SubmitAsync());
        cut.WaitForAssertion(() => Assert.Equal(1, view.PendingCount));
        await cut.InvokeAsync(() => cut.Find("form").SubmitAsync());
        Assert.Equal(1, view.PendingCount);
        await cut.InvokeAsync(() => view.SelectAsync(view.State.Plans[1].Id));
        var successor = view.State.Draft!;
        await cut.InvokeAsync(view.CompletePending);
        await first;
        Assert.Same(successor, view.State.Draft);
        Assert.Equal(1, view.Store.Commits);
        var stored = Assert.Single(view.Store.List(), plan => plan.Title == "Admitted new plan");
        await cut.InvokeAsync(() => view.SelectAsync(stored.Id));
        Assert.Equal("Admitted new plan", view.State.Draft!.Model.Title);
    }

    [Theory]
    [InlineData(TestLabScenario.CommittedWarning)]
    [InlineData(TestLabScenario.RefreshFailure)]
    public async Task Scenario_warning_and_refresh_are_backed_by_one_actual_fake_store_commit(TestLabScenario scenario) {
        using var view = new TestLabScenarioWorkspace(scenario, () => { });
        await view.NewAsync();
        var draft = view.State.Draft!;
        draft.Model.Title = "Stored";
        draft.Model.Cases.Add(new());
        await view.SaveAsync(draft);
        Assert.Equal(TestLabSaveState.SavedWithWarning, draft.SaveState);
        Assert.NotNull(draft.Model.Cases[0].Id);
        await view.RetryAsync();
        Assert.Equal(1, view.Store.Commits);
        Assert.Equal(TestLabSaveState.Saved, draft.SaveState);
        Assert.Equal(draft.Model.Cases[0].Id, view.Store.Read(draft.Model.Id!.Value)!.Cases[0].Id);
    }

    [Fact]
    public async Task Scenario_A_B_A_keeps_newer_pending_operation_locked() {
        using var view = new TestLabScenarioWorkspace(TestLabScenario.DelayedSave, () => { });
        var a = view.State.Plans[0].Id;
        var b = view.State.Plans[1].Id;
        var first = view.SaveAsync(view.State.Draft!);
        await view.SelectAsync(b);
        await view.SelectAsync(a);
        var current = view.State.Draft!;
        current.Model.Title = "Second write";
        var second = view.SaveAsync(current);
        view.CompletePending();
        await first;
        Assert.NotNull(current.Pending);
        Assert.Equal(TestLabSaveState.Pending, current.SaveState);
        view.CompletePending();
        await second;
        Assert.Equal(2, view.Store.Commits);
        Assert.Equal("Second write", view.Store.Read(a)!.Title);
    }

    [Theory]
    [InlineData(TestLabScenario.DelayedRead)]
    [InlineData(TestLabScenario.DelayedReferences)]
    [InlineData(TestLabScenario.DelayedSave)]
    public async Task Switching_scenario_releases_pending_work_without_replacement_callbacks(TestLabScenario scenario) {
        var changes = 0;
        using var view = new TestLabScenarioWorkspace(scenario, () => changes++);
        var pending = scenario switch {
            TestLabScenario.DelayedRead => view.SelectAsync(view.State.Plans[1].Id),
            TestLabScenario.DelayedReferences => view.ChangeProjectAsync(view.State.Draft!, view.State.Projects[1].Id),
            _ => view.SaveAsync(view.State.Draft!)
        };
        Assert.Equal(1, view.PendingCount);
        view.Dispose();
        var before = changes;
        await pending.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(0, view.PendingCount);
        Assert.Equal(before, changes);
        Assert.Equal(scenario == TestLabScenario.DelayedSave ? 1 : 0, view.Store.Commits);
    }

    private static Task ScenarioAsync(IRenderedComponent<CanDoItAll.TestLab.UiSandbox.Components.Home> cut, TestLabScenario scenario) =>
        cut.InvokeAsync(() => cut.Find("[data-testid=testlab-scenario]").ChangeAsync(new() { Value = scenario.ToString() }));

    private static Task TabAsync(IRenderedComponent<CanDoItAll.TestLab.UiSandbox.Components.Home> cut, string label) =>
        cut.InvokeAsync(() => cut.FindComponent<SecondaryTabs>().FindAll("button").Single(button => button.TextContent.Trim().StartsWith(label, StringComparison.Ordinal)).ClickAsync(new()));
}
