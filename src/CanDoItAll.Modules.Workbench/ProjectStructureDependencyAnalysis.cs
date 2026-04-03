using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.Workbench;

internal sealed record ProjectStructureDependencyRelation(
    string NodeId,
    string Title,
    string Status,
    int EffectivePriority,
    bool IsFinished,
    string Reason);

internal sealed record ProjectStructureDependencyNodeAnalysis(
    ProjectStructureNode Node,
    int EffectivePriority,
    bool IsFinished,
    bool IsPausedOrStopped,
    bool CanExecute,
    int? DurationSeconds,
    int EffectiveDurationSeconds,
    IReadOnlyList<ProjectStructureDependencyRelation> Prerequisites,
    IReadOnlyList<ProjectStructureDependencyRelation> Dependents,
    IReadOnlyList<string> ExplicitDependencyIds);

internal sealed record ProjectStructureDependencyAnalysis(
    ProjectStructureSurface Surface,
    int DefaultDurationSeconds,
    IReadOnlyList<ProjectStructureDependencyNodeAnalysis> Nodes)
{
    public ProjectStructureDependencyNodeAnalysis? FindNode(string nodeId)
        => Nodes.FirstOrDefault(item => string.Equals(item.Node.Id, nodeId, StringComparison.Ordinal));
}

internal static class ProjectStructureDependencyAnalyzer
{
    public static ProjectStructureDependencyAnalysis Build(ProjectStructureSurface surface, int defaultDurationSeconds = 3600)
    {
        ArgumentNullException.ThrowIfNull(surface);

        var effectiveDefaultDurationSeconds = defaultDurationSeconds > 0 ? defaultDurationSeconds : 3600;
        var nodesById = surface.Nodes.ToDictionary(node => node.Id, StringComparer.Ordinal);
        var effectivePriorities = ProjectStructureChecklistRules.BuildEffectivePriorityMap(surface.Nodes);
        var analyses = surface.Nodes
            .Select(node => BuildNodeAnalysis(node, nodesById, surface.Links, effectivePriorities, effectiveDefaultDurationSeconds))
            .ToList();

        return new ProjectStructureDependencyAnalysis(surface, effectiveDefaultDurationSeconds, analyses);
    }

    private static ProjectStructureDependencyNodeAnalysis BuildNodeAnalysis(
        ProjectStructureNode node,
        IReadOnlyDictionary<string, ProjectStructureNode> nodesById,
        IReadOnlyList<ProjectStructureLink> links,
        IReadOnlyDictionary<string, int> effectivePriorities,
        int defaultDurationSeconds)
    {
        var prerequisites = BuildPrerequisites(node, nodesById, links, effectivePriorities);
        var dependents = BuildDependents(node, nodesById, links, effectivePriorities);
        var explicitDependencyIds = prerequisites
            .Where(item => item.Reason is "depends-on" or "blocked-by")
            .Select(item => item.NodeId)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var isFinished = ProjectStructureChecklistRules.IsFinished(node);
        var isPausedOrStopped = ProjectStructureChecklistRules.IsPausedOrStopped(node);
        var storedDurationSeconds = ResolveStoredDurationSeconds(node);

        return new ProjectStructureDependencyNodeAnalysis(
            node,
            effectivePriorities.GetValueOrDefault(node.Id),
            isFinished,
            isPausedOrStopped,
            !isFinished && !isPausedOrStopped && prerequisites.All(item => item.IsFinished),
            storedDurationSeconds,
            storedDurationSeconds ?? defaultDurationSeconds,
            prerequisites,
            dependents,
            explicitDependencyIds);
    }

    private static IReadOnlyList<ProjectStructureDependencyRelation> BuildPrerequisites(
        ProjectStructureNode node,
        IReadOnlyDictionary<string, ProjectStructureNode> nodesById,
        IReadOnlyList<ProjectStructureLink> links,
        IReadOnlyDictionary<string, int> effectivePriorities)
    {
        var prerequisites = new List<ProjectStructureDependencyRelation>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var currentParentId = node.ParentId;

        while (!string.IsNullOrWhiteSpace(currentParentId) &&
               nodesById.TryGetValue(currentParentId, out var parent) &&
               seen.Add(parent.Id))
        {
            if (parent.ObjectType == ProjectObjectType.ProjectRoot)
            {
                currentParentId = parent.ParentId;
                continue;
            }

            prerequisites.Add(CreateRelation(parent, "parent", effectivePriorities));
            currentParentId = parent.ParentId;
        }

        foreach (var link in links)
        {
            if (link.Kind == ProjectObjectLinkKind.DependsOn &&
                string.Equals(link.SourceId, node.Id, StringComparison.Ordinal) &&
                nodesById.TryGetValue(link.TargetId, out var dependsOnNode) &&
                seen.Add(dependsOnNode.Id))
            {
                prerequisites.Add(CreateRelation(dependsOnNode, "depends-on", effectivePriorities));
            }

            if (link.Kind == ProjectObjectLinkKind.Blocks &&
                string.Equals(link.TargetId, node.Id, StringComparison.Ordinal) &&
                nodesById.TryGetValue(link.SourceId, out var blockingNode) &&
                seen.Add(blockingNode.Id))
            {
                prerequisites.Add(CreateRelation(blockingNode, "blocked-by", effectivePriorities));
            }
        }

        return prerequisites;
    }

    private static IReadOnlyList<ProjectStructureDependencyRelation> BuildDependents(
        ProjectStructureNode node,
        IReadOnlyDictionary<string, ProjectStructureNode> nodesById,
        IReadOnlyList<ProjectStructureLink> links,
        IReadOnlyDictionary<string, int> effectivePriorities)
    {
        var dependents = new List<ProjectStructureDependencyRelation>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var link in links)
        {
            if (link.Kind == ProjectObjectLinkKind.DependsOn &&
                string.Equals(link.TargetId, node.Id, StringComparison.Ordinal) &&
                nodesById.TryGetValue(link.SourceId, out var dependentNode) &&
                seen.Add(dependentNode.Id))
            {
                dependents.Add(CreateRelation(dependentNode, "required-for", effectivePriorities));
            }

            if (link.Kind == ProjectObjectLinkKind.Blocks &&
                string.Equals(link.SourceId, node.Id, StringComparison.Ordinal) &&
                nodesById.TryGetValue(link.TargetId, out var blockedNode) &&
                seen.Add(blockedNode.Id))
            {
                dependents.Add(CreateRelation(blockedNode, "blocks", effectivePriorities));
            }
        }

        return dependents;
    }

    private static ProjectStructureDependencyRelation CreateRelation(
        ProjectStructureNode node,
        string reason,
        IReadOnlyDictionary<string, int> effectivePriorities)
    {
        return new ProjectStructureDependencyRelation(
            node.Id,
            node.Title,
            node.Status,
            effectivePriorities.GetValueOrDefault(node.Id),
            ProjectStructureChecklistRules.IsFinished(node),
            reason);
    }

    private static int? ResolveStoredDurationSeconds(ProjectStructureNode node)
    {
        if (node.DurationSeconds.HasValue)
        {
            return node.DurationSeconds.Value > 0 ? node.DurationSeconds.Value : null;
        }

        if (!node.StartUtc.HasValue || !node.EndUtc.HasValue)
        {
            return null;
        }

        var totalSeconds = Math.Abs((node.EndUtc.Value - node.StartUtc.Value).TotalSeconds);
        return Math.Max(1, (int)Math.Round(totalSeconds, MidpointRounding.AwayFromZero));
    }
}
