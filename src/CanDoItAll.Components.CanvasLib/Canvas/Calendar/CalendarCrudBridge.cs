namespace CanDoItAll.Components.CanvasLib;

public sealed class CalendarCrudBridgeOperation
{
    public string Label { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public string Tone { get; init; } = "ghost";

    public bool IsEnabled { get; init; }
}

public sealed class CalendarCrudBridgeSnapshot
{
    public string TestHookId { get; init; } = "calendar-crud-bridge";

    public string Label { get; init; } = "Calendar CRUD bridge";

    public string Title { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;

    public string StatePill { get; init; } = string.Empty;

    public IReadOnlyList<string> Metrics { get; init; } = [];

    public IReadOnlyList<CalendarCrudBridgeOperation> Operations { get; init; } = [];
}

public static class CalendarCrudBridgeFactory
{
    public static CalendarCrudBridgeSnapshot Create(CanvasCalendarSurface surface)
    {
        ArgumentNullException.ThrowIfNull(surface);

        var selectedEvent = CalendarBoundaryPreviewSupport.ResolveSelectedEvent(surface);
        var operations = new List<CalendarCrudBridgeOperation>
        {
            BuildOperation("Create / save", "Typed save payloads can create or update a calendar event.", surface.AllowCreate || surface.AllowEdit, "accent"),
            BuildOperation("Delete", "Delete requests stay isolated from selection and state callbacks.", surface.AllowDelete, "danger"),
            BuildOperation("Playlist search", "Playlist lookup keeps recommendation and reuse flows on the same seam.", selectedEvent is not null, "sky"),
            BuildOperation("Playlist mutations", "Link, clone, and unlink requests all share one calendar mutation contract.", selectedEvent is not null, "mint"),
            BuildOperation("Export", "Visible event exports travel through a typed request with range context.", surface.EnableListExport, "warn")
        };

        var enabledOperations = operations.Count(item => item.IsEnabled);
        return new CalendarCrudBridgeSnapshot
        {
            Title = "Calendar operations now resolve through one typed bridge",
            Summary = "Save, delete, playlist search, playlist mutation, and export no longer need to leak raw widget payload rules into page code.",
            StatePill = enabledOperations > 0 ? "Interactive" : "Read-only",
            Metrics =
            [
                $"{enabledOperations}/{operations.Count} operations enabled",
                $"{surface.Events.Count} calendar events",
                $"{surface.EventTypes.Count} event types",
                $"{surface.EventStatuses.Count} statuses"
            ],
            Operations = operations
        };
    }

    private static CalendarCrudBridgeOperation BuildOperation(string label, string description, bool enabled, string tone)
        => new()
        {
            Label = label,
            Description = description,
            Tone = tone,
            IsEnabled = enabled
        };
}


