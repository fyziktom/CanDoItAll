using CanDoItAll.Infrastructure.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace CanDoItAll.Infrastructure.Storage;

public interface IWorkspacePathResolver
{
    string ResolveWorkspaceRoot();

    string ResolveManagedFilesRoot();

    string ResolveExportsRoot();

    string ResolveEvidenceRoot();

    string ResolveManagerArtifactsRoot();
}

public interface IFileStore
{
    Task<string> SaveTextAsync(string relativePath, string content, CancellationToken cancellationToken = default);

    Task<string> SaveBytesAsync(string relativePath, byte[] content, CancellationToken cancellationToken = default);

    Task<string?> ReadTextAsync(string relativePath, CancellationToken cancellationToken = default);
}

public interface IManagedArtifactStore
{
    string GetRelativePath(string category, string fileName);

    Task<string> SaveTextAsync(string category, string fileName, string content, CancellationToken cancellationToken = default);
}

public sealed class WorkspacePathResolver(IOptions<StorageOptions> options, IHostEnvironment hostEnvironment) : IWorkspacePathResolver
{
    private readonly StorageOptions _options = options.Value;

    public string ResolveWorkspaceRoot()
    {
        var root = Path.Combine(hostEnvironment.ContentRootPath, _options.WorkspaceRoot);
        Directory.CreateDirectory(root);
        return root;
    }

    public string ResolveManagedFilesRoot() => EnsureDirectory(_options.ManagedFilesFolder);

    public string ResolveExportsRoot() => EnsureDirectory(_options.ExportsFolder);

    public string ResolveEvidenceRoot() => EnsureDirectory(_options.EvidenceFolder);

    public string ResolveManagerArtifactsRoot()
    {
        var root = Path.Combine(hostEnvironment.ContentRootPath, _options.ManagerArtifactsFolder);
        Directory.CreateDirectory(root);
        return root;
    }

    private string EnsureDirectory(string folder)
    {
        var path = Path.Combine(ResolveWorkspaceRoot(), folder);
        Directory.CreateDirectory(path);
        return path;
    }
}

public sealed class LocalFileStore(IWorkspacePathResolver resolver) : IFileStore
{
    public async Task<string> SaveTextAsync(string relativePath, string content, CancellationToken cancellationToken = default)
    {
        var fullPath = Path.Combine(resolver.ResolveWorkspaceRoot(), relativePath);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllTextAsync(fullPath, content, cancellationToken);
        return fullPath;
    }

    public async Task<string> SaveBytesAsync(string relativePath, byte[] content, CancellationToken cancellationToken = default)
    {
        var fullPath = Path.Combine(resolver.ResolveWorkspaceRoot(), relativePath);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllBytesAsync(fullPath, content, cancellationToken);
        return fullPath;
    }

    public async Task<string?> ReadTextAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        var fullPath = Path.Combine(resolver.ResolveWorkspaceRoot(), relativePath);
        if (!File.Exists(fullPath))
        {
            return null;
        }

        return await File.ReadAllTextAsync(fullPath, cancellationToken);
    }
}

public sealed class ManagedArtifactStore(IFileStore fileStore) : IManagedArtifactStore
{
    public string GetRelativePath(string category, string fileName) => Path.Combine("managed-files", category, fileName);

    public Task<string> SaveTextAsync(string category, string fileName, string content, CancellationToken cancellationToken = default)
        => fileStore.SaveTextAsync(GetRelativePath(category, fileName), content, cancellationToken);
}
