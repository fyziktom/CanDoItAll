using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Maf;

public sealed partial class MafAgentRuntime
{
    private sealed class WorkspaceRuntimePlugin(
        IWorkspaceFileService fileService,
        IWorkspaceCommandExecutionService commandExecutionService,
        IWorkspaceArtifactToolService artifactToolService)
    {
        private readonly IWorkspaceFileService fileService = fileService;
        private readonly IWorkspaceCommandExecutionService commandExecutionService = commandExecutionService;
        private readonly IWorkspaceArtifactToolService artifactToolService = artifactToolService;

        public WorkspaceFileListResult ListWorkspaceFiles(string? relativePath = null, string searchPattern = "*", int maxResults = 100)
            => fileService.ListFiles(relativePath, searchPattern, maxResults);

        public WorkspaceTextSearchResult SearchWorkspace(string query, string? relativePath = null, int maxResults = 20)
            => fileService.SearchText(query, relativePath, maxResults);

        public WorkspaceTextFileReadResult ReadWorkspaceTextFile(string path, int maxCharacters = 12000)
            => fileService.ReadTextFile(path, maxCharacters);

        public WorkspacePathStatResult StatWorkspacePath(string path)
            => fileService.StatPath(path);

        public WorkspaceFileMutationResult CreateWorkspaceDirectory(string path)
            => fileService.CreateDirectory(path);

        public WorkspaceFileMutationResult WriteWorkspaceTextFile(string path, string content, bool overwrite = true)
            => fileService.WriteTextFile(path, content, overwrite);

        public WorkspaceFileMutationResult AppendWorkspaceTextFile(string path, string content)
            => fileService.AppendTextFile(path, content);

        public WorkspaceFileMutationResult CopyWorkspacePath(string sourcePath, string destinationPath, bool overwrite = false)
            => fileService.CopyPath(sourcePath, destinationPath, overwrite);

        public WorkspaceFileMutationResult MoveWorkspacePath(string sourcePath, string destinationPath, bool overwrite = false)
            => fileService.MovePath(sourcePath, destinationPath, overwrite);

        public WorkspaceFileMutationResult DeleteWorkspacePath(string path, bool recursive = false)
            => fileService.DeletePath(path, recursive);

        public WorkspaceTextDiffResult DiffWorkspaceTextFiles(string leftPath, string rightPath, int maxLines = 160)
            => fileService.DiffTextFiles(leftPath, rightPath, maxLines);

        public WorkspaceCommandExecutionResult GetWorkspaceExecutionBoundary()
            => commandExecutionService.GetExecutionBoundary();

        public Task<WorkspaceCommandExecutionResult> GitWorkspaceStatus(bool includeBranch = true, string? workingDirectory = null, int timeoutSeconds = 30)
            => commandExecutionService.GitStatus(includeBranch, workingDirectory, timeoutSeconds);

        public Task<WorkspaceCommandExecutionResult> GitWorkspaceDiff(string? path = null, bool nameOnly = false, string? workingDirectory = null, int timeoutSeconds = 30)
            => commandExecutionService.GitDiff(path, nameOnly, workingDirectory, timeoutSeconds);

        public Task<WorkspaceCommandExecutionResult> DotnetWorkspaceRestore(string? targetPath = null, string? workingDirectory = null, int timeoutSeconds = 600)
            => commandExecutionService.DotnetRestore(targetPath, workingDirectory, timeoutSeconds);

        public Task<WorkspaceCommandExecutionResult> DotnetWorkspaceBuild(string? targetPath = null, string configuration = "Debug", bool noRestore = false, string? workingDirectory = null, int timeoutSeconds = 600)
            => commandExecutionService.DotnetBuild(targetPath, configuration, noRestore, workingDirectory, timeoutSeconds);

        public Task<WorkspaceCommandExecutionResult> DotnetWorkspaceTest(string? targetPath = null, string configuration = "Debug", string? filter = null, bool noBuild = false, bool noRestore = false, string? workingDirectory = null, int timeoutSeconds = 1200)
            => commandExecutionService.DotnetTest(targetPath, configuration, filter, noBuild, noRestore, workingDirectory, timeoutSeconds);

        public Task<WorkspaceCommandExecutionResult> DotnetWorkspaceNew(string template, string name, string? parentDirectory = null, bool force = false, int timeoutSeconds = 300)
            => commandExecutionService.DotnetNew(template, name, parentDirectory, force, timeoutSeconds);

        public Task<WorkspaceCommandExecutionResult> RunWorkspacePythonFile(string path, string[]? arguments = null, string? workingDirectory = null, int timeoutSeconds = 300)
            => commandExecutionService.PythonRunFile(path, arguments, workingDirectory, timeoutSeconds);

        public Task<WorkspaceCommandExecutionResult> RunWorkspacePowerShellScript(string path, string[]? arguments = null, string[]? outputPaths = null, string? workingDirectory = null, int timeoutSeconds = 300)
            => commandExecutionService.PowerShellRunScript(path, arguments, outputPaths, workingDirectory, timeoutSeconds);

        public Task<WorkspaceDocumentConversionResult> ConvertDocumentToMarkdown(string path, string? outputPath = null, int previewCharacters = 4000)
            => artifactToolService.ConvertDocumentToMarkdown(path, outputPath, previewCharacters);

        public Task<WorkspaceSpreadsheetInspectionResult> InspectSpreadsheetFile(string path, int maxRows = 8, int maxColumns = 8, int previewCharacters = 4000)
            => artifactToolService.InspectSpreadsheetFile(path, maxRows, maxColumns, previewCharacters);
    }
}
