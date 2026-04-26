using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

public sealed class WorkspaceFileService : IWorkspaceFileService
{
    private readonly WorkspaceFileQueryService queryService;
    private readonly WorkspaceFileMutationService mutationService;

    public WorkspaceFileService(string workspaceRoot, WorkspaceScopeDescriptor? workspaceScope = null)
    {
        var pathPolicy = new WorkspacePathPolicy(workspaceRoot, workspaceScope);
        var receiptWriter = new WorkspaceFileReceiptWriter(pathPolicy.WorkspaceRoot, pathPolicy.WorkspaceScope);
        var textContentGuard = new WorkspaceTextContentGuard();

        queryService = new WorkspaceFileQueryService(pathPolicy, receiptWriter, textContentGuard);
        mutationService = new WorkspaceFileMutationService(pathPolicy, receiptWriter);
    }

    public WorkspaceFileListResult ListFiles(string? relativePath = null, string searchPattern = "*", int maxResults = 100)
        => queryService.ListFiles(relativePath, searchPattern, maxResults);

    public WorkspaceTextSearchResult SearchText(string query, string? relativePath = null, int maxResults = 20)
        => queryService.SearchText(query, relativePath, maxResults);

    public WorkspaceTextFileReadResult ReadTextFile(string path, int maxCharacters = 12000)
        => queryService.ReadTextFile(path, maxCharacters);

    public WorkspacePathStatResult StatPath(string path)
        => queryService.StatPath(path);

    public WorkspaceFileMutationResult CreateDirectory(string path)
        => mutationService.CreateDirectory(path);

    public WorkspaceFileMutationResult WriteTextFile(string path, string content, bool overwrite = true)
        => mutationService.WriteTextFile(path, content, overwrite);

    public WorkspaceFileMutationResult AppendTextFile(string path, string content)
        => mutationService.AppendTextFile(path, content);

    public WorkspaceFileMutationResult CopyPath(string sourcePath, string destinationPath, bool overwrite = false)
        => mutationService.CopyPath(sourcePath, destinationPath, overwrite);

    public WorkspaceFileMutationResult MovePath(string sourcePath, string destinationPath, bool overwrite = false)
        => mutationService.MovePath(sourcePath, destinationPath, overwrite);

    public WorkspaceFileMutationResult DeletePath(string path, bool recursive = false)
        => mutationService.DeletePath(path, recursive);

    public WorkspaceTextDiffResult DiffTextFiles(string leftPath, string rightPath, int maxLines = 160)
        => queryService.DiffTextFiles(leftPath, rightPath, maxLines);
}
