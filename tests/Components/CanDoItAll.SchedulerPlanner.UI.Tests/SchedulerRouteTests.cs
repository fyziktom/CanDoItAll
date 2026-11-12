using Bunit;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Components.CanvasLib;
using CanDoItAll.Modules.SchedulerPlanner.Presentation;
using CanDoItAll.SchedulerPlanner.UI;
using CanDoItAll.SchedulerPlanner.UiSandbox;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.SchedulerPlanner;

public sealed class SchedulerRouteTests : BunitContext {
    public SchedulerRouteTests() {
        Services.AddCanDoItAllBaseLib();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public async Task User_intents_announce_navigation_and_an_applied_route_stays_silent() {
        var store = new SchedulerScenarioStore();
        using var workspace = new SchedulerWorkspace(store);
        var navigations = new List<(SchedulerNavigationKind Kind, SchedulerRoute Route)>();
        workspace.Navigated += kind => navigations.Add((kind, workspace.Route));
        await workspace.InitializeAsync(new SchedulerRoute(SchedulerTab.Schedules, ScheduleSearch: "Paused", HistorySearch: "missing"));

        Assert.Empty(navigations);
        Assert.Equal(SchedulerTab.Schedules, workspace.Tab);
        Assert.Equal("Paused", workspace.ScheduleFilters.Search);
        Assert.Equal("missing", workspace.HistoryQuery.Search);
        Assert.Empty(workspace.Data!.History);

        workspace.Tab = SchedulerTab.History;
        workspace.SetScheduleFilters(workspace.ScheduleFilters with { State = ScheduleStateFilter.Paused });
        workspace.SetScheduleFilters(workspace.ScheduleFilters);
        await workspace.EditAsync(SchedulerScenarioStore.PlanA);
        workspace.CloseEdit();
        workspace.HistoryQuery.Search = " History ";
        await workspace.SearchHistoryAsync();
        await workspace.SearchHistoryAsync();

        Assert.Equal(
            [SchedulerNavigationKind.Tab, SchedulerNavigationKind.ScheduleFilters, SchedulerNavigationKind.OpenEditor,
                SchedulerNavigationKind.CloseEditor, SchedulerNavigationKind.HistorySearch],
            navigations.Select(item => item.Kind));
        Assert.Equal(SchedulerScenarioStore.PlanA, navigations[2].Route.PlanId);
        Assert.Null(navigations[3].Route.PlanId);
        Assert.Equal("History", navigations[4].Route.HistorySearch);
        Assert.NotEmpty(workspace.Data!.History);

        navigations.Clear();
        await workspace.ApplyRouteAsync(new SchedulerRoute(SchedulerTab.Calendar, SchedulerScenarioStore.PlanB,
            SchedulerCalendarView.Month, new DateOnly(2026, 10, 15), HistorySearch: "History"));

        Assert.Empty(navigations);
        Assert.Equal(SchedulerScenarioStore.PlanB, workspace.EditDraft?.Values.Id);
        Assert.Equal("month", workspace.Data!.CalendarSurface.InitialView);
        Assert.Equal("2026-10-15", workspace.Data.CalendarSurface.SelectedDate);
        // The schedules tab is not shown, so the address does not reset its remembered filters.
        Assert.Equal(new SchedulerScheduleFilters("Paused", null, ScheduleStateFilter.Paused), workspace.ScheduleFilters);
    }

    [Fact]
    public async Task Calendar_navigation_moves_the_address_but_a_selection_inside_the_range_does_not() {
        using var workspace = new SchedulerWorkspace(new SchedulerScenarioStore());
        var navigations = new List<SchedulerRoute>();
        workspace.Navigated += kind => {
            Assert.Equal(SchedulerNavigationKind.Calendar, kind);
            navigations.Add(workspace.Route);
        };
        await workspace.InitializeAsync();
        var selected = workspace.Data!.CalendarSurface.Events.First().Id;

        workspace.SetCalendarState(new("{\"anchorDateKey\":\"2026-09-28\",\"selectedDate\":\"2026-09-30\"}", selected, "2026-09-30", "week", "week", "UTC"));
        Assert.Empty(navigations);

        workspace.SetCalendarState(new("{\"preferredView\":\"month\",\"anchorDateKey\":\"2026-10-15\"}", null, "2026-10-15", "month", "month", "UTC"));
        var moved = Assert.Single(navigations);
        Assert.Equal(SchedulerCalendarView.Month, moved.CalendarView);
        Assert.Equal(new DateOnly(2026, 10, 15), moved.CalendarDate);

        // Returning to the default view and date leaves both out of the address.
        workspace.SetCalendarState(new("{\"preferredView\":\"week\",\"anchorDateKey\":\"2026-09-28\"}", null, "2026-09-28", "week", "week", "UTC"));
        Assert.Equal(2, navigations.Count);
        Assert.Null(navigations[1].CalendarView);
        Assert.Null(navigations[1].CalendarDate);
    }

    [Fact]
    public async Task An_address_position_wins_over_the_remembered_calendar_state() {
        using var workspace = new SchedulerWorkspace(new SchedulerScenarioStore());
        await workspace.InitializeAsync();
        var selected = workspace.Data!.CalendarSurface.Events.First().Id;
        workspace.SetCalendarState(new("{\"preferredView\":\"month\",\"selectedDate\":\"2026-10-15\",\"anchorDateKey\":\"2026-10-15\",\"scope\":\"month\"}",
            selected, "2026-10-15", "month", "month", "Europe/Prague"));

        await workspace.ApplyRouteAsync(new SchedulerRoute(CalendarView: SchedulerCalendarView.Day, CalendarDate: new DateOnly(2026, 11, 2)));
        await workspace.RefreshAsync();

        var surface = workspace.Data!.CalendarSurface;
        Assert.Equal("day", surface.InitialView);
        Assert.Equal("2026-11-02", surface.SelectedDate);
        Assert.Contains("\"preferredView\":\"day\"", surface.ViewStateJson, StringComparison.Ordinal);
        Assert.Contains("\"anchorDateKey\":\"2026-11-02\"", surface.ViewStateJson, StringComparison.Ordinal);
        Assert.Contains("\"scope\":\"month\"", surface.ViewStateJson, StringComparison.Ordinal);
        Assert.Equal(selected, surface.SelectedEventId);
        Assert.Equal("Europe/Prague", surface.Timezone);
    }

    [Fact]
    public async Task A_deep_linked_editor_renders_loading_and_an_unknown_plan_as_unavailable() {
        var store = new SchedulerScenarioStore { Hold = SchedulerWait.Editor };
        using var workspace = new SchedulerWorkspace(store);
        var missing = Guid.Parse("5a0b9f2d-7c61-4bb0-9d55-2f1e1c7e0d11");
        var loading = workspace.InitializeAsync(new SchedulerRoute(PlanId: SchedulerScenarioStore.PlanA));
        await store.Entered.Task;
        var cut = Render<SchedulerWorkspaceSurface>(parameters => parameters.Add(item => item.Workspace, workspace));
        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid=scheduler-edit-dialog] [data-testid=scheduler-edit-loading]")));

        store.Release();
        await loading;
        cut.WaitForAssertion(() => Assert.Equal("Morning review", cut.Find("[data-testid=scheduler-edit-name]").GetAttribute("value")));

        await cut.InvokeAsync(() => workspace.ApplyRouteAsync(new SchedulerRoute(PlanId: missing)));
        cut.WaitForAssertion(() => {
            Assert.NotNull(cut.Find("[data-testid=scheduler-edit-dialog] [data-testid=scheduler-edit-unavailable]"));
            Assert.Empty(cut.FindAll("[data-testid=scheduler-edit-name]"));
            Assert.Empty(cut.FindAll("[data-testid=scheduler-edit-delete]"));
        });
        Assert.Equal(missing, workspace.Route.PlanId);

        var closes = 0;
        workspace.Navigated += kind => closes += kind == SchedulerNavigationKind.CloseEditor ? 1 : 0;
        await cut.Find("[data-testid=scheduler-edit-cancel]").ClickAsync(new());
        Assert.Equal(1, closes);
        Assert.Null(workspace.Route.PlanId);
        Assert.Empty(cut.FindAll("[data-testid=scheduler-edit-dialog]"));
    }

