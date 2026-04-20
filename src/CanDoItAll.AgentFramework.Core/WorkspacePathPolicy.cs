using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

internal readonly record struct WorkspacePathResolution(
    string FullPath,
    string RelativePath,
    string DisplayPath,
    bool IsWorkspacePath);

internal sealed class WorkspacePathPolicy
{
    private readonly string workspaceRoot;
    private readonly string workspaceRootWithSeparator;
    private readonly WorkspaceScopeDescriptor workspaceScope;

    public WorkspacePathPolicy(string workspaceRoot, WorkspaceScopeDescriptor? workspaceScope = null)
    {
        this.workspaceRoot = Path.GetFullPath(workspaceRoot);
        workspaceRootWithSeparator = EnsureTrailingSeparator(this.workspaceRoot);
        this.workspaceScope = workspaceScope ?? WorkspaceScopeDescriptor.Sandbox;
    }

    public string WorkspaceRoot => workspaceRoot;

    public WorkspaceScopeDescriptor WorkspaceScope => workspaceScope;

    public bool TryResolveWorkspacePath(string? path, bool allowWorkspaceRoot, out WorkspacePathResolution resolution, out string validationMessage)
    {
        resolution = CreateWorkspaceResolution(workspaceRoot);
        validationMessage = string.Empty;

        if (string.IsNullOrWhiteSpace(path))
        {
            if (allowWorkspaceRoot)
            {
                return true;
            }

            validationMessage = "Provide a workspace-relative path.";
            return false;
        }

        string fullPath;
        try
        {
            fullPath = ResolveWorkspaceFullPath(path);
        }
        catch (InvalidOperationException exception)
        {
            resolution = default;
            validationMessage = exception.Message;
            return false;
        }

        if (!IsWithinWorkspace(fullPath))
        {
            resolution = default;
            validationMessage = $"Path '{path}' resolves outside the workspace root. Use a workspace-relative path or import the external file into chat attachments first.";
            return false;
        }

        resolution = CreateWorkspaceResolution(fullPath);
        return true;
    }

    public WorkspacePathResolution ResolveAccessiblePath(string path, IReadOnlyList<string>? allowedExternalRoots = null)
    {
        var fullPath = ResolveWorkspaceFullPath(path);
        if (IsWithinWorkspace(fullPath))
        {
            return CreateWorkspaceResolution(fullPath);
        }

        var normalizedAllowedRoots = NormalizeAllowedExternalRoots(allowedExternalRoots);
        if (normalizedAllowedRoots.Any(root => IsPathWithinRoot(fullPath, root)))
        {
            var normalizedAbsolutePath = NormalizeAbsolutePath(fullPath);
            return new WorkspacePathResolution(
                FullPath: fullPath,
                RelativePath: normalizedAbsolutePath,
                DisplayPath: normalizedAbsolutePath,
                IsWorkspacePath: false);
        }

        throw new InvalidOperationException($"Path '{path}' resolves outside the workspace root and is not covered by an explicit external-root allowlist.");
    }

    public WorkspacePathResolution ResolveExistingPath(string path, bool allowFiles, bool allowDirectories, IReadOnlyList<string>? allowedExternalRoots = null)
    {
        var resolution = ResolveAccessiblePath(path, allowedExternalRoots);
        var displayPath = resolution.DisplayPath;

        if (File.Exists(resolution.FullPath))
        {
            if (!allowFiles)
            {
                throw new InvalidOperationException($"Path '{displayPath}' resolves to a file, but a directory was required.");
            }

            return resolution;
        }

        if (Directory.Exists(resolution.FullPath))
        {
            if (!allowDirectories)
            {
                throw new InvalidOperationException($"Path '{displayPath}' resolves to a directory, but a file was required.");
            }

            return resolution;
        }

        throw new InvalidOperationException($"Path '{displayPath}' does not exist.");
    }

    public string ResolveWorkingDirectory(
        string? workingDirectory,
        bool createIfMissing,
        out WorkspacePathResolution resolution,
        IReadOnlyList<string>? allowedExternalRoots = null)
    {
        resolution = string.IsNullOrWhiteSpace(workingDirectory)
            ? CreateWorkspaceResolution(workspaceRoot)
            : ResolveAccessiblePath(workingDirectory, allowedExternalRoots);

        if (File.Exists(resolution.FullPath))
        {
            throw new InvalidOperationException($"Working directory '{resolution.DisplayPath}' resolves to a file.");
        }

        if (!Directory.Exists(resolution.FullPath))
        {
            if (!createIfMissing)
            {
                throw new InvalidOperationException($"Working directory '{resolution.DisplayPath}' does not exist.");
            }

            Directory.CreateDirectory(resolution.FullPath);
        }

        return resolution.DisplayPath;
    }

    public bool IsWithinWorkspace(string fullPath)
    {
        return string.Equals(fullPath, workspaceRoot, StringComparison.OrdinalIgnoreCase)
            || fullPath.StartsWith(workspaceRootWithSeparator, StringComparison.OrdinalIgnoreCase);
    }

    public string ToRelativePath(string fullPath)
    {
        if (string.Equals(fullPath, workspaceRoot, StringComparison.OrdinalIgnoreCase))
        {
            return ".";
        }

        return NormalizeRelativePath(Path.GetRelativePath(workspaceRoot, fullPath));
    }

