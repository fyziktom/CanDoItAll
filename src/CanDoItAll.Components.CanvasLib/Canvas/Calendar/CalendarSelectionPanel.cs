namespace CanDoItAll.Components.CanvasLib;

public sealed class CalendarSelectionPanelSnapshot
{
    public string TestHookId { get; init; } = "calendar-selection-panel";

    public string Label { get; init; } = "Calendar selection panel";

    public string Title { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;

    public string StatePill { get; init; } = string.Empty;

    public string EventTitle { get; init; } = string.Empty;

    public string EventSubtitle { get; init; } = string.Empty;

    public string TimeRangeLabel { get; init; } = string.Empty;

    public IReadOnlyList<string> Metrics { get; init; } = [];

    public IReadOnlyList<CanvasCalendarPlaylist> LinkedPlaylists { get; init; } = [];

    public IReadOnlyList<CanvasCalendarChecklistRow> ChecklistRows { get; init; } = [];
}

public static class CalendarSelectionPanelFactory
{
    public static CalendarSelectionPanelSnapshot Create(CanvasCalendarSurface surface)
    {
        ArgumentNullException.ThrowIfNull(surface);

        var selectedEvent = CalendarBoundaryPreviewSupport.ResolveSelectedEvent(surface);
        if (selectedEvent is null)
        {
            return new CalendarSelectionPanelSnapshot
            {
                Title = "Selection details now live in a dedicated side-panel boundary",
                Summary = "The panel is ready to render event context, playlists, checklists, and follow-up actions as soon as the calendar selects an item.",
                StatePill = "Waiting",
                EventTitle = "No event selected",
                EventSubtitle = "Choose a calendar item to inspect its context.",
                TimeRangeLabel = "Nothing selected",
                Metrics = ["0 linked playlists", "0 checklist rows", "Selection empty"]
            };
        }

        return new CalendarSelectionPanelSnapshot
        {
            Title = "Selection state now projects through a first-class side panel",
            Summary = "Selected-event details, linked playlists, and checklists are modeled separately from the calendar surface so supporting context can evolve without reopening the widget runtime.",
            StatePill = selectedEvent.Status,
            EventTitle = selectedEvent.Title,
            EventSubtitle = string.IsNullOrWhiteSpace(selectedEvent.LocationLabel) ? selectedEvent.EventType : selectedEvent.LocationLabel,
            TimeRangeLabel = $"{CalendarBoundaryPreviewSupport.FormatDateTime(selectedEvent.StartUtc)} to {CalendarBoundaryPreviewSupport.FormatDateTime(selectedEvent.EndUtc)}",
            Metrics =
            [
                $"{selectedEvent.LinkedPlaylists.Count} linked playlist(s)",
                $"{selectedEvent.ChecklistRows.Count} checklist row(s)",
                string.IsNullOrWhiteSpace(selectedEvent.CustomerName) ? "No customer attached" : selectedEvent.CustomerName,
                string.IsNullOrWhiteSpace(selectedEvent.Status) ? "No status" : selectedEvent.Status
            ],
            LinkedPlaylists = selectedEvent.LinkedPlaylists,
            ChecklistRows = selectedEvent.ChecklistRows
        };
    }
}


