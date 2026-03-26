namespace CanDoItAll.Components.CanvasLib;

public sealed class CalendarTimeGridBlock
{
    public string Label { get; init; } = string.Empty;

    public string Subtitle { get; init; } = string.Empty;

    public string Color { get; init; } = "#0f172a";

    public double TopPercent { get; init; }

    public double HeightPercent { get; init; }
}

public sealed class CalendarTimeGridRendererSnapshot
{
    public string TestHookId { get; init; } = "calendar-time-grid-renderer";

    public string Label { get; init; } = "Calendar time grid renderer";

    public string Title { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;

    public string StatePill { get; init; } = string.Empty;

    public string RangeLabel { get; init; } = string.Empty;

    public IReadOnlyList<string> Metrics { get; init; } = [];

    public IReadOnlyList<string> SlotLabels { get; init; } = [];

    public IReadOnlyList<CalendarTimeGridBlock> Blocks { get; init; } = [];
}

public static class CalendarTimeGridRendererFactory
{
    public static CalendarTimeGridRendererSnapshot Create(CanvasCalendarSurface surface)
    {
        ArgumentNullException.ThrowIfNull(surface);

        var anchorDate = CalendarBoundaryPreviewSupport.ResolveAnchorDate(surface);
        var businessStart = Math.Clamp(surface.BusinessHoursStart, 0, 23);
        var businessEnd = Math.Clamp(Math.Max(surface.BusinessHoursEnd, businessStart + 1), businessStart + 1, 24);
        var totalMinutes = (businessEnd - businessStart) * 60d;

        var slotLabels = Enumerable.Range(businessStart, businessEnd - businessStart + 1)
            .Select(hour => $"{hour:00}:00")
            .ToList();

        var blocks = surface.Events
            .Where(item => item.StartUtc is not null && item.EndUtc is not null)
            .Where(item => DateOnly.FromDateTime(item.StartUtc!.Value.UtcDateTime) == anchorDate)
            .OrderBy(item => item.StartUtc)
            .Select(item =>
            {
                var startMinutes = (item.StartUtc!.Value.UtcDateTime.Hour * 60d) + item.StartUtc.Value.UtcDateTime.Minute;
                var endMinutes = (item.EndUtc!.Value.UtcDateTime.Hour * 60d) + item.EndUtc.Value.UtcDateTime.Minute;
                var clampedStart = Math.Clamp(startMinutes - (businessStart * 60d), 0d, totalMinutes);
                var clampedEnd = Math.Clamp(endMinutes - (businessStart * 60d), clampedStart + 30d, totalMinutes);
                return new CalendarTimeGridBlock
                {
                    Label = item.Title,
                    Subtitle = $"{CalendarBoundaryPreviewSupport.FormatTime(item.StartUtc)} to {CalendarBoundaryPreviewSupport.FormatTime(item.EndUtc)}",
                    Color = string.IsNullOrWhiteSpace(item.Color) ? "#0f172a" : item.Color,
                    TopPercent = totalMinutes <= 0 ? 0 : (clampedStart / totalMinutes) * 100d,
                    HeightPercent = totalMinutes <= 0 ? 8 : Math.Max(((clampedEnd - clampedStart) / totalMinutes) * 100d, 8d)
                };
            })
            .ToList();

        return new CalendarTimeGridRendererSnapshot
        {
            Title = "Timed day and week views now have a dedicated renderer boundary",
            Summary = "Time-slot density, business-hour framing, and event-block layout can now be validated as an explicit subsystem instead of disappearing into the monolithic calendar runtime.",
            StatePill = surface.InitialView,
            RangeLabel = $"{CalendarBoundaryPreviewSupport.FormatDate(anchorDate)} - {businessStart:00}:00 to {businessEnd:00}:00",
            Metrics =
            [
                $"{blocks.Count} visible block(s)",
                $"{surface.SlotMinutes} minute slots",
                $"{businessEnd - businessStart} business hours",
                $"{surface.Events.Count} surface event(s)"
            ],
            SlotLabels = slotLabels,
            Blocks = blocks
        };
    }
}


