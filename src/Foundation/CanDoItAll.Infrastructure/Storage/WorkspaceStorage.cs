using CanDoItAll.Infrastructure.Configuration;
using CanDoItAll.Infrastructure.ControlPlane;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using CanDoItAll.Infrastructure;
using CanDoItAll.Infrastructure.FileSystem;
using CanDoItAll.SharedKernel;
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
        var configuredRoot = string.IsNullOrWhiteSpace(activeProfile.Profile.Storage.WorkspaceRoot)
            ? ResolveConfiguredWorkspaceRoot()
            : activeProfile.Profile.Storage.WorkspaceRoot;
        PhysicalPathSyntaxPolicy.EnsureNativeOrRelative(configuredRoot, "workspace root");
        var root = Path.GetFullPath(configuredRoot);
        Directory.CreateDirectory(root);
        return root;
    }

    public string ResolveManagedFilesRoot() => EnsureDirectory(_options.ManagedFilesFolder);

    public string ResolveExportsRoot() => EnsureDirectory(_options.ExportsFolder);

    public string ResolveEvidenceRoot() => EnsureDirectory(_options.EvidenceFolder);

    public string ResolveManagerArtifactsRoot()
    {
        string root = string.IsNullOrWhiteSpace(_options.ManagerArtifactsFolder)
            ? Path.Combine(ApplicationPurposeRootPolicy.ResolveCurrent().StateRoot, "manager-artifacts")
            : ControlPlanePathDefaults.ResolveConfiguredPath(
                hostEnvironment.ContentRootPath,
                _options.ManagerArtifactsFolder);
        Directory.CreateDirectory(root);
        return root;
    }

    private string ResolveConfiguredWorkspaceRoot()
    {
        return string.IsNullOrWhiteSpace(_options.WorkspaceRoot)
            ? ApplicationPurposeRootPolicy.ResolveCurrent().WorkspaceRoot
            : ControlPlanePathDefaults.ResolveConfiguredPath(hostEnvironment.ContentRootPath, _options.WorkspaceRoot);
    }

    private string EnsureDirectory(string folder)
    {
        var path = Path.Combine(ResolveWorkspaceRoot(), folder);
        Directory.CreateDirectory(path);
        return path;
    }
}

public sealed class WorkspacePathAccessGuard(
    IWorkspacePathResolver resolver,
    IPhysicalFileSystemPathPolicyFactory physicalPathPolicyFactory) : IWorkspacePathAccessGuard
{
    public WorkspacePathAccessResult ResolveWorkspacePath(string path, string? basePath = null)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return WorkspacePathAccessResult.Failure("A workspace path is required.");
        }

        try
        {
            PhysicalPathSyntaxPolicy.EnsureNativeOrRelative(path.Trim(), "workspace path");
            if (!string.IsNullOrWhiteSpace(basePath))
            {
                PhysicalPathSyntaxPolicy.EnsureNativeOrRelative(basePath.Trim(), "workspace base path");
            }

            var resolvedWorkspaceRoot = resolver.ResolveWorkspaceRoot();
            PhysicalPathSyntaxPolicy.EnsureNativeOrRelative(resolvedWorkspaceRoot, "workspace root");
            var workspacePathPolicy = physicalPathPolicyFactory.Create(resolvedWorkspaceRoot);
            var workspaceRoot = workspacePathPolicy.RootPath;
            var resolutionBase = string.IsNullOrWhiteSpace(basePath)
                ? workspaceRoot
                : Path.GetFullPath(basePath);

            if (!workspacePathPolicy.IsWithinRoot(resolutionBase))
            {
                return WorkspacePathAccessResult.Failure("The resolved base path is outside the active workspace root.");
            }

            var candidate = ResolveCandidatePath(path, resolutionBase);
            return workspacePathPolicy.IsWithinRoot(candidate)
                ? WorkspacePathAccessResult.Success(candidate)
                : WorkspacePathAccessResult.Failure("The resolved path is outside the active workspace root.");
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or InvalidOperationException or NotSupportedException or PathTooLongException)
        {
            return WorkspacePathAccessResult.Failure(
                exception is InvalidOperationException
                    ? exception.Message
                    : "The resolved path is outside the active workspace root.");
        }
    }

    public WorkspacePathAccessResult ResolveManagedFilePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return WorkspacePathAccessResult.Failure("A managed file path is required.");
        }

        try
        {
            var trimmedPath = path.Trim();
            PhysicalPathSyntaxPolicy.EnsureNativeOrRelative(trimmedPath, "managed file path");
            var resolvedWorkspaceRoot = resolver.ResolveWorkspaceRoot();
            var resolvedManagedFilesRoot = resolver.ResolveManagedFilesRoot();
            PhysicalPathSyntaxPolicy.EnsureNativeOrRelative(resolvedWorkspaceRoot, "workspace root");
            PhysicalPathSyntaxPolicy.EnsureNativeOrRelative(resolvedManagedFilesRoot, "managed files root");
            var workspacePathPolicy = physicalPathPolicyFactory.Create(resolvedWorkspaceRoot);
            var managedFilesPathPolicy = physicalPathPolicyFactory.Create(resolvedManagedFilesRoot);
            var workspaceRoot = workspacePathPolicy.RootPath;
            var managedFilesRoot = managedFilesPathPolicy.RootPath;
            var managedFilesRelativeRoot = NormalizeRelativePath(Path.GetRelativePath(workspaceRoot, managedFilesRoot));
            var candidate = Path.IsPathRooted(trimmedPath)
                ? Path.GetFullPath(trimmedPath)
                : ResolveRelativeManagedFilePath(trimmedPath, workspaceRoot, managedFilesRoot, managedFilesRelativeRoot);

            return managedFilesPathPolicy.IsWithinRoot(candidate)
                ? WorkspacePathAccessResult.Success(candidate)
                : WorkspacePathAccessResult.Failure("The resolved path is outside the active managed files root.");
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or InvalidOperationException or NotSupportedException or PathTooLongException)
        {
            return WorkspacePathAccessResult.Failure(
                exception is InvalidOperationException
                    ? exception.Message
                    : "The resolved path is outside the active managed files root.");
        }
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

        return normalizedPath.Equals(managedFilesRelativeRoot, StringComparison.Ordinal) ||
               (!string.IsNullOrWhiteSpace(managedPrefix) &&
                normalizedPath.StartsWith(managedPrefix, StringComparison.Ordinal))
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
        var trimmedPath = path.Trim();
        if (string.Equals(trimmedPath, ".", StringComparison.Ordinal))
        {
            return trimmedPath;
        }

        var logicalPath = LogicalPath.ParseLegacyWindowsLogicalPath(trimmedPath);
        return Path.Combine(logicalPath.Segments.ToArray());
    }

}

