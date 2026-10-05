using CanDoItAll.Components.Gantt;
using CanDoItAll.Workbench.Planning.UI;

namespace CanDoItAll.Workbench.Planning.UiSandbox;

public sealed class GanttScenario {
    public GanttPresentation Presentation { get; private set; }
    public string? LastAction { get; private set; }

    public GanttScenario(string name) {
        var start = new DateTimeOffset(2026, 10, 5, 8, 0, 0, TimeSpan.Zero);
        Presentation = new(Guid.NewGuid()) {
            ProjectName = name,
            HasProjection = true,
            CanMutate = true,
            Tasks = [
                new(new("design"), "Synthetic design", start, start.AddHours(8), [new(GanttAssignmentKind.Person, "Synthetic designer")]) { ProgressPercent = 25, ExpectedEffort = TimeSpan.FromHours(4) },
                new(new("delivery"), "Synthetic delivery", start.AddHours(8), start.AddHours(16)) { ExpectedEffort = TimeSpan.FromHours(6) },
                new(new("review"), "Synthetic review", start.AddHours(16), start.AddHours(20))
            ],
            Dependencies = [new(new("design-delivery"), new("design"), new("delivery")), new(new("delivery-review"), new("delivery"), new("review"))],
            ProjectionOnlyTaskIds = new HashSet<GanttTaskId> { new("review") },
            IntervalSynthesizedTaskIds = new HashSet<GanttTaskId> { new("review") },
            InsertionCandidate = new(new("insert"), "Synthetic inserted task", start, start.AddHours(8)),
            TotalExpectedEffortHours = 10,
            ExpectedCostTotals = ["USD 120 expected", "EUR 80 expected"]
        };
    }

    public static GanttDependencyId DependencyId(GanttTaskId predecessor, GanttTaskId successor) => new(Guid.NewGuid().ToString("N"));

    public void Title(GanttIntent<GanttTaskTitleChangeRequest> intent) {
        if (!Accept(intent.Origin)) {
            return;
        }
        Presentation = Presentation with { Tasks = Presentation.Tasks.Select(task => task.Id == intent.Value.TaskId
            ? Copy(task, title: intent.Value.ProposedTitle) : task).ToArray() };
        LastAction = "Synthetic title accepted";
    }

    public void Schedule(GanttIntent<GanttTaskScheduleChangeRequest> intent) {
        if (Accept(intent.Origin)) {
            ChangeDates(intent.Value.AffectedTasks);
            LastAction = "Synthetic schedule accepted";
        }
    }

    public void Dependency(GanttIntent<GanttDependencyMutationRequest> intent) {
        if (Accept(intent.Origin)) {
            ChangeDependency(intent.Value);
            ChangeDates(intent.Value.AffectedTasks);
            LastAction = "Synthetic dependency accepted";
        }
    }

    public void Insert(GanttIntent<GanttTaskInsertionRequest> intent) {
        if (!Accept(intent.Origin)) {
            return;
        }
        Presentation = Presentation with { Tasks = [.. Presentation.Tasks, intent.Value.InsertedTask] };
        foreach (var change in intent.Value.DependencyChanges) {
            ChangeDependency(change);
        }
        ChangeDates(intent.Value.AffectedTasks);
        var candidate = Presentation.InsertionCandidate!;
        Presentation = Presentation with { InsertionCandidate = new(new(Guid.NewGuid().ToString("N")), candidate.Title, candidate.Start, candidate.End) };
        LastAction = "Synthetic insertion accepted";
    }

    public void Order(GanttIntent<GanttTaskOrderChangeRequest> intent) {
        if (!Accept(intent.Origin)) {
            return;
        }
        var rows = Presentation.Tasks.ToList();
        var task = rows.Single(row => row.Id == intent.Value.TaskId);
        rows.Remove(task);
        var index = rows.FindIndex(row => row.Id == intent.Value.AnchorTaskId);
        rows.Insert(index + (intent.Value.Placement == GanttTaskOrderPlacement.After ? 1 : 0), task);
        Presentation = Presentation with { Tasks = rows.ToArray() };
        LastAction = "Synthetic row order accepted";
    }

    public void Export(PlanningOriginIntent intent) {
        if (intent.Origin == Presentation.Origin) {
            Presentation = Presentation with { Preview = new(Presentation.Origin, Presentation.ProjectName,
                PlanningGanttMermaidExporter.Build(Presentation.ProjectName, Presentation), Presentation.Tasks.Count, Presentation.Dependencies.Count) };
        }
    }

    public void Close(PlanningOriginIntent intent) {
        if (intent.Origin == Presentation.Origin) {
            Presentation = Presentation with { Preview = null };
        }
    }

    public void Select(GanttIntent<GanttTaskId> intent) {
        if (Accept(intent.Origin)) {
            LastAction = $"Synthetic task details: {intent.Value.Value}";
        }
    }

    public void Timeline(GanttIntent<GanttTimelineDoubleClickEventArgs> intent) {
        if (Accept(intent.Origin)) {
            LastAction = $"Synthetic create below {intent.Value.RowTaskId.Value} at {intent.Value.ClickedAtUtc:O}";
        }
    }

    public void SetState(PlanningReadState state) {
        Presentation = Presentation with {
            IsLoading = state == PlanningReadState.Loading,
            CanMutate = state == PlanningReadState.Ready,
            LoadError = state == PlanningReadState.Failed ? "Synthetic projection read failed" : null,
            Warnings = state == PlanningReadState.Stale ? ["Synthetic stale schedule. Reload before saving."] : []
        };
    }

    private bool Accept(Guid origin) => origin == Presentation.Origin && Presentation.CanMutate && !Presentation.IsLoading;

    private void ChangeDates(IReadOnlyList<GanttTaskDateChange> changes) {
        var dates = changes.ToDictionary(change => change.TaskId);
        Presentation = Presentation with { Tasks = Presentation.Tasks.Select(task => dates.TryGetValue(task.Id, out var change)
            ? Copy(task, start: change.ProposedStart, end: change.ProposedEnd) : task).ToArray() };
    }

    private void ChangeDependency(GanttDependencyMutationRequest change) {
        var dependencies = Presentation.Dependencies.Where(item => item.Id != change.PreviousDependency?.Id).ToList();
        if (change.ProposedDependency is not null) {
            dependencies.Add(change.ProposedDependency);
        }
        Presentation = Presentation with { Dependencies = dependencies.ToArray() };
    }

    private static GanttTask Copy(GanttTask task, string? title = null, DateTimeOffset? start = null, DateTimeOffset? end = null)
        => new(task.Id, title ?? task.Title, start ?? task.Start, end ?? task.End, task.Assignments) {
            ProgressPercent = task.ProgressPercent,
            ExpectedEffort = task.ExpectedEffort
        };
}
