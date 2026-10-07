using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Components.CanvasLib;
using CanDoItAll.Modules.SchedulerPlanner;
using CanDoItAll.Modules.SchedulerPlanner.Presentation;
using CanDoItAll.SchedulerPlanner.UI;

namespace CanDoItAll.SchedulerPlanner.UiSandbox;

public enum SchedulerScenario {
    Representative, Empty, Large, MissingVersion, MissingOption, MalformedJson,
    ReadUnavailable, ProjectionFailure, ReadbackFailure, UnknownSave, RefusedSave, AgentLoading, AgentUnavailable
}
public enum SchedulerWait { None, Workspace, Default, Editor, Schema, Options, Validation, Authority, Save, Readback }

public sealed class SchedulerScenarioStore : ISchedulerWorkspaceOwner {
    public static readonly Guid WorkflowA = new("47ee83a4-199d-4483-ac4a-3015d95cb614");
    public static readonly Guid WorkflowB = new("9c15f81e-a613-4f78-8d90-d319bf5d6138");
    public static readonly Guid VersionA = new("2407708b-22a0-487b-a42f-ce782218c722");
    public static readonly Guid VersionB = new("919c74c4-cddd-4399-abcd-0092e804a328");
    public static readonly Guid PlanA = new("16f257ae-ed84-4282-a2e0-c3710ac03f46");
    public static readonly Guid PlanB = new("b873adfd-ff14-4b30-bea6-7581a57eb313");
    public static readonly DateTimeOffset Now = new(2026, 9, 28, 9, 0, 0, TimeSpan.Zero);
    private TaskCompletionSource gate = NewSignal();
    public TaskCompletionSource Entered { get; private set; } = NewSignal();
    public SchedulerScenario Scenario { get; set; }
    public SchedulerWait Hold { get; set; }
    public bool FailReads { get; set; }
    public bool IgnoreCancellation { get; set; }
    public int Writes { get; private set; }
    public int Reads { get; private set; }
    public int OptionReads { get; private set; }
    public int AgentIntents { get; set; }
    public int EditorReads { get; private set; }
    public Dictionary<Guid, SchedulerDraftValues> Plans { get; } = [];
    public List<SchedulerPlanRunSummary> History { get; } = [];
    public List<Guid> RetainedAdmissions { get; } = [];
    public List<SchedulerDraftValues> Submitted { get; } = [];
    public Func<SchedulerHistoryQuery, CancellationToken, Task<SchedulerWorkspaceData>>? ReadOverride { get; set; }
    public Func<SchedulerDraftValues, CancellationToken, Task<SchedulerWorkflowInputValidationResult>>? ValidationOverride { get; set; }
    public Func<WorkflowInputParameterDescriptor, IReadOnlyDictionary<string, string>, CancellationToken, Task<IReadOnlyList<WorkflowInputParameterOption>>>? OptionsOverride { get; set; }

    public SchedulerScenarioStore(SchedulerScenario scenario = SchedulerScenario.Representative) {
        Scenario = scenario;
        if (scenario == SchedulerScenario.Empty) {
            return;
        }
        Plans[PlanA] = DefaultValues() with { Id = PlanA, Name = "Morning review" };
        Plans[PlanB] = DefaultValues() with { Id = PlanB, Name = "Paused report", TargetId = WorkflowB, TargetVersionId = VersionB, IsEnabled = false };
        if (scenario == SchedulerScenario.Large) {
            for (var index = 0; index < 80; index++) {
                var id = Guid.NewGuid();
                Plans[id] = DefaultValues() with { Id = id, Name = $"Bounded fixture {index:00}" };
            }
        }
        foreach (var status in Enum.GetValues<SchedulerPlanRunDispatchStatus>()) {
            var id = Guid.NewGuid();
            History.Add(new(id, PlanA, "History fixture", SchedulerPlanTargetKind.Workflow, "Review workflow",
                Now.AddHours((int)status + 1), status, 1, null,
                status == SchedulerPlanRunDispatchStatus.NoMessages ? SchedulerPlanRunRoutes.NoMessages : SchedulerPlanRunRoutes.Processed,
                status == SchedulerPlanRunDispatchStatus.WaitingForApproval ? SchedulerPlanRunRetryCategory.WorkflowWaitingForApproval : SchedulerPlanRunRetryCategory.None,
                "Scenario display history; no Workflow was executed.", string.Empty, Now));
            RetainedAdmissions.Add(id);
        }
    }

