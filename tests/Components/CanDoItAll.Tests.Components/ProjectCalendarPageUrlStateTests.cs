using System.Globalization;
using System.Text.Json;
using Bunit;
using Bunit.TestDoubles;
using CanDoItAll.Components.CanvasLib;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Modules.Workbench.Pages;
using CanDoItAll.SharedKernel;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.ProjectStructure;

public sealed class ProjectCalendarPageUrlStateTests {
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Loading_makes_the_resolved_view_and_date_explicit_without_a_new_entry() {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var (projectId, _) = await CreateProjectAsync(harness, "Resolved calendar");
        var navigation = Navigation(harness);
        navigation.NavigateTo($"/projects/{projectId:D}/calendar");
        var entries = navigation.History.Count;

        var cut = harness.Context.Render<ProjectCalendarPage>(parameters => parameters.Add(page => page.ProjectId, projectId));

        cut.WaitForAssertion(() => Assert.Single(cut.FindComponents<CanvasCalendar>()));
        var today = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        cut.WaitForAssertion(() => Assert.Equal($"/projects/{projectId:D}/calendar?view=week&date={today}", Location(navigation)));
        Assert.Equal(entries, navigation.History.Count);
        Assert.Equal(today, cut.FindComponent<CanvasCalendar>().Instance.Surface.SelectedDate);
    }

    [Fact]
    public async Task The_location_wins_over_the_persisted_preference_which_wins_over_the_default() {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var (projectId, admission) = await CreateProjectAsync(harness, "Preferred calendar");
        var workbench = harness.Context.Services.GetRequiredService<ProjectWorkbenchService>();
        await workbench.SaveCalendarViewStateAsync(admission, "{\"view\":\"month\",\"selectedDate\":\"2026-01-10\",\"timezone\":\"UTC\"}");
        var navigation = Navigation(harness);
        navigation.NavigateTo($"/projects/{projectId:D}/calendar?view=year&date=2026-03-04");

        var cut = harness.Context.Render<ProjectCalendarPage>(parameters => parameters.Add(page => page.ProjectId, projectId));

        cut.WaitForAssertion(() => {
            var surface = cut.FindComponent<CanvasCalendar>().Instance.Surface;
            Assert.Equal("year", surface.InitialView);
            Assert.Equal("2026-03-04", surface.SelectedDate);
        });
        Assert.Equal($"/projects/{projectId:D}/calendar?view=year&date=2026-03-04", Location(navigation));

        await cut.InvokeAsync(() => navigation.NavigateTo($"/projects/{projectId:D}/calendar"));

        cut.WaitForAssertion(() => {
            var surface = cut.FindComponent<CanvasCalendar>().Instance.Surface;
            Assert.Equal("month", surface.InitialView);
            Assert.Equal("2026-01-10", surface.SelectedDate);
            Assert.Equal($"/projects/{projectId:D}/calendar?view=month&date=2026-01-10", Location(navigation));
        });
    }

    [Fact]
    public async Task A_view_change_pushes_and_returning_to_the_previous_location_recreates_the_calendar() {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var (projectId, _) = await CreateProjectAsync(harness, "Navigated calendar");
        var navigation = Navigation(harness);
        navigation.NavigateTo($"/projects/{projectId:D}/calendar?view=week&date=2026-10-05");
        var cut = harness.Context.Render<ProjectCalendarPage>(parameters => parameters.Add(page => page.ProjectId, projectId));
        cut.WaitForAssertion(() => Assert.Single(cut.FindComponents<CanvasCalendar>()));
        var entries = navigation.History.Count;
        var original = cut.FindComponent<CanvasCalendar>().Instance;

        await cut.InvokeAsync(() => original.StateChanged.InvokeAsync(new CanvasCalendarStateChangedEventArgs(
            "{}", null, "2026-11-02", "month", "month", "UTC")));

        Assert.Equal($"/projects/{projectId:D}/calendar?view=month&date=2026-11-02", Location(navigation));
        Assert.Equal(entries + 1, navigation.History.Count);
        Assert.Same(original, cut.FindComponent<CanvasCalendar>().Instance);

        await cut.InvokeAsync(() => navigation.NavigateTo($"/projects/{projectId:D}/calendar?view=week&date=2026-10-05"));

        cut.WaitForAssertion(() => {
            var current = cut.FindComponent<CanvasCalendar>().Instance;
            Assert.NotSame(original, current);
            Assert.Equal("week", current.Surface.InitialView);
            Assert.Equal("2026-10-05", current.Surface.SelectedDate);
        });
    }

