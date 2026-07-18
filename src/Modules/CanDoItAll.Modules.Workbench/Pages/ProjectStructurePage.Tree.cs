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
            .ToList();
        var nodesById = surface.Nodes.ToDictionary(node => node.Id, StringComparer.Ordinal);
        var candidateNodes = normalizedIds
            .Select(nodeId => nodesById.GetValueOrDefault(nodeId))
            .Where(node => node is not null && (predicate is null || predicate(node)))
            .Cast<ProjectStructureNode>()
            .ToList();
        var candidateIds = candidateNodes
            .Select(node => node.Id)
            .ToHashSet(StringComparer.Ordinal);
        var roots = new List<ProjectStructureNode>();

        foreach (var node in candidateNodes)
        {
            var currentParentId = node.ParentId;
            var isCoveredBySelectedAncestor = false;
            while (!string.IsNullOrWhiteSpace(currentParentId) &&
                   nodesById.TryGetValue(currentParentId, out var parent))
            {
                if (candidateIds.Contains(parent.Id))
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

    private string BuildNodeInfoCopyText(ProjectStructureNode node)
    {
        var nodeType = ResolveClipboardNodeTypeToken(node);
        var nodeTitle = SanitizeClipboardSegment(node.Title, preserveCase: true, fallback: "Untitled");
        var nodeHash = ExtractClipboardNodeHash(node.Id);
        return $"{nodeType}_{nodeTitle}:{nodeHash}";
    }

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
            builder.Append(BuildNodeInfoCopyText(entry.Node));
        }

        return builder.ToString();
    }

    private static string ResolveClipboardNodeTypeToken(ProjectStructureNode node)
    {
        var label = SanitizeClipboardSegment(
            ProjectStructureCanvasCatalog.ResolveNodeLabel(node),
            preserveCase: false,
            fallback: node.ObjectType.ToString().ToLowerInvariant());

        return label.EndsWith("-block", StringComparison.Ordinal)
            ? label[..^"-block".Length]
            : label;
    }

    private static string ExtractClipboardNodeHash(string nodeId)
    {
        if (string.IsNullOrWhiteSpace(nodeId))
        {
            return "node";
        }

        var separatorIndex = nodeId.IndexOf(':', StringComparison.Ordinal);
        return separatorIndex >= 0 && separatorIndex < nodeId.Length - 1
            ? nodeId[(separatorIndex + 1)..]
            : nodeId;
    }

    private static string SanitizeClipboardSegment(string? value, bool preserveCase, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        var builder = new StringBuilder();
        var justWroteDash = false;
        foreach (var character in value.Trim())
        {
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(preserveCase ? character : char.ToLowerInvariant(character));
                justWroteDash = false;
                continue;
            }

            if (justWroteDash)
            {
                continue;
            }

            builder.Append('-');
            justWroteDash = true;
        }

        var sanitized = builder.ToString().Trim('-');
        return string.IsNullOrWhiteSpace(sanitized)
            ? fallback
            : sanitized;
    }
}
