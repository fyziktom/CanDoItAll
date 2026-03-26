using System.Globalization;

namespace CanDoItAll.Components.CanvasLib;

public sealed class CalendarMiniMonthDay
{
    public string DayLabel { get; init; } = string.Empty;

    public bool IsCurrentMonth { get; init; }

    public bool IsSelected { get; init; }

    public bool HasEvents { get; init; }
}

public sealed class CalendarMiniMonthNavigatorSnapshot
{
    public string TestHookId { get; init; } = "calendar-mini-month-navigator";

    public string Label { get; init; } = "Calendar mini month navigator";

    public string Title { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;

    public string StatePill { get; init; } = string.Empty;

    public string MonthLabel { get; init; } = string.Empty;

    public IReadOnlyList<string> Metrics { get; init; } = [];

    public IReadOnlyList<CalendarMiniMonthDay> Days { get; init; } = [];
}

public static class CalendarMiniMonthNavigatorFactory
{
    public static CalendarMiniMonthNavigatorSnapshot Create(CanvasCalendarSurface surface)
    {
        ArgumentNullException.ThrowIfNull(surface);

        var anchorDate = CalendarBoundaryPreviewSupport.ResolveAnchorDate(surface);
        var monthStart = new DateOnly(anchorDate.Year, anchorDate.Month, 1);
        var offset = ((int)monthStart.DayOfWeek - surface.WeekStartsOn + 7) % 7;
        var gridStart = monthStart.AddDays(-offset);
        var eventDays = surface.Events
            .Where(item => item.StartUtc is not null)
            .Select(item => DateOnly.FromDateTime(item.StartUtc!.Value.UtcDateTime))
            .ToHashSet();

        var days = Enumerable.Range(0, 42)
            .Select(index =>
            {
                var date = gridStart.AddDays(index);
                return new CalendarMiniMonthDay
                {
                    DayLabel = date.Day.ToString(CultureInfo.InvariantCulture),
                    IsCurrentMonth = date.Month == monthStart.Month,
                    IsSelected = date == anchorDate,
                    HasEvents = eventDays.Contains(date)
                };
            })
            .ToList();

        return new CalendarMiniMonthNavigatorSnapshot
        {
            Title = "Month navigation now has a named jump and scope component",
            Summary = "The mini-month boundary keeps date jumping, eventful-day hints, and selected-range context explicit instead of hiding those rules inside the calendar widget shell.",
            StatePill = $"{surface.InitialView} view",
            MonthLabel = monthStart.ToString("MMMM yyyy", CultureInfo.InvariantCulture),
            Metrics =
            [
                $"{eventDays.Count} eventful day(s)",
                $"{surface.MiniMonthCount} mini month panel(s)",
                $"{CalendarBoundaryPreviewSupport.FormatDate(anchorDate)} selected",
                $"Week starts on {surface.WeekStartsOn}"
            ],
            Days = days
        };
    }
}


