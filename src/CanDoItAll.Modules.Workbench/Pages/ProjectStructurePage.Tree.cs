using System.Text;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.Workbench.Pages;

public partial class ProjectStructurePage
{
    private IReadOnlyList<ProjectStructureNode> ResolveSubtreeRootNodes(
        IReadOnlyCollection<string> selectedIds,
        Func<ProjectStructureNode, bool>? predicate = null)
    {
        if (surface is null || selectedIds.Count == 0)
        {
            return [];
        }

        var normalizedIds = selectedIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .ToHashSet(StringComparer.Ordinal);
        var nodesById = surface.Nodes.ToDictionary(node => node.Id, StringComparer.Ordinal);
        var roots = new List<ProjectStructureNode>();

        foreach (var nodeId in normalizedIds)
        {
            if (!nodesById.TryGetValue(nodeId, out var node))
            {
                continue;
            }

            if (predicate is not null && !predicate(node))
            {
                continue;
            }

            var currentParentId = node.ParentId;
            var isCoveredBySelectedAncestor = false;
            while (!string.IsNullOrWhiteSpace(currentParentId) &&
                   nodesById.TryGetValue(currentParentId, out var parent))
            {
                if (normalizedIds.Contains(parent.Id))
                {
                    isCoveredBySelectedAncestor = true;
                    break;
                }

                currentParentId = parent.ParentId;
            }

            if (!isCoveredBySelectedAncestor)
            {
                roots.Add(node);
            }
        }

        return roots
            .OrderBy(node => node.Y)
            .ThenBy(node => node.X)
            .ThenBy(node => node.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private IReadOnlyList<(ProjectStructureNode Node, int Depth)> ResolveSubtreeEntries(
        string rootNodeId,
        bool includeRoot = true,
        Func<ProjectStructureNode, bool>? predicate = null)
    {
        if (surface is null)
        {
            return [];
        }

        var nodesById = surface.Nodes.ToDictionary(node => node.Id, StringComparer.Ordinal);
        if (!nodesById.TryGetValue(rootNodeId, out var rootNode))
        {
            return [];
        }

        var entries = new List<(ProjectStructureNode Node, int Depth)>();
        if (includeRoot && (predicate is null || predicate(rootNode)))
        {
            entries.Add((rootNode, 0));
        }

        AppendChildren(rootNodeId, includeRoot ? 1 : 0);
        return entries;

        void AppendChildren(string parentId, int depth)
        {
            var children = surface.Nodes
                .Where(node => string.Equals(node.ParentId, parentId, StringComparison.Ordinal))
                .OrderBy(node => node.Y)
                .ThenBy(node => node.X)
                .ThenBy(node => node.Title, StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var child in children)
            {
                if (predicate is null || predicate(child))
                {
                    entries.Add((child, depth));
                    AppendChildren(child.Id, depth + 1);
                    continue;
                }

                AppendChildren(child.Id, depth);
            }
        }
    }

    private IReadOnlyList<ProjectStructureNode> ResolveSubtreeNodes(
        string rootNodeId,
        bool includeRoot = true,
        Func<ProjectStructureNode, bool>? predicate = null)
        => ResolveSubtreeEntries(rootNodeId, includeRoot, predicate)
            .Select(entry => entry.Node)
            .ToList();

    private int CountSubtreeDescendants(string rootNodeId, Func<ProjectStructureNode, bool>? predicate = null)
        => ResolveSubtreeEntries(rootNodeId, includeRoot: false, predicate).Count;

    private bool IsMovableCanvasNode(ProjectStructureNode node)
        => node.ProjectRole != ProjectStructureProjectRole.AdditionalParentProject;

    private bool IsUserAuthoredCanvasNode(ProjectStructureNode node)
        => node.Id.StartsWith("custom:", StringComparison.Ordinal);

    private string BuildSubtreeIdCopyText(string rootNodeId)
    {
        var entries = ResolveSubtreeEntries(rootNodeId);
        if (entries.Count == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        foreach (var entry in entries)
        {
            if (builder.Length > 0)
            {
                builder.AppendLine();
            }

            builder.Append(' ', entry.Depth * 2);
            builder.Append(entry.Node.Id);
        }

        return builder.ToString();
    }
}
