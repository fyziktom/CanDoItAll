using Bunit;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Modules.Workbench.Pages;
using CanDoItAll.Workbench.Planning.UI;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.WorkbenchPlanning;

public sealed class TaskComparisonUiTests {
    [Fact]
    public async Task Current_untracked_progress_becomes_the_validation_baseline_after_Apply() {
        await using var context = Context();
        var owner = new Owner();
        var writes = 0;
        var cut = Editor(context, owner, draft => {
            Assert.Equal(-1, draft.ProgressPercent);
            writes++;
            return Task.FromResult(PlanningTaskSaveResult.Accepted);
        }, progress: 10, execution: ProjectTaskExecutionSnapshot.Unknown);
        owner.Current = Assert.IsType<PlanningTaskEditorInput.Gantt>(cut.Instance.ReadCloseRequest().Input) with { Progress = "-1" };
        await Compare(cut);
        await Click(cut, "planning-task-compare-apply");
        Assert.Equal("-1", cut.Find("[data-testid='project-structure-gantt-task-progress']").GetAttribute("value"));
        await Click(cut, "project-structure-gantt-task-submit");
        Assert.Equal(1, writes);
    }

    [Fact]
    public async Task A_retained_local_execution_transition_stays_visible_when_current_lifecycle_makes_it_invalid() {
        await using var context = Context();
        var owner = new Owner();
        var writes = 0;
        var cut = Editor(context, owner, _ => {
            writes++;
            return Task.FromResult(PlanningTaskSaveResult.Accepted);
        });
        var original = Assert.IsType<PlanningTaskEditorInput.Gantt>(cut.Instance.ReadCloseRequest().Input);
        owner.Current = original with { Execution = new(ProjectTaskExecutionState.Completed, "2026-11-02T12:00", "2026-11-02T14:00") };
        await cut.InvokeAsync(() => cut.Find("[data-testid='project-structure-gantt-task-execution-state']").ChangeAsync(new() { Value = "Started" }));
        await cut.InvokeAsync(() => cut.Find("[data-testid='project-structure-gantt-task-execution-started']").InputAsync(new() { Value = "2026-11-02T12:00" }));
        await Compare(cut);
        await cut.InvokeAsync(() => cut.FindAll("[data-testid='planning-task-field-execution'] button")
            .Single(button => button.TextContent.Trim() == "Use my draft").ClickAsync(new()));
        await Click(cut, "planning-task-compare-apply");
        var retained = Assert.Single(cut.FindAll("[data-testid='project-structure-gantt-task-execution-state'] option"),
            option => option.GetAttribute("value") == nameof(ProjectTaskExecutionState.Started));
        Assert.Contains("retained draft", retained.TextContent, StringComparison.Ordinal);
        cut.Find("[data-testid='project-structure-gantt-task-validation-error']");
        await Click(cut, "project-structure-gantt-task-submit");
        Assert.Equal(0, writes);
        Assert.Equal(ProjectTaskExecutionState.Started, Assert.IsType<PlanningTaskEditorInput.Gantt>(cut.Instance.ReadCloseRequest().Input).Execution!.State);
    }

    [Fact]
    public async Task Apply_retains_unblurred_invalid_cost_in_the_mounted_editor_and_Undo_keeps_current_authority() {
        await using var context = Context();
        var owner = new Owner();
        var writes = 0;
        var cut = Editor(context, owner, _ => {
            writes++;
            return Task.FromResult(PlanningTaskSaveResult.Accepted);
        });
        var original = Assert.IsType<PlanningTaskEditorInput.Gantt>(cut.Instance.ReadCloseRequest().Input);
        owner.Current = original with { Title = "Peer title" };
        var control = cut.Find("[data-testid='project-structure-gantt-task-estimate-cost']");
        var mountedEstimate = cut.FindComponent<ProjectStructureTaskEstimateEditor>().Instance;
        await cut.InvokeAsync(() => control.InputAsync(new() { Value = "2e" }));
        await Compare(cut);
        await Click(cut, "planning-task-compare-apply");

        Assert.Same(mountedEstimate, cut.FindComponent<ProjectStructureTaskEstimateEditor>().Instance);
        Assert.Equal("2e", cut.Find("[data-testid='project-structure-gantt-task-estimate-cost']").GetAttribute("value"));
        Assert.Equal("Peer title", cut.Find("[data-testid='project-structure-gantt-task-title']").GetAttribute("value"));
        Assert.Equal(1, owner.Accepts);
        Assert.Equal(0, writes);
        await Click(cut, "project-structure-gantt-task-submit");
        Assert.Equal(0, writes);
        await Click(cut, "planning-task-compare-undo");
        Assert.Equal("Original", cut.Find("[data-testid='project-structure-gantt-task-title']").GetAttribute("value"));
        Assert.Equal("2e", control.GetAttribute("value"));
        Assert.Equal(1, owner.Accepts);
        Assert.Equal("Peer title", Assert.IsType<PlanningTaskEditorInput.Gantt>(owner.Current).Title);
    }

