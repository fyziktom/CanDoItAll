using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure;
using CanDoItAll.Infrastructure.FileSystem;
using CanDoItAll.SharedKernel;
using CanDoItAll.Infrastructure.Storage;

namespace CanDoItAll.Modules.Workbench;

internal static class ProjectStructureOutputRootAuthorityResolver
{
    private const int MinimumImplicitRootSegmentCount = 1;
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
    private static readonly IPhysicalFileSystemPathPolicyFactory PhysicalPathPolicyFactory =
        new PhysicalFileSystemPathPolicyFactory();

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
        IReadOnlyList<AgentChatContextEntityReference>? selectedEntities,
        IExternalTargetPathRegistry externalTargetPathRegistry)
    {
        ArgumentNullException.ThrowIfNull(externalTargetPathRegistry);
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
        var resolvedAliases = new HashSet<string>(ExternalTargetAliasCodec.EqualityComparer);

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
                ambiguousNodeIds,
                externalTargetPathRegistry);
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
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    private static ChatRootResolution ResolveChatRootForSelection(
        ProjectStructureNode selectedNode,
        IReadOnlyDictionary<string, ProjectStructureNode> nodesById,
        IReadOnlySet<string> ambiguousNodeIds,
        IExternalTargetPathRegistry externalTargetPathRegistry)
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
                return ResolveAllRootsFromTypedProjectBlock(node, externalTargetPathRegistry);
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
        ProjectStructureNode node,
        IExternalTargetPathRegistry externalTargetPathRegistry)
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

        var aliases = new HashSet<string>(ExternalTargetAliasCodec.EqualityComparer);
        foreach (var root in EnumerateProjectBlockRoots(projectBlock)
                     .Where(static root => !string.IsNullOrWhiteSpace(root)))
        {
            var alias = AgentWorkspaceToolAccessMetadata.NormalizeExternalTargetAlias(
                root,
                externalTargetPathRegistry);
            if (string.IsNullOrWhiteSpace(alias) ||
                !IsAllowedImplicitChatRoot(alias, externalTargetPathRegistry))
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

    private static bool IsAllowedImplicitChatRoot(
        string normalizedAlias,
        IExternalTargetPathRegistry externalTargetPathRegistry)
    {
        if (externalTargetPathRegistry.TryResolve(
                normalizedAlias,
                out var physicalRoot,
                out _) != ExternalTargetAliasResolutionKind.Resolved)
        {
            return false;
        }

        var filesystemRoot = Path.GetPathRoot(physicalRoot);
        if (string.IsNullOrWhiteSpace(filesystemRoot))
        {
            return false;
        }

        var segments = Path.GetRelativePath(filesystemRoot, physicalRoot)
            .Split(
                [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length < MinimumImplicitRootSegmentCount ||
            OperatingSystem.IsWindows() && ProtectedDriveFolders.Contains(segments[0]))
        {
            return false;
        }

        return !ProtectedRoots.Any(root => IsProtectedRoot(physicalRoot, root));
    }

    private static IReadOnlyList<ProtectedRoot> BuildProtectedRoots()
    {
        var roots = new List<ProtectedRoot>();
        AddFolder(Environment.SpecialFolder.UserProfile, protectDescendants: false);
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (OperatingSystem.IsWindows() &&
            !string.IsNullOrWhiteSpace(userProfile) &&
            Directory.GetParent(userProfile) is { } userContainer &&
            !IsFileSystemRoot(userContainer.FullName))
        {
            AddPath(userContainer.FullName, protectDescendants: true);
        }
        AddFolder(Environment.SpecialFolder.Windows, protectDescendants: true);
        AddFolder(Environment.SpecialFolder.System, protectDescendants: true);
        AddFolder(Environment.SpecialFolder.ProgramFiles, protectDescendants: true);
        AddFolder(Environment.SpecialFolder.ProgramFilesX86, protectDescendants: true);
        AddFolder(Environment.SpecialFolder.CommonApplicationData, protectDescendants: true);
        AddFolder(Environment.SpecialFolder.ApplicationData, protectDescendants: true);
        AddFolder(Environment.SpecialFolder.LocalApplicationData, protectDescendants: true);
        return roots
            .DistinctBy(root => root.Path, StringComparer.Ordinal)
            .ToArray();

        void AddFolder(Environment.SpecialFolder folder, bool protectDescendants)
        {
            AddPath(Environment.GetFolderPath(folder), protectDescendants);
        }

        void AddPath(string path, bool protectDescendants)
        {
            if (!string.IsNullOrWhiteSpace(path) && Path.IsPathRooted(path))
            {
                roots.Add(new ProtectedRoot(Path.GetFullPath(path), protectDescendants));
            }
        }

        static bool IsFileSystemRoot(string path)
        {
            var fullPath = Path.GetFullPath(path);
            var root = Path.GetPathRoot(fullPath);
            return !string.IsNullOrWhiteSpace(root) &&
                   string.Equals(
                       Path.TrimEndingDirectorySeparator(fullPath),
                       Path.TrimEndingDirectorySeparator(root),
                       OperatingSystem.IsWindows()
                           ? StringComparison.OrdinalIgnoreCase
                           : StringComparison.Ordinal);
        }
    }

    private static string EnsureTrailingSeparator(string path)
        => path.EndsWith(Path.DirectorySeparatorChar) || path.EndsWith(Path.AltDirectorySeparatorChar)
            ? path
            : path + Path.DirectorySeparatorChar;

    private static bool IsProtectedRoot(string physicalRoot, ProtectedRoot protectedRoot)
    {
        var policy = PhysicalPathPolicyFactory.Create(protectedRoot.Path);
        var comparison = policy.CaseSensitivity == PhysicalFileSystemCaseSensitivity.Sensitive
            ? StringComparison.Ordinal
            : StringComparison.OrdinalIgnoreCase;
        return string.Equals(physicalRoot, protectedRoot.Path, comparison) ||
               protectedRoot.ProtectDescendants &&
               physicalRoot.StartsWith(EnsureTrailingSeparator(protectedRoot.Path), comparison);
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

    private sealed record ProtectedRoot(string Path, bool ProtectDescendants);

    private readonly record struct ChatRootResolution(string Alias, bool IsConflict)
    {
        public static ChatRootResolution None { get; } = new(string.Empty, IsConflict: false);

        public static ChatRootResolution Conflict { get; } = new(string.Empty, IsConflict: true);
    }
}
