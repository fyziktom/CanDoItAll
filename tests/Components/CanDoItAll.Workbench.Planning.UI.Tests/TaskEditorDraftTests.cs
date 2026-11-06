using Bunit;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Components.CanvasLib;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Modules.Workbench.Pages;
using CanDoItAll.Workbench.Planning.UI;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.WorkbenchPlanning;

public sealed class TaskEditorDraftTests {
    private static readonly DateTimeOffset Start = new DateTimeOffset(2026, 10, 5, 8, 0, 0, TimeSpan.Zero).AddTicks(1234567);

    [Theory]
    [InlineData("start", "2026-11-")]
    [InlineData("duration", "2e")]
    [InlineData("estimate-cost", "2e")]
    [InlineData("execution-started", "2026-11-")]
    public async Task Unblurred_Gantt_input_is_captured_and_an_old_discard_cannot_remove_newer_input(string field, string raw) {
        await using var context = CreateContext();
        var execution = field == "execution-started"
            ? new ProjectTaskExecutionSnapshot(ProjectTaskExecutionState.Started, Start, null)
            : ProjectTaskExecutionSnapshot.NotStarted;
        var model = new PlanningGanttTaskEditModel(new("retained-task"), "Original", Start, Start.AddHours(8),
            0, new(8, ProjectWorkItemEffortUnit.Hours, null, "USD"), null, false, true, execution);
        var cut = context.Render<PlanningGanttTaskEditor>(parameters => parameters
            .Add(component => component.ProjectId, Guid.NewGuid()).Add(component => component.EditModel, model));
        Assert.False(cut.Instance.ReadCloseRequest().IsDirty);

        await cut.InvokeAsync(() => cut.Find($"[data-testid='project-structure-gantt-task-{field}']").InputAsync(new() { Value = raw }));

        var capture = cut.Instance.ReadCloseRequest();
        var input = Assert.IsType<PlanningTaskEditorInput.Gantt>(capture.Input);
        Assert.True(capture.IsDirty);
        Assert.Equal(raw, field switch {
            "start" => input.Start,
            "duration" => input.Duration,
            "estimate-cost" => input.Estimate!.Cost,
            "execution-started" => input.Execution!.Started,
            _ => throw new InvalidOperationException("Unknown test field.")
        });
        var leaving = cut.InvokeAsync(cut.Instance.ConfirmLeaveAsync);
        cut.WaitForElement("[data-testid='planning-task-dirty-close']");
        await cut.InvokeAsync(() => cut.Find("[data-testid='project-structure-gantt-task-title']").InputAsync(new() { Value = "Newer input" }));
        await cut.InvokeAsync(() => cut.Find("[data-testid='planning-task-dirty-discard']").ClickAsync(new()));
        Assert.False(await leaving);
        Assert.Equal("Newer input", cut.Find("[data-testid='project-structure-gantt-task-title']").GetAttribute("value"));
        Assert.Equal(raw, cut.Find($"[data-testid='project-structure-gantt-task-{field}']").GetAttribute("value"));
    }

    [Theory]
    [InlineData("due", "2026-11-")]
    [InlineData("estimate-cost", "2e")]
    public async Task Unblurred_canvas_Task_input_survives_Continue(string field, string raw) {
        await using var context = CreateContext();
        var cut = context.Render<PlanningStructureTaskEditor>(parameters => parameters
            .Add(component => component.ProjectId, Guid.NewGuid()).Add(component => component.CreateRequest, Request([])));
        Assert.False(cut.Instance.ReadCloseRequest().IsDirty);

        await cut.InvokeAsync(() => cut.Find($"[data-testid='project-structure-task-create-{field}']").InputAsync(new() { Value = raw }));

        var capture = cut.Instance.ReadCloseRequest();
        var input = Assert.IsType<PlanningTaskEditorInput.Structure>(capture.Input);
        Assert.True(capture.IsDirty);
        Assert.Equal(raw, field == "due" ? input.Due : input.Estimate!.Cost);
        var leaving = cut.InvokeAsync(cut.Instance.ConfirmLeaveAsync);
        cut.WaitForElement("[data-testid='planning-task-dirty-close']");
        await cut.InvokeAsync(() => cut.Find("[data-testid='planning-task-dirty-continue']").ClickAsync(new()));
        Assert.False(await leaving);
        Assert.Equal(raw, cut.Find($"[data-testid='project-structure-task-create-{field}']").GetAttribute("value"));
    }

