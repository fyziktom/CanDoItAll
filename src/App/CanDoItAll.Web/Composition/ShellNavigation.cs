using CanDoItAll.SharedKernel;
using System.Globalization;

namespace CanDoItAll.Web.Composition;

public static class ShellNavigation
{
    public static readonly IReadOnlyList<ShellNavigationItem> Items =
    [
        new("Dashboard", "/", "dashboard", "Operational summary, provider health, and recent work.", PinnedByDefault: true),
        new("Projects", "/projects", "folder_open", "Project setup, phases, stack profile, and delivery context.", PinnedByDefault: false),
        new("Collaboration", "/collaboration", "forum", "Human escalation, inbox, and process-scoped conversations.", PinnedByDefault: false),
        new("CRM / HR", "/crm-hr", "groups", "Unified party directory, CRM, workforce, recruiting, agents, and assignments.", PinnedByDefault: false),
        new("Agents", "/agents", "smart_toy", "Integrated AgentFramework foundation, imported tabs, and runtime governance.", PinnedByDefault: false),
        new("Resources", "/resources", "inventory_2", "Typed resources, connectors, and validation status.", PinnedByDefault: false),
        new("Plugins", "/plugins", "extension", "Plugin catalog, runtime packages, installation state, and availability.", PinnedByDefault: false),
        new("Prompt Gallery", "/prompt-gallery", "library_books", "Prompt library, collections, versions, and usage.", PinnedByDefault: false),
        new("Test Lab", "/test-lab", "science", "Plans, evidence, linked tests, and execution records.", PinnedByDefault: false),
        new("Scheduler", "/scheduler", "calendar_month", "Calendar-backed workflow and process run planning.", PinnedByDefault: false),
        new("Settings", "/settings", "settings", "Workspace defaults, providers, secrets, and environment settings.", PinnedByDefault: true)
    ];

    public static IReadOnlyList<ShellNavigationItem> GetItems(int collaborationUnreadCount)
        => GetItems(collaborationUnreadCount, []);

    public static IReadOnlyList<ShellNavigationItem> GetItems(
        int collaborationUnreadCount,
        IEnumerable<IShellNavigationContributor> contributors)
    {
        var items = BuildItems(contributors);
        if (collaborationUnreadCount <= 0)
        {
            return items;
        }

        var badgeText = collaborationUnreadCount > 99
            ? "99+"
            : collaborationUnreadCount.ToString(CultureInfo.InvariantCulture);

        return items
            .Select(item => string.Equals(item.Route, "/collaboration", StringComparison.OrdinalIgnoreCase)
                ? item with { BadgeText = badgeText }
                : item)
            .ToArray();
    }

    public static ShellNavigationItem MatchRoute(string relativeRoute)
        => MatchRoute(relativeRoute, []);

    public static ShellNavigationItem MatchRoute(
        string relativeRoute,
        IEnumerable<IShellNavigationContributor> contributors)
    {
        var normalized = string.IsNullOrWhiteSpace(relativeRoute) ? "/" : $"/{relativeRoute.TrimStart('/')}";
        var items = BuildItems(contributors);

        return items
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

    private static IReadOnlyList<ShellNavigationItem> BuildItems(IEnumerable<IShellNavigationContributor> contributors)
    {
        var contributions = contributors
            .SelectMany(contributor => contributor.GetShellNavigationContributions())
            .Where(contribution => !string.IsNullOrWhiteSpace(contribution.ParentRoute))
            .Where(contribution => !string.IsNullOrWhiteSpace(contribution.Item.Route))
            .OrderBy(contribution => contribution.Order)
            .ThenBy(contribution => contribution.Item.Title, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (contributions.Length == 0)
        {
            return Items;
        }

        var contributionsByParent = contributions
            .GroupBy(contribution => NormalizeRouteKey(contribution.ParentRoute), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.OrdinalIgnoreCase);

        var merged = new List<ShellNavigationItem>(Items.Count + contributions.Length);
        var visitedRoutes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in Items)
        {
            AddItemWithChildren(item);
        }

        foreach (var contribution in contributions)
        {
            AddItemWithChildren(contribution.Item);
        }

        return merged;

        void AddItemWithChildren(ShellNavigationItem item)
        {
            var routeKey = NormalizeRouteKey(item.Route);
            if (!visitedRoutes.Add(routeKey))
            {
                return;
            }

            merged.Add(item);

            if (!contributionsByParent.TryGetValue(routeKey, out var children))
            {
                return;
            }

            foreach (var child in children)
            {
                AddItemWithChildren(child.Item);
            }
        }
    }

    private static string NormalizeRouteKey(string route)
    {
        if (string.IsNullOrWhiteSpace(route))
        {
            return "/";
        }

        var normalized = route.Trim();
        if (string.Equals(normalized, "/", StringComparison.Ordinal))
        {
            return "/";
        }

        return $"/{normalized.Trim('/')}";
    }
}



