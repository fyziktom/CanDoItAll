using CanDoItAll.SharedKernel;

namespace CanDoItAll.Web.Composition;

public static class ShellNavigation
{
    public static readonly IReadOnlyList<ShellNavigationItem> Items =
    [
        new("Dashboard", "/", "DB", "Operational summary, provider health, and recent work."),
        new("Projects", "/projects", "PR", "Project setup, phases, stack profile, and delivery context."),
        new("Resources", "/resources", "RS", "Typed resources, connectors, and validation status."),
        new("Prompt Gallery", "/prompt-gallery", "PG", "Prompt library, collections, versions, and usage."),
        new("Prompt Factory", "/prompt-factory", "PF", "Guided prompt assembly with flow templates and blueprints."),
        new("Validation Center", "/validation", "VC", "Checklists, findings, review decisions, and coverage."),
        new("Test Lab", "/test-lab", "TL", "Plans, evidence, linked tests, and execution records."),
        new("Activity", "/activity", "AC", "Timeline and cross-entity search for recent work."),
        new("Automation", "/automation", "AU", "Background jobs, exports, sends, and operational diagnostics."),
        new("Settings", "/settings", "ST", "Workspace defaults, providers, secrets, and environment settings.")
    ];

    public static ShellNavigationItem MatchRoute(string relativeRoute)
    {
        var normalized = string.IsNullOrWhiteSpace(relativeRoute) ? "/" : $"/{relativeRoute.TrimStart('/')}";
        return Items.FirstOrDefault(item => string.Equals(item.Route, normalized, StringComparison.OrdinalIgnoreCase))
            ?? Items[0];
    }
}
