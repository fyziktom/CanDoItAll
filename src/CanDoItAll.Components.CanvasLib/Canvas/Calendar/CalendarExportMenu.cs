namespace CanDoItAll.Components.CanvasLib;

public sealed class CalendarExportFormatOption
{
    public string Label { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public bool IsPrimary { get; init; }
}

public sealed class CalendarExportMenuSnapshot
{
    public string TestHookId { get; init; } = "calendar-export-menu";

    public string Label { get; init; } = "Calendar export menu";

    public string Title { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;

    public string StatePill { get; init; } = string.Empty;

    public IReadOnlyList<string> Metrics { get; init; } = [];

    public IReadOnlyList<CalendarExportFormatOption> Formats { get; init; } = [];
}

public static class CalendarExportMenuFactory
{
    public static CalendarExportMenuSnapshot Create(CanvasCalendarSurface surface)
    {
        ArgumentNullException.ThrowIfNull(surface);

        var formats = surface.EnableListExport
            ? new List<CalendarExportFormatOption>
            {
                new() { Label = "Agenda JSON", Description = "Serialize the visible events with full route and status metadata.", IsPrimary = true },
                new() { Label = "Calendar ICS", Description = "Emit a calendar-friendly handoff for external planning tools." },
                new() { Label = "Briefing Markdown", Description = "Export a readable summary for handoff and reporting." }
            }
            : new List<CalendarExportFormatOption>
            {
                new() { Label = "Export disabled", Description = "This surface is currently read-only for list export." }
            };

        return new CalendarExportMenuSnapshot
        {
            Title = "Visible ranges can now export through a dedicated menu boundary",
            Summary = "Export options stay explicit about format and scope, while the typed request keeps the current view, date, and timezone attached to the handoff.",
            StatePill = surface.EnableListExport ? "Export ready" : "Disabled",
            Metrics =
            [
                $"{formats.Count} format options",
                $"{surface.Events.Count} visible events",
                $"{surface.InitialView} view",
                string.IsNullOrWhiteSpace(surface.Timezone) ? "UTC timezone" : surface.Timezone
            ],
            Formats = formats
        };
    }
}