public sealed class LocalFileStore(
    IWorkspacePathAccessGuard pathAccessGuard,
    IStorageCatalogService catalogService,
    IStorageDriverRegistry driverRegistry) : IFileStore, IStorageCompatibilityFileStoreAdapter
{
    public async Task<string> SaveTextAsync(string relativePath, string content, CancellationToken cancellationToken = default)
    {
        var logicalPath = NormalizeLogicalStoragePath(relativePath);
        var fullPath = ResolveWorkspacePath(logicalPath);
        var (storage, driver) = await ResolveFileSystemDriverAsync(cancellationToken);
        await driver.SaveAsync(
            storage,
            new StorageWriteRequest(
                Path.GetFileName(logicalPath),
                "text/plain",
                Encoding.UTF8.GetBytes(content),
                StorageUsagePurpose.Unknown,
                StorageContentKind.Text,
                RelativePathHint: logicalPath),
            cancellationToken);
        return fullPath;
    }

    public async Task<string> SaveBytesAsync(string relativePath, byte[] content, CancellationToken cancellationToken = default)
    {
        var logicalPath = NormalizeLogicalStoragePath(relativePath);
        var fullPath = ResolveWorkspacePath(logicalPath);
        var (storage, driver) = await ResolveFileSystemDriverAsync(cancellationToken);
        await driver.SaveAsync(
            storage,
            new StorageWriteRequest(
                Path.GetFileName(logicalPath),
                "application/octet-stream",
                content,
                StorageUsagePurpose.Unknown,
                RelativePathHint: logicalPath),
            cancellationToken);
        return fullPath;
    }

    public async Task<string?> ReadTextAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        var logicalPath = NormalizeLogicalStoragePath(relativePath);
        var fullPath = ResolveWorkspacePath(logicalPath);
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
                logicalPath),
            cancellationToken);
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: false);
        return await reader.ReadToEndAsync(cancellationToken);
    }

    private static string NormalizeLogicalStoragePath(string relativePath)
    {
        return LogicalPath.ParseLegacyWindowsLogicalPath(relativePath).Value;
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
    public string GetRelativePath(string category, string fileName)
        => LogicalPath.ParseLegacyWindowsLogicalPath($"managed-files/{category}/{fileName}").Value;

    public Task<string> SaveTextAsync(string category, string fileName, string content, CancellationToken cancellationToken = default)
        => fileStore.SaveTextAsync(GetRelativePath(category, fileName), content, cancellationToken);
}
