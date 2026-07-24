using System.Diagnostics.CodeAnalysis;

namespace CanDoItAll.Modules.CrmHr;

public enum CrmHrWorkspaceArea
{
    Home,
    Directory,
    Crm,
    Workforce,
    Recruiting,
    Agents,
    Assignments
}

public sealed record CrmHrRouteDefinition(
    CrmHrWorkspaceArea Area,
    string Key,
    string Route,
    string TabLabel,
    string WorkbenchTitle,
    string Description);

public static class CrmHrRouteCatalog
{
    public static readonly IReadOnlyList<CrmHrRouteDefinition> Items =
    [
        new(
            CrmHrWorkspaceArea.Home,
            "home",
            "/crm-hr",
            "Home",
            "CRM / HR",
            "Module summary, routing hub, and at-a-glance counts."),
        new(
            CrmHrWorkspaceArea.Directory,
            "directory",
            "/crm-hr/directory",
            "Directory",
            "Directory",
            "Unified party directory for people, organizations, units, and AI agents."),
        new(
            CrmHrWorkspaceArea.Crm,
            "crm",
            "/crm-hr/crm",
            "CRM",
            "CRM",
            "Account and opportunity workspace."),
        new(
            CrmHrWorkspaceArea.Workforce,
            "workforce",
            "/crm-hr/workforce",
            "Workforce",
            "Workforce",
            "People, units, and staffing supply overview."),
        new(
            CrmHrWorkspaceArea.Recruiting,
            "recruiting",
            "/crm-hr/recruiting",
            "Recruiting",
            "Recruiting",
            "Candidate and onboarding workspace."),
        new(
            CrmHrWorkspaceArea.Agents,
            "agents",
            "/crm-hr/agents",
            "Agents",
            "CRM Agents",
            "AI agent directory and governance workspace."),
        new(
            CrmHrWorkspaceArea.Assignments,
            "assignments",
            "/crm-hr/assignments",
            "Assignments",
            "Assignments",
            "Project-linked staffing and party assignment workspace.")
    ];

    public static CrmHrRouteDefinition Get(CrmHrWorkspaceArea area)
        => Items.FirstOrDefault(item => item.Area == area)
           ?? throw new ArgumentOutOfRangeException(nameof(area), area, "Unknown CRM / HR workspace area.");

    public static bool TryGetByKey(
        string key,
        [NotNullWhen(true)] out CrmHrRouteDefinition? definition)
    {
        definition = Items.FirstOrDefault(item => string.Equals(item.Key, key, StringComparison.Ordinal));
        return definition is not null;
    }

    public static bool TryResolve(
        string path,
        [NotNullWhen(true)] out CrmHrRouteDefinition? definition)
    {
        var normalizedPath = NormalizePath(path);
        definition = null;

        foreach (var candidate in Items)
        {
            if (!IsRouteMatch(normalizedPath, candidate.Route) ||
                definition is not null && definition.Route.Length >= candidate.Route.Length)
            {
                continue;
            }

            definition = candidate;
        }

        return definition is not null;
    }

    private static bool IsRouteMatch(string path, string route)
        => string.Equals(path, route, StringComparison.OrdinalIgnoreCase) ||
           path.StartsWith($"{route}/", StringComparison.OrdinalIgnoreCase);

    private static string NormalizePath(string path)
    {
        var trimmed = path.Trim();
        var suffixIndex = trimmed.IndexOfAny('?', '#');
        if (suffixIndex >= 0)
        {
            trimmed = trimmed[..suffixIndex];
        }

        return string.IsNullOrWhiteSpace(trimmed) || string.Equals(trimmed, "/", StringComparison.Ordinal)
            ? "/"
            : $"/{trimmed.Trim('/')}";
    }
}