    [Fact]
    public async Task Deleting_the_plan_open_in_the_editor_removes_the_editor() {
        var store = new SchedulerScenarioStore();
        using var workspace = new SchedulerWorkspace(store);
        var navigations = new List<SchedulerNavigationKind>();
        workspace.Navigated += navigations.Add;
        await workspace.InitializeAsync();
        await workspace.EditAsync(SchedulerScenarioStore.PlanA);
        workspace.AskDelete(workspace.Data!.Plans.Single(plan => plan.Id == SchedulerScenarioStore.PlanA));

        await workspace.DeleteAsync();

        Assert.Null(workspace.EditDraft);
        Assert.Null(workspace.Route.PlanId);
        Assert.Equal([SchedulerNavigationKind.OpenEditor, SchedulerNavigationKind.EditorRemoved], navigations);
        Assert.DoesNotContain(workspace.RetainedDrafts, draft => draft.Values.Id == SchedulerScenarioStore.PlanA);
    }

    [Fact]
    public async Task Schedule_filter_controls_raise_one_intent_each() {
        using var workspace = new SchedulerWorkspace(new SchedulerScenarioStore());
        await workspace.InitializeAsync(new SchedulerRoute(SchedulerTab.Schedules));
        var filters = new List<SchedulerScheduleFilters>();
        workspace.Navigated += kind => {
            Assert.Equal(SchedulerNavigationKind.ScheduleFilters, kind);
            filters.Add(workspace.ScheduleFilters);
        };
        var cut = Render<SchedulerWorkspaceSurface>(parameters => parameters.Add(item => item.Workspace, workspace));

        cut.Find("[data-testid=scheduler-schedules-search]").Change("Paused");
        cut.Find("[data-testid=scheduler-schedules-target-kind]").Change("Workflow");
        cut.Find("[data-testid=scheduler-schedules-state]").Change(nameof(ScheduleStateFilter.Paused));
        await cut.FindAll("[data-testid=scheduler-schedules-toolbar] button")
            .Single(button => button.TextContent.Contains("Clear", StringComparison.Ordinal)).ClickAsync(new());

        Assert.Equal([
            new SchedulerScheduleFilters("Paused"),
            new SchedulerScheduleFilters("Paused", CanDoItAll.Modules.SchedulerPlanner.SchedulerPlanTargetKind.Workflow),
            new SchedulerScheduleFilters("Paused", CanDoItAll.Modules.SchedulerPlanner.SchedulerPlanTargetKind.Workflow, ScheduleStateFilter.Paused),
            new SchedulerScheduleFilters()
        ], filters);
        Assert.Equal(2, cut.FindAll("[data-testid=scheduler-plan-card]").Count);
    }
}
