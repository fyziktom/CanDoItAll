using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.Workbench;

public sealed class ProjectStructureChecklistService(ProjectWorkbenchService projectWorkbenchService)
{
    public async Task<ProjectStructureChecklistResponse> GetChecklistAsync(
        Guid projectId,
        ProjectStructureChecklistRequest request,
        CancellationToken cancellationToken = default)
    {
        var surface = await projectWorkbenchService.GetStructureAsync(projectId, cancellationToken);
        var warnings = new List<string>();
        var dependencyAnalysis = ProjectStructureDependencyAnalyzer.Build(surface);

        var items = dependencyAnalysis.Nodes
            .Where(item => item.Node.ObjectType != ProjectObjectType.ProjectRoot)
            .Where(item => !item.IsFinished)
            .Where(item => request.IncludePaused || !item.IsPausedOrStopped)
            .Where(item => request.ObjectTypes is null || request.ObjectTypes.Count == 0 || request.ObjectTypes.Contains(item.Node.ObjectType))
            .Select(MapItem)
            .Where(item => !request.MaxPriority.HasValue || item.EffectivePriority > 0 && item.EffectivePriority <= request.MaxPriority.Value)
            .OrderBy(item => item.EffectivePriority == 0 ? int.MaxValue : item.EffectivePriority)
            .ThenBy(item => item.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (request.Take.HasValue && items.Count > request.Take.Value)
        {
            items = items.Take(Math.Max(1, request.Take.Value)).ToList();
            warnings.Add($"Checklist truncated to {request.Take.Value} items.");
        }

        return new ProjectStructureChecklistResponse(surface.ProjectId, surface.ProjectName, items, warnings);
    }

    private static ProjectStructureChecklistItem MapItem(ProjectStructureDependencyNodeAnalysis analysis)
    {
        return new ProjectStructureChecklistItem(
            analysis.Node.Id,
            analysis.Node.ParentId,
            analysis.Node.ObjectType,
            analysis.Node.ObjectSubtype,
            analysis.Node.Title,
            analysis.Node.Status,
            analysis.Node.ProgressMode,
            analysis.Node.ProgressPercent,
            analysis.Node.MarkerLabel,
            analysis.Node.Priority,
            analysis.EffectivePriority,
            analysis.Node.Route,
            analysis.Prerequisites
                .Where(item => !item.IsFinished)
                .Select(item => new ProjectStructureChecklistPrerequisite(
                    item.NodeId,
                    item.Title,
                    item.Status,
                    item.EffectivePriority,
                    item.Reason))
                .ToList());
    }
}

internal static class ProjectStructureChecklistRules
{
    public static IReadOnlyDictionary<string, int> BuildEffectivePriorityMap(IReadOnlyList<ProjectStructureNode> nodes)
    {
        var nodesById = nodes.ToDictionary(node => node.Id, StringComparer.Ordinal);
        var childrenByParent = nodes
            .Where(node => !string.IsNullOrWhiteSpace(node.ParentId))
            .GroupBy(node => node.ParentId!, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Select(item => item.Id).ToList(),
                StringComparer.Ordinal);
        var memo = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var node in nodes)
        {
            Compute(node.Id, nodesById, childrenByParent, memo);
        }

        return memo;
    }

    public static bool IsFinished(ProjectStructureNode node)
    {
        if (string.Equals(node.ProgressMode, "complete", StringComparison.OrdinalIgnoreCase) ||
            node.ProgressPercent >= 100)
        {
            return true;
        }

        var status = node.Status?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(status))
        {
            return false;
        }

        return status.Contains("done", StringComparison.OrdinalIgnoreCase) ||
               status.Contains("complete", StringComparison.OrdinalIgnoreCase) ||
               status.Contains("approved", StringComparison.OrdinalIgnoreCase) ||
               status.Contains("ready", StringComparison.OrdinalIgnoreCase) ||
               status.Contains("final", StringComparison.OrdinalIgnoreCase) ||
               status.Contains("archived", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsPausedOrStopped(ProjectStructureNode node)
    {
        var status = node.Status?.Trim() ?? string.Empty;
        var markerLabel = node.MarkerLabel?.Trim() ?? string.Empty;

        return status.Contains("paused", StringComparison.OrdinalIgnoreCase) ||
               status.Contains("on hold", StringComparison.OrdinalIgnoreCase) ||
               status.Contains("onhold", StringComparison.OrdinalIgnoreCase) ||
               status.Contains("stopped", StringComparison.OrdinalIgnoreCase) ||
               status.Contains("cancel", StringComparison.OrdinalIgnoreCase) ||
               markerLabel.Contains("pause", StringComparison.OrdinalIgnoreCase) ||
               markerLabel.Contains("hold", StringComparison.OrdinalIgnoreCase) ||
               markerLabel.Contains("stop", StringComparison.OrdinalIgnoreCase) ||
               markerLabel.Contains("wait", StringComparison.OrdinalIgnoreCase);
    }

    public static bool BlocksPriorityPropagation(ProjectStructureNode node)
    {
        return IsFinished(node) || IsPausedOrStopped(node);
    }

    private static int Compute(
        string nodeId,
        IReadOnlyDictionary<string, ProjectStructureNode> nodesById,
        IReadOnlyDictionary<string, List<string>> childrenByParent,
        IDictionary<string, int> memo)
    {
        if (memo.TryGetValue(nodeId, out var cached))
        {
            return cached;
        }

        if (!nodesById.TryGetValue(nodeId, out var node))
        {
            memo[nodeId] = 0;
            return 0;
        }

        var effectivePriority = Math.Clamp(node.Priority, 0, 6);
        if (!BlocksPriorityPropagation(node) &&
            childrenByParent.TryGetValue(nodeId, out var childIds))
        {
            foreach (var childId in childIds)
            {
                var childPriority = Compute(childId, nodesById, childrenByParent, memo);
                if (childPriority <= 0)
                {
                    continue;
                }

                effectivePriority = effectivePriority == 0
                    ? childPriority
                    : Math.Min(effectivePriority, childPriority);
            }
        }

        memo[nodeId] = effectivePriority;
        return effectivePriority;
    }
}
