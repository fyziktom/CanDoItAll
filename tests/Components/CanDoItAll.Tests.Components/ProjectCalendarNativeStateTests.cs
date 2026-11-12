using System.Data.Common;
using Bunit;
using CanDoItAll.Components.CanvasLib;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Modules.Workbench.Pages;
using CanDoItAll.Workbench.Planning.UI;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.ProjectStructure;

public sealed class ProjectCalendarNativeStateTests {
    [Fact]
    public async Task Same_owner_writes_finish_in_issue_order_even_when_first_commit_acknowledgement_is_held() {
        var probe = new StoreProbe();
        await using var harness = await CreateHarnessAsync(probe);
        var (projectId, cut) = await OpenAsync(harness);
        var callback = cut.FindComponent<CanvasCalendar>().Instance.StateChanged;
        probe.HoldNextCommit = true;
        var first = cut.InvokeAsync(() => callback.InvokeAsync(State("month")));
        await probe.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var second = cut.InvokeAsync(() => callback.InvokeAsync(State("year")));
        cut.WaitForAssertion(() => Assert.Equal("year", cut.FindComponent<CanvasCalendar>().Instance.Surface.InitialView));
        Assert.Equal(1, probe.CalendarCommitCount);
        Assert.False(second.IsCompleted);
        probe.Release.SetResult();
        await Task.WhenAll(first, second);

        var stored = await harness.Context.Services.GetRequiredService<ProjectWorkbenchService>().GetCalendarAsync(projectId);
        Assert.Equal("year", ProjectCalendarStateParser.Parse(stored.ViewStateJson).View);
        Assert.Equal(2, probe.CalendarCommitCount);
    }

    [Fact]
    public async Task Retiring_a_calendar_keeps_its_accepted_commit_and_drops_a_queued_write() {
        var probe = new StoreProbe();
        await using var harness = await CreateHarnessAsync(probe);
        var (firstId, cut) = await OpenAsync(harness);
        var secondId = await CreateProjectAsync(harness, "Successor calendar");
        var callback = cut.FindComponent<CanvasCalendar>().Instance.StateChanged;
        probe.HoldNextCommit = true;
        var first = cut.InvokeAsync(() => callback.InvokeAsync(State("month")));
        await probe.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var queued = cut.InvokeAsync(() => callback.InvokeAsync(State("year")));
        cut.Render(parameters => parameters.Add(page => page.ProjectId, secondId));
        cut.WaitForAssertion(() => Assert.Contains("Successor calendar", cut.Markup));
        probe.Release.SetResult();
        await Task.WhenAll(first, queued);

        var owner = harness.Context.Services.GetRequiredService<ProjectWorkbenchService>();
        Assert.Equal("month", ProjectCalendarStateParser.Parse((await owner.GetCalendarAsync(firstId)).ViewStateJson).View);
        Assert.Null((await owner.GetCalendarAsync(secondId)).ViewStateJson);
        Assert.Equal(1, probe.CalendarCommitCount);
        Assert.Empty(cut.FindAll("[data-testid='planning-calendar-persistence-warning']"));
    }

