using CanDoItAll.Infrastructure;
using CanDoItAll.Infrastructure.FileSystem;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.AgentFramework.Maf;

internal static class MafRuntimePathResolver
{
    public static string ResolvePathFromWorkspace(
        string workspaceRoot,
        string path,
        bool allowExternal,
        IPhysicalFileSystemPathPolicyFactory physicalPathPolicyFactory,
        IReadOnlyList<string>? allowedExternalRoots = null)
    {
        var workspacePathPolicy = physicalPathPolicyFactory.Create(workspaceRoot);
        var normalizedWorkspaceRoot = workspacePathPolicy.RootPath;
        if (string.IsNullOrWhiteSpace(path))
        {
            return normalizedWorkspaceRoot;
        }

        var expandedPath = ExpandPortablePath(path);
        PhysicalPathSyntaxPolicy.EnsureNativeOrRelative(expandedPath, "MAF runtime path");
        var nativePath = Path.IsPathRooted(expandedPath)
            ? expandedPath
            : ToNativeLogicalPath(expandedPath);
        var fullPath = Path.GetFullPath(Path.IsPathRooted(nativePath)
            ? nativePath
            : Path.Combine(normalizedWorkspaceRoot, nativePath));
        if (workspacePathPolicy.IsWithinRoot(fullPath))
        {
            return fullPath;
        }

        if (!allowExternal)
        {
            throw new InvalidOperationException($"Path '{path}' resolves outside the workspace root. Use a workspace-relative path or import the external file into chat attachments first.");
        }

        var allowedRoots = allowedExternalRoots?
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(ExpandPortablePath)
            .Select(item =>
            {
                PhysicalPathSyntaxPolicy.EnsureNativeOrRelative(item, "MAF allowed external root");
                var nativeAllowedRoot = Path.IsPathRooted(item) ? item : ToNativeLogicalPath(item);
                var fullAllowedRoot = Path.GetFullPath(Path.IsPathRooted(nativeAllowedRoot)
                    ? nativeAllowedRoot
                    : Path.Combine(normalizedWorkspaceRoot, nativeAllowedRoot));
                return physicalPathPolicyFactory.Create(fullAllowedRoot);
            })
            .ToList()
            ?? [];

        if (allowedRoots.Any(allowedRoot => allowedRoot.IsWithinRoot(fullPath)))
        {
            return fullPath;
        }

        throw new InvalidOperationException($"Path '{path}' resolves outside the workspace root and is not covered by an explicit external-root allowlist.");
    }

    public static string ExpandPortablePath(string path)
    {
        return ExpandPortablePath(
            path,
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            Environment.GetEnvironmentVariable,
            PortablePathTemplateCompatibility.LegacyWindowsEnvironmentTokens);
    }

    internal static string ExpandPortablePath(
        string path,
        string? homeDirectory,
        Func<string, string?> variableResolver,
        PortablePathTemplateCompatibility compatibility)
    {
        return PortablePathTemplate.Expand(path, homeDirectory, variableResolver, compatibility);
    }

    public static bool IsPathWithinRoot(
        string fullPath,
        string rootPath,
        IPhysicalFileSystemPathPolicyFactory physicalPathPolicyFactory)
    {
        return physicalPathPolicyFactory.Create(rootPath).IsWithinRoot(fullPath);
    }

    private static string ToNativeLogicalPath(string path)
    {
        if (string.Equals(path, ".", StringComparison.Ordinal))
        {
            return path;
        }

        var logicalPath = LogicalPath.ParseLegacyWindowsLogicalPath(path);
        return Path.Combine(logicalPath.Segments.ToArray());
    }

}
