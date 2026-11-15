using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.Projects.Pages.Components;

internal static class ProjectsPresentation
{
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
