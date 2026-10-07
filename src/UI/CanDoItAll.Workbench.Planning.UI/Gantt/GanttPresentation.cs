using CanDoItAll.Components.Gantt;

namespace CanDoItAll.Workbench.Planning.UI;

public sealed record GanttIntent<T>(Guid Origin, T Value);

public sealed record GanttPreview(Guid Origin, string ProjectName, string Source, int TaskCount, int DependencyCount);

public enum PlanningCommitState { Committed, Rejected, Unknown }

public sealed record GanttMutationReceipt(Guid Origin, Guid Operation, PlanningCommitState State, string Message, IReadOnlyList<GanttTaskId> TaskIds);

public sealed record GanttPresentation(Guid Origin) {
    public string ProjectName { get; init; } = string.Empty;
    public bool HasProjection { get; init; }
    public bool IsLoading { get; init; }
    public bool CanMutate { get; init; }
    public string? LoadError { get; init; }
    public IReadOnlyList<GanttTask> Tasks { get; init; } = [];
    public IReadOnlyList<GanttDependency> Dependencies { get; init; } = [];
    public IReadOnlySet<GanttTaskId> ProjectionOnlyTaskIds { get; init; } = new HashSet<GanttTaskId>();
    public IReadOnlySet<GanttTaskId> IntervalSynthesizedTaskIds { get; init; } = new HashSet<GanttTaskId>();
    public IReadOnlyList<string> Warnings { get; init; } = [];
    public IReadOnlyList<string> Errors { get; init; } = [];
    public IReadOnlyList<string> ExpectedCostTotals { get; init; } = [];
    public decimal? TotalExpectedEffortHours { get; init; }
    public GanttTask? InsertionCandidate { get; init; }
    public GanttPreview? Preview { get; init; }
    public GanttMutationReceipt? Receipt { get; init; }
    public bool IsValid => Errors.Count == 0;
}
