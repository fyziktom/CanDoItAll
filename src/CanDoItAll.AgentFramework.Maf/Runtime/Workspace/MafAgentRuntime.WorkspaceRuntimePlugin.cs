using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Maf;

public sealed partial class MafAgentRuntime
{
    private sealed class WorkspaceRuntimePlugin(
        IWorkspaceFileService fileService,
        IWorkspaceCommandExecutionService commandExecutionService,
        IWorkspaceArtifactToolService artifactToolService,
        string workspaceRoot,
        AgentWorkspaceToolAccessSettings accessSettings)
    {
        private readonly IWorkspaceFileService fileService = fileService;
        private readonly IWorkspaceCommandExecutionService commandExecutionService = commandExecutionService;
        private readonly IWorkspaceArtifactToolService artifactToolService = artifactToolService;
        private readonly string workspaceRoot = Path.GetFullPath(workspaceRoot);
        private readonly string workspaceRootWithSeparator = EnsureTrailingSeparator(Path.GetFullPath(workspaceRoot));
        private readonly AgentWorkspaceToolAccessSettings accessSettings = AgentWorkspaceToolAccessMetadata.Normalize(accessSettings);

        public WorkspaceFileListResult ListWorkspaceFiles(string? relativePath = null, string searchPattern = "*", int maxResults = 100)
        {
            EnsureFileReadAllowed(relativePath);
            return fileService.ListFiles(relativePath, searchPattern, maxResults);
        }

        public WorkspaceTextSearchResult SearchWorkspace(string query, string? relativePath = null, int maxResults = 20)
        {
            EnsureFileReadAllowed(relativePath);
            return fileService.SearchText(query, relativePath, maxResults);
        }

        public WorkspaceTextFileReadResult ReadWorkspaceTextFile(string path, int maxCharacters = 12000)
        {
            EnsureFileReadAllowed(path);
            return fileService.ReadTextFile(path, maxCharacters);
        }

        public WorkspacePathStatResult StatWorkspacePath(string path)
        {
            EnsureFileReadAllowed(path);
            return fileService.StatPath(path);
        }

        public WorkspaceFileMutationResult CreateWorkspaceDirectory(string path)
        {
            EnsureFileWriteAllowed(path);
            return fileService.CreateDirectory(path);
        }

        public WorkspaceFileMutationResult WriteWorkspaceTextFile(string path, string content, bool overwrite = true)
        {
            EnsureFileWriteAllowed(path);
            return fileService.WriteTextFile(path, content, overwrite);
        }

        public WorkspaceFileMutationResult AppendWorkspaceTextFile(string path, string content)
        {
            EnsureFileWriteAllowed(path);
            return fileService.AppendTextFile(path, content);
        }

        public WorkspaceFileMutationResult CopyWorkspacePath(string sourcePath, string destinationPath, bool overwrite = false)
        {
            EnsureFileReadAllowed(sourcePath);
            EnsureFileWriteAllowed(destinationPath);
            return fileService.CopyPath(sourcePath, destinationPath, overwrite);
        }

        public WorkspaceFileMutationResult MoveWorkspacePath(string sourcePath, string destinationPath, bool overwrite = false)
        {
            EnsureFileWriteAllowed(sourcePath);
            EnsureFileWriteAllowed(destinationPath);
            return fileService.MovePath(sourcePath, destinationPath, overwrite);
        }

        public WorkspaceFileMutationResult DeleteWorkspacePath(string path, bool recursive = false)
        {
            EnsureFileWriteAllowed(path);
            return fileService.DeletePath(path, recursive);
        }

        public WorkspaceTextDiffResult DiffWorkspaceTextFiles(string leftPath, string rightPath, int maxLines = 160)
        {
            EnsureFileReadAllowed(leftPath);
            EnsureFileReadAllowed(rightPath);
            return fileService.DiffTextFiles(leftPath, rightPath, maxLines);
        }

        public WorkspaceCommandExecutionResult GetWorkspaceExecutionBoundary()
            => commandExecutionService.GetExecutionBoundary();

        public Task<WorkspaceCommandExecutionResult> GitWorkspaceStatus(bool includeBranch = true, string? workingDirectory = null, int timeoutSeconds = 30)
        {
            EnsureFileReadAllowed(workingDirectory);
            return commandExecutionService.GitStatus(includeBranch, workingDirectory, timeoutSeconds);
        }

        public Task<WorkspaceCommandExecutionResult> GitWorkspaceDiff(string? path = null, bool nameOnly = false, string? workingDirectory = null, int timeoutSeconds = 30)
        {
            EnsureFileReadAllowed(path);
            EnsureFileReadAllowed(workingDirectory);
            return commandExecutionService.GitDiff(path, nameOnly, workingDirectory, timeoutSeconds);
        }

        public Task<WorkspaceCommandExecutionResult> DotnetWorkspaceRestore(string? targetPath = null, string? workingDirectory = null, int timeoutSeconds = 600)
        {
            EnsureFileWriteAllowed(targetPath);
            EnsureFileWriteAllowed(workingDirectory);
            return commandExecutionService.DotnetRestore(targetPath, workingDirectory, timeoutSeconds);
        }

        public Task<WorkspaceCommandExecutionResult> DotnetWorkspaceBuild(string? targetPath = null, string configuration = "Debug", bool noRestore = false, string? workingDirectory = null, int timeoutSeconds = 600)
        {
            EnsureFileWriteAllowed(targetPath);
            EnsureFileWriteAllowed(workingDirectory);
            return commandExecutionService.DotnetBuild(targetPath, configuration, noRestore, workingDirectory, timeoutSeconds);
        }

        public Task<WorkspaceCommandExecutionResult> DotnetWorkspaceTest(string? targetPath = null, string configuration = "Debug", string? filter = null, bool noBuild = false, bool noRestore = false, string? workingDirectory = null, int timeoutSeconds = 1200)
        {
            EnsureFileWriteAllowed(targetPath);
            EnsureFileWriteAllowed(workingDirectory);
            return commandExecutionService.DotnetTest(targetPath, configuration, filter, noBuild, noRestore, workingDirectory, timeoutSeconds);
        }

        public Task<WorkspaceCommandExecutionResult> DotnetWorkspaceRun(string targetPath, string? url = null, string configuration = "Debug", bool noBuild = true, bool waitForHttp = true, string? workingDirectory = null, int startupTimeoutSeconds = 45, int timeoutSeconds = 120)
        {
            EnsureFileWriteAllowed(targetPath);
            EnsureFileWriteAllowed(workingDirectory);
            return commandExecutionService.DotnetRun(targetPath, url, configuration, noBuild, waitForHttp, workingDirectory, startupTimeoutSeconds, timeoutSeconds);
        }

        public Task<WorkspaceCommandExecutionResult> DotnetWorkspaceNew(string template, string name, string? parentDirectory = null, bool force = false, int timeoutSeconds = 300)
        {
            EnsureFileWriteAllowed(parentDirectory);
            return commandExecutionService.DotnetNew(template, name, parentDirectory, force, timeoutSeconds);
        }

        public Task<WorkspaceCommandExecutionResult> RunWorkspacePythonFile(string path, string[]? arguments = null, string? workingDirectory = null, int timeoutSeconds = 300)
        {
            EnsureFileReadAllowed(path);
            EnsureFileReadAllowed(workingDirectory);
            return commandExecutionService.PythonRunFile(path, arguments, workingDirectory, timeoutSeconds);
        }

        public Task<WorkspaceCommandExecutionResult> RunWorkspacePowerShellScript(string path, string[]? arguments = null, string[]? outputPaths = null, string? workingDirectory = null, int timeoutSeconds = 300)
        {
            EnsureFileReadAllowed(path);
            EnsureFileReadAllowed(workingDirectory);
            foreach (var outputPath in outputPaths ?? [])
            {
                EnsureFileWriteAllowed(outputPath);
            }

            return commandExecutionService.PowerShellRunScript(path, arguments, outputPaths, workingDirectory, timeoutSeconds);
        }

        public Task<WorkspaceDocumentConversionResult> ConvertDocumentToMarkdown(string path, string? outputPath = null, int previewCharacters = 4000)
        {
            EnsureFileReadAllowed(path);
            EnsureFileWriteAllowed(outputPath);
            return artifactToolService.ConvertDocumentToMarkdown(path, outputPath, previewCharacters);
        }

        public Task<WorkspaceSpreadsheetInspectionResult> InspectSpreadsheetFile(string path, int maxRows = 8, int maxColumns = 8, int previewCharacters = 4000)
        {
            EnsureFileReadAllowed(path);
            return artifactToolService.InspectSpreadsheetFile(path, maxRows, maxColumns, previewCharacters);
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

            if (!AgentWorkspaceToolAccessMetadata.IsExternalTargetAliasAllowed(
                    normalizedAlias,
                    accessSettings.AllowedExternalTargetAliases))
            {
                throw new InvalidOperationException(
                    $"External workspace path '{normalizedAlias}' is not in this agent's allowed external workspace roots.");
            }

            if (requireWrite && !accessSettings.CanWriteFiles)
            {
                throw new InvalidOperationException(
                    $"External workspace path '{normalizedAlias}' is read-only for this agent.");
            }
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
}
