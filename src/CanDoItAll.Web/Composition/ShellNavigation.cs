using CanDoItAll.SharedKernel;
using System.Globalization;

namespace CanDoItAll.Web.Composition;

public static class ShellNavigation
{
    public static readonly IReadOnlyList<ShellNavigationItem> Items =
    [
        new("Dashboard", "/", "DB", "Operational summary, provider health, and recent work.", PinnedByDefault: true),
        new("Projects", "/projects", "PR", "Project setup, phases, stack profile, and delivery context.", PinnedByDefault: false),
        new("Processes", "/processes", "PM", "Role-first process definitions, runtime orchestration, evidence, and improvement signals.", PinnedByDefault: false),
        new("Live Processes", "/processes/live", "LP", "Live projection of running processes, active agents, metrics, and tool usage.", PinnedByDefault: false),
        new("Collaboration", "/collaboration", "CO", "Human escalation, inbox, and process-scoped conversations.", PinnedByDefault: false),
        new("CRM / HR", "/crm-hr", "CH", "Unified party directory, CRM, workforce, recruiting, agents, and assignments.", PinnedByDefault: false),
        new("Agents", "/agents", "AG", "Integrated AgentFramework foundation, imported tabs, and runtime governance.", PinnedByDefault: false),
        new("Resources", "/resources", "RS", "Typed resources, connectors, and validation status.", PinnedByDefault: false),
        new("Plugins", "/plugins", "PL", "Bundled plugin catalog, installation state, and availability.", PinnedByDefault: false),
        new("Prompt Gallery", "/prompt-gallery", "PG", "Prompt library, collections, versions, and usage.", PinnedByDefault: false),
        new("Prompt Factory", "/prompt-factory", "PF", "Guided prompt assembly with flow templates and blueprints.", PinnedByDefault: false),
        new("Validation Center", "/validation", "VC", "Checklists, findings, review decisions, and coverage.", PinnedByDefault: false),
        new("Test Lab", "/test-lab", "TL", "Plans, evidence, linked tests, and execution records.", PinnedByDefault: false),
        new("Activity", "/activity", "AC", "Timeline and cross-entity search for recent work.", PinnedByDefault: false),
        new("Automation", "/automation", "AU", "Background jobs, exports, sends, and operational diagnostics.", PinnedByDefault: false),
        new("Scheduler", "/scheduler", "SC", "Calendar-backed workflow and process run planning.", PinnedByDefault: false),
        new("Settings", "/settings", "ST", "Workspace defaults, providers, secrets, and environment settings.", PinnedByDefault: true)
    ];

    public static IReadOnlyList<ShellNavigationItem> GetItems(int collaborationUnreadCount)
    {
        if (collaborationUnreadCount <= 0)
        {
            return Items;
        }

        var badgeText = collaborationUnreadCount > 99
            ? "99+"
            : collaborationUnreadCount.ToString(CultureInfo.InvariantCulture);

        return Items
            .Select(item => string.Equals(item.Route, "/collaboration", StringComparison.OrdinalIgnoreCase)
                ? item with { BadgeText = badgeText }
                : item)
            .ToArray();
    }

    public static ShellNavigationItem MatchRoute(string relativeRoute)
    {
        var normalized = string.IsNullOrWhiteSpace(relativeRoute) ? "/" : $"/{relativeRoute.TrimStart('/')}";
        return Items
            .Where(item => IsRouteMatch(normalized, item.Route))
            .OrderByDescending(item => item.Route.Length)
            .FirstOrDefault()
            ?? Items[0];
    }

    public static bool IsRouteMatch(string currentRoute, string navigationRoute)
    {
        if (string.Equals(navigationRoute, "/", StringComparison.Ordinal))
        {
            return string.Equals(currentRoute, "/", StringComparison.OrdinalIgnoreCase);
        }

        return string.Equals(currentRoute, navigationRoute, StringComparison.OrdinalIgnoreCase) ||
               currentRoute.StartsWith($"{navigationRoute}/", StringComparison.OrdinalIgnoreCase);
    }
}


