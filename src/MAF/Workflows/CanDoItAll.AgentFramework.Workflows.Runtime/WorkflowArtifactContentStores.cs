using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

public sealed class FileWorkflowArtifactContentStore : IWorkflowArtifactContentStore
{
    private readonly string workspaceRoot;
    private readonly string artifactRoot;
    private readonly WorkspaceScopeDescriptor workspaceScope;

    public FileWorkflowArtifactContentStore(
        string workspaceRoot,
        WorkspaceScopeDescriptor? workspaceScope = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);

        var resolvedScope = workspaceScope ?? WorkspaceScopeDescriptor.Sandbox;
        this.workspaceScope = resolvedScope;
        this.workspaceRoot = Path.GetFullPath(workspaceRoot);
        artifactRoot = Path.GetFullPath(resolvedScope.ResolveArtifactRoot(this.workspaceRoot));
    }

    public async Task SaveContentAsync(
        WorkflowArtifactRecord artifact,
        string content,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        cancellationToken.ThrowIfCancellationRequested();

        var fullPath = ResolveArtifactContentPath(artifact);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllTextAsync(fullPath, content ?? string.Empty, cancellationToken);
    }

    public async Task<WorkflowArtifactContent?> ReadContentAsync(
        WorkflowArtifactRecord artifact,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        cancellationToken.ThrowIfCancellationRequested();

        var artifactContentPath = ResolveArtifactContentPath(artifact);
        if (File.Exists(artifactContentPath))
        {
            return new WorkflowArtifactContent(
                artifact,
                await File.ReadAllTextAsync(artifactContentPath, cancellationToken));
        }

        if (artifact.Kind == WorkflowArtifactKind.File &&
            IsTextLike(artifact.ContentType) &&
            TryResolveWorkspaceFile(artifact.StoragePath, out var workspaceFilePath) &&
            File.Exists(workspaceFilePath))
        {
            return new WorkflowArtifactContent(
                artifact,
                await File.ReadAllTextAsync(workspaceFilePath, cancellationToken));
        }

        return null;
    }

    private string ResolveArtifactContentPath(WorkflowArtifactRecord artifact)
    {
        var storagePath = WorkspaceScopeDescriptor.NormalizeRelativePath(artifact.StoragePath);
        if (string.IsNullOrWhiteSpace(storagePath))
        {
            throw new InvalidOperationException($"Workflow artifact '{artifact.Id}' does not have a storage path.");
        }

        if (Path.IsPathRooted(storagePath) ||
            storagePath.Split('/').Any(segment => segment == ".."))
        {
            throw new InvalidOperationException($"Workflow artifact '{artifact.Id}' storage path is not workspace-artifact scoped.");
        }

        var fullPath = Path.GetFullPath(Path.Combine(
            artifactRoot,
            storagePath.Replace('/', Path.DirectorySeparatorChar)));
        if (!IsPathWithinRoot(fullPath, artifactRoot))
        {
            throw new InvalidOperationException($"Workflow artifact '{artifact.Id}' storage path escapes the artifact root.");
        }

        return fullPath;
    }

    private bool TryResolveWorkspaceFile(string storagePath, out string fullPath)
    {
        fullPath = string.Empty;
        try
        {
            var fullPathCandidate = ResolveWorkspaceFilePath(storagePath);
            if (!IsPathWithinRoot(fullPathCandidate, workspaceRoot))
            {
                return false;
            }

            fullPath = fullPathCandidate;
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private static bool IsTextLike(string contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
        {
            return false;
        }

        return contentType.StartsWith("text/", StringComparison.OrdinalIgnoreCase) ||
               contentType.Contains("json", StringComparison.OrdinalIgnoreCase) ||
               contentType.Contains("markdown", StringComparison.OrdinalIgnoreCase) ||
               contentType.Contains("xml", StringComparison.OrdinalIgnoreCase);
    }

    private string ResolveWorkspaceFilePath(string storagePath)
    {
        var normalized = WorkspaceScopeDescriptor.NormalizeRelativePath(storagePath);
        if (string.IsNullOrWhiteSpace(normalized) ||
            Path.IsPathRooted(normalized) ||
            normalized.Split('/').Any(segment => segment == ".."))
        {
            throw new InvalidOperationException("Workflow artifact workspace file path is not workspace scoped.");
        }

        var scopedRelativePath = ApplyManagedRootScope(normalized);
        var fullPath = Path.GetFullPath(Path.Combine(
            workspaceRoot,
            scopedRelativePath.Replace('/', Path.DirectorySeparatorChar)));
        if (!IsPathWithinRoot(fullPath, workspaceRoot))
        {
            throw new InvalidOperationException("Workflow artifact workspace file path escapes the workspace root.");
        }

        return fullPath;
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

    private string? TryMapManagedRoot(
        string relativePath,
        string rootName,
        string scopedRootRelativePath)
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

    private static bool IsPathWithinRoot(string fullPath, string rootPath)
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

    private static bool MatchesRoot(string relativePath, string rootRelativePath)
        => string.Equals(relativePath, rootRelativePath, StringComparison.OrdinalIgnoreCase) ||
           relativePath.StartsWith(rootRelativePath + "/", StringComparison.OrdinalIgnoreCase);

    private static string RemoveRoot(string relativePath, string rootRelativePath)
        => string.Equals(relativePath, rootRelativePath, StringComparison.OrdinalIgnoreCase)
            ? string.Empty
            : relativePath[(rootRelativePath.Length + 1)..];

    private static string EnsureTrailingSeparator(string path)
        => path.EndsWith(Path.DirectorySeparatorChar) || path.EndsWith(Path.AltDirectorySeparatorChar)
            ? path
            : path + Path.DirectorySeparatorChar;
}
