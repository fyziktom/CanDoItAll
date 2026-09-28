using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

public interface IWorkspaceProcessHost
{
    ExecutionBoundaryDescriptor DescribeBoundary();

    Task<WorkspaceProcessExecutionResult> ExecuteAsync(WorkspaceProcessExecutionRequest request, CancellationToken cancellationToken = default);
}

public sealed class WorkspaceProcessStartException : InvalidOperationException
{
    public WorkspaceProcessStartException(string message, Exception? innerException = null)
        : this(message, WorkspaceProcessStartFailureKind.Unknown, innerException)
    {
    }

    public WorkspaceProcessStartException(
        string message,
        WorkspaceProcessStartFailureKind failureKind,
        Exception? innerException = null,
        string? operatorDetail = null)
        : base(message, innerException)
    {
        FailureKind = failureKind;
        OperatorDetail = operatorDetail ?? string.Empty;
    }

    public WorkspaceProcessStartFailureKind FailureKind { get; }

    /// <summary>Operator-only detail (can name paths); never return it to an agent.</summary>
    public string OperatorDetail { get; }
}

public interface IWorkspaceLongRunningProcessHost : IWorkspaceProcessHost
{
    Task<IWorkspaceProcessSession> StartSessionAsync(
        WorkspaceProcessSessionRequest request,
        CancellationToken cancellationToken = default);

    Task<WorkspaceProcessTerminationResult> TerminateOwnedProcessAsync(
        WorkspaceOwnedProcessIdentity identity,
        CancellationToken cancellationToken = default);
}

public interface IWorkspaceProcessSession : IAsyncDisposable
{
    WorkspaceOwnedProcessIdentity Identity { get; }

    bool HasExited { get; }

    WorkspaceProcessOutputSnapshot CaptureOutput();

    Task<WorkspaceProcessExecutionResult> WaitForExitAsync(CancellationToken cancellationToken = default);

    Task<WorkspaceProcessExecutionResult> TerminateAsync(
        WorkspaceProcessTerminationReason reason,
        string failureMessage,
        CancellationToken cancellationToken = default);

    WorkspaceOwnedProcessIdentity Detach();
}

public interface IWorkspaceDuplexProcessSession : IWorkspaceProcessSession
{
    Stream StandardInput { get; }

    Stream StandardOutput { get; }

    void CompleteStandardInput();
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

    // artifactsPath (restore/build/test/run): a workspace-relative folder for bin/obj, set only for
    // read-only external targets (see WorkspaceReadOnlyBuildOutput). Null keeps dotnet's default layout.
    Task<WorkspaceCommandExecutionResult> DotnetRestore(string? targetPath = null, string? workingDirectory = null, int timeoutSeconds = 600, string? artifactsPath = null);

    Task<WorkspaceCommandExecutionResult> DotnetBuild(string? targetPath = null, string configuration = "Debug", bool noRestore = false, string? workingDirectory = null, int timeoutSeconds = 600, string? artifactsPath = null);

    Task<WorkspaceCommandExecutionResult> DotnetTest(string? targetPath = null, string configuration = "Debug", string? filter = null, bool noBuild = false, bool noRestore = false, string? workingDirectory = null, int timeoutSeconds = 300, string? artifactsPath = null);

    Task<WorkspaceCommandExecutionResult> DotnetRun(string targetPath, string? url = null, string configuration = "Debug", bool noBuild = true, bool waitForHttp = true, string? workingDirectory = null, int startupTimeoutSeconds = 45, int timeoutSeconds = 120, bool keepAlive = false, WorkspaceProcessLifetimeScope lifetimeScope = WorkspaceProcessLifetimeScope.ExecutionRun, string? artifactsPath = null);

    Task<WorkspaceCommandExecutionResult> DotnetStop(string startupReceiptPath, int timeoutSeconds = 30);

    Task<WorkspaceCommandExecutionResult> DotnetNew(
        string template,
        string name,
        string? parentDirectory = null,
        bool force = false,
        int timeoutSeconds = 300,
        string? targetFramework = null);

    Task<WorkspaceCommandExecutionResult> PythonRunFile(string path, string[]? arguments = null, string? workingDirectory = null, int timeoutSeconds = 300, string? sideEffectManifest = null, bool extendedEnvironmentAllowed = false);

    Task<WorkspaceCommandExecutionResult> PowerShellRunScript(string path, string[]? arguments = null, string[]? outputPaths = null, string? workingDirectory = null, int timeoutSeconds = 300, string? sideEffectManifest = null, bool extendedEnvironmentAllowed = false);

