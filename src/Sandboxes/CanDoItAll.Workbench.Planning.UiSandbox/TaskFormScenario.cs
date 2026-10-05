using CanDoItAll.Components.CanvasLib;
using CanDoItAll.Components.Gantt;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Workbench.Planning.UI;

namespace CanDoItAll.Workbench.Planning.UiSandbox;

public enum TaskFormMode { GanttCreate, GanttEdit, StructureCreate, StructureEdit }
public enum TaskFormOutcome { Success, Rejected, Partial, Unknown, Restricted, InvalidRaw, HeldQuote }

public sealed class TaskFormScenario {
    private static readonly DateTimeOffset Start = new(2026, 10, 5, 8, 0, 0, TimeSpan.Zero);
    private readonly List<TaskCompletionSource<ProjectStructureTaskResourceCostQuote>> pendingQuotes = [];
    private PlanningTaskSaveResult? lastResult;
    public Guid ProjectId { get; } = Guid.NewGuid();
    public TaskFormOutcome Outcome { get; set; }
    public int SubmissionCount { get; private set; }
    public IReadOnlyList<ProjectStructureTaskResourceOption> Resources { get; } = [
        new(ProjectStructureTaskResourceKind.Person, Guid.NewGuid(), null, "Synthetic designer", "Person", "USD 25 / hour", false, false),
        new(ProjectStructureTaskResourceKind.Agent, Guid.NewGuid(), null, "Synthetic delivery agent", "Agent", "No project authority", false, false),
        new(ProjectStructureTaskResourceKind.Workflow, Guid.NewGuid(), Guid.NewGuid(), "Synthetic review workflow", "Definition", "Additive attachment", false, false),
        new(ProjectStructureTaskResourceKind.Process, Guid.NewGuid(), null, "Synthetic release process", "Definition", "Does not launch a process", false, false)
    ];
    public CanvasWorkbenchCreateActionRequest Request(TaskFormMode mode) => new(
        mode == TaskFormMode.StructureEdit ? $"edit:{ProjectStructureTaskActionIds.Create}" : ProjectStructureTaskActionIds.Create,
        "synthetic-parent", 10, 20, "synthetic-parent", "Synthetic planning task", "Delivery", "Preserved synthetic notes.",
        "child", ProjectStructureTaskActionIds.CreateMode, "task", null, [
            new() { Key = "dueUtc", Value = Start.AddDays(1).ToString("O") },
            new() { Key = ProjectTaskEstimateInputKeys.ExpectedEffortValue, Value = Outcome == TaskFormOutcome.InvalidRaw ? "unfinished effort" : "8" },
            new() { Key = "syntheticHiddenMetadata", Value = "preserved" },
            new() { Key = "repositoryRef", Value = "synthetic-repository" }
        ]);

    public PlanningGanttTaskEditModel Model(GanttTask? task = null) => new(task?.Id ?? new("synthetic-task"),
        task?.Title ?? "Synthetic planning task", task?.Start ?? Start, task?.End ?? Start.AddHours(8),
        task?.ProgressPercent ?? -1, new(8, ProjectWorkItemEffortUnit.Hours, 0, "USD"),
        new(Resources[0].Kind, Resources[0].ResourceId), false, Outcome != TaskFormOutcome.Restricted,
        ProjectTaskExecutionSnapshot.Unknown);

    public Task<ProjectStructureTaskResourceCostQuote> QuoteAsync(ProjectStructureTaskResourceCostRequest request, CancellationToken token) {
        if (Outcome != TaskFormOutcome.HeldQuote) {
            return Task.FromResult(Quote());
        }
        var pendingQuote = new TaskCompletionSource<ProjectStructureTaskResourceCostQuote>(TaskCreationOptions.RunContinuationsAsynchronously);
        pendingQuotes.Add(pendingQuote);
        return pendingQuote.Task;
    }

    public void ReleaseQuote() {
        foreach (var pendingQuote in pendingQuotes) {
            pendingQuote.TrySetResult(Quote());
        }
        pendingQuotes.Clear();
    }

    public PlanningTaskSaveResult Save(string taskId, Action apply) {
        SubmissionCount++;
        if (Outcome == TaskFormOutcome.Rejected) {
            return new(PlanningTaskSaveStatus.Rejected, "Synthetic rejection: correct the draft and retry.", [], [], []);
        }
        apply();
        var status = Outcome switch {
            TaskFormOutcome.Partial => PlanningTaskSaveStatus.Partial,
            TaskFormOutcome.Unknown => PlanningTaskSaveStatus.Unknown,
            _ => PlanningTaskSaveStatus.Committed
        };
        lastResult = new(status, status switch {
            PlanningTaskSaveStatus.Partial => "Synthetic partial result: task saved, attachment failed. The draft remains open.",
            PlanningTaskSaveStatus.Unknown => "Synthetic acknowledgement loss: read the memory specimen before another write.",
            _ => "Synthetic task saved in this independent host."
        }, status == PlanningTaskSaveStatus.Unknown ? [] : [taskId], [], [
            new(PlanningTaskPhase.Task, status == PlanningTaskSaveStatus.Unknown ? PlanningTaskPhaseState.Unconfirmed : PlanningTaskPhaseState.Committed,
                "Synthetic task outcome; no native writes."),
            new(PlanningTaskPhase.Attachment, status == PlanningTaskSaveStatus.Partial ? PlanningTaskPhaseState.Rejected : PlanningTaskPhaseState.NotAttempted,
                "Synthetic attachment phase.")
        ], status != PlanningTaskSaveStatus.Committed);
        return lastResult;
    }

    public Task<PlanningTaskSaveResult> ReadbackAsync() => Task.FromResult((lastResult ?? PlanningTaskSaveResult.Accepted) with {
        RequiresReadback = false, AllowsCorrectedSubmission = false,
        Message = "Synthetic memory read completed. No submission was replayed. Close and reopen to continue."
    });

    private static ProjectStructureTaskResourceCostQuote Quote() => new(ProjectStructureTaskResourceCostQuoteStatus.Available,
        200, "USD", "Synthetic rate", "Synthetic preview; a native save would revalidate pricing.", Start,
        ProjectStructureTaskResourceCostSource.CrmWorkforceRate);
}
