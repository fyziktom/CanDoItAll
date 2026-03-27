using CanDoItAll.Components.CanvasLib;

namespace CanDoItAll.Modules.Workbench;

public static class ProjectCalendarAdapter
{
    public static CanvasCalendarSurface BuildSurface(ProjectCalendarSurface surface, ProjectCalendarViewState viewState)
        => new()
        {
            SurfaceId = $"project-calendar:{surface.ProjectId:N}",
            Events = surface.Events.Select(MapEvent).ToList(),
            InitialView = NormalizeView(viewState.View, surface.PreferredView),
            SelectedDate = viewState.SelectedDate,
            SelectedEventId = viewState.SelectedEventId?.ToString("D") ?? string.Empty,
            Timezone = string.IsNullOrWhiteSpace(viewState.Timezone) ? "UTC" : viewState.Timezone,
            Locale = "en-US",
            AllowCreate = false,
            AllowEdit = false,
            AllowDelete = false,
            AllowDragDrop = false,
            AllowResize = false,
            EnableListExport = true,
            WorkspaceModal = true,
            EventTypes = surface.Events
                .Select(item => item.ObjectType.ToString())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            EventStatuses = surface.Events
                .Select(item => item.Status)
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            ViewStateJson = surface.ViewStateJson
        };

    private static CanvasCalendarEvent MapEvent(ProjectCalendarEvent item)
        => new()
        {
            Id = item.Id.ToString("D"),
            EventId = item.Id.ToString("D"),
            Title = item.Title,
            Description = item.Route,
            StartUtc = item.StartUtc,
            EndUtc = item.EndUtc,
            Timezone = "UTC",
            TimezoneName = "UTC",
            LocationLabel = item.ArtifactKind,
            Category = item.ArtifactKind,
            Color = string.IsNullOrWhiteSpace(item.AccentColor) ? "#0f172a" : item.AccentColor,
            ReadOnly = true,
            EventType = item.ObjectType.ToString(),
            Status = item.Status,
            Notes = item.Route
        };

    private static string NormalizeView(string? view, string? fallback)
        => view?.Trim().ToLowerInvariant() switch
        {
            "day" => "day",
            "month" => "month",
            "list" => "list",
            "year" => "year",
            "week" => "week",
            _ => fallback?.Trim().ToLowerInvariant() switch
            {
                "day" => "day",
                "month" => "month",
                "list" => "list",
                "year" => "year",
                _ => "week"
            }
        };
}


