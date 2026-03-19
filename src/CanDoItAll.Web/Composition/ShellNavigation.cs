using CanDoItAll.SharedKernel;

namespace CanDoItAll.Web.Composition;

public static class ShellNavigation
{
    public static readonly IReadOnlyList<ShellNavigationItem> Items =
    [
        new("Dashboard", "/", "DB", "Operational summary, provider health, and recent work.", PinnedByDefault: true),
        new("Projects", "/projects", "PR", "Project setup, phases, stack profile, and delivery context.", PinnedByDefault: false),
        new("Resources", "/resources", "RS", "Typed resources, connectors, and validation status.", PinnedByDefault: false),
        new("Prompt Gallery", "/prompt-gallery", "PG", "Prompt library, collections, versions, and usage.", PinnedByDefault: false),
        new("Prompt Factory", "/prompt-factory", "PF", "Guided prompt assembly with flow templates and blueprints.", PinnedByDefault: false),
        new("Validation Center", "/validation", "VC", "Checklists, findings, review decisions, and coverage.", PinnedByDefault: false),
        new("Test Lab", "/test-lab", "TL", "Plans, evidence, linked tests, and execution records.", PinnedByDefault: false),
        new("Activity", "/activity", "AC", "Timeline and cross-entity search for recent work.", PinnedByDefault: false),
        new("Automation", "/automation", "AU", "Background jobs, exports, sends, and operational diagnostics.", PinnedByDefault: false),
        new("Settings", "/settings", "ST", "Workspace defaults, providers, secrets, and environment settings.", PinnedByDefault: true)
    ];

    public static ShellNavigationItem MatchRoute(string relativeRoute)
    {
        var normalized = string.IsNullOrWhiteSpace(relativeRoute) ? "/" : $"/{relativeRoute.TrimStart('/')}";
        return Items.FirstOrDefault(item => string.Equals(item.Route, normalized, StringComparison.OrdinalIgnoreCase))
            ?? Items[0];
    }
}