    [Fact]
    public async Task Correcting_an_invalid_cost_preserves_man_day_effort() {
        using var context = CreateContext();
        PlanningGanttTaskCreate? saved = null;
        var cut = context.Render<PlanningGanttTaskEditor>(parameters => parameters
            .Add(component => component.ProjectId, Guid.NewGuid())
            .Add(component => component.DefaultStartUtc, Start)
            .Add(component => component.DefaultEndUtc, Start.AddHours(8))
            .Add(component => component.DefaultEstimate, new(8, ProjectWorkItemEffortUnit.ManDays, -1, "USD"))
            .Add(component => component.CreateSubmitted, draft => {
                saved = draft;
                return Task.FromResult(PlanningTaskSaveResult.Accepted);
            }));
        cut.Find("[data-testid='project-structure-gantt-task-title']").Input("Corrected cost");
        cut.Find("[data-testid='project-structure-gantt-task-estimate-cost']").Change("50");
        await cut.Find("[data-testid='project-structure-gantt-task-submit']").ClickAsync(new MouseEventArgs());
        Assert.NotNull(saved);
        Assert.Equal(8, saved.Estimate.ExpectedEffortHours);
        Assert.Equal(ProjectWorkItemEffortUnit.ManDays, saved.Estimate.ExpectedEffortUnit);
        Assert.Equal(50, saved.Estimate.ExpectedCostAmount);
    }

    [Fact]
    public async Task Initially_invalid_estimate_cannot_be_replaced_by_a_resource_quote() {
        using var context = CreateContext();
        var calls = 0;
        var resourceId = Guid.NewGuid();
        var cut = context.Render<PlanningGanttTaskEditor>(parameters => parameters
            .Add(component => component.ProjectId, Guid.NewGuid())
            .Add(component => component.DefaultStartUtc, Start)
            .Add(component => component.DefaultEndUtc, Start.AddHours(8))
            .Add(component => component.DefaultEstimate, new(8, ProjectWorkItemEffortUnit.ManDays, -1, "USD"))
            .Add(component => component.ResourceOptions, [new(ProjectStructureTaskResourceKind.Person, resourceId, null, "Person", "Person", "Synthetic", false, false)])
            .Add(component => component.QuoteResolver, (_, _) => {
                calls++;
                return Task.FromResult(new ProjectStructureTaskResourceCostQuote(ProjectStructureTaskResourceCostQuoteStatus.Available,
                    100, "USD", "Rate", "Unexpected replacement", Start, ProjectStructureTaskResourceCostSource.CrmWorkforceRate));
            }));
        await cut.Find($"[data-testid='project-structure-gantt-task-resource-person-{resourceId:N}']").ClickAsync(new MouseEventArgs());
        Assert.Equal(0, calls);
        Assert.Equal("-1", cut.Find("[data-testid='project-structure-gantt-task-estimate-cost']").GetAttribute("value"));
    }

    [Fact]
    public async Task Gantt_title_edit_preserves_exact_schedule_estimate_and_unknown_execution() {
        using var context = CreateContext();
        ProjectStructureTaskEditDialogResult? saved = null;
        var estimate = new ProjectTaskEstimate(8.123456789m, ProjectWorkItemEffortUnit.Hours, 0, "USD");
        var model = new PlanningGanttTaskEditModel(new("original-task"), "Original", Start, Start.AddHours(8).AddTicks(123),
            -1, estimate, null, false, false, ProjectTaskExecutionSnapshot.Unknown);
        var cut = context.Render<PlanningGanttTaskEditor>(parameters => parameters
            .Add(component => component.ProjectId, Guid.NewGuid())
            .Add(component => component.EditModel, model)
            .Add(component => component.EditSubmitted, draft => {
                saved = draft;
                return Task.FromResult(PlanningTaskSaveResult.Accepted);
            }));
        cut.Find("[data-testid='project-structure-gantt-task-title']").Input("New title");
        await cut.Find("[data-testid='project-structure-gantt-task-submit']").ClickAsync(new MouseEventArgs());
        Assert.NotNull(saved);
        Assert.Equal(model.StartUtc, saved.StartUtc);
        Assert.Equal(model.EndUtc, saved.EndUtc);
        Assert.Equal(model.Estimate, saved.Estimate);
        Assert.Equal(-1, saved.ProgressPercent);
        Assert.Equal(ProjectTaskExecutionSnapshot.Unknown, saved.Execution);
        Assert.False(saved.AssigneeChanged);
    }

