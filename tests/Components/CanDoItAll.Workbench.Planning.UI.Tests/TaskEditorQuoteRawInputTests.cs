using Bunit;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Workbench.Planning.UI;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.WorkbenchPlanning;

public sealed class TaskEditorQuoteRawInputTests {
    [Theory]
    [InlineData("effort")]
    [InlineData("cost")]
    public async Task Held_quote_cannot_replace_newer_invalid_raw_estimate_input(string field) {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddCanDoItAllBaseLib();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var released = new TaskCompletionSource<ProjectStructureTaskResourceCostQuote>(TaskCreationOptions.RunContinuationsAsynchronously);
        var resourceId = Guid.NewGuid();
        var start = new DateTimeOffset(2026, 10, 5, 8, 0, 0, TimeSpan.Zero);
        var submissions = new List<PlanningGanttTaskCreate>();
        var cut = context.Render<PlanningGanttTaskEditor>(parameters => parameters
            .Add(component => component.ProjectId, Guid.NewGuid())
            .Add(component => component.DefaultStartUtc, start)
            .Add(component => component.DefaultEndUtc, start.AddHours(8))
            .Add(component => component.DefaultEstimate, new(8, ProjectWorkItemEffortUnit.Hours, 10, "USD"))
            .Add(component => component.ResourceOptions, [new(ProjectStructureTaskResourceKind.Person, resourceId, null, "Person", "Person", "Synthetic", false, false)])
            .Add(component => component.QuoteResolver, (_, _) => {
                started.TrySetResult();
                return released.Task;
            })
            .Add(component => component.CreateSubmitted, draft => {
                submissions.Add(draft);
                return Task.FromResult(PlanningTaskSaveResult.Accepted);
            }));
        var selection = cut.Find($"[data-testid='project-structure-gantt-task-resource-person-{resourceId:N}']").ClickAsync(new MouseEventArgs());
        await started.Task.WaitAsync(TimeSpan.FromSeconds(2));
        cut.Find($"[data-testid='project-structure-gantt-task-estimate-{field}']").Change("-");
        released.SetResult(new(ProjectStructureTaskResourceCostQuoteStatus.Available, 500, "EUR", "Late quote", "Obsolete amount", start, ProjectStructureTaskResourceCostSource.CrmWorkforceRate));
        await selection.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal("-", cut.Find($"[data-testid='project-structure-gantt-task-estimate-{field}']").GetAttribute("value"));
        Assert.DoesNotContain("Obsolete amount", cut.Markup);
        await cut.Find("[data-testid='project-structure-gantt-task-submit']").ClickAsync(new MouseEventArgs());
        Assert.Empty(submissions);
    }
}
