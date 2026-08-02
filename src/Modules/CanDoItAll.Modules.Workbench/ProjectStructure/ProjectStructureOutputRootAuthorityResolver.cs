using CanDoItAll.AgentFramework.Models;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.Workbench;

internal static class ProjectStructureOutputRootAuthorityResolver
{
    private const int MinimumImplicitRootSegmentCount = 1;
    private const string ExternalTargetAliasRoot = "external-target";

    private static readonly HashSet<string> ProtectedDriveFolders = new(
        [
            "$Recycle.Bin",
            "Documents and Settings",
            "PerfLogs",
            "Program Files",
            "Program Files (x86)",
            "ProgramData",
            "Recovery",
            "System Volume Information",
            "Windows"
        ],
        StringComparer.OrdinalIgnoreCase);

    private static readonly IReadOnlyList<ProtectedRoot> ProtectedRoots =
        BuildProtectedRoots();

    public static string ResolveProcessOutputRoot(
        ProjectStructureSurface? surface,
        ProjectStructureNode? focusNode)
    {
        if (focusNode is null)
        {
            return string.Empty;
        }

        if (surface is null)
        {
            return ResolveFirstRootFromTypedProjectBlock(focusNode);
        }

        var currentFocusNodes = surface.Nodes
            .Where(node => string.Equals(node.Id, focusNode.Id, StringComparison.Ordinal))
            .Take(2)
            .ToArray();
        if (currentFocusNodes.Length != 1)
        {
            return string.Empty;
        }

        var currentFocusNode = currentFocusNodes[0];
        var direct = ResolveFirstRootFromTypedProjectBlock(currentFocusNode);
        if (!string.IsNullOrWhiteSpace(direct))
        {
            return direct;
        }

        foreach (var node in EnumerateAuthorityNodes(surface, currentFocusNode))
        {
            var candidate = ResolveFirstRootFromTypedProjectBlock(node);
            if (!string.IsNullOrWhiteSpace(candidate))
            {
                return candidate;
            }
        }

        return string.Empty;
    }

    public static IReadOnlyList<string> ResolveChatDiscoveryRoots(
        ProjectStructureSurface? surface,
        IReadOnlyList<AgentChatContextEntityReference>? selectedEntities)
    {
        if (surface is null || selectedEntities is null || selectedEntities.Count == 0)
        {
            return [];
        }

        var nodeGroups = surface.Nodes
            .Where(node => !string.IsNullOrWhiteSpace(node.Id))
            .GroupBy(node => node.Id, StringComparer.Ordinal)
            .ToArray();
        var nodesById = nodeGroups
            .Where(group => group.Count() == 1)
            .ToDictionary(group => group.Key, group => group.Single(), StringComparer.Ordinal);
        var ambiguousNodeIds = nodeGroups
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToHashSet(StringComparer.Ordinal);
        var resolvedAliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var selectedNodeId in selectedEntities
                     .Where(entity =>
                         entity is not null &&
                         string.Equals(entity.Kind, "project-node", StringComparison.Ordinal))
                     .Select(entity => entity.Id?.Trim())
                     .Where(static id => !string.IsNullOrWhiteSpace(id))
                     .Cast<string>()
                     .Distinct(StringComparer.Ordinal)
                     .Take(AgentChatPositionLimits.MaximumSelectedEntities))
        {
            if (ambiguousNodeIds.Contains(selectedNodeId))
            {
                return [];
            }

            if (!nodesById.TryGetValue(selectedNodeId, out var selectedNode))
            {
                continue;
            }

            var resolution = ResolveChatRootForSelection(
                selectedNode,
                nodesById,
                ambiguousNodeIds);
            if (resolution.IsConflict)
            {
                return [];
            }

            if (!string.IsNullOrWhiteSpace(resolution.Alias))
            {
                resolvedAliases.Add(resolution.Alias);
            }
        }

        return resolvedAliases
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static ChatRootResolution ResolveChatRootForSelection(
        ProjectStructureNode selectedNode,
        IReadOnlyDictionary<string, ProjectStructureNode> nodesById,
        IReadOnlySet<string> ambiguousNodeIds)
    {
        var visited = new HashSet<string>(StringComparer.Ordinal)
        {
            selectedNode.Id
        };
        var node = selectedNode;
        while (true)
        {
            if (node.ObjectType == ProjectObjectType.ProjectBlock)
            {
                return ResolveAllRootsFromTypedProjectBlock(node);
            }

            if (string.IsNullOrWhiteSpace(node.ParentId))
            {
                return ChatRootResolution.None;
            }

            if (!visited.Add(node.ParentId) ||
                ambiguousNodeIds.Contains(node.ParentId) ||
                !nodesById.TryGetValue(node.ParentId, out node))
            {
                return ChatRootResolution.Conflict;
            }
        }
    }

