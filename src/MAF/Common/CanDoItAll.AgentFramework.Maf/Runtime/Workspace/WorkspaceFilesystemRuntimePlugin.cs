using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Maf;

internal sealed class WorkspaceFilesystemRuntimePlugin(
    IWorkspaceFileService fileService,
    string workspaceRoot,
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
    private readonly string workspaceRootWithSeparator = EnsureTrailingSeparator(Path.GetFullPath(workspaceRoot));
    private readonly WorkspaceScopeDescriptor workspaceScope = workspaceScope;
    private readonly AgentWorkspaceToolAccessSettings accessSettings = AgentWorkspaceToolAccessMetadata.Normalize(accessSettings);

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
    }

    private string? PrepareFileReadPath(string? path)
    {
        EnsureFileReadAllowed(path);
        return NormalizeRecoverableCurrentRunArtifactPath(NormalizeAllowedExternalPathForWorkspaceTools(path));
    }

    private string? PrepareFileWritePath(string? path)
    {
        EnsureFileWriteAllowed(path);
        return NormalizeAllowedExternalPathForWorkspaceTools(path);
    }

    private void EnsureFileReadAllowed(string? path)
    {
        if (!accessSettings.CanReadFiles && !accessSettings.CanWriteFiles)
        {
            throw WorkspaceToolAccessDeniedException.FileReadDisabled();
        }

        EnsureExternalAliasAllowed(path, requireWrite: false);
    }

    private void EnsureFileWriteAllowed(string? path)
    {
        if (!accessSettings.CanWriteFiles)
        {
            throw WorkspaceToolAccessDeniedException.FileWriteDisabled();
        }

        EnsureExternalAliasAllowed(path, requireWrite: true);
    }

    private void EnsureExternalAliasAllowed(string? path, bool requireWrite)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        var normalizedAlias = AgentWorkspaceToolAccessMetadata.NormalizeExternalTargetAlias(path);
        if (string.IsNullOrWhiteSpace(normalizedAlias))
        {
            return;
        }

        if (IsManagedWorkspaceAbsolutePath(path))
        {
            return;
        }

        var externalAccess = ResolveExternalTargetAccess();
        if (requireWrite && externalAccess.CanWrite(normalizedAlias))
        {
            return;
        }

        if (!requireWrite && externalAccess.CanRead(normalizedAlias))
        {
            return;
        }

        if (requireWrite && externalAccess.CanRead(normalizedAlias))
        {
            throw WorkspaceToolAccessDeniedException.ExternalTargetReadOnly(normalizedAlias);
        }

        throw WorkspaceToolAccessDeniedException.ExternalTargetNotAuthorized(normalizedAlias);
    }

    private void EnsureDeleteAllowed(string path, bool recursive)
    {
        var normalizedAlias = AgentWorkspaceToolAccessMetadata.NormalizeExternalTargetAlias(path);
        if (string.IsNullOrWhiteSpace(normalizedAlias))
        {
            return;
        }

        var externalAccess = ResolveExternalTargetAccess();
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
            !ResolveExternalTargetAccess().HasEffectiveReadOnlyDescendant(normalizedAlias))
        {
            return;
        }

        throw WorkspaceToolAccessDeniedException.ReadOnlyAncestorMutation(operation, normalizedAlias);
    }

    private string? NormalizeAllowedExternalPathForWorkspaceTools(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) ||
            IsManagedWorkspaceAbsolutePath(path))
        {
            return path;
        }

        var normalizedAlias = AgentWorkspaceToolAccessMetadata.NormalizeExternalTargetAlias(path);
        return string.IsNullOrWhiteSpace(normalizedAlias)
            ? path
            : normalizedAlias;
    }

    private string? NormalizeRecoverableCurrentRunArtifactPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return path;
        }

        var auditScope = WorkspaceExecutionAuditContext.Current;
        var currentRunId = auditScope?.ProcessRunId;
        var currentWorkspaceScope = auditScope?.ContextWorkspaceScope ?? workspaceScope;
        return WorkspaceProcessRunArtifactPath.TryBuildRecoverableCurrentRunPath(
            path,
            currentRunId,
            currentWorkspaceScope,
            out var currentRunPath)
            ? currentRunPath
            : path;
    }

    private EffectiveExternalTargetAccessScope ResolveExternalTargetAccess()
    {
        var auditScope = WorkspaceExecutionAuditContext.Current;
        return EffectiveExternalTargetAccessResolver.Resolve(
            accessSettings,
            auditScope?.AllowedExternalTargetAliases,
            auditScope?.ReadOnlyExternalTargetAliases);
    }

    private string ResolveFileWriteAuthorityRoot(string path)
    {
        if (IsManagedWorkspaceAbsolutePath(path))
        {
            return workspaceRoot;
        }

        var normalizedPath = AgentWorkspaceToolAccessMetadata.NormalizeExternalTargetAlias(path);
        if (string.IsNullOrWhiteSpace(normalizedPath))
        {
            return workspaceRoot;
        }

        return ResolveExternalTargetAccess().WritableAliases
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

    private bool IsManagedWorkspaceAbsolutePath(string path)
    {
        try
        {
            if (!Path.IsPathRooted(path))
            {
                return false;
            }

            var fullPath = Path.GetFullPath(path);
            return string.Equals(fullPath, workspaceRoot, StringComparison.OrdinalIgnoreCase) ||
                   fullPath.StartsWith(workspaceRootWithSeparator, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static string EnsureTrailingSeparator(string path)
    {
        return path.EndsWith(Path.DirectorySeparatorChar) || path.EndsWith(Path.AltDirectorySeparatorChar)
            ? path
            : path + Path.DirectorySeparatorChar;
    }
}
