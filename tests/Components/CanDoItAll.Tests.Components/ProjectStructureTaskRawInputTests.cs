using Bunit;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Components.CanvasLib;
using CanDoItAll.Infrastructure.Configuration;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Modules.Workbench.Pages;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.ProjectStructure;

public sealed class ProjectStructureTaskRawInputTests {
    private static readonly DateTimeOffset Start = new(2026, 10, 5, 8, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("duration", "-2")]
    [InlineData("start", "invalid UTC")]
    [InlineData("end", "2026-10-05T07:00")]
    [InlineData("progress", "101")]
    public async Task Gantt_invalid_raw_input_cannot_submit_an_earlier_valid_value_after_title_edit(string field, string raw) {
        using var context = CreateContext();
        var host = context.Render<DialogHost>();
        var result = OpenGantt(context, 10);
        host.WaitForElement($"[data-testid='project-structure-gantt-task-{field}']").Change(raw);
        host.Find("[data-testid='project-structure-gantt-task-title']").Input("New title");
        await host.Find("[data-testid='project-structure-gantt-task-submit']").ClickAsync(new MouseEventArgs());
        Assert.False(result.IsCompleted, "Invalid raw input must keep the original draft open.");
        Assert.Equal(raw, host.Find($"[data-testid='project-structure-gantt-task-{field}']").GetAttribute("value"));
    }

    [Fact]
    public async Task Gantt_preserves_untracked_progress_on_unrelated_title_edit() {
        using var context = CreateContext();
        var host = context.Render<DialogHost>();
        var result = OpenGantt(context, ProjectProgressPolicy.UntrackedPercent);
        host.WaitForElement("[data-testid='project-structure-gantt-task-title']").Input("Only the title changes");
        await host.Find("[data-testid='project-structure-gantt-task-submit']").ClickAsync(new MouseEventArgs());
        Assert.Equal(ProjectProgressPolicy.UntrackedPercent,
            Assert.IsType<ProjectStructureTaskEditDialogResult>(await result.WaitAsync(TimeSpan.FromSeconds(2))).ProgressPercent);
    }

    [Fact]
    public async Task General_invalid_due_input_survives_unrelated_title_edit() {
        using var context = CreateContext();
        var host = context.Render<DialogHost>();
        var request = new CanvasWorkbenchCreateActionRequest(ProjectStructureTaskActionIds.Create,
            "parent", 10, 20, "parent", "Task", "Lane", "Notes", "child",
            ProjectStructureTaskActionIds.CreateMode, "task", null,
            [new() { Key = "dueUtc", Value = Start.ToString("O") }]);
        var result = context.Services.GetRequiredService<DialogService>().OpenAsync<ProjectStructureTaskCreateDialog>("General task",
            new Dictionary<string, object?>() {
                [nameof(ProjectStructureTaskCreateDialog.ProjectId)] = Guid.NewGuid(),
                [nameof(ProjectStructureTaskCreateDialog.CreateRequest)] = request
            });
        host.WaitForElement("[data-testid='project-structure-task-create-due']").Change("invalid UTC");
        host.Find("[data-testid='project-structure-task-create-title']").Input("New title");
        await host.Find("[data-testid='project-structure-task-create-submit']").ClickAsync(new MouseEventArgs());
        Assert.False(result.IsCompleted, "The original valid due date must not replace newer invalid text.");
        Assert.Equal("invalid UTC", host.Find("[data-testid='project-structure-task-create-due']").GetAttribute("value"));
    }

    [Fact]
    public void Cancelled_execution_does_not_drop_an_invalid_optional_actual_start() {
        using var context = CreateContext();
        var cut = context.Render<ProjectStructureTaskExecutionEditor>(parameters => parameters
            .Add(component => component.Value, new(ProjectTaskExecutionState.Cancelled, null, Start)));
        cut.Find("[data-testid='project-structure-task-execution-started']").Change("invalid UTC");
        Assert.False(cut.Instance.TryGetValue(out _, out var error));
        Assert.NotEmpty(error);
        Assert.Equal("invalid UTC", cut.Find("[data-testid='project-structure-task-execution-started']").GetAttribute("value"));
    }

    private static Task<object?> OpenGantt(BunitContext context, int progress) {
        return context.Services.GetRequiredService<DialogService>().OpenAsync<ProjectStructureGanttTaskDialog>("Edit task", new Dictionary<string, object?>() {
            [nameof(ProjectStructureGanttTaskDialog.ProjectId)] = Guid.NewGuid(),
            [nameof(ProjectStructureGanttTaskDialog.DefaultStartUtc)] = Start,
            [nameof(ProjectStructureGanttTaskDialog.DefaultEndUtc)] = Start.AddHours(8),
            [nameof(ProjectStructureGanttTaskDialog.EditModel)] = new ProjectStructureGanttTaskEditModel(
                new("custom:original"), "Task", Start, Start.AddHours(8), progress,
                new(8, ProjectWorkItemEffortUnit.Hours, null, ""), null, Execution: ProjectTaskExecutionSnapshot.Unknown)
        });
    }

    private static BunitContext CreateContext() {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddCanDoItAllBaseLib();
        context.Services.AddSingleton<ICurrencyFormatter>(new Currency());
        return context;
    }

    private sealed class Currency : ICurrencyFormatter {
        public string CurrencyCode => "USD";
        public string Format(decimal value) => $"USD {value:0.00}";
    }
}