    [Fact]
    public async Task Lost_commit_acknowledgement_is_visible_and_readback_does_not_replay_the_write() {
        var probe = new StoreProbe();
        await using var harness = await CreateHarnessAsync(probe);
        var (projectId, cut) = await OpenAsync(harness);
        probe.FailNextCommitAcknowledgement = true;
        await cut.InvokeAsync(() => cut.FindComponent<CanvasCalendar>().Instance.StateChanged.InvokeAsync(State("year")));
        Assert.Contains("could not be confirmed", cut.Markup);
        var owner = harness.Context.Services.GetRequiredService<ProjectWorkbenchService>();
        Assert.Equal("year", ProjectCalendarStateParser.Parse((await owner.GetCalendarAsync(projectId)).ViewStateJson).View);
        var renderer = cut.FindComponent<PlanningCalendarSurface>().Instance;
        await cut.InvokeAsync(() => renderer.RetryRequested.InvokeAsync(new(renderer.Presentation.Origin)));
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll("[data-testid='planning-calendar-persistence-warning']")));
        Assert.Equal(1, probe.CalendarCommitCount);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Original_profile_and_lifetime_are_required_at_native_view_state_commit(bool replaceProfile) {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var projectId = await CreateProjectAsync(harness, "Admission calendar");
        var owner = harness.Context.Services.GetRequiredService<ProjectWorkbenchService>();
        var admission = Assert.IsType<ProjectWriteAdmission>((await owner.GetCalendarAsync(projectId)).ExpectedProjectAdmission);
        await owner.SaveCalendarViewStateAsync(admission, "{\"view\":\"month\"}");
        var invalid = new ProjectWriteAdmission(replaceProfile ? Guid.NewGuid() : admission.DatabaseProfileId,
            projectId, replaceProfile ? admission.LifetimeId : Guid.NewGuid());
        await Assert.ThrowsAsync<ProjectWriteAdmissionRejectedException>(() => owner.SaveCalendarViewStateAsync(invalid, "{\"view\":\"year\"}"));
        Assert.Equal("month", ProjectCalendarStateParser.Parse((await owner.GetCalendarAsync(projectId)).ViewStateJson).View);
    }

    [Fact]
    public async Task Failed_refresh_preserves_only_explicitly_stale_facts_and_retry_restores_readiness() {
        var probe = new StoreProbe();
        await using var harness = await CreateHarnessAsync(probe);
        var (_, cut) = await OpenAsync(harness);
        probe.FailNextRead = true;
        var renderer = cut.FindComponent<PlanningCalendarSurface>().Instance;
        await cut.InvokeAsync(() => renderer.RetryRequested.InvokeAsync(new(renderer.Presentation.Origin)));
        Assert.Equal(PlanningReadState.Stale, renderer.Presentation.State);
        Assert.Contains("stale", cut.Markup);
        await cut.InvokeAsync(() => renderer.RetryRequested.InvokeAsync(new(renderer.Presentation.Origin)));
        Assert.Equal(PlanningReadState.Ready, renderer.Presentation.State);
    }

    private static Task<ComponentTestHarness> CreateHarnessAsync(StoreProbe probe)
        => ComponentTestHarness.CreateAsync(services => services.AddSingleton<IDbContextFactory<WorkbenchDbContext>>(provider =>
            new ProbeFactory(new DbContextOptionsBuilder<WorkbenchDbContext>(provider.GetRequiredService<DbContextOptions<WorkbenchDbContext>>())
                .AddInterceptors(probe).Options, probe)));

    private static async Task<(Guid, IRenderedComponent<ProjectCalendarPage>)> OpenAsync(ComponentTestHarness harness) {
        var projectId = await CreateProjectAsync(harness, "Ordered calendar");
        var cut = harness.Context.Render<ProjectCalendarPage>(parameters => parameters.Add(page => page.ProjectId, projectId));
        cut.WaitForAssertion(() => Assert.Single(cut.FindComponents<CanvasCalendar>()));
        return (projectId, cut);
    }

    private static async Task<Guid> CreateProjectAsync(ComponentTestHarness harness, string name) {
        var projects = harness.Context.Services.GetRequiredService<ProjectsService>();
        var project = await projects.GetAsync(null);
        project.Name = name;
        var result = await projects.SaveAsync(project);
        Assert.True(result.IsSuccess);
        return result.Value;
    }

    private static CanvasCalendarStateChangedEventArgs State(string view)
        => new("{}", null, "2026-10-05", view, view, "UTC");

    private sealed class ProbeFactory(DbContextOptions<WorkbenchDbContext> options, StoreProbe probe) : IDbContextFactory<WorkbenchDbContext> {
        public WorkbenchDbContext CreateDbContext() => new(options);
        public Task<WorkbenchDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) {
            if (probe.FailNextRead) {
                probe.FailNextRead = false;
                throw new IOException("Injected native calendar read failure.");
            }
            return Task.FromResult(CreateDbContext());
        }
    }

    private sealed class StoreProbe : DbTransactionInterceptor {
        public bool HoldNextCommit { get; set; }
        public bool FailNextCommitAcknowledgement { get; set; }
        public bool FailNextRead { get; set; }
        public int CalendarCommitCount { get; private set; }
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData,
            CancellationToken cancellationToken = default) {
            if (eventData.Context?.ChangeTracker.Entries<ProjectWorkbenchViewStateRecord>().Any() != true) {
                return;
            }
            CalendarCommitCount++;
            if (HoldNextCommit) {
                HoldNextCommit = false;
                Entered.TrySetResult();
                await Release.Task;
            }
            if (FailNextCommitAcknowledgement) {
                FailNextCommitAcknowledgement = false;
                throw new IOException("Injected lost native view-state commit acknowledgement.");
            }
        }
    }
}