    [Fact]
    public async Task An_unknown_event_link_reports_the_event_as_unavailable_until_another_is_selected() {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var (projectId, _) = await CreateProjectAsync(harness, "Event calendar");
        var workbench = harness.Context.Services.GetRequiredService<ProjectWorkbenchService>();
        var start = new DateTimeOffset(2026, 10, 6, 9, 0, 0, TimeSpan.Zero);
        await workbench.SeedProjectObjectsAsync(projectId,
            [new ProjectObjectSeedRequest(ProjectObjectType.Note, "Planned review", "Calendar", "Calendar event.", start, start.AddHours(1))]);
        var scheduled = Assert.Single((await workbench.GetCalendarAsync(projectId)).Events);
        var navigation = Navigation(harness);
        navigation.NavigateTo($"/projects/{projectId:D}/calendar?view=week&date=2026-10-05&eventId={Guid.NewGuid():D}");

        var cut = harness.Context.Render<ProjectCalendarPage>(parameters => parameters.Add(page => page.ProjectId, projectId));

        cut.WaitForAssertion(() => Assert.Contains("requested calendar event is unavailable", cut.Find("[data-testid='planning-calendar-load-error']").TextContent));
        Assert.Empty(cut.FindAll("[data-testid='planning-calendar-detail']"));
        var calendar = cut.FindComponent<CanvasCalendar>();
        var canvasEvent = Assert.Single(calendar.Instance.Surface.Events);
        var entries = navigation.History.Count;

        await cut.InvokeAsync(() => calendar.Instance.OnSelectionChanged(JsonSerializer.Serialize(canvasEvent, JsonOptions), "{}"));

        cut.WaitForAssertion(() => {
            Assert.Single(cut.FindAll("[data-testid='planning-calendar-detail']"));
            Assert.Empty(cut.FindAll("[data-testid='planning-calendar-load-error']"));
        });
        Assert.Equal($"/projects/{projectId:D}/calendar?view=week&date=2026-10-05&eventId={scheduled.Id:D}", Location(navigation));
        Assert.Equal(entries + 1, navigation.History.Count);
    }

    [Fact]
    public async Task Selecting_another_event_moves_the_date_in_the_same_entry_and_a_later_date_change_pushes() {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var (projectId, _) = await CreateProjectAsync(harness, "Selected calendar");
        var workbench = harness.Context.Services.GetRequiredService<ProjectWorkbenchService>();
        var monday = new DateTimeOffset(2026, 10, 5, 9, 0, 0, TimeSpan.Zero);
        await workbench.SeedProjectObjectsAsync(projectId, [
            new ProjectObjectSeedRequest(ProjectObjectType.Note, "Kickoff", "Calendar", "First event.", monday, monday.AddHours(1)),
            new ProjectObjectSeedRequest(ProjectObjectType.Note, "Review", "Calendar", "Second event.", monday.AddDays(2), monday.AddDays(2).AddHours(1))
        ]);
        var review = (await workbench.GetCalendarAsync(projectId)).Events.Single(item => item.Title == "Review");
        var navigation = Navigation(harness);
        navigation.NavigateTo($"/projects/{projectId:D}/calendar?view=week&date=2026-10-05");
        var cut = harness.Context.Render<ProjectCalendarPage>(parameters => parameters.Add(page => page.ProjectId, projectId));
        cut.WaitForAssertion(() => Assert.Single(cut.FindComponents<CanvasCalendar>()));
        var calendar = cut.FindComponent<CanvasCalendar>().Instance;
        var canvasEvent = calendar.Surface.Events.Single(item => item.Title == "Review");
        var entries = navigation.History.Count;

        // The native calendar announces a clicked event and then the date it moved to, as one user action.
        await cut.InvokeAsync(() => calendar.OnSelectionChanged(JsonSerializer.Serialize(canvasEvent, JsonOptions), "{}"));
        await cut.InvokeAsync(() => calendar.OnStateChanged("{}", canvasEvent.Id, "2026-10-07", "week", "week", "UTC"));

        Assert.Equal($"/projects/{projectId:D}/calendar?view=week&date=2026-10-07&eventId={review.Id:D}", Location(navigation));
        Assert.Equal(entries + 1, navigation.History.Count);

        await cut.InvokeAsync(() => calendar.OnStateChanged("{}", canvasEvent.Id, "2026-10-12", "week", "week", "UTC"));

        Assert.Equal($"/projects/{projectId:D}/calendar?view=week&date=2026-10-12&eventId={review.Id:D}", Location(navigation));
        Assert.Equal(entries + 2, navigation.History.Count);
    }

    private static async Task<(Guid ProjectId, ProjectWriteAdmission Admission)> CreateProjectAsync(ComponentTestHarness harness, string name) {
        var projects = harness.Context.Services.GetRequiredService<ProjectsService>();
        var project = await projects.GetAsync(null);
        project.Name = name;
        var result = await projects.SaveAsync(project);
        Assert.True(result.IsSuccess);
        var calendar = await harness.Context.Services.GetRequiredService<ProjectWorkbenchService>().GetCalendarAsync(result.Value);
        return (result.Value, Assert.IsType<ProjectWriteAdmission>(calendar.ExpectedProjectAdmission));
    }

    private static BunitNavigationManager Navigation(ComponentTestHarness harness)
        => Assert.IsType<BunitNavigationManager>(harness.Context.Services.GetRequiredService<NavigationManager>());

    private static string Location(NavigationManager navigation) => "/" + navigation.ToBaseRelativePath(navigation.Uri);
}
