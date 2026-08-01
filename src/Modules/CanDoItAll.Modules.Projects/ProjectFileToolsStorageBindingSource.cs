using CanDoItAll.FileTools.FileBrowser;
using CanDoItAll.FileTools.Integration;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Projects;

internal sealed class ProjectFileToolsStorageBindingSource(
    IDbContextFactory<AppDbContext> dbContextFactory,
    IStorageCatalogService storageCatalog,
    IWorkspacePathResolver workspacePathResolver) : IFileToolsStorageBindingSource
{
    private static readonly FileToolsBrowseWorkLimits WorkLimits = new(
        maximumReturnedItems: 50,
        maximumInspectedItems: 2_000,
        maximumMetadataProbes: 50,
        maximumConcurrentMetadataProbes: 1,
        maximumDuration: TimeSpan.FromSeconds(5));

    public FileToolsSemanticScopeKind ScopeKind => FileToolsSemanticScopeKind.Project;

    public async ValueTask<IReadOnlyList<FileToolsStorageBinding>> ResolveAsync(
        FileToolsSemanticScope scope,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scope);
        if (scope.Kind != ScopeKind ||
            !Guid.TryParseExact(scope.Id.Value, "N", out Guid projectId) ||
            projectId == Guid.Empty)
        {
            throw ProviderError(
                FileBrowserErrorCode.InvalidOperation,
                "The project file scope identifier is invalid.");
        }

        await using AppDbContext dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        bool projectExists = await dbContext.Set<Project>()
            .AsNoTracking()
            .AnyAsync(project => project.Id == projectId, cancellationToken);
        if (!projectExists)
        {
            throw ProviderError(
                FileBrowserErrorCode.NotFound,
                "The project file scope no longer exists.");
        }

        StorageCatalogRecord storage = await storageCatalog.EnsureBootstrapFileSystemStorageAsync(cancellationToken);
        var root = new FileToolsStorageRoot($"managed-files/project-media/files/{projectId:N}");
        EnsureProjectDirectory(root);
        return
        [
            new FileToolsStorageBinding(
                storage.Id,
                $"{scope.DisplayName} files",
                WorkLimits,
                root,
                FileToolsHostBrowseCacheMode.Disabled)
        ];
    }

    private void EnsureProjectDirectory(FileToolsStorageRoot root)
    {
        string workspaceRoot = Path.GetFullPath(workspacePathResolver.ResolveWorkspaceRoot());
        string projectDirectory = Path.GetFullPath(Path.Combine(
            workspaceRoot,
            root.Value.Replace('/', Path.DirectorySeparatorChar)));
        string workspacePrefix = workspaceRoot.TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!projectDirectory.StartsWith(workspacePrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw ProviderError(
                FileBrowserErrorCode.Forbidden,
                "The project file scope is outside the active workspace.");
        }

        Directory.CreateDirectory(projectDirectory);
    }

    private static FileBrowserProviderException ProviderError(FileBrowserErrorCode code, string message)
        => new(new FileBrowserError(code, message));
}
