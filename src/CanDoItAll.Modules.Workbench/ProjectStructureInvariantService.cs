using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.Workbench;

internal sealed class ProjectStructureInvariantService
{
    public void ValidateParentAssignment(
        Guid projectId,
        string nodeKey,
        string parentNodeKey,
        IReadOnlyCollection<ProjectObjectRecord> nodes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nodeKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(parentNodeKey);

        if (string.Equals(nodeKey, parentNodeKey, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("A node cannot be its own parent.");
        }

        if (IsProjectRootNodeKey(projectId, parentNodeKey))
        {
            return;
        }

        var nodesByKey = nodes.ToDictionary(item => item.NodeKey, StringComparer.Ordinal);
        if (!nodesByKey.ContainsKey(parentNodeKey))
        {
            throw new InvalidOperationException($"Parent node '{parentNodeKey}' was not found in project '{projectId}'.");
        }

        var visited = new HashSet<string>(StringComparer.Ordinal);
        var currentNodeKey = parentNodeKey;

        while (!string.IsNullOrWhiteSpace(currentNodeKey) && visited.Add(currentNodeKey))
        {
            if (string.Equals(currentNodeKey, nodeKey, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Reparenting would create a hierarchy cycle.");
            }

            if (IsProjectRootNodeKey(projectId, currentNodeKey))
            {
                return;
            }

            if (!nodesByKey.TryGetValue(currentNodeKey, out var currentNode))
            {
                return;
            }

            currentNodeKey = currentNode.ParentNodeKey?.Trim() ?? string.Empty;
        }
    }

    public void ValidateUserAuthoredLink(
        Guid projectId,
        string sourceNodeKey,
        string targetNodeKey,
        ProjectObjectLinkKind linkKind,
        IReadOnlyCollection<ProjectObjectRecord> nodes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceNodeKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetNodeKey);

        if (string.Equals(sourceNodeKey, targetNodeKey, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("A node cannot link to itself.");
        }

        if (linkKind is ProjectObjectLinkKind.Contains or ProjectObjectLinkKind.BelongsTo)
        {
            throw new InvalidOperationException("Hierarchy links must be created through the explicit parent relationship.");
        }

        var nodesByKey = nodes.ToDictionary(item => item.NodeKey, StringComparer.Ordinal);
        if (!nodesByKey.ContainsKey(sourceNodeKey))
        {
            throw new InvalidOperationException($"Source node '{sourceNodeKey}' was not found in project '{projectId}'.");
        }

        if (!nodesByKey.ContainsKey(targetNodeKey))
        {
            throw new InvalidOperationException($"Target node '{targetNodeKey}' was not found in project '{projectId}'.");
        }
    }

    private static bool IsProjectRootNodeKey(Guid projectId, string nodeKey)
    {
        return string.Equals(nodeKey, $"project:{projectId}", StringComparison.Ordinal);
    }
}
