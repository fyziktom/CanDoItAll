using CanDoItAll.Infrastructure.Configuration;
using CanDoItAll.Infrastructure.ControlPlane;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using System.Text;

namespace CanDoItAll.Infrastructure.Storage;

public interface IWorkspacePathResolver
{
    string ResolveWorkspaceRoot();

    string ResolveManagedFilesRoot();

    string ResolveExportsRoot();

    string ResolveEvidenceRoot();

    string ResolveManagerArtifactsRoot();
}

public sealed record WorkspacePathAccessResult(bool IsSuccess, string FullPath, string Message)
{
    public static WorkspacePathAccessResult Success(string fullPath)
        => new(true, fullPath, string.Empty);

    public static WorkspacePathAccessResult Failure(string message)
        => new(false, string.Empty, message);
}

public interface IWorkspacePathAccessGuard
{
    WorkspacePathAccessResult ResolveWorkspacePath(string path, string? basePath = null);

    WorkspacePathAccessResult ResolveManagedFilePath(string path);
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

public sealed class WorkspacePathResolver(
    IOptions<StorageOptions> options,
    IHostEnvironment hostEnvironment,
    IActiveDatabaseProfileResolver activeDatabaseProfileResolver) : IWorkspacePathResolver
{
    private readonly StorageOptions _options = options.Value;

    public string ResolveWorkspaceRoot()
    {
        var activeProfile = activeDatabaseProfileResolver.ResolveCurrentProfile();
        var root = string.IsNullOrWhiteSpace(activeProfile.Profile.Storage.WorkspaceRoot)
            ? ControlPlanePathDefaults.ResolveConfiguredPath(hostEnvironment.ContentRootPath, _options.WorkspaceRoot)
            : Path.GetFullPath(activeProfile.Profile.Storage.WorkspaceRoot);
        Directory.CreateDirectory(root);
        return root;
    }

    public string ResolveManagedFilesRoot() => EnsureDirectory(_options.ManagedFilesFolder);

    public string ResolveExportsRoot() => EnsureDirectory(_options.ExportsFolder);

    public string ResolveEvidenceRoot() => EnsureDirectory(_options.EvidenceFolder);

    public string ResolveManagerArtifactsRoot()
    {
        var root = ControlPlanePathDefaults.ResolveConfiguredPath(
            hostEnvironment.ContentRootPath,
            _options.ManagerArtifactsFolder);
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

public sealed class WorkspacePathAccessGuard(IWorkspacePathResolver resolver) : IWorkspacePathAccessGuard
{
    public WorkspacePathAccessResult ResolveWorkspacePath(string path, string? basePath = null)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return WorkspacePathAccessResult.Failure("A workspace path is required.");
        }

        var workspaceRoot = Path.GetFullPath(resolver.ResolveWorkspaceRoot());
        var resolutionBase = string.IsNullOrWhiteSpace(basePath)
            ? workspaceRoot
            : Path.GetFullPath(basePath);

        if (!IsWithinRoot(workspaceRoot, resolutionBase))
        {
            return WorkspacePathAccessResult.Failure("The resolved base path is outside the active workspace root.");
        }

        var candidate = ResolveCandidatePath(path, resolutionBase);
        return IsWithinRoot(workspaceRoot, candidate)
            ? WorkspacePathAccessResult.Success(candidate)
            : WorkspacePathAccessResult.Failure("The resolved path is outside the active workspace root.");
    }

    public WorkspacePathAccessResult ResolveManagedFilePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return WorkspacePathAccessResult.Failure("A managed file path is required.");
        }

        var workspaceRoot = Path.GetFullPath(resolver.ResolveWorkspaceRoot());
        var managedFilesRoot = Path.GetFullPath(resolver.ResolveManagedFilesRoot());
        var managedFilesRelativeRoot = NormalizeRelativePath(Path.GetRelativePath(workspaceRoot, managedFilesRoot));
        var trimmedPath = path.Trim();
        var candidate = Path.IsPathRooted(trimmedPath)
            ? Path.GetFullPath(trimmedPath)
            : ResolveRelativeManagedFilePath(trimmedPath, workspaceRoot, managedFilesRoot, managedFilesRelativeRoot);

