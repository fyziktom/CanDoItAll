using CanDoItAll.Components.CanvasLib;

namespace CanDoItAll.Workbench.Planning.UI;

public enum PlanningReadState { Loading, Ready, Failed, Unavailable, Stale }

public sealed record CalendarEventDetail(
    Guid Id,
    string Title,
    string Kind,
    string Status,
    string Start,
    string End,
    string Timezone,
    string Route);

public sealed record CalendarPresentation(Guid Origin) {
    public PlanningReadState State { get; init; } = PlanningReadState.Loading;
    public string ProjectName { get; init; } = string.Empty;
    public CanvasCalendarSurface? Calendar { get; init; }
    public CalendarEventDetail? SelectedEvent { get; init; }
    public int LinkedArtifactCount { get; init; }
    public int StatusCount { get; init; }
    public string? Error { get; init; }
    public string? PersistenceWarning { get; init; }
}

public readonly record struct CalendarSelectionIntent(Guid Origin, Guid? EventId);
public readonly record struct CalendarStateIntent(Guid Origin, CanvasCalendarStateChangedEventArgs State);
public readonly record struct CalendarOpenIntent(Guid Origin, Guid EventId);
public readonly record struct PlanningOriginIntent(Guid Origin);
