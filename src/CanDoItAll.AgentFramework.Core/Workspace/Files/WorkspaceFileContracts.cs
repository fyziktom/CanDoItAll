using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

public interface IWorkspaceFileService
{
    WorkspaceFileListResult ListFiles(string? relativePath = null, string searchPattern = "*", int maxResults = 100);

    WorkspaceTextSearchResult SearchText(string query, string? relativePath = null, int maxResults = 20);

    WorkspaceTextFileReadResult ReadTextFile(string path, int maxCharacters = 12000);

    WorkspacePathStatResult StatPath(string path);

    WorkspacePathHashResult HashPath(string path, int maxFiles = 200, long maxBytes = 10485760);

    WorkspaceFileMutationResult CreateDirectory(string path);

    WorkspaceFileMutationResult WriteTextFile(string path, string content, bool overwrite = true);

    WorkspaceFileMutationResult AppendTextFile(string path, string content);

    WorkspaceFileMutationResult CopyPath(string sourcePath, string destinationPath, bool overwrite = false);

    WorkspaceFileMutationResult MovePath(string sourcePath, string destinationPath, bool overwrite = false);

    WorkspaceFileMutationResult DeletePath(string path, bool recursive = false);

    WorkspaceArchiveMutationResult ZipPath(string sourcePath, string destinationPath, bool overwrite = false, int maxFiles = 200, long maxBytes = 10485760);

    WorkspaceArchiveMutationResult UnzipArchive(string sourcePath, string destinationPath, bool overwrite = false, int maxFiles = 200, long maxBytes = 10485760);

    WorkspaceTextDiffResult DiffTextFiles(string leftPath, string rightPath, int maxLines = 160);
}
