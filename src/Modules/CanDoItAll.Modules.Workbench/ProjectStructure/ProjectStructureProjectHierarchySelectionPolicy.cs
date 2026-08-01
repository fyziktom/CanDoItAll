using CanDoItAll.Modules.Projects;

namespace CanDoItAll.Modules.Workbench;

internal static class ProjectStructureProjectHierarchySelectionPolicy
{
    public static bool CanAttachProjectAsSubproject(
        Guid parentProjectId,
        Guid candidateChildProjectId,
        IReadOnlyList<ProjectHierarchyLinkSummary> hierarchyLinks)
    {
        ArgumentNullException.ThrowIfNull(hierarchyLinks);

        if (candidateChildProjectId == parentProjectId)
        {
            return false;
        }

        if (hierarchyLinks.Any(link =>
                link.ParentProjectId == parentProjectId &&
                link.ChildProjectId == candidateChildProjectId))
        {
            return false;
        }

        return !IsProjectReachable(
            parentProjectId,
            candidateChildProjectId,
            hierarchyLinks,
            static (link, currentProjectId) =>
                link.ChildProjectId == currentProjectId
                    ? link.ParentProjectId
                    : null);
    }

    public static bool CanReconnectProjectToParent(
        Guid childProjectId,
        Guid candidateParentProjectId,
        Guid? currentParentProjectId,
        IReadOnlyList<ProjectHierarchyLinkSummary> hierarchyLinks)
    {
        ArgumentNullException.ThrowIfNull(hierarchyLinks);

        if (candidateParentProjectId == childProjectId)
        {
            return false;
        }

        if (currentParentProjectId.HasValue &&
            candidateParentProjectId == currentParentProjectId.Value)
        {
            return false;
        }

        return !IsProjectReachable(
            childProjectId,
            candidateParentProjectId,
            hierarchyLinks,
            static (link, currentProjectId) =>
                link.ParentProjectId == currentProjectId
                    ? link.ChildProjectId
                    : null);
    }

    private static bool IsProjectReachable(
        Guid startProjectId,
        Guid targetProjectId,
        IReadOnlyList<ProjectHierarchyLinkSummary> hierarchyLinks,
        Func<ProjectHierarchyLinkSummary, Guid, Guid?> resolveRelatedProjectId)
    {
        var visited = new HashSet<Guid>();
        var pending = new Queue<Guid>();
        pending.Enqueue(startProjectId);

        while (pending.Count > 0)
        {
            var currentProjectId = pending.Dequeue();
            if (!visited.Add(currentProjectId))
            {
                continue;
            }

            foreach (var link in hierarchyLinks)
            {
                var relatedProjectId = resolveRelatedProjectId(link, currentProjectId);
                if (!relatedProjectId.HasValue)
                {
                    continue;
                }

                if (relatedProjectId.Value == targetProjectId)
                {
                    return true;
                }

                pending.Enqueue(relatedProjectId.Value);
            }
        }

        return false;
    }
}
