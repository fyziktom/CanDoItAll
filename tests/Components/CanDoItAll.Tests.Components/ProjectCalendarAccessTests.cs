using System.Data.Common;
using System.Security.Claims;
using Bunit;
using CanDoItAll.Components.CanvasLib;
using CanDoItAll.Editing;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Modules.Workbench.Pages;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using CanDoItAll.Workbench.Planning.UI;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.ProjectStructure;

[Trait("Category", "HostPlatform")]
public sealed class ProjectCalendarAccessTests {
    public enum AccessCondition { Allowed, DeniedBefore, DeniedAfterSql }
    private const string PrivateTitle = "Calendar private event";

    [Theory]
    [InlineData(AccessCondition.Allowed)]
    [InlineData(AccessCondition.DeniedBefore)]
    [InlineData(AccessCondition.DeniedAfterSql)]
    public async Task Initial_native_calendar_private_read_requires_access_before_and_after_actual_SQL(AccessCondition condition) {
        var access = new EditingUiTestAccess();
        var probe = new ReadProbe(access);
        await using var harness = await ComponentTestHarness.CreateAsync(services => {
            services.AddSingleton<IEditingUiAccess>(access);
            services.ConfigureDbContext<WorkbenchDbContext>(options => options.AddInterceptors(probe), ServiceLifetime.Singleton);
        });
        var project = await CreateProjectAsync(harness);
        probe.Armed = condition == AccessCondition.DeniedAfterSql;
        access.Allowed = condition != AccessCondition.DeniedBefore;
        var page = Open(harness, project);
        page.WaitForAssertion(() => Assert.NotEqual(PlanningReadState.Loading, Presentation(page).State));
        Assert.Equal(condition == AccessCondition.DeniedAfterSql, probe.Reached);
        if (condition == AccessCondition.Allowed) {
            Assert.Equal(PlanningReadState.Ready, Presentation(page).State);
            Assert.Contains(PrivateTitle, page.Markup, StringComparison.Ordinal);
        } else {
            Assert.Null(Presentation(page).Calendar);
            Assert.DoesNotContain(PrivateTitle, page.Markup, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task Denied_native_calendar_refresh_preserves_range_timezone_and_selection_without_publishing_acquired_peer_body() {
        var access = new EditingUiTestAccess();
        var probe = new ReadProbe(access);
        await using var harness = await ComponentTestHarness.CreateAsync(services => {
            services.AddSingleton<IEditingUiAccess>(access);
            services.ConfigureDbContext<WorkbenchDbContext>(options => options.AddInterceptors(probe), ServiceLifetime.Singleton);
        });
        var project = await CreateProjectAsync(harness);
        var workbench = harness.Context.Services.GetRequiredService<ProjectWorkbenchService>();
        var calendar = await workbench.GetCalendarAsync(project);
        var selected = Assert.Single(calendar.Events).Id;
        await workbench.SaveCalendarViewStateAsync(calendar.ExpectedProjectAdmission!,
            "{\"view\":\"month\",\"selectedDate\":\"2026-11-03\",\"timezone\":\"America/La_Paz\",\"selectedEventId\":\"" + selected + "\"}");
        var page = Open(harness, project);
        page.WaitForAssertion(() => Assert.Equal(PlanningReadState.Ready, Presentation(page).State));
        var before = Presentation(page);
        var canvas = Assert.IsType<CanvasCalendarSurface>(before.Calendar);
        await using (var db = await harness.Context.Services.GetRequiredService<IDbContextFactory<WorkbenchDbContext>>().CreateDbContextAsync()) {
            var row = await db.Set<ProjectObjectRecord>().SingleAsync(item => item.ProjectId == project && item.Title == PrivateTitle);
            row.Title = "Private peer calendar title withheld";
            await db.SaveChangesAsync();
        }
        probe.Armed = true;
        var renderer = page.FindComponent<PlanningCalendarSurface>().Instance;
        await page.InvokeAsync(() => renderer.RetryRequested.InvokeAsync(new(before.Origin)));
        Assert.True(probe.Reached);
        var after = Presentation(page);
        Assert.Equal(PlanningReadState.Stale, after.State);
        Assert.Equal(before.SelectedEvent, after.SelectedEvent);
        Assert.Equal(canvas.InitialView, after.Calendar!.InitialView);
        Assert.Equal(canvas.SelectedDate, after.Calendar.SelectedDate);
        Assert.Equal(canvas.Timezone, after.Calendar.Timezone);
        Assert.DoesNotContain("Private peer calendar title withheld", page.Markup, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(AccessCondition.Allowed)]
    [InlineData(AccessCondition.DeniedBefore)]
    [InlineData(AccessCondition.DeniedAfterSql)]
    public async Task Native_calendar_preference_write_requires_current_access_at_physical_commit(AccessCondition condition) {
        var access = new EditingUiTestAccess();
        var probe = new WriteProbe(access);
        await using var harness = await ComponentTestHarness.CreateAsync(services => {
            services.AddSingleton<IEditingUiAccess>(access);
            services.ConfigureDbContext<WorkbenchDbContext>(options => options.AddInterceptors(probe), ServiceLifetime.Singleton);
        });
        var project = await CreateProjectAsync(harness);
        var page = Open(harness, project);
        page.WaitForAssertion(() => Assert.Equal(PlanningReadState.Ready, Presentation(page).State));
        var owner = harness.Context.Services.GetRequiredService<ProjectWorkbenchService>();
        var before = (await owner.GetCalendarAsync(project)).ViewStateJson;
        probe.Project = project;
        probe.Armed = condition == AccessCondition.DeniedAfterSql;
        access.Allowed = condition != AccessCondition.DeniedBefore;
        var renderer = page.FindComponent<PlanningCalendarSurface>().Instance;
        await page.InvokeAsync(() => renderer.StateChanged.InvokeAsync(new(renderer.Presentation.Origin,
            new("{}", null, "2026-11-03", "year", "year", "America/La_Paz"))));
        var after = (await owner.GetCalendarAsync(project)).ViewStateJson;
        Assert.Equal(condition == AccessCondition.DeniedAfterSql, probe.Reached);
        if (condition == AccessCondition.Allowed) {
            var accepted = ProjectCalendarStateParser.Parse(after);
            Assert.Equal("year", accepted.View);
            Assert.Equal("America/La_Paz", accepted.Timezone);
            Assert.Null(Presentation(page).PersistenceWarning);
        } else {
            Assert.Equal(before, after);
            Assert.Contains("not saved", Presentation(page).PersistenceWarning, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("could not be confirmed", Presentation(page).PersistenceWarning, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task A_changed_cascading_actor_retires_the_original_calendar_preference_callback() {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var project = await CreateProjectAsync(harness);
        var first = Actor("First calendar actor");
        var next = Actor("Next calendar actor");
        var wrapper = harness.Context.Render<CascadingValue<Task<AuthenticationState>>>(parameters => parameters
            .Add(component => component.Value, first)
            .AddChildContent<ProjectCalendarPage>(parameters => parameters.Add(component => component.ProjectId, project)));
        wrapper.WaitForAssertion(() => Assert.Equal(PlanningReadState.Ready,
            wrapper.FindComponent<PlanningCalendarSurface>().Instance.Presentation.State));
        var renderer = wrapper.FindComponent<PlanningCalendarSurface>().Instance;
        var queued = renderer.StateChanged;
        var origin = renderer.Presentation.Origin;
        await wrapper.InvokeAsync(() => wrapper.Render(parameters => parameters
            .Add(component => component.Value, next)
            .AddChildContent<ProjectCalendarPage>(parameters => parameters.Add(component => component.ProjectId, project))));
        await wrapper.InvokeAsync(() => queued.InvokeAsync(new(origin,
            new("{}", null, "2026-11-03", "year", "year", "UTC"))));
        var stored = await harness.Context.Services.GetRequiredService<ProjectWorkbenchService>().GetCalendarAsync(project);
        Assert.Null(stored.ViewStateJson);
        wrapper.WaitForAssertion(() => {
            var accepted = wrapper.FindComponent<PlanningCalendarSurface>().Instance.Presentation;
            Assert.Equal(PlanningReadState.Ready, accepted.State);
            Assert.NotEqual(origin, accepted.Origin);
        });
    }

    private static Task<AuthenticationState> Actor(string name)
        => Task.FromResult(new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, name)], "calendar-tests"))));

    [Fact]
    public async Task Explicit_calendar_pull_keeps_local_range_timezone_and_selection_when_peer_preferences_changed() {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var project = await CreateProjectAsync(harness);
        var workbench = harness.Context.Services.GetRequiredService<ProjectWorkbenchService>();
        var calendar = await workbench.GetCalendarAsync(project);
        var selected = Assert.Single(calendar.Events).Id;
        await workbench.SaveCalendarViewStateAsync(calendar.ExpectedProjectAdmission!,
            "{\"view\":\"month\",\"selectedDate\":\"2026-11-03\",\"timezone\":\"America/La_Paz\",\"selectedEventId\":\"" + selected + "\"}");
        var page = Open(harness, project);
        page.WaitForAssertion(() => Assert.Equal(PlanningReadState.Ready, Presentation(page).State));
        var before = Presentation(page);
        await workbench.SaveCalendarViewStateAsync(calendar.ExpectedProjectAdmission!,
            "{\"view\":\"year\",\"selectedDate\":\"2027-04-01\",\"timezone\":\"UTC\"}");
        var peer = (await workbench.GetCalendarAsync(project)).ViewStateJson;
        var renderer = page.FindComponent<PlanningCalendarSurface>().Instance;
        await page.InvokeAsync(() => renderer.RetryRequested.InvokeAsync(new(before.Origin)));
        var after = Presentation(page);
        Assert.Equal(PlanningReadState.Ready, after.State);
        Assert.Equal(before.Calendar!.InitialView, after.Calendar!.InitialView);
        Assert.Equal(before.Calendar.SelectedDate, after.Calendar.SelectedDate);
        Assert.Equal(before.Calendar.Timezone, after.Calendar.Timezone);
        Assert.Equal(before.SelectedEvent!.Id, after.SelectedEvent!.Id);
        Assert.Equal(peer, (await workbench.GetCalendarAsync(project)).ViewStateJson);
    }

    private static CalendarPresentation Presentation(IRenderedComponent<ProjectCalendarPage> page)
        => page.FindComponent<PlanningCalendarSurface>().Instance.Presentation;

    private static IRenderedComponent<ProjectCalendarPage> Open(ComponentTestHarness harness, Guid project) {
        harness.Context.Services.GetRequiredService<NavigationManager>().NavigateTo($"/projects/{project:D}/calendar");
        return harness.Context.Render<ProjectCalendarPage>(parameters => parameters.Add(component => component.ProjectId, project));
    }

    private static async Task<Guid> CreateProjectAsync(ComponentTestHarness harness) {
        var services = harness.Context.Services;
        var project = (await services.GetRequiredService<ProjectsService>().SaveAsync(new() { Name = "Calendar authority" })).Value;
        var workbench = services.GetRequiredService<ProjectWorkbenchService>();
        var admission = (await workbench.GetCalendarAsync(project)).ExpectedProjectAdmission!;
        var start = new DateTimeOffset(2026, 11, 3, 8, 0, 0, TimeSpan.Zero);
        await workbench.CreateObjectAsync(project, new(ProjectObjectType.Note, PrivateTitle, "", "", $"project:{project:D}",
            StartUtc: start, EndUtc: start.AddHours(1)) { ExpectedProjectAdmission = admission });
        return project;
    }

    private sealed class ReadProbe(EditingUiTestAccess access) : DbCommandInterceptor {
        public bool Armed { get; set; }
        public bool Reached { get; private set; }

        public override ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData, DbDataReader result,
            CancellationToken cancellationToken = default) {
            if (Armed && command.CommandText.Contains("\"Workbench_ProjectObjects\"", StringComparison.Ordinal)) {
                Armed = false;
                Reached = true;
                access.Allowed = false;
            }
            return ValueTask.FromResult(result);
        }
    }

    private sealed class WriteProbe(EditingUiTestAccess access) : SaveChangesInterceptor {
        public Guid Project { get; set; }
        public bool Armed { get; set; }
        public bool Reached { get; private set; }

        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result,
            CancellationToken cancellationToken = default) {
            if (Armed && result > 0 && eventData.Context is WorkbenchDbContext context &&
                    context.ChangeTracker.Entries<ProjectWorkbenchViewStateRecord>().Any(entry => entry.Entity.ProjectId == Project)) {
                Assert.NotNull(context.Database.CurrentTransaction);
                Armed = false;
                Reached = true;
                access.Allowed = false;
            }
            return ValueTask.FromResult(result);
        }
    }
}