    public void Release() {
        Hold = SchedulerWait.None;
        gate.TrySetResult();
        gate = NewSignal();
        Entered = NewSignal();
    }
    private async Task WaitAsync(SchedulerWait phase, CancellationToken token = default) {
        if (Hold != phase) {
            return;
        }
        Entered.TrySetResult();
        await gate.Task.WaitAsync(IgnoreCancellation ? CancellationToken.None : token);
    }
    public async Task<SchedulerWorkspaceData> ReadAsync(SchedulerHistoryQuery query, CancellationToken token) {
        Reads++;
        if (ReadOverride is not null) {
            return await ReadOverride(query, token);
        }
        await WaitAsync(Writes > 0 && Hold == SchedulerWait.Readback ? SchedulerWait.Readback : SchedulerWait.Workspace, token);
        if (FailReads || Scenario == SchedulerScenario.ReadUnavailable) {
            throw new IOException("Scenario workspace read unavailable.");
        }
        var plans = Plans.Values.Select(Summary).OrderBy(plan => plan.Name).ToArray();
        var history = History.Where(run => (query.Status is null || query.Status == run.Status)
            && (query.TargetKind is null || query.TargetKind == run.TargetKind)
            && (query.FromUtc is null || run.FiredAtUtc >= query.FromUtc)
            && (query.ToUtc is null || run.FiredAtUtc <= query.ToUtc)
            && (query.Search.Length == 0 || run.PlanName.Contains(query.Search, StringComparison.OrdinalIgnoreCase)))
            .Take(Math.Clamp(query.Take, 1, 250)).ToArray();
        return new(plans, history, Targets(), Calendar(plans, history));
    }
    public async Task<SchedulerDraftValues> DefaultAsync(CancellationToken token) {
        await WaitAsync(SchedulerWait.Default, token);
        return DefaultValues() with { InputJson = Scenario == SchedulerScenario.MalformedJson ? "{broken" : "{}" };
    }
    public async Task<SchedulerDraftValues> EditorAsync(Guid id, CancellationToken token) {
        EditorReads++;
        await WaitAsync(SchedulerWait.Editor, token);
        return Plans.GetValueOrDefault(id) ?? throw new KeyNotFoundException("The exact scenario plan is missing.");
    }
    public async Task<SchedulerWorkflowInputSchema> SchemaAsync(SchedulerDraftValues values, CancellationToken token) {
        await WaitAsync(SchedulerWait.Schema, token);
        if (!Targets().Any(target => (target.Id, target.VersionId) == (values.TargetId, values.TargetVersionId))) {
            throw new KeyNotFoundException("The exact scenario Workflow version is unavailable.");
        }
        return new(new(values.TargetId), new(values.TargetVersionId!.Value), "Scenario input schema", Parameters(), false);
    }
    public async Task<IReadOnlyList<WorkflowInputParameterOption>> OptionsAsync(WorkflowInputParameterDescriptor parameter,
        IReadOnlyDictionary<string, string> values, CancellationToken token) {
        OptionReads++;
        if (OptionsOverride is not null) {
            return await OptionsOverride(parameter, values, token);
        }
        await WaitAsync(SchedulerWait.Options, token);
        return Scenario == SchedulerScenario.MissingOption ? [] : [new("project-a", "Scenario project A", "Fixture option")];
    }
    public async Task<SchedulerWorkflowInputValidationResult> ValidateAsync(SchedulerDraftValues values, CancellationToken token) {
        if (ValidationOverride is not null) {
            return await ValidationOverride(values, token);
        }
        await WaitAsync(SchedulerWait.Validation, token);
        return new(true, values.InputJson, []);
    }
    public async Task<SchedulerMutationReceipt> SaveAsync(SchedulerDraftValues values) {
        await WaitAsync(SchedulerWait.Authority);
        await WaitAsync(SchedulerWait.Save);
        Submitted.Add(values);
        if (Scenario == SchedulerScenario.RefusedSave) {
            return new(SchedulerMutationKind.Save, SchedulerMutationStatus.Refused, values.Id, SchedulerMutationStage.None, "Scenario refused the write before persistence.");
        }
        var saved = values with { Id = values.Id ?? Guid.NewGuid(), Name = values.Name.Trim() };
        if (saved.Id is not { } id || Plans.Count >= 100 && !Plans.ContainsKey(id)) {
            return new(SchedulerMutationKind.Save, SchedulerMutationStatus.Refused, values.Id, SchedulerMutationStage.None, "Scenario storage limit reached.");
        }
        Plans[id] = saved;
        Writes++;
        FailReads = Scenario == SchedulerScenario.ReadbackFailure;
        return Scenario switch {
            SchedulerScenario.UnknownSave => new(SchedulerMutationKind.Save, SchedulerMutationStatus.Unknown, values.Id, SchedulerMutationStage.None, "Scenario unknown outcome; replay remains locked."),
            SchedulerScenario.ProjectionFailure => new(SchedulerMutationKind.Save, SchedulerMutationStatus.CommittedWithWarning, id, SchedulerMutationStage.Persisted, "Scenario persisted; projection unavailable.", saved),
            _ => new(SchedulerMutationKind.Save, SchedulerMutationStatus.Committed, id, SchedulerMutationStage.ProjectionSynchronized, "Scenario storage updated; projection is fixture data.", saved)
        };
    }
    public async Task<SchedulerMutationReceipt> ToggleAsync(Guid id, bool enabled) {
        await WaitAsync(SchedulerWait.Save);
        Plans[id] = Plans[id] with { IsEnabled = enabled };
        Writes++;
        return new(enabled ? SchedulerMutationKind.Enable : SchedulerMutationKind.Disable, SchedulerMutationStatus.Committed,
            id, SchedulerMutationStage.ProjectionSynchronized, "Scenario enabled state updated.");
    }
    public async Task<SchedulerMutationReceipt> DeleteAsync(Guid id) {
        await WaitAsync(SchedulerWait.Save);
        Plans.Remove(id);
        History.RemoveAll(run => run.PlanId == id);
        Writes++;
        return new(SchedulerMutationKind.Delete, SchedulerMutationStatus.Committed, id, SchedulerMutationStage.ProjectionSynchronized,
            "Scenario plan and display history removed; admissions retained.");
    }
    public string DescribeCron(string expression, string timeZoneId) => $"Scenario description: {expression} ({timeZoneId}). Live Quartz behavior is validated separately.";
    private IReadOnlyList<SchedulerTargetOption> Targets() => [
        new(SchedulerPlanTargetKind.Workflow, WorkflowA, Scenario == SchedulerScenario.MissingVersion ? VersionB : VersionA, "Review workflow", "Harmless scenario target A", "Draft / InProcess"),
        new(SchedulerPlanTargetKind.Workflow, WorkflowB, VersionB, "Report workflow", "Harmless scenario target B", "Published / InProcess")
    ];
    public static SchedulerDraftValues DefaultValues() => new() { Name = "New review", TargetId = WorkflowA, TargetVersionId = VersionA, TimeZoneId = "UTC" };
    private static SchedulerPlanSummary Summary(SchedulerDraftValues values) => new(values.Id!.Value, values.Name, values.Description,
        values.TargetKind, values.TargetId, values.TargetVersionId, values.TargetId == WorkflowA ? "Review workflow" : "Report workflow",
        values.CronExpression, "Scenario: weekdays at 09:00", values.TimeZoneId, values.MisfirePolicy, values.IsEnabled,
        values.StartAtUtc, values.EndAtUtc, values.IsEnabled ? Now : null, Now.AddDays(-1), string.Empty, Now);
    private static CanvasCalendarSurface Calendar(IReadOnlyList<SchedulerPlanSummary> plans, IReadOnlyList<SchedulerPlanRunSummary> history) => new() {
        SurfaceId = "scheduler-scenario", SelectedDate = "2026-09-28", InitialView = "week", Timezone = "UTC",
        AllowCreate = false, AllowEdit = false, AllowDelete = false, AllowDragDrop = false, AllowResize = false,
        Events = plans.Where(plan => plan.IsEnabled).Take(80).Select((plan, index) => new CanvasCalendarEvent {
            Id = $"planned-{plan.Id:N}", EventId = $"planned-{plan.Id:N}", RepositoryId = plan.Id.ToString("D"),
            Title = plan.Name, StartUtc = Now.AddHours(index % 8), EndUtc = Now.AddHours(index % 8).AddMinutes(30),
            Timezone = "UTC", EventType = "planned", ReadOnly = false, Color = "#4f46e5"
        }).Concat(history.Take(80).Select(run => new CanvasCalendarEvent {
            Id = $"run-{run.Id:N}", EventId = $"run-{run.Id:N}", Title = run.PlanName, StartUtc = run.FiredAtUtc,
            EndUtc = run.FiredAtUtc.AddMinutes(30), ReadOnly = true, Timezone = "UTC", Status = run.Status.ToString(), EventType = "history"
        })).ToList()
    };
    private static TaskCompletionSource NewSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    private static IReadOnlyList<WorkflowInputParameterDescriptor> Parameters() => [
        new("project", "Project", WorkflowInputParameterKind.ProjectId, false, "Scenario project option", "$.project", string.Empty,
            new(WorkflowInputParameterOptionSourceKind.ProjectStructureProjects, string.Empty, []), null, null, ""),
        new("node", "Node", WorkflowInputParameterKind.ProjectNodeId, false, "Depends only on project", "$.node", string.Empty,
            new(WorkflowInputParameterOptionSourceKind.ProjectStructureNodes, "project", []), null, null, ""),
        new("note", "Note", WorkflowInputParameterKind.Text, false, "Unknown properties remain in the raw JSON", "$.note", string.Empty,
            WorkflowInputParameterOptionSource.None, null, null, "Text"),
        new("minutes", "Minutes", WorkflowInputParameterKind.Integer, false, "Incomplete numbers remain editable", "$.minutes", "10",
            WorkflowInputParameterOptionSource.None, 1, 120, "10")
    ];
}
