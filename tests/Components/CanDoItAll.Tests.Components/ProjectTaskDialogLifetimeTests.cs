using Bunit;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Components.CanvasLib;
using CanDoItAll.Infrastructure.Configuration;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Modules.Workbench.Pages;
using CanDoItAll.Workbench.Planning.UI;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.ProjectStructure;

public sealed class ProjectTaskDialogLifetimeTests {
    private static readonly DateTimeOffset Start = new(2026, 10, 5, 8, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Cancel_and_late_save_close_only_the_original_dialog_and_queued_submit_is_retired(bool general) {
        using var context = CreateContext();
        var host = context.Render<DialogHost>();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<PlanningTaskSaveResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var count = 0;
        var first = Open(context, general, () => {
            count++;
            started.TrySetResult();
            return release.Task;
        });
        var actions = host.FindComponent<PlanningTaskFormActions>().Instance;
        var submitted = actions.Submitted;
        var cancel = actions.Cancelled;
        var oldPort = CapturePort(host, general);
        var pending = host.InvokeAsync(() => submitted.InvokeAsync());
        await started.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await host.InvokeAsync(() => submitted.InvokeAsync());
        Assert.Equal(1, count);
        var second = Open(context, !general, () => Task.FromResult(PlanningTaskSaveResult.Accepted));
        await host.InvokeAsync(() => cancel.InvokeAsync());
        Assert.Null(await first.WaitAsync(TimeSpan.FromSeconds(2)));
        Assert.False(second.IsCompleted);
        release.SetResult(new(PlanningTaskSaveStatus.Committed, "Original task saved", ["original-task"], [], []));
        await pending.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.False(second.IsCompleted);
        await host.InvokeAsync(oldPort);
        Assert.Equal(1, count);
        Assert.Single(context.Services.GetRequiredService<DialogService>().Dialogs);
        Assert.DoesNotContain("Original task saved", host.Markup);
    }

    [Theory]
    [InlineData(false, PlanningTaskSaveStatus.Partial)]
    [InlineData(true, PlanningTaskSaveStatus.Partial)]
    [InlineData(false, PlanningTaskSaveStatus.Unknown)]
    [InlineData(true, PlanningTaskSaveStatus.Unknown)]
    public async Task Partial_and_unknown_results_retain_draft_known_ids_and_block_replay(bool general, PlanningTaskSaveStatus status) {
        using var context = CreateContext();
        var host = context.Render<DialogHost>();
        var calls = 0;
        var reads = 0;
        var result = new PlanningTaskSaveResult(status, "Task outcome needs review", ["exact-task"], ["exact-resource"], [
            new(PlanningTaskPhase.Task, PlanningTaskPhaseState.Committed, "Task fields saved."),
            new(PlanningTaskPhase.Attachment, PlanningTaskPhaseState.Unconfirmed, "Attachment needs review.")
        ], true);
        var opening = Open(context, general, () => {
            calls++;
            return Task.FromResult(result);
        }, () => {
            reads++;
            return Task.FromResult(result with { RequiresReadback = false, AllowsCorrectedSubmission = false });
        });
        var prefix = general ? "project-structure-task-create" : "project-structure-gantt-task";
        host.Find($"[data-testid='{prefix}-title']").Input("Draft retained for review");
        var submit = host.FindComponent<PlanningTaskFormActions>().Instance.Submitted;
        await host.InvokeAsync(() => submit.InvokeAsync());
        Assert.False(opening.IsCompleted);
        Assert.Equal("Draft retained for review", host.Find($"[data-testid='{prefix}-title']").GetAttribute("value"));
        Assert.Contains("exact-task", host.Markup);
        Assert.Contains("exact-resource", host.Markup);
        await host.InvokeAsync(() => submit.InvokeAsync());
        Assert.Equal(1, calls);
        await host.Find("[data-testid='planning-task-readback']").ClickAsync(new MouseEventArgs());
        await host.InvokeAsync(() => submit.InvokeAsync());
        Assert.Equal(1, calls);
        Assert.Equal(1, reads);
        Assert.False(opening.IsCompleted);
    }

    private static Func<Task> CapturePort(IRenderedComponent<DialogHost> host, bool general) {
        if (general) {
            var port = host.FindComponent<PlanningStructureTaskEditor>().Instance.Submitted!;
            return () => port(new(Request(), null, Estimate: ProjectTaskEstimate.Empty()));
        }
        var create = host.FindComponent<PlanningGanttTaskEditor>().Instance.CreateSubmitted!;
        return () => create(new("Retired", Start, Start.AddHours(8), null, null, ProjectTaskEstimate.Empty()));
    }

    private static Task<object?> Open(BunitContext context, bool general, Func<Task<PlanningTaskSaveResult>> save,
        Func<Task<PlanningTaskSaveResult>>? read = null) {
        var dialogs = context.Services.GetRequiredService<DialogService>();
        return general ? dialogs.OpenAsync<ProjectStructureTaskCreateDialog>("General task", new Dictionary<string, object?> {
            [nameof(ProjectStructureTaskCreateDialog.ProjectId)] = Guid.NewGuid(),
            [nameof(ProjectStructureTaskCreateDialog.CreateRequest)] = Request(),
            [nameof(ProjectStructureTaskCreateDialog.Submitted)] = new Func<ProjectStructureTaskDialogResult, Task<PlanningTaskSaveResult>>(_ => save()),
            [nameof(ProjectStructureTaskCreateDialog.Readback)] = read
        }) : dialogs.OpenAsync<ProjectStructureGanttTaskDialog>("Gantt task", new Dictionary<string, object?> {
            [nameof(ProjectStructureGanttTaskDialog.ProjectId)] = Guid.NewGuid(),
            [nameof(ProjectStructureGanttTaskDialog.DefaultStartUtc)] = Start,
            [nameof(ProjectStructureGanttTaskDialog.DefaultEndUtc)] = Start.AddHours(8),
            [nameof(ProjectStructureGanttTaskDialog.CreateSubmitted)] = new Func<ProjectStructureTaskCreateRequest, Task<PlanningTaskSaveResult>>(_ => save()),
            [nameof(ProjectStructureGanttTaskDialog.Readback)] = read
        });
    }

    private static CanvasWorkbenchCreateActionRequest Request() => new(ProjectStructureTaskActionIds.Create, "parent", 1, 2,
        "parent", "Task", "Lane", "Notes", "child", ProjectStructureTaskActionIds.CreateMode, "task", null, []);
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