    private static ChatRootResolution ResolveAllRootsFromTypedProjectBlock(
        ProjectStructureNode node)
    {
        if (node.ObjectType != ProjectObjectType.ProjectBlock ||
            string.IsNullOrWhiteSpace(node.MetadataJson))
        {
            return ChatRootResolution.None;
        }

        ProjectBlockMetadata? projectBlock;
        try
        {
            projectBlock = ProjectObjectMetadataSerializer.Parse(node.MetadataJson).ProjectBlock;
        }
        catch (InvalidOperationException)
        {
            return ChatRootResolution.Conflict;
        }

        if (projectBlock is null)
        {
            return ChatRootResolution.None;
        }

        var aliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var root in EnumerateProjectBlockRoots(projectBlock)
                     .Where(static root => !string.IsNullOrWhiteSpace(root)))
        {
            var alias = AgentWorkspaceToolAccessMetadata.NormalizeExternalTargetAlias(root);
            if (string.IsNullOrWhiteSpace(alias) || !IsAllowedImplicitChatRoot(alias))
            {
                return ChatRootResolution.Conflict;
            }

            aliases.Add(alias);
            if (aliases.Count > 1)
            {
                return ChatRootResolution.Conflict;
            }
        }

        return aliases.Count == 0
            ? ChatRootResolution.None
            : new ChatRootResolution(aliases.Single(), IsConflict: false);
    }

    private static bool IsAllowedImplicitChatRoot(string normalizedAlias)
    {
        var segments = normalizedAlias.Split(
            '/',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length < MinimumImplicitRootSegmentCount + 2 ||
            !string.Equals(
                segments[0],
                ExternalTargetAliasRoot,
                StringComparison.OrdinalIgnoreCase) ||
            segments[1].Length != 1 ||
            !char.IsLetter(segments[1][0]) ||
            ProtectedDriveFolders.Contains(segments[2]) ||
            IsUserProfileContainerRoot(segments))
        {
            return false;
        }

        return !ProtectedRoots.Any(root =>
            string.Equals(normalizedAlias, root.Alias, StringComparison.OrdinalIgnoreCase) ||
            root.ProtectDescendants &&
            normalizedAlias.StartsWith(root.Alias + "/", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsUserProfileContainerRoot(IReadOnlyList<string> segments)
        => segments.Count is 3 or 4 &&
           string.Equals(segments[2], "Users", StringComparison.OrdinalIgnoreCase);

    private static IReadOnlyList<ProtectedRoot> BuildProtectedRoots()
    {
        var roots = new List<ProtectedRoot>();
        Add(Environment.SpecialFolder.UserProfile, protectDescendants: false);
        Add(Environment.SpecialFolder.Windows, protectDescendants: true);
        Add(Environment.SpecialFolder.System, protectDescendants: true);
        Add(Environment.SpecialFolder.ProgramFiles, protectDescendants: true);
        Add(Environment.SpecialFolder.ProgramFilesX86, protectDescendants: true);
        Add(Environment.SpecialFolder.CommonApplicationData, protectDescendants: true);
        Add(Environment.SpecialFolder.ApplicationData, protectDescendants: true);
        Add(Environment.SpecialFolder.LocalApplicationData, protectDescendants: true);
        return roots
            .DistinctBy(root => root.Alias, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        void Add(Environment.SpecialFolder folder, bool protectDescendants)
        {
            var alias = AgentWorkspaceToolAccessMetadata.NormalizeExternalTargetAlias(
                Environment.GetFolderPath(folder));
            if (!string.IsNullOrWhiteSpace(alias))
            {
                roots.Add(new ProtectedRoot(alias, protectDescendants));
            }
        }
    }

    private static IEnumerable<ProjectStructureNode> EnumerateAuthorityNodes(
        ProjectStructureSurface surface,
        ProjectStructureNode focusNode)
    {
        var nodesById = surface.Nodes
            .Where(node => !string.IsNullOrWhiteSpace(node.Id))
            .GroupBy(node => node.Id, StringComparer.Ordinal)
            .Where(group => group.Count() == 1)
            .ToDictionary(group => group.Key, group => group.Single(), StringComparer.Ordinal);
        var visited = new HashSet<string>(StringComparer.Ordinal)
        {
            focusNode.Id
        };
        var parentId = focusNode.ParentId;
        while (!string.IsNullOrWhiteSpace(parentId) &&
               visited.Add(parentId) &&
               nodesById.TryGetValue(parentId, out var node))
        {
            yield return node;
            if (node.ObjectType == ProjectObjectType.ProjectBlock)
            {
                yield break;
            }

            parentId = node.ParentId;
        }
    }

    private static string ResolveFirstRootFromTypedProjectBlock(ProjectStructureNode node)
    {
        if (node.ObjectType != ProjectObjectType.ProjectBlock ||
            string.IsNullOrWhiteSpace(node.MetadataJson))
        {
            return string.Empty;
        }

        try
        {
            var projectBlock = ProjectObjectMetadataSerializer.Parse(node.MetadataJson).ProjectBlock;
            return projectBlock is null
                ? string.Empty
                : EnumerateProjectBlockRoots(projectBlock)
                    .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?
                    .Trim() ?? string.Empty;
        }
        catch (InvalidOperationException)
        {
            return string.Empty;
        }
    }

    private static IEnumerable<string?> EnumerateProjectBlockRoots(
        ProjectBlockMetadata projectBlock)
    {
        yield return projectBlock.OutputRoot;
        yield return projectBlock.ProductRoot;
        yield return projectBlock.TargetRoot;
        yield return projectBlock.RepositoryRoot;
        yield return projectBlock.WorkspaceRoot;
    }

    private sealed record ProtectedRoot(string Alias, bool ProtectDescendants);

    private readonly record struct ChatRootResolution(string Alias, bool IsConflict)
    {
        public static ChatRootResolution None { get; } = new(string.Empty, IsConflict: false);

        public static ChatRootResolution Conflict { get; } = new(string.Empty, IsConflict: true);
    }
}
