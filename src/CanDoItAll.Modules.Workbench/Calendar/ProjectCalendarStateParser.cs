using System.Text.Json;
using CanDoItAll.Components.CanvasLib;

namespace CanDoItAll.Modules.Workbench;

public sealed record ProjectCalendarViewState(
    string View,
    string Scope,
    string SelectedDate,
    string Timezone,
    Guid? SelectedEventId)
{
    public static ProjectCalendarViewState Empty { get; } = new("week", "week", string.Empty, "UTC", null);
}

public static class ProjectCalendarStateParser
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public static ProjectCalendarViewState Parse(string? stateJson)
    {
        if (string.IsNullOrWhiteSpace(stateJson))
        {
            return ProjectCalendarViewState.Empty;
        }

        try
        {
            var persisted = JsonSerializer.Deserialize<PersistedState>(stateJson, SerializerOptions);
            return BuildState(
                persisted?.View,
                persisted?.PreferredView,
                persisted?.Scope,
                persisted?.SelectedDate,
                persisted?.SelectedDateKey,
                persisted?.AnchorDateKey,
                persisted?.Timezone,
                persisted?.SelectedEventId);
        }
        catch
        {
            return ProjectCalendarViewState.Empty;
        }
    }

    public static ProjectCalendarViewState FromStateChanged(CanvasCalendarStateChangedEventArgs args)
        => BuildState(
            args.View,
            null,
            args.Scope,
            args.SelectedDate,
            null,
            null,
            args.Timezone,
            args.SelectedEventId);

    private static ProjectCalendarViewState BuildState(
        string? view,
        string? preferredView,
        string? scope,
        string? selectedDate,
        string? selectedDateKey,
        string? anchorDateKey,
        string? timezone,
        string? selectedEventId)
        => new(
            NormalizeView(view ?? preferredView),
            string.IsNullOrWhiteSpace(scope) ? "week" : scope.Trim(),
            FirstNonEmpty(selectedDate, selectedDateKey, anchorDateKey),
            string.IsNullOrWhiteSpace(timezone) ? "UTC" : timezone.Trim(),
            Guid.TryParse(selectedEventId, out var parsed) ? parsed : null);

    private static string NormalizeView(string? view)
        => view?.Trim().ToLowerInvariant() switch
        {
            "day" => "day",
            "month" => "month",
            "list" => "list",
            "year" => "year",
            _ => "week"
        };

    private static string FirstNonEmpty(params string?[] values)
        => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;

    private sealed class PersistedState
    {
        public string? PreferredView { get; set; }

        public string? View { get; set; }

        public string? Scope { get; set; }

        public string? SelectedDate { get; set; }

        public string? SelectedDateKey { get; set; }

        public string? AnchorDateKey { get; set; }

        public string? Timezone { get; set; }

        public string? SelectedEventId { get; set; }
    }
}


