namespace CanDoItAll.Components.CanvasLib;

public sealed class CalendarEventEditorField
{
    public string Label { get; init; } = string.Empty;

    public string Value { get; init; } = string.Empty;
}

public sealed class CalendarEventEditorModalSnapshot
{
    public string TestHookId { get; init; } = "calendar-event-editor-modal";

    public string Label { get; init; } = "Calendar event editor modal";

    public string Title { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;

    public string StatePill { get; init; } = string.Empty;

    public string ModeLabel { get; init; } = string.Empty;

    public IReadOnlyList<string> Metrics { get; init; } = [];

    public IReadOnlyList<CalendarEventEditorField> Fields { get; init; } = [];

    public IReadOnlyList<string> ValidationMessages { get; init; } = [];
}

public static class CalendarEventEditorModalFactory
{
    public static CalendarEventEditorModalSnapshot Create(CanvasCalendarSurface surface)
    {
        ArgumentNullException.ThrowIfNull(surface);

        var selectedEvent = CalendarBoundaryPreviewSupport.ResolveSelectedEvent(surface) ?? new CanvasCalendarEvent
        {
            Title = "New project milestone",
            StartUtc = DateTimeOffset.UtcNow,
            EndUtc = DateTimeOffset.UtcNow.AddHours(1),
            Status = "Draft"
        };

        var fields = new List<CalendarEventEditorField>
        {
            new() { Label = "Title", Value = selectedEvent.Title },
            new() { Label = "Status", Value = selectedEvent.Status },
            new() { Label = "Starts", Value = CalendarBoundaryPreviewSupport.FormatDateTime(selectedEvent.StartUtc) },
            new() { Label = "Ends", Value = CalendarBoundaryPreviewSupport.FormatDateTime(selectedEvent.EndUtc) },
            new() { Label = "Location", Value = selectedEvent.LocationLabel },
            new() { Label = "Notes", Value = string.IsNullOrWhiteSpace(selectedEvent.Notes) ? "No editor notes yet." : selectedEvent.Notes }
        };

        var validationMessages = new List<string>();
        if (string.IsNullOrWhiteSpace(selectedEvent.Title))
        {
            validationMessages.Add("Title is required before the event can be saved.");
        }

        if (selectedEvent.StartUtc is null)
        {
            validationMessages.Add("Start time is required.");
        }

        if (selectedEvent.EndUtc is null)
        {
            validationMessages.Add("End time is required.");
        }
        else if (selectedEvent.StartUtc is not null && selectedEvent.EndUtc < selectedEvent.StartUtc)
        {
            validationMessages.Add("End time must be later than the start time.");
        }

        return new CalendarEventEditorModalSnapshot
        {
            Title = "Calendar event editing now has a named validation and save surface",
            Summary = "The editor boundary owns required-field checks, timing coherence, playlist context, and delete eligibility instead of burying that behavior inside the widget runtime.",
            StatePill = validationMessages.Count == 0 ? "Ready" : "Needs input",
            ModeLabel = surface.AllowEdit ? "Edit event" : "Preview event",
            Metrics =
            [
                $"{fields.Count} visible fields",
                $"{validationMessages.Count} validation checks",
                $"{selectedEvent.LinkedPlaylists.Count} linked playlists",
                $"{selectedEvent.ChecklistRows.Count} checklist rows"
            ],
            Fields = fields,
            ValidationMessages = validationMessages
        };
    }
}