    [Fact]
    public async Task A_queued_input_during_verification_prevents_the_entire_patch_and_native_baseline_acceptance() {
        await using var context = Context();
        var owner = new Owner();
        var cut = Editor(context, owner);
        owner.Current = Assert.IsType<PlanningTaskEditorInput.Gantt>(cut.Instance.ReadCloseRequest().Input) with { Title = "Peer title" };
        await Compare(cut);
        owner.Verification = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var applying = Click(cut, "planning-task-compare-apply");
        cut.WaitForAssertion(() => Assert.Equal(1, owner.Verifications));
        await cut.InvokeAsync(() => cut.Find("[data-testid='project-structure-gantt-task-title']").InputAsync(new() { Value = "Newer queued title" }));
        owner.Verification.SetResult(true);
        await applying;
        Assert.Equal(0, owner.Accepts);
        Assert.Equal("Newer queued title", cut.Find("[data-testid='project-structure-gantt-task-title']").GetAttribute("value"));
        Assert.Contains("Nothing was applied", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Access_retirement_removes_private_current_and_previous_comparisons_but_retains_local_input() {
        await using var context = Context();
        var owner = new Owner();
        var cut = Editor(context, owner);
        owner.Current = Assert.IsType<PlanningTaskEditorInput.Gantt>(cut.Instance.ReadCloseRequest().Input) with { Title = "Private peer title" };
        await cut.InvokeAsync(() => cut.Find("[data-testid='project-structure-gantt-task-title']").InputAsync(new() { Value = "Local title" }));
        await Compare(cut);
        Assert.Contains("Private peer title", cut.Markup, StringComparison.Ordinal);
        await cut.InvokeAsync(owner.Retire);
        cut.WaitForAssertion(() => Assert.DoesNotContain("Private peer title", cut.Markup, StringComparison.Ordinal));
        Assert.Equal("Local title", cut.Find("[data-testid='project-structure-gantt-task-title']").GetAttribute("value"));
        Assert.True(cut.Find("[data-testid='planning-task-compare']").HasAttribute("disabled"));
    }

    [Fact]
    public async Task An_unknown_submission_can_be_read_but_cannot_be_rearmed_by_a_comparison() {
        await using var context = Context();
        var owner = new Owner();
        var writes = 0;
        var cut = Editor(context, owner, _ => {
            writes++;
            return Task.FromResult(new PlanningTaskSaveResult(PlanningTaskSaveStatus.Unknown, "Unconfirmed save", [], [], [], true));
        });
        owner.Current = cut.Instance.ReadCloseRequest().Input;
        await Click(cut, "project-structure-gantt-task-submit");
        await Compare(cut);
        Assert.True(cut.Find("[data-testid='planning-task-compare-apply']").HasAttribute("disabled"));
        await Click(cut, "planning-task-compare-apply");
        Assert.Equal(0, owner.Accepts);
        Assert.Equal(1, writes);
    }

    [Fact]
    public async Task A_new_comparison_result_invalidates_an_older_discard_decision() {
        await using var context = Context();
        var owner = new Owner();
        var cut = Editor(context, owner);
        owner.Current = cut.Instance.ReadCloseRequest().Input;
        await Compare(cut);
        var leaving = cut.InvokeAsync(cut.Instance.ConfirmLeaveAsync);
        cut.WaitForElement("[data-testid='planning-task-dirty-discard']");
        await cut.InvokeAsync(() => cut.Find("[data-testid='planning-task-result-title']").InputAsync(new() { Value = "New comparison result" }));
        await Click(cut, "planning-task-dirty-discard");
        Assert.False(await leaving);
        Assert.Equal("New comparison result", cut.Find("[data-testid='planning-task-result-title']").GetAttribute("value"));
        Assert.Equal("Original", cut.Find("[data-testid='project-structure-gantt-task-title']").GetAttribute("value"));
    }

    private static BunitContext Context() {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddCanDoItAllBaseLib();
        return context;
    }

    private static IRenderedComponent<PlanningGanttTaskEditor> Editor(BunitContext context, Owner owner,
        Func<ProjectStructureTaskEditDialogResult, Task<PlanningTaskSaveResult>>? submit = null, int progress = 0,
        ProjectTaskExecutionSnapshot? execution = null) {
        var start = new DateTimeOffset(2026, 11, 2, 12, 0, 0, TimeSpan.Zero).AddTicks(1234567);
        var model = new PlanningGanttTaskEditModel(new("task:comparison"), "Original", start, start.AddHours(8), progress,
            new(8, ProjectWorkItemEffortUnit.Hours, null, "USD"), null, false, true, execution ?? ProjectTaskExecutionSnapshot.NotStarted);
        return context.Render<PlanningGanttTaskEditor>(parameters => parameters.Add(component => component.ProjectId, Guid.NewGuid())
            .Add(component => component.EditModel, model).Add(component => component.ComparisonOwner, owner)
            .Add(component => component.EditSubmitted, submit));
    }

    private static Task Click(IRenderedComponent<PlanningGanttTaskEditor> cut, string id) =>
        cut.InvokeAsync(() => cut.Find($"[data-testid='{id}']").ClickAsync(new()));

    private static async Task Compare(IRenderedComponent<PlanningGanttTaskEditor> cut) {
        await Click(cut, "planning-task-compare");
        cut.WaitForElement("[data-testid='planning-task-field-title']");
    }

    private sealed class Owner : IPlanningTaskComparisonOwner {
        private readonly Guid witness = Guid.NewGuid();
        public PlanningTaskEditorInput Current { get; set; } = default!;
        public bool IsActive { get; private set; } = true;
        public PlanningTaskUnavailableReason? UnavailableReason => IsActive ? null : PlanningTaskUnavailableReason.AccessChanged;
        public long ChangeRevision => 0;
        public int Accepts { get; private set; }
        public int Verifications { get; private set; }
        public TaskCompletionSource<bool>? Verification { get; set; }
        public event Action? Changed;
        public Task<PlanningTaskComparisonCurrent> ReadCurrentAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new PlanningTaskComparisonCurrent(witness, Current, true, false));
        public Task<bool> VerifyCurrentAsync(Guid expected, CancellationToken cancellationToken) {
            Verifications++;
            return Verification?.Task ?? Task.FromResult(expected == witness && IsActive);
        }
        public bool AcceptCurrent(Guid expected) {
            Assert.Equal(witness, expected);
            Accepts++;
            return IsActive;
        }
        public void Retire() {
            IsActive = false;
            Changed?.Invoke();
        }
    }
}
