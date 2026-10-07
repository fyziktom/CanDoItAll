using CanDoItAll.Components.CanvasLib;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Workbench.Planning.UI;

namespace CanDoItAll.Workbench.Planning.UiSandbox;

public sealed class CalendarScenario {
    private readonly Guid origin = Guid.NewGuid();
    private readonly Guid firstId = Guid.Parse("97e598a7-558f-4343-9c76-6358d26f41c9");
    private readonly Guid secondId = Guid.Parse("2b18566d-e2da-49b3-811e-10b053da85cf");
    private ProjectCalendarViewState view = new("week", "week", "2026-10-05", "UTC", null);
    public PlanningReadState State { get; set; } = PlanningReadState.Ready;
    public string Name { get; }
    public string? LastAction { get; private set; }
    public CalendarScenario(string name) => Name = name;

    public CalendarPresentation Presentation {
        get {
            var events = new List<CanvasCalendarEvent> {
                new() { Id = firstId.ToString("D"), EventId = firstId.ToString("D"), Title = "Synthetic delivery review", StartUtc = new(2026, 10, 5, 14, 0, 0, TimeSpan.Zero), EndUtc = new(2026, 10, 5, 16, 0, 0, TimeSpan.Zero), ReadOnly = true, Status = "Planned", EventType = "Task", Color = "#2563eb", Timezone = "UTC" },
                new() { Id = secondId.ToString("D"), EventId = secondId.ToString("D"), Title = "Synthetic DST handoff", StartUtc = new(2026, 11, 1, 5, 30, 0, TimeSpan.Zero), EndUtc = new(2026, 11, 1, 6, 30, 0, TimeSpan.Zero), ReadOnly = true, Status = "Review", EventType = "Milestone", Color = "#0f766e", Timezone = "UTC" }
            };
            var selected = events.FirstOrDefault(item => item.EventId == view.SelectedEventId?.ToString("D"));
            return new(origin) {
                State = State, ProjectName = Name, StatusCount = 2, LinkedArtifactCount = 2,
                Error = State is PlanningReadState.Failed or PlanningReadState.Unavailable ? "Synthetic unavailable schedule." : null,
                Calendar = new() {
                    SurfaceId = $"synthetic-calendar:{origin:N}", Events = events, InitialView = view.View,
                    SelectedDate = view.SelectedDate, SelectedEventId = view.SelectedEventId?.ToString("D") ?? string.Empty,
                    Timezone = view.Timezone, TimeZoneOptions = ["UTC", "America/New_York", "Asia/Kathmandu"],
                    AllowCreate = false, AllowEdit = false, AllowDelete = false, AllowDragDrop = false, AllowResize = false,
                    EnableListExport = true, ViewStateJson = ProjectCalendarStateParser.Serialize(view, "{}")
                },
                SelectedEvent = selected is null ? null : new(Guid.Parse(selected.EventId), selected.Title, selected.EventType, selected.Status,
                    CalendarTimeDisplay.Format(selected.StartUtc!.Value, view.Timezone), CalendarTimeDisplay.Format(selected.EndUtc!.Value, view.Timezone), view.Timezone, "/synthetic-artifact")
            };
        }
    }

    public void Select(CalendarSelectionIntent intent) {
        if (intent.Origin == origin && (intent.EventId is null || intent.EventId == firstId || intent.EventId == secondId)) {
            view = view with { SelectedEventId = intent.EventId };
        }
    }
    public void ChangeState(CalendarStateIntent intent) {
        if (intent.Origin == origin) {
            view = ProjectCalendarStateParser.FromStateChanged(intent.State);
        }
    }
    public void Open(CalendarOpenIntent intent) {
        if (intent.Origin == origin) {
            LastAction = $"Synthetic artifact intent: {intent.EventId:D}";
        }
    }
    public void Retry(PlanningOriginIntent intent) {
        if (intent.Origin == origin) {
            State = PlanningReadState.Ready;
        }
    }
}
