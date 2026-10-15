using Bunit;
using Bunit.TestDoubles;
using CanDoItAll.AppComponents.Navigation;
using CanDoItAll.Modules.SchedulerPlanner;
using CanDoItAll.Modules.SchedulerPlanner.Pages;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CanDoItAll.Tests.Components.Processes;

public sealed partial class SchedulerPlannerPageTests
{
    [Fact]
    public async Task Scheduler_page_opens_a_deep_linked_tab_and_editor_and_close_replaces_it_with_the_parent()
    {
        var planId = Guid.NewGuid();
        var schedulerService = CreateUrlSchedulerService(planId);
        var back = new RecordingSchedulerBack();
        await using var harness = await CreateUrlHarnessAsync(schedulerService, back);
        var navigation = back.Navigation!;
        navigation.NavigateTo($"/scheduler?tab=schedules&planId={planId:D}");

        var cut = harness.Context.Render<SchedulerPlannerPage>();

        cut.WaitForAssertion(() => Assert.Equal(
            "Office365 email watch",
            cut.Find("[data-testid='scheduler-edit-dialog'] [data-testid='scheduler-edit-name']").GetAttribute("value")));
        Assert.NotEmpty(cut.FindAll("[data-testid='scheduler-plan-list']"));
        Assert.Equal(planId, schedulerService.LastLoadedPlanId);

        cut.Find("[data-testid='scheduler-edit-cancel']").Click();

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll("[data-testid='scheduler-edit-dialog']")));
        // A deep-linked editor has no parent entry of its own, so Close replaces the location with the parent.
        Assert.Equal(0, back.Calls);
        Assert.Equal("/scheduler?tab=schedules", Location(navigation));
        Assert.True(navigation.History.First().Options.ReplaceHistoryEntry);
    }

    [Fact]
    public async Task Scheduler_page_pushes_tabs_and_editors_replaces_instant_filters_and_closes_with_back()
    {
        var planId = Guid.NewGuid();
        var schedulerService = CreateUrlSchedulerService(planId);
        var back = new RecordingSchedulerBack();
        await using var harness = await CreateUrlHarnessAsync(schedulerService, back);
        var navigation = back.Navigation!;
        navigation.NavigateTo("/scheduler");
        var cut = harness.Context.Render<SchedulerPlannerPage>();
        cut.WaitForElement("[data-testid='scheduler-tabs']");
        var entries = navigation.History.Count;

        cut.FindAll("button[role='tab']").Single(tab => tab.TextContent.Contains("Schedules", StringComparison.Ordinal)).Click();
        cut.WaitForElement("[data-testid='scheduler-plan-list']");
        Assert.Equal("/scheduler?tab=schedules", Location(navigation));
        Assert.Equal(entries + 1, navigation.History.Count);

        cut.Find("[data-testid='scheduler-schedules-search']").Change("Office365");
        cut.WaitForAssertion(() => Assert.Equal("/scheduler?tab=schedules&q=Office365", Location(navigation)));
        Assert.True(navigation.History.First().Options.ReplaceHistoryEntry);

        cut.Find("[data-testid='scheduler-plan-edit']").Click();
        cut.WaitForElement("[data-testid='scheduler-edit-dialog']");
        Assert.Equal($"/scheduler?tab=schedules&planId={planId:D}&q=Office365", Location(navigation));
        Assert.False(navigation.History.First().Options.ReplaceHistoryEntry);

        cut.Find("[data-testid='scheduler-edit-cancel']").Click();

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll("[data-testid='scheduler-edit-dialog']")));
        Assert.Equal(1, back.Calls);
        Assert.Equal("/scheduler?tab=schedules&q=Office365", Location(navigation));
        Assert.Single(cut.FindAll("[data-testid='scheduler-plan-card']"));
    }

    [Fact]
    public async Task Scheduler_page_applies_a_location_change_to_the_tab_and_the_committed_history_query()
    {
        var planId = Guid.NewGuid();
        var schedulerService = CreateUrlSchedulerService(planId);
        var back = new RecordingSchedulerBack();
        await using var harness = await CreateUrlHarnessAsync(schedulerService, back);
        var navigation = back.Navigation!;
        navigation.NavigateTo("/scheduler");
        var cut = harness.Context.Render<SchedulerPlannerPage>();
        cut.WaitForElement("[data-testid='scheduler-calendar']");
        Assert.Equal(string.Empty, schedulerService.LastHistoryQuery?.Search);

        navigation.NavigateTo("/scheduler?tab=history&historyQ=Office365&historyStatus=no-messages");

        cut.WaitForElement("[data-testid='scheduler-history']");
        cut.WaitForAssertion(() =>
        {
            Assert.Equal("Office365", schedulerService.LastHistoryQuery?.Search);
            Assert.Equal(SchedulerPlanRunDispatchStatus.NoMessages, schedulerService.LastHistoryQuery?.Status);
            Assert.Equal("Office365", cut.Find("[data-testid='scheduler-history-search']").GetAttribute("value"));
        });
        Assert.Equal("/scheduler?tab=history&historyQ=Office365&historyStatus=no-messages", Location(navigation));
    }

    [Fact]
    public async Task Scheduler_page_reports_an_unknown_deep_linked_plan_as_unavailable_without_replacing_it()
    {
        var planId = Guid.NewGuid();
        var missingPlanId = Guid.NewGuid();
        var schedulerService = CreateUrlSchedulerService(planId);
        var back = new RecordingSchedulerBack();
        await using var harness = await CreateUrlHarnessAsync(schedulerService, back);
        var navigation = back.Navigation!;
        navigation.NavigateTo($"/scheduler?planId={missingPlanId:D}");

        var cut = harness.Context.Render<SchedulerPlannerPage>();

        cut.WaitForElement("[data-testid='scheduler-edit-dialog'] [data-testid='scheduler-edit-unavailable']");
        Assert.Empty(cut.FindAll("[data-testid='scheduler-edit-name']"));
        Assert.Equal(missingPlanId, schedulerService.LastLoadedPlanId);
        Assert.Equal($"/scheduler?planId={missingPlanId:D}", Location(navigation));
    }

    [Fact]
    public async Task Scheduler_page_canonicalizes_a_malformed_location_once_with_replace()
    {
        var planId = Guid.NewGuid();
        var schedulerService = CreateUrlSchedulerService(planId);
        var back = new RecordingSchedulerBack();
        await using var harness = await CreateUrlHarnessAsync(schedulerService, back);
        var navigation = back.Navigation!;
        navigation.NavigateTo("/scheduler?tab=Schedules&state=paused&kind=job&planId=not-a-plan&view=month&utm=mail");

        var cut = harness.Context.Render<SchedulerPlannerPage>();

        cut.WaitForElement("[data-testid='scheduler-plan-list']");
        Assert.Equal("/scheduler?tab=schedules&state=paused&utm=mail", Location(navigation));
        Assert.Single(navigation.History);
        Assert.True(navigation.History.First().Options.ReplaceHistoryEntry);
        Assert.Null(schedulerService.LastLoadedPlanId);
    }

    private static StubSchedulerPlannerService CreateUrlSchedulerService(Guid planId)
    {
        var workflowId = Guid.NewGuid();
        var workflowVersionId = Guid.NewGuid();
        return new StubSchedulerPlannerService(
            CreateScheduledPlanWorkspace(planId, workflowId, workflowVersionId),
            new SchedulerPlanEditorModel
            {
                Name = "Office365 email watch",
                TargetKind = SchedulerPlanTargetKind.Workflow,
                TargetId = workflowId,
                TargetVersionId = workflowVersionId,
                CronExpression = "0 0 9 ? * MON-FRI",
                TimeZoneId = "UTC",
                InputJson = "{}",
                IsEnabled = true
            });
    }

    private static async Task<ComponentTestHarness> CreateUrlHarnessAsync(StubSchedulerPlannerService schedulerService, RecordingSchedulerBack back)
    {
        var harness = await ComponentTestHarness.CreateAsync(services =>
        {
            services.RemoveAll<ISchedulerPlannerService>();
            services.RemoveAll<ISchedulerWorkflowInputSchemaService>();
            services.AddSingleton<ISchedulerWorkflowInputSchemaService>(new EmptySchedulerSchemaService());
            services.AddSingleton<ISchedulerPlannerService>(schedulerService);
            services.AddSingleton<IAppNavigationHistory>(back);
        });
        back.Navigation = (BunitNavigationManager)harness.Context.Services.GetRequiredService<NavigationManager>();
        return harness;
    }

    private static string Location(NavigationManager navigation) => "/" + navigation.ToBaseRelativePath(navigation.Uri);

    // Mirrors the browser's Back: the previous entry returns with its own entry state.
    private sealed class RecordingSchedulerBack : IAppNavigationHistory
    {
        public BunitNavigationManager? Navigation { get; set; }

        public int Calls { get; private set; }

        public ValueTask BackAsync(CancellationToken cancellationToken = default)
        {
            Calls++;
            var navigation = Navigation ?? throw new InvalidOperationException("The navigation manager is not attached.");
            var previous = navigation.History.ElementAt(1);
            navigation.NavigateTo(previous.Uri, new NavigationOptions { HistoryEntryState = previous.Options.HistoryEntryState });
            return ValueTask.CompletedTask;
        }
    }
}
