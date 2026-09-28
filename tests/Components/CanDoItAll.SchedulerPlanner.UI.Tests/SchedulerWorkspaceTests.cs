using System.Text.Json.Nodes;
using Bunit;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Modules.SchedulerPlanner;
using CanDoItAll.Modules.SchedulerPlanner.Presentation;
using CanDoItAll.SchedulerPlanner.UI;
using CanDoItAll.SchedulerPlanner.UiSandbox;
using Microsoft.Extensions.DependencyInjection;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Tests.Components.SchedulerPlanner;

public sealed class SchedulerWorkspaceTests : BunitContext {
    public SchedulerWorkspaceTests() {
        Services.AddCanDoItAllBaseLib();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public async Task Calendar_view_date_timezone_and_selection_survive_remount_and_refresh() {
        using var workspace = new SchedulerWorkspace(new SchedulerScenarioStore());
        await workspace.InitializeAsync();
        var selectedId = workspace.Data!.CalendarSurface.Events.First().Id;
        workspace.SetCalendarState(new("{\"preferredView\":\"month\"}", selectedId, "2026-10-15", "month", "month", "Europe/Prague"));
        workspace.Tab = SchedulerTab.History;
        workspace.Tab = SchedulerTab.Calendar;
        AssertState();
        await workspace.RefreshAsync();
        AssertState();
        void AssertState() {
            var surface = workspace.Data!.CalendarSurface;
            Assert.Equal("month", surface.InitialView);
            Assert.Equal("2026-10-15", surface.SelectedDate);
            Assert.Equal("Europe/Prague", surface.Timezone);
            Assert.Equal(selectedId, surface.SelectedEventId);
            Assert.Equal("{\"preferredView\":\"month\"}", surface.ViewStateJson);
        }
    }

    [Fact]
    public async Task Schedule_filters_survive_tab_unmount_without_owner_reads() {
        var store = new SchedulerScenarioStore();
        using var workspace = new SchedulerWorkspace(store);
        await workspace.InitializeAsync();
        workspace.Tab = SchedulerTab.Schedules;
        var cut = Render<SchedulerWorkspaceSurface>(parameters => parameters.Add(item => item.Workspace, workspace));
        cut.Find("[data-testid=scheduler-schedules-search]").Change("Paused");
        Assert.Contains("Paused report", Assert.Single(cut.FindAll("[data-testid=scheduler-plan-card]")).TextContent);
        await cut.InvokeAsync(() => workspace.Tab = SchedulerTab.NewSchedule);
        cut.WaitForElement("[data-testid=scheduler-name]");
        await cut.InvokeAsync(() => workspace.Tab = SchedulerTab.Schedules);
        cut.WaitForAssertion(() => Assert.Equal("Paused", cut.Find("[data-testid=scheduler-schedules-search]").GetAttribute("value")));
        Assert.Contains("Paused report", Assert.Single(cut.FindAll("[data-testid=scheduler-plan-card]")).TextContent);
        Assert.Equal(1, store.Reads);
        Assert.Equal(0, store.Writes);
    }

    [Theory]
    [InlineData("-")]
    [InlineData("  ")]
    public async Task Later_typed_input_remains_dirty_and_recoverable_after_earlier_save(string later) {
        var store = new SchedulerScenarioStore();
        using var workspace = new SchedulerWorkspace(store);
        await workspace.InitializeAsync();
        await workspace.EditAsync(SchedulerScenarioStore.PlanA);
        var draft = workspace.EditDraft!;
        store.Hold = SchedulerWait.Validation;
        var saving = workspace.SaveAsync(draft);
        await store.Entered.Task;
        await workspace.SetInputAsync(draft, draft.Schema!.Parameters.Single(item => item.Key == "minutes"), later);
        store.Release();
        await saving;
        Assert.True(draft.IsDirty);
        workspace.CloseEdit();
        await workspace.EditAsync(SchedulerScenarioStore.PlanA);
        Assert.Same(draft, workspace.EditDraft);
        Assert.Equal(later, draft.InputValues["minutes"]);
        Assert.Equal(1, store.Writes);
    }

    [Theory]
    [InlineData(SchedulerWait.Validation, false)]
    [InlineData(SchedulerWait.Authority, false)]
    [InlineData(SchedulerWait.Save, false)]
    [InlineData(SchedulerWait.Validation, true)]
    [InlineData(SchedulerWait.Authority, true)]
    [InlineData(SchedulerWait.Save, true)]
    public async Task Submission_is_captured_before_wait_and_all_later_fields_survive(SchedulerWait phase, bool existing) {
        var store = new SchedulerScenarioStore();
        using var workspace = new SchedulerWorkspace(store);
        await workspace.InitializeAsync();
        if (existing) {
            await workspace.EditAsync(SchedulerScenarioStore.PlanA);
        }
        var draft = existing ? workspace.EditDraft! : workspace.NewDraft;
        var captured = draft.Values with { Name = " captured ", Description = "first", InputJson = "{\"unknown\":true}",
            StartAtUtc = SchedulerScenarioStore.Now, EndAtUtc = SchedulerScenarioStore.Now.AddDays(2) };
        workspace.SetValues(draft, captured);
        store.Hold = phase;
        var pending = workspace.SaveAsync(draft);
        await store.Entered.Task;
        var later = captured with { Name = " later ", Description = " second ", CronExpression = "0 15 10 ? * *",
            TimeZoneId = "Europe/Prague", IsEnabled = false, InputJson = "{\"later\":2}" };
        workspace.SetValues(draft, later);
        await workspace.SaveAsync(draft);
        if (existing) {
            workspace.CloseEdit();
            await workspace.EditAsync(SchedulerScenarioStore.PlanB);
        } else {
            await workspace.ResetAsync();
        }
        workspace.Tab = SchedulerTab.History;
        store.Release();
        await pending;
        Assert.Equal(captured, Assert.Single(store.Submitted));
        Assert.Equal(later with { Id = draft.Values.Id }, draft.Values);
        Assert.NotNull(draft.Values.Id);
        Assert.Equal(captured.StartAtUtc, store.Plans[draft.Values.Id!.Value].StartAtUtc);
        Assert.Equal(captured.EndAtUtc, store.Plans[draft.Values.Id!.Value].EndAtUtc);
        Assert.Equal(SchedulerTab.History, workspace.Tab);
        Assert.Equal(1, store.Writes);
        Assert.Contains(draft, workspace.RetainedDrafts);
        if (existing) {
            Assert.Equal(SchedulerScenarioStore.PlanB, workspace.EditDraft!.Values.Id);
        } else {
            Assert.NotSame(draft, workspace.NewDraft);
        }
    }

    [Theory]
    [InlineData(SchedulerScenario.ProjectionFailure)]
    [InlineData(SchedulerScenario.ReadbackFailure)]
    public async Task Known_create_adopts_identity_before_readback_and_next_save_updates_it(SchedulerScenario scenario) {
        var store = new SchedulerScenarioStore(scenario);
        using var workspace = new SchedulerWorkspace(store);
        await workspace.InitializeAsync();
        var draft = workspace.NewDraft;
        await workspace.SaveAsync(draft);
        var id = Assert.IsType<Guid>(draft.Values.Id);
        Assert.True(store.Plans.ContainsKey(id));
        Assert.Equal(3, store.Plans.Count);
        Assert.False(draft.IsLocked);
        Assert.Equal(scenario == SchedulerScenario.ProjectionFailure ? SchedulerMutationStatus.CommittedWithWarning : SchedulerMutationStatus.Committed, draft.Receipt!.Status);
        Assert.Null(draft.Receipt.SavedValues!.StartAtUtc);
        await workspace.SaveAsync(draft);
        Assert.Equal(3, store.Plans.Count);
        Assert.Equal(id, store.Submitted[1].Id);
        await workspace.RefreshAsync();
        Assert.Equal(2, store.Writes);
    }

    [Fact]
    public async Task Identity_is_accepted_while_readback_is_still_pending() {
        var store = new SchedulerScenarioStore();
        using var workspace = new SchedulerWorkspace(store);
        await workspace.InitializeAsync();
        store.Hold = SchedulerWait.Readback;
        var save = workspace.SaveAsync(workspace.NewDraft);
        await store.Entered.Task;
        Assert.NotNull(workspace.NewDraft.Values.Id);
        Assert.Equal(SchedulerMutationStatus.Committed, workspace.NewDraft.Receipt!.Status);
        store.Release();
        await save;
    }

    [Theory]
    [InlineData(SchedulerScenario.UnknownSave, true)]
    [InlineData(SchedulerScenario.RefusedSave, false)]
    public async Task Unknown_keeps_replay_lock_and_requires_explicit_exact_plan_review(SchedulerScenario scenario, bool locked) {
        var store = new SchedulerScenarioStore(scenario);
        using var workspace = new SchedulerWorkspace(store);
        await workspace.InitializeAsync();
        var draft = workspace.NewDraft;
        await workspace.SaveAsync(draft);
        Assert.Equal(locked, draft.IsLocked);
        await workspace.RefreshAsync();
        Assert.Equal(locked, draft.IsLocked);
        await workspace.SaveAsync(draft);
        Assert.Equal(locked ? 1 : 2, store.Submitted.Count);
        if (locked) {
            await workspace.ReviewUnknownAsync(draft, SchedulerScenarioStore.PlanB);
            Assert.True(draft.IsLocked);
            var exact = store.Plans.Keys.Except([SchedulerScenarioStore.PlanA, SchedulerScenarioStore.PlanB]).Single();
            await workspace.ReviewUnknownAsync(draft, exact);
            Assert.Equal(exact, draft.Values.Id);
            Assert.False(draft.IsLocked);
            Assert.Equal(1, store.Writes);
        }
    }

    [Fact]
    public async Task Pending_save_blocks_same_plan_toggle_and_delete_but_not_other_plan() {
        var store = new SchedulerScenarioStore();
        using var workspace = new SchedulerWorkspace(store);
        await workspace.InitializeAsync();
        await workspace.EditAsync(SchedulerScenarioStore.PlanA);
        store.Hold = SchedulerWait.Validation;
        var saving = workspace.SaveAsync(workspace.EditDraft!);
        await store.Entered.Task;
        var first = workspace.Data!.Plans.Single(plan => plan.Id == SchedulerScenarioStore.PlanA);
        await workspace.ToggleAsync(first);
        workspace.AskDelete(first);
        await workspace.DeleteAsync();
        Assert.Equal(0, store.Writes);
        await workspace.ToggleAsync(workspace.Data.Plans.Single(plan => plan.Id == SchedulerScenarioStore.PlanB));
        Assert.Equal(1, store.Writes);
        Assert.True(store.Plans[SchedulerScenarioStore.PlanB].IsEnabled);
        store.Release();
        await saving;
        Assert.Equal(2, store.Writes);
    }

    [Theory]
    [InlineData("{broken")]
    [InlineData("[]")]
    [InlineData("null")]
    [InlineData("")]
    [InlineData("42")]
    public async Task Actual_input_event_keeps_invalid_raw_json_and_never_writes(string raw) {
        var store = new SchedulerScenarioStore();
        using var workspace = new SchedulerWorkspace(store);
        await workspace.InitializeAsync();
        workspace.Tab = SchedulerTab.NewSchedule;
        var cut = Render<SchedulerWorkspaceSurface>(parameters => parameters.Add(item => item.Workspace, workspace));
        await cut.Find("[data-testid=scheduler-name]").InputAsync(new() { Value = " unblurred " });
        await cut.Find("[data-testid=scheduler-input-json]").InputAsync(new() { Value = raw });
        await cut.Find("[data-testid=scheduler-save]").ClickAsync(new());
        Assert.Equal(raw, workspace.NewDraft.Values.InputJson);
        Assert.Equal(" unblurred ", workspace.NewDraft.Values.Name);
        Assert.Equal(0, store.Writes);
        Assert.NotEmpty(cut.FindAll("[data-testid=scheduler-draft-error]"));
    }

    [Fact]
    public async Task Typed_input_preserves_unknown_types_incomplete_numbers_and_option_budget() {
        var store = new SchedulerScenarioStore();
        using var workspace = new SchedulerWorkspace(store);
        await workspace.InitializeAsync();
        var draft = workspace.NewDraft;
        await workspace.SetRawInputAsync(draft, "{\"unknown\":{\"value\":true},\"note\":\" original \",\"minutes\":3}");
        var count = store.OptionReads;
        var note = draft.Schema!.Parameters.Single(item => item.Key == "note");
        var minutes = draft.Schema.Parameters.Single(item => item.Key == "minutes");
        await workspace.SetInputAsync(draft, note, " unblurred ");
        await workspace.SetInputAsync(draft, note, " unblurred ");
        Assert.Equal(count, store.OptionReads);
        await workspace.SetInputAsync(draft, minutes, "-");
        await workspace.SaveAsync(draft);
        Assert.Equal("-", draft.InputValues[minutes.Key]);
        Assert.Equal(0, store.Writes);
        await workspace.SetInputAsync(draft, minutes, "12");
        var json = JsonNode.Parse(draft.Values.InputJson)!;
        Assert.True(json["unknown"]!["value"]!.GetValue<bool>());
        Assert.Equal(" unblurred ", json["note"]!.GetValue<string>());
        Assert.Equal(12, json["minutes"]!.GetValue<int>());
        var project = draft.Schema.Parameters.Single(item => item.Key == "project");
        await workspace.SetInputAsync(draft, project, "project-a");
        Assert.Equal(count + 1, store.OptionReads);
        Assert.False(draft.InputValues.ContainsKey("node"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Late_workspace_success_or_failure_cannot_replace_newer_read(bool fail) {
        var store = new SchedulerScenarioStore();
        using var workspace = new SchedulerWorkspace(store);
        await workspace.InitializeAsync();
        var stale = workspace.Data!;
        var held = new TaskCompletionSource<SchedulerWorkspaceData>(TaskCreationOptions.RunContinuationsAsynchronously);
        store.ReadOverride = (_, _) => held.Task;
        var old = workspace.RefreshAsync();
        store.ReadOverride = (_, _) => Task.FromResult(stale with { Plans = [] });
        await workspace.RefreshAsync();
        if (fail) {
            held.SetException(new IOException("Stale failure"));
        } else {
            held.SetResult(stale);
        }
        await old;
        Assert.Empty(workspace.Data!.Plans);
        Assert.Equal(SchedulerReadStatus.Ready, workspace.ReadStatus);
        Assert.Empty(workspace.Error);
    }

    [Theory]
    [InlineData(SchedulerWait.Default)]
    [InlineData(SchedulerWait.Schema)]
    public async Task Late_initial_load_does_not_replace_later_raw_input(SchedulerWait phase) {
        var store = new SchedulerScenarioStore { Hold = phase, IgnoreCancellation = true };
        using var workspace = new SchedulerWorkspace(store);
        var load = workspace.InitializeAsync();
        await store.Entered.Task;
        await workspace.SetRawInputAsync(workspace.NewDraft, "{\"later\":true}");
        workspace.SetValues(workspace.NewDraft, workspace.NewDraft.Values with { Name = "typed" });
        store.Release();
        await load;
        Assert.Equal("typed", workspace.NewDraft.Values.Name);
        Assert.Equal("{\"later\":true}", workspace.NewDraft.Values.InputJson);
    }

    [Fact]
    public async Task Editor_A_B_A_and_disposal_retire_old_reads_without_abandoned_loading() {
        var store = new SchedulerScenarioStore { IgnoreCancellation = true };
        using var workspace = new SchedulerWorkspace(store);
        await workspace.InitializeAsync();
        store.Hold = SchedulerWait.Editor;
        var first = workspace.EditAsync(SchedulerScenarioStore.PlanA);
        await store.Entered.Task;
        store.Hold = SchedulerWait.None;
        await workspace.EditAsync(SchedulerScenarioStore.PlanB);
        await workspace.EditAsync(SchedulerScenarioStore.PlanA);
        workspace.SetValues(workspace.EditDraft!, workspace.EditDraft!.Values with { Name = "A successor" });
        store.Release();
        await first;
        Assert.Equal("A successor", workspace.EditDraft.Values.Name);
        Assert.False(workspace.EditorLoading);
        store.Hold = SchedulerWait.Editor;
        var retired = workspace.EditAsync(SchedulerScenarioStore.PlanB);
        await store.Entered.Task;
        workspace.Dispose();
        store.Release();
        await retired;
        Assert.Null(workspace.EditDraft);
    }

    [Fact]
    public async Task Missing_version_never_adopts_current_version_and_delete_retains_admission_fixture() {
        var store = new SchedulerScenarioStore(SchedulerScenario.MissingVersion);
        using var workspace = new SchedulerWorkspace(store);
        await workspace.InitializeAsync();
        Assert.Equal(SchedulerScenarioStore.VersionA, workspace.NewDraft.Values.TargetVersionId);
        Assert.Equal(SchedulerReadStatus.Unavailable, workspace.NewDraft.SchemaStatus);
        workspace.Tab = SchedulerTab.NewSchedule;
        var cut = Render<SchedulerWorkspaceSurface>(parameters => parameters.Add(item => item.Workspace, workspace));
        Assert.Contains("Exact target unavailable", cut.Markup, StringComparison.Ordinal);
        var admissions = store.RetainedAdmissions.ToArray();
        workspace.AskDelete(workspace.Data!.Plans.Single(plan => plan.Id == SchedulerScenarioStore.PlanA));
        cut.Render();
        Assert.Contains("Display run history is deleted", cut.Markup, StringComparison.Ordinal);
        await workspace.DeleteAsync();
        Assert.Empty(store.History);
        Assert.Equal(admissions, store.RetainedAdmissions);
    }

    [Fact]
    public async Task A_B_A_input_revision_rejects_old_normalization_even_when_text_returns_to_same_value() {
        var store = new SchedulerScenarioStore();
        using var workspace = new SchedulerWorkspace(store);
        await workspace.InitializeAsync();
        var draft = workspace.NewDraft;
        var held = new TaskCompletionSource<SchedulerWorkflowInputValidationResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        store.ValidationOverride = (_, _) => held.Task;
        var save = workspace.SaveAsync(draft);
        await workspace.SetRawInputAsync(draft, "{\"changed\":true}");
        await workspace.SetRawInputAsync(draft, "{}");
        held.SetResult(new(true, "{\"default\":3}", []));
        await save;
        Assert.Equal("{}", draft.Values.InputJson);
        Assert.Equal("{\"default\":3}", Assert.Single(store.Submitted).InputJson);
        Assert.NotNull(draft.Values.Id);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Older_dependent_option_success_or_failure_cannot_overwrite_current_choices(bool failure) {
        var store = new SchedulerScenarioStore();
        using var workspace = new SchedulerWorkspace(store);
        await workspace.InitializeAsync();
        var draft = workspace.NewDraft;
        var project = draft.Schema!.Parameters.Single(item => item.Key == "project");
        var held = new TaskCompletionSource<IReadOnlyList<WorkflowInputParameterOption>>(TaskCreationOptions.RunContinuationsAsynchronously);
        store.OptionsOverride = (_, values, _) => values.GetValueOrDefault("project") == "first"
            ? held.Task : Task.FromResult<IReadOnlyList<WorkflowInputParameterOption>>([new("second", "Second", string.Empty)]);
        var old = workspace.SetInputAsync(draft, project, "first");
        await workspace.SetInputAsync(draft, project, "second");
        if (failure) {
            held.SetException(new IOException("Retired option read"));
        } else {
            held.SetResult([new("first", "First", string.Empty)]);
        }
        await old;
        Assert.Equal("second", Assert.Single(draft.Options["node"].Values).Value);
        Assert.Equal(SchedulerReadStatus.Ready, draft.Options["node"].Status);
        Assert.False(draft.InputValues.ContainsKey("node"));
    }

    [Theory]
    [InlineData(SchedulerWait.Workspace)]
    [InlineData(SchedulerWait.Default)]
    [InlineData(SchedulerWait.Schema)]
    [InlineData(SchedulerWait.Options)]
    public async Task Disposed_initial_read_lanes_drain_without_callbacks_or_successor_mutation(SchedulerWait phase) {
        var store = new SchedulerScenarioStore { Hold = phase, IgnoreCancellation = true };
        using var workspace = new SchedulerWorkspace(store);
        var callbacks = 0;
        workspace.Changed += () => callbacks++;
        var loading = workspace.InitializeAsync();
        await store.Entered.Task;
        workspace.Dispose();
        var count = callbacks;
        store.Release();
        await loading;
        Assert.Equal(count, callbacks);
        Assert.Equal(0, store.Writes);
        using var successor = new SchedulerWorkspace(new SchedulerScenarioStore(SchedulerScenario.Empty));
        await successor.InitializeAsync();
        Assert.Empty(successor.Data!.Plans);
    }
}
