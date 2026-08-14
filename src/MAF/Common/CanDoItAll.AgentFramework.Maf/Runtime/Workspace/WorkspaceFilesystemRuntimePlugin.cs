using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.FileSystem;

namespace CanDoItAll.AgentFramework.Maf;

internal sealed class WorkspaceFilesystemRuntimePlugin(
    IWorkspaceFileService fileService,
    string workspaceRoot,
    IPhysicalFileSystemPathPolicyFactory physicalPathPolicyFactory,
    WorkspaceScopeDescriptor workspaceScope,
    AgentWorkspaceToolAccessSettings accessSettings)
{
    private static readonly HashSet<string> ProtectedExternalTargetDirectoryNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "components",
        "domain",
        "features",
        "models",
        "pages",
        "properties",
        "services",
        "source",
        "src",
        "test",
        "tests",
        "wwwroot"
    };

    private readonly IWorkspaceFileService fileService = fileService ?? throw new ArgumentNullException(nameof(fileService));
    private readonly string workspaceRoot = Path.GetFullPath(workspaceRoot);
    private readonly WorkspaceRuntimeFileAccessGuard fileAccess = new(
        workspaceRoot,
        physicalPathPolicyFactory,
        workspaceScope,
        accessSettings);

    public WorkspaceFileListResult ListWorkspaceDirectory(string? relativePath = null, int maxResults = 100)
    {
        var allowedPath = PrepareFileReadPath(relativePath);
        return ExecuteWithSafeAccessDenial(
            allowedPath ?? ".",
            () => fileService.ListDirectory(allowedPath, maxResults));
    }

    public WorkspaceFileListResult ListWorkspaceFiles(string? relativePath = null, string searchPattern = "*", int maxResults = 100)
    {
        var allowedPath = PrepareFileReadPath(relativePath);
        return ExecuteWithSafeAccessDenial(
            allowedPath ?? ".",
            () => fileService.ListFiles(allowedPath, searchPattern, maxResults));
    }

    public WorkspaceTextSearchResult SearchWorkspace(string query, string? relativePath = null, int maxResults = 20)
    {
        var allowedPath = PrepareFileReadPath(relativePath);
        return ExecuteWithSafeAccessDenial(
            allowedPath ?? ".",
            () => fileService.SearchText(query, allowedPath, maxResults));
    }

    public WorkspaceTextFileReadResult ReadWorkspaceTextFile(string path, int maxCharacters = 12000)
    {
        var allowedPath = PrepareFileReadPath(path) ?? path;
        return ExecuteWithSafeAccessDenial(
            allowedPath,
            () => fileService.ReadTextFile(allowedPath, maxCharacters));
    }

    public WorkspacePathStatResult StatWorkspacePath(string path)
    {
        var allowedPath = PrepareFileReadPath(path) ?? path;
        return ExecuteWithSafeAccessDenial(
            allowedPath,
            () => fileService.StatPath(allowedPath));
    }

    public WorkspacePathHashResult HashWorkspacePath(string path, int maxFiles = 200, long maxBytes = 10485760)
    {
        var allowedPath = PrepareFileReadPath(path) ?? path;
        return ExecuteWithSafeAccessDenial(
            allowedPath,
            () => fileService.HashPath(allowedPath, maxFiles, maxBytes));
    }

    public WorkspaceFileMutationResult CreateWorkspaceDirectory(string path)
    {
        var allowedPath = PrepareFileWritePath(path) ?? path;
        return ExecuteWithSafeAccessDenial(
            allowedPath,
            () => fileService.CreateDirectory(allowedPath));
    }

    public WorkspaceFileMutationResult WriteWorkspaceTextFile(string path, string content, bool overwrite = true)
    {
        var allowedPath = PrepareFileWritePath(path) ?? path;
        var authorityRootPath = ResolveFileWriteAuthorityRoot(allowedPath);
        return ExecuteWithSafeAccessDenial(
            allowedPath,
            () => fileService.WriteTextFile(allowedPath, content, overwrite, authorityRootPath));
    }

    public WorkspaceFileMutationResult AppendWorkspaceTextFile(string path, string content)
    {
        var allowedPath = PrepareFileWritePath(path) ?? path;
        var authorityRootPath = ResolveFileWriteAuthorityRoot(allowedPath);
        return ExecuteWithSafeAccessDenial(
            allowedPath,
            () => fileService.AppendTextFile(allowedPath, content, authorityRootPath));
    }

    public WorkspaceFileMutationResult CopyWorkspacePath(string sourcePath, string destinationPath, bool overwrite = false)
    {
        var allowedSourcePath = PrepareFileReadPath(sourcePath) ?? sourcePath;
        var allowedDestinationPath = PrepareFileWritePath(destinationPath) ?? destinationPath;
        EnsureNoReadOnlyDescendantMutation(
            allowedDestinationPath,
            WorkspaceReadOnlyAncestorMutationOperation.CopyOrReplace);
        return ExecuteWithSafeAccessDenial(
            allowedSourcePath,
            allowedDestinationPath,
            () => fileService.CopyPath(
                allowedSourcePath,
                allowedDestinationPath,
                overwrite,
                ResolveFileWriteAuthorityRoot(allowedDestinationPath)));
    }

    public WorkspaceFileMutationResult MoveWorkspacePath(string sourcePath, string destinationPath, bool overwrite = false)
    {
        var allowedSourcePath = PrepareFileWritePath(sourcePath) ?? sourcePath;
        var allowedDestinationPath = PrepareFileWritePath(destinationPath) ?? destinationPath;
        EnsureNoReadOnlyDescendantMutation(
            allowedSourcePath,
            WorkspaceReadOnlyAncestorMutationOperation.Move);
        EnsureNoReadOnlyDescendantMutation(
            allowedDestinationPath,
            WorkspaceReadOnlyAncestorMutationOperation.MoveOrReplace);
        return ExecuteWithSafeAccessDenial(
            allowedSourcePath,
            allowedDestinationPath,
            () => fileService.MovePath(
                allowedSourcePath,
                allowedDestinationPath,
                overwrite,
                ResolveFileWriteAuthorityRoot(allowedDestinationPath)));
    }

    public WorkspaceFileMutationResult DeleteWorkspacePath(string path, bool recursive = false)
    {
        var allowedPath = PrepareFileWritePath(path) ?? path;
        EnsureDeleteAllowed(allowedPath, recursive);
        return ExecuteWithSafeAccessDenial(
            allowedPath,
            () => fileService.DeletePath(allowedPath, recursive));
    }

    public WorkspaceArchiveMutationResult ZipWorkspacePath(
        string sourcePath,
        string destinationPath,
        bool overwrite = false,
        int maxFiles = 200,
        long maxBytes = 10485760)
    {
        var allowedSourcePath = PrepareFileReadPath(sourcePath) ?? sourcePath;
        var allowedDestinationPath = PrepareFileWritePath(destinationPath) ?? destinationPath;
        return ExecuteWithSafeAccessDenial(
            allowedSourcePath,
            allowedDestinationPath,
            () => fileService.ZipPath(allowedSourcePath, allowedDestinationPath, overwrite, maxFiles, maxBytes));
    }

    public WorkspaceArchiveMutationResult UnzipWorkspaceArchive(
        string sourcePath,
        string destinationPath,
        bool overwrite = false,
        int maxFiles = 200,
        long maxBytes = 10485760)
    {
        var allowedSourcePath = PrepareFileReadPath(sourcePath) ?? sourcePath;
        var allowedDestinationPath = PrepareFileWritePath(destinationPath) ?? destinationPath;
        EnsureNoReadOnlyDescendantMutation(
            allowedDestinationPath,
            WorkspaceReadOnlyAncestorMutationOperation.ExtractInto);
        return ExecuteWithSafeAccessDenial(
            allowedSourcePath,
            allowedDestinationPath,
            () => fileService.UnzipArchive(
                allowedSourcePath,
                allowedDestinationPath,
                overwrite,
                maxFiles,
                maxBytes,
                ResolveFileWriteAuthorityRoot(allowedDestinationPath)));
    }

    public WorkspaceTextDiffResult DiffWorkspaceTextFiles(string leftPath, string rightPath, int maxLines = 160)
    {
        var allowedLeftPath = PrepareFileReadPath(leftPath) ?? leftPath;
        var allowedRightPath = PrepareFileReadPath(rightPath) ?? rightPath;
        return ExecuteWithSafeAccessDenial(
            allowedLeftPath,
            allowedRightPath,
            () => fileService.DiffTextFiles(allowedLeftPath, allowedRightPath, maxLines));
    }

    private static TResult ExecuteWithSafeAccessDenial<TResult>(
        string requestedPath,
        Func<TResult> operation)
    {
        try
        {
            return operation();
        }
        catch (UnauthorizedAccessException)
        {
            throw WorkspaceToolAccessDeniedException.InaccessiblePath(requestedPath);
        }
        catch (WorkspacePathResolutionException exception)
        {
            throw CreatePathInputFailure(exception);
        }
    }

    private static TResult ExecuteWithSafeAccessDenial<TResult>(
        string firstRequestedPath,
        string secondRequestedPath,
        Func<TResult> operation)
    {
        try
        {
            return operation();
        }
        catch (UnauthorizedAccessException)
        {
            throw WorkspaceToolAccessDeniedException.InaccessiblePaths(
                firstRequestedPath,
                secondRequestedPath);
        }
        catch (WorkspacePathResolutionException exception)
        {
            throw CreatePathInputFailure(exception);
        }
    }

    private static AgentToolInputValidationException CreatePathInputFailure(
        WorkspacePathResolutionException exception)
        => AgentToolInputValidationException.Create(
            $"{exception.SafeMessage} Supply a corrected workspace path and retry.");

    private string? PrepareFileReadPath(string? path)
        => fileAccess.PrepareFileReadPath(path);

    private string? PrepareFileWritePath(string? path)
        => fileAccess.PrepareFileWritePath(path);

    private void EnsureDeleteAllowed(string path, bool recursive)
    {
        var normalizedAlias = AgentWorkspaceToolAccessMetadata.NormalizeExternalTargetAlias(path);
        if (string.IsNullOrWhiteSpace(normalizedAlias))
        {
            return;
        }

        var externalAccess = fileAccess.ResolveExternalTargetAccess();
        if (recursive && externalAccess.HasEffectiveReadOnlyDescendant(normalizedAlias))
        {
            throw WorkspaceToolAccessDeniedException.RecursiveDeleteReadOnlyAncestor(normalizedAlias);
        }

        var allowedAliases = externalAccess.WritableAliases
            .Select(AgentWorkspaceToolAccessMetadata.NormalizeExternalTargetAlias)
            .Where(alias => !string.IsNullOrWhiteSpace(alias))
            .Select(alias => TrimExternalTargetAlias(alias!))
            .ToArray();
        var normalizedDeleteAlias = TrimExternalTargetAlias(normalizedAlias);

        foreach (var allowedAlias in allowedAliases)
        {
            if (string.Equals(normalizedDeleteAlias, allowedAlias, StringComparison.OrdinalIgnoreCase))
            {
                throw WorkspaceToolAccessDeniedException.GroundedTargetRootDelete(normalizedDeleteAlias);
            }

            if (recursive &&
                IsProtectedExternalTargetDirectoryDelete(normalizedDeleteAlias, allowedAlias))
            {
                throw WorkspaceToolAccessDeniedException.ProtectedProductDirectoryDelete(normalizedDeleteAlias);
            }
        }
    }

    private void EnsureNoReadOnlyDescendantMutation(
        string path,
        WorkspaceReadOnlyAncestorMutationOperation operation)
    {
        var normalizedAlias = AgentWorkspaceToolAccessMetadata.NormalizeExternalTargetAlias(path);
        if (string.IsNullOrWhiteSpace(normalizedAlias) ||
            !fileAccess.ResolveExternalTargetAccess().HasEffectiveReadOnlyDescendant(normalizedAlias))
        {
            return;
        }

        throw WorkspaceToolAccessDeniedException.ReadOnlyAncestorMutation(operation, normalizedAlias);
    }

    private string ResolveFileWriteAuthorityRoot(string path)
    {
        if (fileAccess.IsManagedWorkspaceAbsolutePath(path))
        {
            return workspaceRoot;
        }

        var normalizedPath = AgentWorkspaceToolAccessMetadata.NormalizeExternalTargetAlias(path);
        if (string.IsNullOrWhiteSpace(normalizedPath))
        {
            return workspaceRoot;
        }

        return fileAccess.ResolveExternalTargetAccess().WritableAliases
                   .Select(AgentWorkspaceToolAccessMetadata.NormalizeExternalTargetAlias)
                   .Where(alias =>
                       !string.IsNullOrWhiteSpace(alias) &&
                       (string.Equals(normalizedPath, alias, StringComparison.OrdinalIgnoreCase) ||
                        normalizedPath.StartsWith(alias + "/", StringComparison.OrdinalIgnoreCase)))
                   .OrderByDescending(alias => alias!.Length)
                   .FirstOrDefault()
               ?? normalizedPath;
    }

    private static bool IsProtectedExternalTargetDirectoryDelete(string normalizedDeleteAlias, string allowedAlias)
    {
        var allowedAliasPrefix = EnsureExternalAliasTrailingSlash(allowedAlias);
        if (!normalizedDeleteAlias.StartsWith(allowedAliasPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var relativePath = normalizedDeleteAlias[allowedAliasPrefix.Length..].Trim('/');
        var firstSegment = relativePath
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault();
        return !string.IsNullOrWhiteSpace(firstSegment) &&
               ProtectedExternalTargetDirectoryNames.Contains(firstSegment);
    }

    private static string TrimExternalTargetAlias(string alias)
        => alias.Trim().TrimEnd('/');

    private static string EnsureExternalAliasTrailingSlash(string alias)
    {
        var trimmedAlias = TrimExternalTargetAlias(alias);
        return trimmedAlias.EndsWith('/')
            ? trimmedAlias
            : trimmedAlias + "/";
    }

}
