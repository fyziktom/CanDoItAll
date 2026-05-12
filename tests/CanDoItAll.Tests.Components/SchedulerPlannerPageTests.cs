using Bunit;
using CanDoItAll.Modules.SchedulerPlanner.Pages;

namespace CanDoItAll.Tests.Components;

public sealed class SchedulerPlannerPageTests
{
    [Fact]
    public async Task Scheduler_page_renders_tabs_and_canvas_calendar_host()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();

        var cut = harness.Context.RenderComponent<SchedulerPlannerPage>();

        cut.WaitForAssertion(() =>
        {
            var errors = cut.FindAll("[data-testid='scheduler-error']")
                .Select(element => element.TextContent.Trim())
                .ToArray();
            if (errors.Length > 0)
            {
                Assert.Fail(string.Join(Environment.NewLine, errors));
            }

            Assert.NotEmpty(cut.FindAll("[data-testid='scheduler-tabs']"));
        });
        cut.WaitForElement("[data-testid='scheduler-calendar']");

        Assert.Contains("Scheduled runs", cut.Markup);
        Assert.Contains("New schedule", cut.Markup);
        Assert.Contains("History", cut.Markup);
        Assert.Contains("scheduler-planner-calendar", cut.Markup);
    }
}
