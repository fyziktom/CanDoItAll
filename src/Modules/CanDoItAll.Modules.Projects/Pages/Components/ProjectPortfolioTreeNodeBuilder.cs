using CanDoItAll.Components.BaseLib;

namespace CanDoItAll.Modules.Projects.Pages.Components;

public static class ProjectPortfolioTreeNodeBuilder
{
    private const string ProjectNodePrefix = "project:";

    public static IReadOnlyList<TreeViewNode> Build(
        IReadOnlyList<ProjectSummary> projects,
        IReadOnlyList<ProjectHierarchyLinkSummary> hierarchyLinks,
        Guid? selectedProjectId,
        IReadOnlyDictionary<string, bool> expansionOverrides)
    {
        if (projects.Count == 0)
        {
            return [];
        }

        var projectsById = projects.ToDictionary(project => project.Id);
        var validLinks = hierarchyLinks
            .Where(link => projectsById.ContainsKey(link.ParentProjectId) && projectsById.ContainsKey(link.ChildProjectId))
            .ToArray();
        var childProjectIds = validLinks
            .Select(link => link.ChildProjectId)
            .ToHashSet();
        var childrenByParent = validLinks
            .GroupBy(link => link.ParentProjectId)
            .ToDictionary(
                group => group.Key,
                group => group
                    .Select(link => projectsById[link.ChildProjectId])
                    .OrderBy(project => project.Name, StringComparer.OrdinalIgnoreCase)
                    .ToArray());
        var roots = projects
            .Where(project => !childProjectIds.Contains(project.Id))
            .OrderBy(project => project.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (roots.Length == 0)
        {
            roots = projects
                .OrderBy(project => project.Name, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        return roots
            .Select(project => BuildProjectNode(
                project,
                childrenByParent,
                selectedProjectId,
                expansionOverrides,
                []))
            .ToArray();
    }

    public static string BuildProjectNodeId(Guid projectId)
        => $"{ProjectNodePrefix}{projectId:N}";

    public static bool TryReadProjectId(string nodeId, out Guid projectId)
    {
        projectId = Guid.Empty;
        return nodeId.StartsWith(ProjectNodePrefix, StringComparison.OrdinalIgnoreCase) &&
               Guid.TryParseExact(nodeId[ProjectNodePrefix.Length..], "N", out projectId);
    }

    private static TreeViewNode BuildProjectNode(
        ProjectSummary project,
        IReadOnlyDictionary<Guid, ProjectSummary[]> childrenByParent,
        Guid? selectedProjectId,
        IReadOnlyDictionary<string, bool> expansionOverrides,
        HashSet<Guid> path)
    {
        var nodeId = BuildProjectNodeId(project.Id);
        if (!path.Add(project.Id))
        {
            return new TreeViewNode
            {
                Id = nodeId,
                Text = project.Name,
                Icon = "warning",
                Tooltip = "Hierarchy cycle detected. Open project details to repair the relationship.",
                BadgeText = "cycle",
                IsSelected = selectedProjectId == project.Id,
                DataTestId = $"projects-tree-node-{project.Id:N}"
            };
        }

        var childNodes = childrenByParent.TryGetValue(project.Id, out var children)
            ? children
                .Select(child => BuildProjectNode(
                    child,
                    childrenByParent,
                    selectedProjectId,
                    expansionOverrides,
                    new HashSet<Guid>(path)))
                .ToArray()
            : [];
        var hasSelectedDescendant = childNodes.Any(ContainsSelectedNode);
        var isExpanded = expansionOverrides.TryGetValue(nodeId, out var expansionOverride)
            ? expansionOverride
            : path.Count == 1 || hasSelectedDescendant;

        return new TreeViewNode
        {
            Id = nodeId,
            Text = project.Name,
            Icon = childNodes.Length > 0 ? "account_tree" : "folder_open",
            Tooltip = BuildTooltip(project),
            Children = childNodes,
            IsExpanded = childNodes.Length > 0 && isExpanded,
            IsSelected = selectedProjectId == project.Id,
            BadgeText = BuildBadge(project),
            DataTestId = $"projects-tree-node-{project.Id:N}",
            ChildrenDataTestId = $"projects-tree-children-{project.Id:N}"
        };
    }

    private static bool ContainsSelectedNode(TreeViewNode node)
    {
        return node.IsSelected || node.Children.Any(ContainsSelectedNode);
    }

    private static string BuildTooltip(ProjectSummary project)
    {
        var phase = string.IsNullOrWhiteSpace(project.CurrentPhase)
            ? "No current phase"
            : project.CurrentPhase;

        return $"{project.Name} - {project.Status}, {phase}";
    }

    private static string BuildBadge(ProjectSummary project)
    {
        if (project.ChildCount > 0)
        {
            return $"{project.ChildCount} sub";
        }

        if (project.ParentCount > 0)
        {
            return $"{project.ParentCount} parent";
        }

        return project.Status.ToString();
    }
}
