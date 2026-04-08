namespace CanDoItAll.Components.CanvasLib;

public sealed class CanvasCalendarSurface
{
    public string SurfaceId { get; set; } = string.Empty;

    public List<CanvasCalendarEvent> Events { get; set; } = [];

    public string InitialView { get; set; } = "week";

    public string SelectedDate { get; set; } = string.Empty;

    public string SelectedEventId { get; set; } = string.Empty;

    public string Timezone { get; set; } = "UTC";

    public string Locale { get; set; } = "en-US";

    public int WeekStartsOn { get; set; } = 1;

    public int SlotMinutes { get; set; } = 30;

    public int BusinessHoursStart { get; set; } = 7;

    public int BusinessHoursEnd { get; set; } = 22;

    public int MiniMonthCount { get; set; } = 2;

    public bool AllowCreate { get; set; } = true;

    public bool AllowEdit { get; set; } = true;

    public bool AllowDelete { get; set; } = true;

    public bool AllowDragDrop { get; set; } = true;

    public bool AllowResize { get; set; } = true;

    public bool EnableListExport { get; set; } = true;

    public bool WorkspaceModal { get; set; } = true;

    public List<string> EventTypes { get; set; } = [];

    public List<string> EventStatuses { get; set; } = [];

    public List<string> TimeZoneOptions { get; set; } = [];

    public string? ViewStateJson { get; set; }
}

public sealed class CanvasCalendarEvent
{
    public string Id { get; set; } = string.Empty;

    public string EventId { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public DateTimeOffset? StartUtc { get; set; }

    public DateTimeOffset? EndUtc { get; set; }

    public bool AllDay { get; set; }

    public string Timezone { get; set; } = "UTC";

    public string TimezoneName { get; set; } = "UTC";

    public string Location { get; set; } = string.Empty;

    public string LocationLabel { get; set; } = string.Empty;

    public string LocationAddress { get; set; } = string.Empty;

    public double? LocationLat { get; set; }

    public double? LocationLng { get; set; }

    public string CustomerName { get; set; } = string.Empty;

    public string CustomerEmail { get; set; } = string.Empty;

    public string CustomerPhone { get; set; } = string.Empty;

    public decimal? PriceAmount { get; set; }

    public string Currency { get; set; } = "USD";

    public string Category { get; set; } = string.Empty;

    public string Color { get; set; } = "#4f46e5";

    public bool ReadOnly { get; set; }

    public string EventType { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public string Notes { get; set; } = string.Empty;

    public string LogisticsNote { get; set; } = string.Empty;

    public int LinkedPlaylistCount { get; set; }

    public List<CanvasCalendarPlaylist> LinkedPlaylists { get; set; } = [];

    public int ChecklistItemCount { get; set; }

    public List<CanvasCalendarChecklistRow> ChecklistRows { get; set; } = [];

    public string RepositoryId { get; set; } = string.Empty;

    public string CurrentCommitSha256 { get; set; } = string.Empty;

    public string PlaylistsBuilderUrl { get; set; } = string.Empty;

    public DateTimeOffset? CreatedUtc { get; set; }

    public DateTimeOffset? UpdatedUtc { get; set; }
}

public sealed class CanvasCalendarPlaylist
{
    public string PlaylistId { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Subtitle { get; set; } = string.Empty;

    public string Purpose { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public int TotalScores { get; set; }

    public int ConnectedEventCount { get; set; }

    public bool IsPrimaryEvent { get; set; }

    public string BuilderUrl { get; set; } = string.Empty;

    public List<CanvasCalendarConnectedEvent> ConnectedEvents { get; set; } = [];
}

public sealed class CanvasCalendarConnectedEvent
{
    public string EventId { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public DateTimeOffset? ScheduledStartUtc { get; set; }

    public string TimezoneName { get; set; } = "UTC";

    public string LocationLabel { get; set; } = string.Empty;

    public string LocationAddress { get; set; } = string.Empty;

    public string CustomerName { get; set; } = string.Empty;

    public bool IsPrimary { get; set; }

    public string EventUrl { get; set; } = string.Empty;
}

public sealed class CanvasCalendarChecklistRow
{
    public string Label { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public string Note { get; set; } = string.Empty;
}

public sealed class CanvasCalendarOperationContext
{
    public string View { get; set; } = "week";

    public string Scope { get; set; } = "week";

    public string SelectedDate { get; set; } = string.Empty;

    public string Timezone { get; set; } = "UTC";
}

public sealed record CanvasCalendarSaveRequest(
    CanvasCalendarEvent Event,
    CanvasCalendarOperationContext Context,
    string Mode);

public sealed record CanvasCalendarDeleteRequest(
    CanvasCalendarEvent Event,
    CanvasCalendarOperationContext Context);

public sealed record CanvasCalendarPlaylistSearchRequest(
    string Query,
    CanvasCalendarEvent? Event,
    CanvasCalendarOperationContext Context);

public sealed record CanvasCalendarPlaylistMutationRequest(
    CanvasCalendarEvent Event,
    CanvasCalendarPlaylist Playlist,
    CanvasCalendarOperationContext Context);

public sealed record CanvasCalendarSelectionChangedEventArgs(
    CanvasCalendarEvent? SelectedEvent,
    CanvasCalendarOperationContext Context);

public sealed record CanvasCalendarStateChangedEventArgs(
    string StateJson,
    string? SelectedEventId,
    string SelectedDate,
    string View,
    string Scope,
    string Timezone);

public sealed record CanvasCalendarExportRequest(
    string Format,
    IReadOnlyList<CanvasCalendarEvent> VisibleEvents,
    CanvasCalendarOperationContext Context);


