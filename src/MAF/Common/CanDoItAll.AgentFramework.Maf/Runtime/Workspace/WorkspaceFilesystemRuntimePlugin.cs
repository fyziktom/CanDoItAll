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
        return fileService.ListDirectory(allowedPath, maxResults);
    }

    public WorkspaceFileListResult ListWorkspaceFiles(string? relativePath = null, string searchPattern = "*", int maxResults = 100)
    {
        var allowedPath = PrepareFileReadPath(relativePath);
        return fileService.ListFiles(allowedPath, searchPattern, maxResults);
    }

    public WorkspaceTextSearchResult SearchWorkspace(string query, string? relativePath = null, int maxResults = 20)
    {
        var allowedPath = PrepareFileReadPath(relativePath);
        return fileService.SearchText(query, allowedPath, maxResults);
    }

    public WorkspaceTextFileReadResult ReadWorkspaceTextFile(string path, int maxCharacters = 12000)
    {
        var allowedPath = PrepareFileReadPath(path) ?? path;
        return fileService.ReadTextFile(allowedPath, maxCharacters);
    }

    public WorkspacePathStatResult StatWorkspacePath(string path)
    {
        var allowedPath = PrepareFileReadPath(path) ?? path;
        return fileService.StatPath(allowedPath);
    }

    public WorkspacePathHashResult HashWorkspacePath(string path, int maxFiles = 200, long maxBytes = 10485760)
    {
        var allowedPath = PrepareFileReadPath(path) ?? path;
        return fileService.HashPath(allowedPath, maxFiles, maxBytes);
    }

    public WorkspaceFileMutationResult CreateWorkspaceDirectory(string path)
    {
        var allowedPath = PrepareFileWritePath(path) ?? path;
        return fileService.CreateDirectory(allowedPath);
    }

    public WorkspaceFileMutationResult WriteWorkspaceTextFile(string path, string content, bool overwrite = true)
    {
        var allowedPath = PrepareFileWritePath(path) ?? path;
        return fileService.WriteTextFile(allowedPath, content, overwrite);
    }

    public WorkspaceFileMutationResult AppendWorkspaceTextFile(string path, string content)
    {
        var allowedPath = PrepareFileWritePath(path) ?? path;
        return fileService.AppendTextFile(allowedPath, content);
    }

    public WorkspaceFileMutationResult CopyWorkspacePath(string sourcePath, string destinationPath, bool overwrite = false)
    {
        var allowedSourcePath = PrepareFileReadPath(sourcePath) ?? sourcePath;
        var allowedDestinationPath = PrepareFileWritePath(destinationPath) ?? destinationPath;
        return fileService.CopyPath(allowedSourcePath, allowedDestinationPath, overwrite);
    }

    public WorkspaceFileMutationResult MoveWorkspacePath(string sourcePath, string destinationPath, bool overwrite = false)
    {
        var allowedSourcePath = PrepareFileWritePath(sourcePath) ?? sourcePath;
        var allowedDestinationPath = PrepareFileWritePath(destinationPath) ?? destinationPath;
        return fileService.MovePath(allowedSourcePath, allowedDestinationPath, overwrite);
    }

    public WorkspaceFileMutationResult DeleteWorkspacePath(string path, bool recursive = false)
    {
        var allowedPath = PrepareFileWritePath(path) ?? path;
        EnsureDeleteAllowed(allowedPath, recursive);
        return fileService.DeletePath(allowedPath, recursive);
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
        return fileService.ZipPath(allowedSourcePath, allowedDestinationPath, overwrite, maxFiles, maxBytes);
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
        return fileService.UnzipArchive(allowedSourcePath, allowedDestinationPath, overwrite, maxFiles, maxBytes);
    }

    public WorkspaceTextDiffResult DiffWorkspaceTextFiles(string leftPath, string rightPath, int maxLines = 160)
    {
        var allowedLeftPath = PrepareFileReadPath(leftPath) ?? leftPath;
        var allowedRightPath = PrepareFileReadPath(rightPath) ?? rightPath;
        return fileService.DiffTextFiles(allowedLeftPath, allowedRightPath, maxLines);
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
            throw new InvalidOperationException("This agent is not allowed to read workspace files.");
        }

        EnsureExternalAliasAllowed(path, requireWrite: false);
    }

    private void EnsureFileWriteAllowed(string? path)
    {
        if (!accessSettings.CanWriteFiles)
        {
            throw new InvalidOperationException("This agent is not allowed to write workspace files.");
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

        var readOnlyAliases = ResolveReadOnlyExternalTargetAliases();
        if (requireWrite &&
            IsExternalTargetAliasAllowed(normalizedAlias, readOnlyAliases))
        {
            throw new InvalidOperationException(
                $"External workspace path '{normalizedAlias}' is read-only for this run.");
        }

        var allowedAliases = ResolveAllowedExternalTargetAliases();
        var isAllowedForRead = IsExternalTargetAliasAllowed(normalizedAlias, allowedAliases) ||
                               IsExternalTargetAliasAllowed(normalizedAlias, readOnlyAliases);
        if (!isAllowedForRead)
        {
            throw new InvalidOperationException(
                $"External workspace path '{normalizedAlias}' is not in this agent's allowed external workspace roots.");
        }

        if (requireWrite &&
            !IsExternalTargetAliasAllowed(normalizedAlias, allowedAliases))
        {
            throw new InvalidOperationException(
                $"External workspace path '{normalizedAlias}' is read-only for this run.");
        }
    }

    private void EnsureDeleteAllowed(string path, bool recursive)
    {
        var normalizedAlias = AgentWorkspaceToolAccessMetadata.NormalizeExternalTargetAlias(path);
        if (string.IsNullOrWhiteSpace(normalizedAlias))
        {
            return;
        }

        var allowedAliases = ResolveAllowedExternalTargetAliases()
            .Select(AgentWorkspaceToolAccessMetadata.NormalizeExternalTargetAlias)
            .Where(alias => !string.IsNullOrWhiteSpace(alias))
            .Select(alias => TrimExternalTargetAlias(alias!))
            .ToArray();
        var normalizedDeleteAlias = TrimExternalTargetAlias(normalizedAlias);

        foreach (var allowedAlias in allowedAliases)
        {
            if (string.Equals(normalizedDeleteAlias, allowedAlias, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Refusing to delete grounded external target root '{normalizedDeleteAlias}'. Repair the scaffold in place or delete only explicit generated evidence files.");
            }

            if (recursive &&
                IsProtectedExternalTargetDirectoryDelete(normalizedDeleteAlias, allowedAlias))
            {
                throw new InvalidOperationException(
                    $"Refusing to recursively delete protected external product directory '{normalizedDeleteAlias}'. Repair source and test files in place instead.");
            }
        }
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

    private IReadOnlyList<string> ResolveAllowedExternalTargetAliases()
    {
        var auditScope = WorkspaceExecutionAuditContext.Current;
        if (auditScope is not null &&
            (auditScope.AllowedExternalTargetAliases.Count > 0 ||
             auditScope.ReadOnlyExternalTargetAliases.Count > 0))
        {
            return auditScope.AllowedExternalTargetAliases
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        return accessSettings.AllowedExternalTargetAliases
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IReadOnlyList<string> ResolveReadOnlyExternalTargetAliases()
    {
        return WorkspaceExecutionAuditContext.Current?.ReadOnlyExternalTargetAliases
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray() ?? [];
    }

    private static bool IsExternalTargetAliasAllowed(
        string normalizedAlias,
        IReadOnlyList<string> allowedAliases)
    {
        return AgentWorkspaceToolAccessMetadata.IsExternalTargetAliasAllowed(
            normalizedAlias,
            allowedAliases);
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