        return IsWithinRoot(managedFilesRoot, candidate)
            ? WorkspacePathAccessResult.Success(candidate)
            : WorkspacePathAccessResult.Failure("The resolved path is outside the active managed files root.");
    }

    private static string ResolveRelativeManagedFilePath(
        string path,
        string workspaceRoot,
        string managedFilesRoot,
        string managedFilesRelativeRoot)
    {
        var normalizedPath = NormalizeRelativePath(path);
        var managedPrefix = string.IsNullOrWhiteSpace(managedFilesRelativeRoot)
            ? string.Empty
            : managedFilesRelativeRoot + Path.DirectorySeparatorChar;

        return normalizedPath.Equals(managedFilesRelativeRoot, PathComparison) ||
               (!string.IsNullOrWhiteSpace(managedPrefix) &&
                normalizedPath.StartsWith(managedPrefix, PathComparison))
            ? Path.GetFullPath(Path.Combine(workspaceRoot, normalizedPath))
            : Path.GetFullPath(Path.Combine(managedFilesRoot, normalizedPath));
    }

    private static string ResolveCandidatePath(string path, string resolutionBase)
    {
        var trimmedPath = path.Trim();
        return Path.IsPathRooted(trimmedPath)
            ? Path.GetFullPath(trimmedPath)
            : Path.GetFullPath(Path.Combine(resolutionBase, NormalizeRelativePath(trimmedPath)));
    }

    private static string NormalizeRelativePath(string path)
    {
        return path
            .Trim()
            .Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar)
            .TrimStart(Path.DirectorySeparatorChar);
    }

    private static bool IsWithinRoot(string root, string candidate)
    {
        var normalizedRoot = root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return candidate.StartsWith(normalizedRoot, PathComparison) ||
               string.Equals(candidate, root.TrimEnd(Path.DirectorySeparatorChar), PathComparison);
    }

    private static StringComparison PathComparison => OperatingSystem.IsWindows()
        ? StringComparison.OrdinalIgnoreCase
        : StringComparison.Ordinal;
}

public sealed class LocalFileStore(
    IWorkspacePathAccessGuard pathAccessGuard,
    IStorageCatalogService catalogService,
    IStorageDriverRegistry driverRegistry) : IFileStore, IStorageCompatibilityFileStoreAdapter
{
    public async Task<string> SaveTextAsync(string relativePath, string content, CancellationToken cancellationToken = default)
    {
        var fullPath = ResolveWorkspacePath(relativePath);
        var (storage, driver) = await ResolveFileSystemDriverAsync(cancellationToken);
        await driver.SaveAsync(
            storage,
            new StorageWriteRequest(
                Path.GetFileName(relativePath),
                "text/plain",
                Encoding.UTF8.GetBytes(content),
                StorageUsagePurpose.Unknown,
                StorageContentKind.Text,
                RelativePathHint: relativePath),
            cancellationToken);
        return fullPath;
    }

    public async Task<string> SaveBytesAsync(string relativePath, byte[] content, CancellationToken cancellationToken = default)
    {
        var fullPath = ResolveWorkspacePath(relativePath);
        var (storage, driver) = await ResolveFileSystemDriverAsync(cancellationToken);
        await driver.SaveAsync(
            storage,
            new StorageWriteRequest(
                Path.GetFileName(relativePath),
                "application/octet-stream",
                content,
                StorageUsagePurpose.Unknown,
                RelativePathHint: relativePath),
            cancellationToken);
        return fullPath;
    }

    public async Task<string?> ReadTextAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        var fullPath = ResolveWorkspacePath(relativePath);
        if (!File.Exists(fullPath))
        {
            return null;
        }

        var (storage, driver) = await ResolveFileSystemDriverAsync(cancellationToken);
        await using var stream = await driver.OpenReadAsync(
            storage,
            new StorageObjectReference(
                storage.Id,
                StorageProviderKind.FileSystem,
                StorageLocatorKind.RelativePath,
                relativePath.Replace('\\', '/').TrimStart('/')),
            cancellationToken);
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: false);
        return await reader.ReadToEndAsync(cancellationToken);
    }

    private string ResolveWorkspacePath(string relativePath)
    {
        var resolution = pathAccessGuard.ResolveWorkspacePath(relativePath);
        if (!resolution.IsSuccess)
        {
            throw new InvalidOperationException(resolution.Message);
        }

        return resolution.FullPath;
    }

    private async Task<(StorageCatalogRecord Storage, IStorageDriver Driver)> ResolveFileSystemDriverAsync(CancellationToken cancellationToken)
    {
        var storage = await catalogService.EnsureBootstrapFileSystemStorageAsync(cancellationToken);
        return (storage, driverRegistry.Resolve(StorageProviderKind.FileSystem));
    }
}

public sealed class ManagedArtifactStore(IFileStore fileStore) : IManagedArtifactStore, IStorageCompatibilityArtifactStoreAdapter
{
    public string GetRelativePath(string category, string fileName) => Path.Combine("managed-files", category, fileName);

    public Task<string> SaveTextAsync(string category, string fileName, string content, CancellationToken cancellationToken = default)
        => fileStore.SaveTextAsync(GetRelativePath(category, fileName), content, cancellationToken);
}
