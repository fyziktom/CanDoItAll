using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

public static class WorkspaceFileLimits
{
    public const int MaxTextReadCharacters = 64_000;
    public const int MaxTextMutationBytes = 4 * 1024 * 1024;
}

public interface IWorkspaceFileInspectionService
{
    WorkspaceFileListResult ListFiles(
        string path,
        string searchPattern,
        int maxResults,
        string authorityRootPath);

    WorkspaceTextFileReadResult ReadTextFile(
        string path,
        int maxCharacters,
        string authorityRootPath);

    WorkspacePathStatResult StatPath(string path, string authorityRootPath);
}

public interface IWorkspaceFileService : IWorkspaceFileInspectionService
{
    WorkspaceFileListResult ListDirectory(string? relativePath = null, int maxResults = 100);

    WorkspaceFileListResult ListFiles(string? relativePath = null, string searchPattern = "*", int maxResults = 100);

    WorkspaceTextSearchResult SearchText(string query, string? relativePath = null, int maxResults = 20);

    WorkspaceTextFileReadResult ReadTextFile(string path, int maxCharacters = 12000);

    WorkspacePathStatResult StatPath(string path);

    WorkspacePathHashResult HashPath(string path, int maxFiles = 200, long maxBytes = 10485760);

    WorkspaceFileMutationResult CreateDirectory(string path);

    WorkspaceFileMutationResult WriteTextFile(string path, string content, bool overwrite = true);

    WorkspaceFileMutationResult WriteTextFile(
        string path,
        string content,
        bool overwrite,
        string authorityRootPath);

    WorkspaceFileMutationResult AppendTextFile(string path, string content);

    WorkspaceFileMutationResult AppendTextFile(
        string path,
        string content,
        string authorityRootPath);

    WorkspaceFileMutationResult CopyPath(string sourcePath, string destinationPath, bool overwrite = false);

    WorkspaceFileMutationResult CopyPath(
        string sourcePath,
        string destinationPath,
        bool overwrite,
        string destinationAuthorityRootPath);

    WorkspaceFileMutationResult MovePath(string sourcePath, string destinationPath, bool overwrite = false);

    WorkspaceFileMutationResult MovePath(
        string sourcePath,
        string destinationPath,
        bool overwrite,
        string destinationAuthorityRootPath);

    WorkspaceFileMutationResult DeletePath(string path, bool recursive = false);

    WorkspaceArchiveMutationResult ZipPath(string sourcePath, string destinationPath, bool overwrite = false, int maxFiles = 200, long maxBytes = 10485760);

    WorkspaceArchiveMutationResult UnzipArchive(string sourcePath, string destinationPath, bool overwrite = false, int maxFiles = 200, long maxBytes = 10485760);

    WorkspaceArchiveMutationResult UnzipArchive(
        string sourcePath,
        string destinationPath,
        bool overwrite,
        int maxFiles,
        long maxBytes,
        string destinationAuthorityRootPath);

    WorkspaceTextDiffResult DiffTextFiles(string leftPath, string rightPath, int maxLines = 160);
}
