using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.Projects.Pages.Components;

internal static class ProjectsPresentation
{
    public static string GetProjectCardCss(ProjectStatus status)
    {
        var accent = status switch
        {
            ProjectStatus.Active => "border-emerald-200 bg-[linear-gradient(180deg,_rgba(236,253,245,0.96),_rgba(255,255,255,0.98))]",
            ProjectStatus.Completed => "border-sky-200 bg-[linear-gradient(180deg,_rgba(240,249,255,0.96),_rgba(255,255,255,0.98))]",
            ProjectStatus.OnHold => "border-amber-200 bg-[linear-gradient(180deg,_rgba(255,251,235,0.96),_rgba(255,255,255,0.98))]",
            ProjectStatus.Archived => "border-slate-300 bg-[linear-gradient(180deg,_rgba(248,250,252,0.96),_rgba(255,255,255,0.98))]",
            _ => "border-slate-200 bg-[linear-gradient(180deg,_rgba(248,250,252,0.96),_rgba(255,255,255,0.98))]"
        };

        return $"app-board-card {accent}";
    }

    public static string GetStatusTone(ProjectStatus status)
    {
        return status switch
        {
            ProjectStatus.Active => "success",
            ProjectStatus.Completed => "info",
            ProjectStatus.OnHold => "warning",
            _ => "neutral"
        };
    }

    public static string GetPhaseStatusTone(ProjectPhaseStatus status)
    {
        return status switch
        {
            ProjectPhaseStatus.Active => "success",
            ProjectPhaseStatus.Completed => "info",
            ProjectPhaseStatus.Blocked => "danger",
            _ => "neutral"
        };
    }

    public static string FormatDate(DateTime? value)
        => value.HasValue ? value.Value.ToString("MMM d, yyyy") : "Not set";

    public static string FormatTimestamp(DateTimeOffset? value)
        => value.HasValue ? value.Value.LocalDateTime.ToString("g") : "Not available";

    public static DateTime GetPhaseOrderKey(ProjectPhaseEditorModel phase)
        => phase.StartDateUtc ?? phase.EndDateUtc ?? DateTime.MaxValue;

    public static string GetProjectMonogram(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "PR";
        }

        var parts = name
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(part => part.Length > 0)
            .Take(2)
            .Select(part => char.ToUpperInvariant(part[0]))
            .ToArray();

        return parts.Length == 0 ? "PR" : new string(parts);
    }
}
