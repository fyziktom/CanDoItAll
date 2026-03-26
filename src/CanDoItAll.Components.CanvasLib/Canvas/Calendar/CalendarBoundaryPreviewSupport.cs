using System.Globalization;

namespace CanDoItAll.Components.CanvasLib;

internal static class CalendarBoundaryPreviewSupport
{
    public static CanvasCalendarEvent? ResolveSelectedEvent(CanvasCalendarSurface surface)
    {
        ArgumentNullException.ThrowIfNull(surface);

        if (!string.IsNullOrWhiteSpace(surface.SelectedEventId))
        {
            var selected = surface.Events.FirstOrDefault(item =>
                string.Equals(item.EventId, surface.SelectedEventId, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(item.Id, surface.SelectedEventId, StringComparison.OrdinalIgnoreCase));
            if (selected is not null)
            {
                return selected;
            }
        }

        return surface.Events
            .OrderBy(item => item.StartUtc ?? DateTimeOffset.MaxValue)
            .FirstOrDefault();
    }

    public static DateOnly ResolveAnchorDate(CanvasCalendarSurface surface)
    {
        ArgumentNullException.ThrowIfNull(surface);

        if (DateOnly.TryParse(surface.SelectedDate, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDate))
        {
            return parsedDate;
        }

        var selectedEvent = ResolveSelectedEvent(surface);
        if (selectedEvent?.StartUtc is DateTimeOffset startUtc)
        {
            return DateOnly.FromDateTime(startUtc.UtcDateTime);
        }

        return DateOnly.FromDateTime(DateTime.UtcNow);
    }

    public static string FormatDateTime(DateTimeOffset? value)
        => value?.ToUniversalTime().ToString("ddd, MMM d yyyy HH:mm 'UTC'", CultureInfo.InvariantCulture) ?? "Not set";

    public static string FormatDate(DateOnly value)
        => value.ToString("ddd, MMM d yyyy", CultureInfo.InvariantCulture);

    public static string FormatTime(DateTimeOffset? value)
        => value?.ToUniversalTime().ToString("HH:mm", CultureInfo.InvariantCulture) ?? "--:--";
}