    public string ToDisplayPath(string fullPath)
        => IsWithinWorkspace(fullPath) ? ToRelativePath(fullPath) : NormalizeAbsolutePath(fullPath);

    public IReadOnlyList<string> NormalizeAllowedExternalRoots(IReadOnlyList<string>? allowedExternalRoots)
    {
        return allowedExternalRoots?
            .Where(root => !string.IsNullOrWhiteSpace(root))
            .Select(root => ResolveWorkspaceFullPath(root!))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList()
            ?? [];
    }

    public static string NormalizeRelativePath(string path)
        => path.Replace('\\', '/').Trim();

    public static string NormalizeAbsolutePath(string path)
        => Path.GetFullPath(path).Replace('\\', '/');

    public static bool IsPathWithinRoot(string fullPath, string rootPath)
    {
        var normalizedFullPath = Path.GetFullPath(fullPath);
        var normalizedRoot = Path.GetFullPath(rootPath);
        if (string.Equals(normalizedFullPath, normalizedRoot, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var normalizedRootWithSeparator = EnsureTrailingSeparator(normalizedRoot);
        return normalizedFullPath.StartsWith(normalizedRootWithSeparator, StringComparison.OrdinalIgnoreCase);
    }

    public static string ExpandPortablePath(string path)
    {
        var expanded = Environment.ExpandEnvironmentVariables(path.Trim());
        if (string.Equals(expanded, "~", StringComparison.Ordinal))
        {
            return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        }

        if (expanded.StartsWith("~/", StringComparison.Ordinal) || expanded.StartsWith("~\\", StringComparison.Ordinal))
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(home, expanded[2..]);
        }

        return expanded;
    }

    private string ResolveWorkspaceFullPath(string path)
    {
        var expandedPath = ExpandPortablePath(path);
        var candidateFullPath = Path.GetFullPath(
            Path.IsPathRooted(expandedPath)
                ? expandedPath
                : Path.Combine(workspaceRoot, expandedPath));
        if (!IsWithinWorkspace(candidateFullPath))
        {
            return candidateFullPath;
        }

        var relativePath = NormalizeRelativePath(Path.GetRelativePath(workspaceRoot, candidateFullPath));
        if (string.IsNullOrWhiteSpace(relativePath) || string.Equals(relativePath, ".", StringComparison.Ordinal))
        {
            return candidateFullPath;
        }

        var scopedRelativePath = ApplyManagedRootScope(relativePath);
        return Path.GetFullPath(Path.Combine(workspaceRoot, scopedRelativePath.Replace('/', Path.DirectorySeparatorChar)));
    }

    private static string ResolveFullPath(string path, string workspaceRoot)
    {
        var expandedPath = ExpandPortablePath(path);
        return Path.GetFullPath(Path.IsPathRooted(expandedPath) ? expandedPath : Path.Combine(workspaceRoot, expandedPath));
    }

    private WorkspacePathResolution CreateWorkspaceResolution(string fullPath)
    {
        var relativePath = ToRelativePath(fullPath);
        return new WorkspacePathResolution(
            FullPath: fullPath,
            RelativePath: relativePath,
            DisplayPath: relativePath,
            IsWorkspacePath: true);
    }

    private static string EnsureTrailingSeparator(string path)
    {
        return path.EndsWith(Path.DirectorySeparatorChar) || path.EndsWith(Path.AltDirectorySeparatorChar)
            ? path
            : path + Path.DirectorySeparatorChar;
    }

    private string ApplyManagedRootScope(string relativePath)
    {
        if (workspaceScope.IsDefaultSandbox)
        {
            return relativePath;
        }

        return TryMapManagedRoot(relativePath, "artifacts", workspaceScope.ArtifactRootRelativePath)
            ?? TryMapManagedRoot(relativePath, "output", workspaceScope.OutputRootRelativePath)
            ?? TryMapManagedRoot(relativePath, "integration-map", workspaceScope.IntegrationMapRootRelativePath)
            ?? TryMapManagedRoot(relativePath, "data", workspaceScope.DataRootRelativePath)
            ?? relativePath;
    }

    private string? TryMapManagedRoot(string relativePath, string rootName, string scopedRootRelativePath)
    {
        if (!MatchesRoot(relativePath, rootName))
        {
            return null;
        }

        if (MatchesRoot(relativePath, scopedRootRelativePath))
        {
            return relativePath;
        }

        var foreignScopedPrefix = $"{rootName}/scopes/";
        if (relativePath.StartsWith(foreignScopedPrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Path '{relativePath}' targets a different managed {rootName} scope. Use the current scope '{workspaceScope.DisplayName}'.");
        }

        var suffix = RemoveRoot(relativePath, rootName);
        return string.IsNullOrWhiteSpace(suffix)
            ? scopedRootRelativePath
            : WorkspaceScopeDescriptor.NormalizeRelativePath(Path.Combine(scopedRootRelativePath, suffix));
    }

    private static bool MatchesRoot(string relativePath, string rootRelativePath)
    {
        return string.Equals(relativePath, rootRelativePath, StringComparison.OrdinalIgnoreCase)
               || relativePath.StartsWith(rootRelativePath + "/", StringComparison.OrdinalIgnoreCase);
    }

    private static string RemoveRoot(string relativePath, string rootRelativePath)
    {
        if (string.Equals(relativePath, rootRelativePath, StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }

        return relativePath[(rootRelativePath.Length + 1)..];
    }
}
