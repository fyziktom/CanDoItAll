using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.AgentFramework.Core;

public sealed class FileWorkflowArtifactContentStore : IWorkflowArtifactContentStore
{
    private readonly WorkspaceScopeDescriptor workspaceScope;
    private readonly IWorkspacePathResolutionService pathResolutionService;
    private readonly IWorkspaceFileService workspaceFiles;

    public FileWorkflowArtifactContentStore(
        WorkspaceScopeDescriptor workspaceScope,
        IWorkspacePathResolutionService pathResolutionService,
        IWorkspaceFileService workspaceFiles)
    {
        this.workspaceScope = workspaceScope ?? throw new ArgumentNullException(nameof(workspaceScope));
        this.pathResolutionService = pathResolutionService ??
            throw new ArgumentNullException(nameof(pathResolutionService));
        this.workspaceFiles = workspaceFiles ?? throw new ArgumentNullException(nameof(workspaceFiles));
    }

    public Task SaveContentAsync(
        WorkflowArtifactRecord artifact,
        string content,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        cancellationToken.ThrowIfCancellationRequested();

        WorkspaceResolvedPath resolved = ResolveArtifactContentPath(artifact, allowMissing: true);
        WorkspaceFileMutationResult result = workspaceFiles.WriteTextFile(
            resolved.RelativePath,
            content ?? string.Empty,
            overwrite: true);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                $"Workflow artifact '{artifact.Id}' content could not be persisted: {result.Message}");
        }

        return Task.CompletedTask;
    }

    public async Task<WorkflowArtifactContent?> ReadContentAsync(
        WorkflowArtifactRecord artifact,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        cancellationToken.ThrowIfCancellationRequested();

        WorkspaceResolvedPath artifactContentPath = ResolveArtifactContentPath(artifact, allowMissing: true);
        if (File.Exists(artifactContentPath.FullPath))
        {
            artifactContentPath = ResolveArtifactContentPath(artifact, allowMissing: false);
            return new WorkflowArtifactContent(
                artifact,
                await File.ReadAllTextAsync(artifactContentPath.FullPath, cancellationToken));
        }

        if (artifact.Kind == WorkflowArtifactKind.File &&
            IsTextLike(artifact.ContentType) &&
            TryResolveWorkspaceFile(artifact.StoragePath, out WorkspaceResolvedPath workspaceFilePath) &&
            File.Exists(workspaceFilePath.FullPath))
        {
            workspaceFilePath = pathResolutionService.ResolveFilePath(
                workspaceFilePath.RelativePath,
                allowMissing: false);
            return new WorkflowArtifactContent(
                artifact,
                await File.ReadAllTextAsync(workspaceFilePath.FullPath, cancellationToken));
        }

        return null;
    }

    private WorkspaceResolvedPath ResolveArtifactContentPath(
        WorkflowArtifactRecord artifact,
        bool allowMissing)
    {
        LogicalPath storagePath;
        try
        {
            storagePath = LogicalPath.Parse(artifact.StoragePath);
        }
        catch (ArgumentException exception)
        {
            throw new InvalidOperationException(
                $"Workflow artifact '{artifact.Id}' storage path is not a canonical logical path.",
                exception);
        }

        string relativePath = WorkspaceScopeDescriptor.NormalizeRelativePath(Path.Combine(
            workspaceScope.ArtifactRootRelativePath,
            storagePath.Value));
        WorkspaceResolvedPath resolved = pathResolutionService.ResolveFilePath(relativePath, allowMissing);
        if (!resolved.IsWorkspacePath)
        {
            throw new InvalidOperationException($"Workflow artifact '{artifact.Id}' storage path escapes the artifact root.");
        }

        return resolved;
    }

    private bool TryResolveWorkspaceFile(string storagePath, out WorkspaceResolvedPath resolved)
    {
        resolved = default!;
        try
        {
            string relativePath = ResolveWorkspaceFilePath(storagePath);
            resolved = pathResolutionService.ResolveFilePath(relativePath, allowMissing: true);
            return resolved.IsWorkspacePath;
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
        LogicalPath normalized;
        try
        {
            normalized = LogicalPath.Parse(storagePath);
        }
        catch (ArgumentException exception)
        {
            throw new InvalidOperationException(
                "Workflow artifact workspace file path is not a canonical logical path.",
                exception);
        }

        return ApplyManagedRootScope(normalized.Value);
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
        if (relativePath.StartsWith(foreignScopedPrefix, StringComparison.Ordinal))
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
        => string.Equals(relativePath, rootRelativePath, StringComparison.Ordinal) ||
           relativePath.StartsWith(rootRelativePath + "/", StringComparison.Ordinal);

    private static string RemoveRoot(string relativePath, string rootRelativePath)
        => string.Equals(relativePath, rootRelativePath, StringComparison.Ordinal)
            ? string.Empty
            : relativePath[(rootRelativePath.Length + 1)..];

}
