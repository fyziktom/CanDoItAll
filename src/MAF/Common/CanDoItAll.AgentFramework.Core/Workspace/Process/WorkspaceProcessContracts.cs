using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

public interface IWorkspaceProcessHost
{
    ExecutionBoundaryDescriptor DescribeBoundary();

    Task<WorkspaceProcessExecutionResult> ExecuteAsync(WorkspaceProcessExecutionRequest request, CancellationToken cancellationToken = default);
}

public interface IWorkspaceCommandExecutionService
{
    ExecutionBoundaryDescriptor DescribeBoundary();

    WorkspaceCommandExecutionResult GetExecutionBoundary();

    Task<WorkspaceCommandExecutionResult> GitStatus(bool includeBranch = true, string? workingDirectory = null, int timeoutSeconds = 30);

    Task<WorkspaceCommandExecutionResult> GitDiff(string? path = null, bool nameOnly = false, string? workingDirectory = null, int timeoutSeconds = 30);

    Task<WorkspaceCommandExecutionResult> GitLog(int count = 10, string? workingDirectory = null, int timeoutSeconds = 30);

    Task<WorkspaceCommandExecutionResult> GitShow(string revision, string? workingDirectory = null, int timeoutSeconds = 30);

    Task<WorkspaceCommandExecutionResult> GitAdd(string[]? paths, string? workingDirectory = null, int timeoutSeconds = 30);

    Task<WorkspaceCommandExecutionResult> GitUnstage(string[]? paths, string? workingDirectory = null, int timeoutSeconds = 30);

    Task<WorkspaceCommandExecutionResult> GitCommit(string message, string? workingDirectory = null, int timeoutSeconds = 30);

    Task<WorkspaceCommandExecutionResult> GitBranchCreate(string branchName, string? workingDirectory = null, int timeoutSeconds = 30);

    Task<WorkspaceCommandExecutionResult> GitSwitch(string branchName, string? workingDirectory = null, int timeoutSeconds = 30);

    Task<WorkspaceCommandExecutionResult> DotnetRestore(string? targetPath = null, string? workingDirectory = null, int timeoutSeconds = 600);

    Task<WorkspaceCommandExecutionResult> DotnetBuild(string? targetPath = null, string configuration = "Debug", bool noRestore = false, string? workingDirectory = null, int timeoutSeconds = 600);

    Task<WorkspaceCommandExecutionResult> DotnetTest(string? targetPath = null, string configuration = "Debug", string? filter = null, bool noBuild = false, bool noRestore = false, string? workingDirectory = null, int timeoutSeconds = 300);

    Task<WorkspaceCommandExecutionResult> DotnetRun(string targetPath, string? url = null, string configuration = "Debug", bool noBuild = true, bool waitForHttp = true, string? workingDirectory = null, int startupTimeoutSeconds = 45, int timeoutSeconds = 120, bool keepAlive = false, WorkspaceProcessLifetimeScope lifetimeScope = WorkspaceProcessLifetimeScope.ExecutionRun);

    Task<WorkspaceCommandExecutionResult> DotnetStop(string startupReceiptPath, int timeoutSeconds = 30);

    Task<WorkspaceCommandExecutionResult> DotnetNew(
        string template,
        string name,
        string? parentDirectory = null,
        bool force = false,
        int timeoutSeconds = 300,
        string? targetFramework = null);

    Task<WorkspaceCommandExecutionResult> PythonRunFile(string path, string[]? arguments = null, string? workingDirectory = null, int timeoutSeconds = 300, string? sideEffectManifest = null);

    Task<WorkspaceCommandExecutionResult> PowerShellRunScript(string path, string[]? arguments = null, string[]? outputPaths = null, string? workingDirectory = null, int timeoutSeconds = 300, string? sideEffectManifest = null);

    Task<WorkspaceCommandExecutionResult> InspectSpreadsheetPreview(string path, int maxRows = 8, int maxColumns = 8, int timeoutSeconds = 300);

    Task<WorkspaceCommandExecutionResult> RunSkillScript(string skillName, string scriptPath, string[]? arguments = null, string? workingDirectory = null, bool approvalRequired = true, string trustLevel = "FileSkill", IReadOnlyList<string>? allowedExternalRoots = null);

    WorkspaceLocalMcpLaunchDescriptor PrepareLocalMcpServerLaunch(string capabilityName, string command, string[]? arguments = null, string? workingDirectory = null, IReadOnlyDictionary<string, string?>? environmentVariables = null, bool approvalRequired = true);

    WorkspaceCommandExecutionResult RunLegacyCommand(string executable, string arguments = "", string? workingDirectory = null, int timeoutSeconds = 120);
}

public enum WorkspaceProcessLifetimeScope
{
    ExecutionRun = 0,
    ProcessRun = 1
}

public sealed record WorkspaceProcessExecutionRequest(
    string ToolName,
    string RecipeId,
    string ExecutablePath,
    IReadOnlyList<string> Arguments,
    string WorkingDirectory,
    IReadOnlyDictionary<string, string?> EnvironmentVariables,
    int TimeoutSeconds,
    int StdoutLimitCharacters,
    int StderrLimitCharacters);

public sealed record WorkspaceProcessExecutionResult(
    bool Started,
    int ExitCode,
    string Stdout,
    string Stderr,
    bool StdoutTruncated,
    bool StderrTruncated,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset CompletedAtUtc,
    bool TimedOut,
    ExecutionBoundaryDescriptor Boundary,
    string FailureMessage);
