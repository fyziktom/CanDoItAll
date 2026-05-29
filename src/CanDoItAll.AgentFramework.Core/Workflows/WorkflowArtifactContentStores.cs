using System.Collections.Concurrent;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

public sealed class InMemoryWorkflowArtifactContentStore : IWorkflowArtifactContentStore
{
    private readonly ConcurrentDictionary<WorkflowArtifactId, WorkflowArtifactContent> contents = new();

    public Task SaveContentAsync(
        WorkflowArtifactRecord artifact,
        string content,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        cancellationToken.ThrowIfCancellationRequested();

        contents[artifact.Id] = new WorkflowArtifactContent(artifact, content ?? string.Empty);
        return Task.CompletedTask;
    }

    public Task<WorkflowArtifactContent?> ReadContentAsync(
        WorkflowArtifactRecord artifact,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        cancellationToken.ThrowIfCancellationRequested();

        contents.TryGetValue(artifact.Id, out var content);
        return Task.FromResult(content);
    }
}

public sealed class FileWorkflowArtifactContentStore : IWorkflowArtifactContentStore
{
    private readonly string workspaceRoot;
    private readonly string artifactRoot;
    private readonly WorkspacePathPolicy workspacePathPolicy;

    public FileWorkflowArtifactContentStore(
        string workspaceRoot,
        WorkspaceScopeDescriptor? workspaceScope = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);

        var resolvedScope = workspaceScope ?? WorkspaceScopeDescriptor.Sandbox;
        this.workspaceRoot = Path.GetFullPath(workspaceRoot);
        artifactRoot = Path.GetFullPath(resolvedScope.ResolveArtifactRoot(this.workspaceRoot));
        workspacePathPolicy = new WorkspacePathPolicy(this.workspaceRoot, resolvedScope);
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
        if (!WorkspacePathPolicy.IsPathWithinRoot(fullPath, artifactRoot))
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
            var resolution = workspacePathPolicy.ResolveAccessiblePath(storagePath);
            if (!WorkspacePathPolicy.IsPathWithinRoot(resolution.FullPath, workspaceRoot))
            {
                return false;
            }

            fullPath = resolution.FullPath;
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
}