    [Theory]
    [InlineData(ProjectTaskEstimateInputKeys.ExpectedEffortValue, "broken effort", "effort")]
    [InlineData(ProjectTaskEstimateInputKeys.ExpectedCostAmount, "unfinished cost", "cost")]
    [InlineData("dueUtc", "invalid date", "due")]
    public async Task General_initial_invalid_input_stays_visible_and_cannot_submit(string key, string value, string field) {
        using var context = CreateContext();
        var calls = 0;
        var cut = context.Render<PlanningStructureTaskEditor>(parameters => parameters
            .Add(component => component.ProjectId, Guid.NewGuid())
            .Add(component => component.CreateRequest, Request([new() { Key = key, Value = value }]))
            .Add(component => component.Submitted, _ => {
                calls++;
                return Task.FromResult(PlanningTaskSaveResult.Accepted);
            }));
        cut.Find("[data-testid='project-structure-task-create-title']").Input("Unrelated change");
        await cut.Find("[data-testid='project-structure-task-create-submit']").ClickAsync(new MouseEventArgs());
        Assert.Equal(0, calls);
        var suffix = field == "due" ? "due" : $"estimate-{field}";
        Assert.Equal(value, cut.Find($"[data-testid='project-structure-task-create-{suffix}']").GetAttribute("value"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("0")]
    public async Task General_snapshot_preserves_hidden_input_repository_null_or_zero_and_timestamp_precision(string cost) {
        using var context = CreateContext();
        ProjectStructureTaskDialogResult? saved = null;
        var inputs = new List<CanvasWorkbenchInputValue> {
            new() { Key = "externalMetadata", Value = "original" },
            new() { Key = "repositoryRef", Value = "repository:original" },
            new() { Key = "dueUtc", Value = Start.ToString("O") },
            new() { Key = ProjectTaskEstimateInputKeys.ExpectedEffortValue, Value = "1.123456789" },
            new() { Key = ProjectTaskEstimateInputKeys.ExpectedEffortUnit, Value = "ManDays" },
            new() { Key = ProjectTaskEstimateInputKeys.ExpectedCostAmount, Value = cost },
            new() { Key = ProjectTaskEstimateInputKeys.ExpectedCostCurrencyCode, Value = "USD" }
        };
        var cut = context.Render<PlanningStructureTaskEditor>(parameters => parameters
            .Add(component => component.ProjectId, Guid.NewGuid())
            .Add(component => component.CreateRequest, Request(inputs))
            .Add(component => component.Submitted, draft => {
                saved = draft;
                return Task.FromResult(PlanningTaskSaveResult.Accepted);
            }));
        inputs[0].Value = "replacement owner";
        cut.Find("[data-testid='project-structure-task-create-title']").Input("Unrelated change");
        await cut.Find("[data-testid='project-structure-task-create-submit']").ClickAsync(new MouseEventArgs());
        Assert.NotNull(saved);
        Assert.Contains(saved.CreateRequest.InputValues!, item => item.Key == "externalMetadata" && item.Value == "original");
        Assert.Contains(saved.CreateRequest.InputValues!, item => item.Key == "repositoryRef" && item.Value == "repository:original");
        var due = Assert.Single(saved.CreateRequest.InputValues!, item => item.Key == "dueUtc");
        Assert.Equal(Start, DateTimeOffset.Parse(due.Value!));
        Assert.Equal(8.987654312m, saved.Estimate!.ExpectedEffortHours);
        Assert.Equal(cost.Length == 0 ? null : (decimal?)0, saved.Estimate.ExpectedCostAmount);
    }

    [Fact]
    public void Historical_execution_timestamps_survive_an_unchanged_editor() {
        using var context = CreateContext();
        var original = new ProjectTaskExecutionSnapshot(ProjectTaskExecutionState.Completed, Start, Start.AddHours(8).AddTicks(77));
        var cut = context.Render<ProjectStructureTaskExecutionEditor>(parameters => parameters.Add(component => component.Value, original));
        Assert.True(cut.Instance.TryGetValue(out var value, out var error), error);
        Assert.Equal(original, value);
        Assert.Contains(".123", cut.Find("[data-testid='project-structure-task-execution-started']").GetAttribute("value"));
    }

    private static CanvasWorkbenchCreateActionRequest Request(IReadOnlyList<CanvasWorkbenchInputValue> inputs)
        => new(ProjectStructureTaskActionIds.Create, "parent", 1, 2, "parent", "Original", "Lane", "Notes", "child",
            ProjectStructureTaskActionIds.CreateMode, "task", null, inputs);

    private static BunitContext CreateContext() {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddCanDoItAllBaseLib();
        return context;
    }
}