    Task<WorkspaceCommandExecutionResult> InspectSpreadsheetPreview(string path, int maxRows = 8, int maxColumns = 8, int timeoutSeconds = 300);

    Task<WorkspaceCommandExecutionResult> RunSkillScript(string skillName, string scriptPath, string[]? arguments = null, string? workingDirectory = null, bool approvalRequired = true, string trustLevel = "FileSkill", IReadOnlyList<string>? allowedExternalRoots = null);

    WorkspaceLocalMcpLaunchDescriptor PrepareLocalMcpServerLaunch(string capabilityName, string command, string[]? arguments = null, string? workingDirectory = null, IReadOnlyDictionary<string, string?>? environmentVariables = null, bool approvalRequired = true, string? workingDirectoryDisplayPath = null, IReadOnlyCollection<string>? environmentVariableNames = null);

    WorkspaceCommandExecutionResult RunLegacyCommand(string executable, string arguments = "", string? workingDirectory = null, int timeoutSeconds = 120);
}

public enum WorkspaceProcessLifetimeScope
{
    ExecutionRun = 0,
    ProcessRun = 1
}

public enum WorkspaceProcessTerminationReason
{
    Completed = 0,
    StartFailed = 1,
    TimedOut = 2,
    CallerCanceled = 3,
    TerminationFailed = 4,
    Running = 5
}

public enum WorkspaceProcessTerminationStatus
{
    Terminated = 0,
    AlreadyExited = 1,
    IdentityMismatch = 2,
    Failed = 3
}

public enum WorkspaceProcessTerminationMode
{
    ForceTree,
    GracefulThenForceTree
}

public enum WorkspaceProcessStandardIoMode
{
    Captured,
    Duplex
}

public enum WorkspaceProcessTextCaptureMode
{
    Prefix,
    Tail
}

public enum WorkspaceOwnedProcessBoundaryKind
{
    WindowsJobObject,
    UnixProcessGroup
}

public sealed record WorkspaceOwnedProcessBoundary(
    WorkspaceOwnedProcessBoundaryKind Kind,
    long NativeId,
    Guid InstanceId);

public sealed record WorkspaceProcessExecutionRequest(
    string ToolName,
    string RecipeId,
    string ExecutablePath,
    IReadOnlyList<string> Arguments,
    string WorkingDirectory,
    IReadOnlyDictionary<string, string?> EnvironmentVariables,
    int TimeoutSeconds,
    int StdoutLimitCharacters,
    int StderrLimitCharacters,
    string? StandardInput = null);

public sealed record WorkspaceProcessSessionRequest(
    string ToolName,
    string RecipeId,
    string ExecutablePath,
    IReadOnlyList<string> Arguments,
    string WorkingDirectory,
    IReadOnlyDictionary<string, string?> EnvironmentVariables,
    int StdoutLimitCharacters,
    int StderrLimitCharacters,
    string? StandardInput = null,
    WorkspaceProcessTerminationMode TerminationMode = WorkspaceProcessTerminationMode.ForceTree,
    WorkspaceProcessStandardIoMode StandardIoMode = WorkspaceProcessStandardIoMode.Captured,
    WorkspaceProcessTextCaptureMode StderrCaptureMode = WorkspaceProcessTextCaptureMode.Prefix);

public sealed record WorkspaceOwnedProcessIdentity(
    int ProcessId,
    DateTimeOffset StartedAtUtc,
    string ExecutablePathFingerprint,
    WorkspaceOwnedProcessBoundary Boundary);

public sealed record WorkspaceProcessOutputSnapshot(
    string Stdout,
    string Stderr,
    bool StdoutTruncated,
    bool StderrTruncated);

public sealed record WorkspaceProcessTerminationResult(
    WorkspaceProcessTerminationStatus Status,
    bool ResidualProcessPossible,
    string Message);

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
    string FailureMessage,
    WorkspaceProcessTerminationReason TerminationReason = WorkspaceProcessTerminationReason.Completed,
    bool ResidualProcessPossible = false)
{
    /// <summary>Why the process did not start; <see cref="WorkspaceProcessStartFailureKind.None"/> when it started.</summary>
    public WorkspaceProcessStartFailureKind StartFailureKind { get; init; }

    /// <summary>Operator-only start-failure detail (can name paths); never return it to an agent.</summary>
    public string StartFailureDetail { get; init; } = string.Empty;
}
