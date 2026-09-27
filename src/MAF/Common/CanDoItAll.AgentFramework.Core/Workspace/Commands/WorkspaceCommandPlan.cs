using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

internal sealed record WorkspaceCommandPlan(
    ToolExecutionDecision Decision,
    bool MutatesWorkspace,
    IReadOnlyList<string> TargetPaths,
    string WorkspaceRootPath,
    string WorkingDirectory,
    string WorkingDirectoryPath,
    IReadOnlyList<string> ExecutableCandidates,
    IReadOnlyList<string> Arguments,
    int TimeoutSeconds,
    int StdoutLimitCharacters,
    int StderrLimitCharacters,
    IReadOnlyDictionary<string, string?>? EnvironmentVariables = null,
    ToolExecutionSideEffectMode DeclaredSideEffectMode = ToolExecutionSideEffectMode.Unspecified,
    WorkspaceDotnetRunLifecyclePlan? DotnetRunLifecycle = null,
    WorkspaceDotnetStopLifecyclePlan? DotnetStopLifecycle = null)
{
    /// <summary>
    /// Whether the operator's network-trust and extra variable names reach this process. Agent script
    /// tools get them only when the agent may read the process environment.
    /// </summary>
    public bool ExtendedEnvironmentAllowed { get; init; } = true;
}

internal sealed record WorkspaceDotnetRunLifecyclePlan(
    string ListenUrl,
    string ProbeUrl,
    int StartupTimeoutSeconds,
    bool KeepAlive,
    WorkspaceProcessLifetimeScope LifetimeScope,
    string StdoutLogFullPath,
    string StdoutLogRelativePath,
    string StderrLogFullPath,
    string StderrLogRelativePath,
    string StartupReceiptFullPath,
    string StartupReceiptRelativePath,
    string CleanupReceiptFullPath,
    string CleanupReceiptRelativePath);

internal sealed record WorkspaceDotnetStopLifecyclePlan(
    string StartupReceiptFullPath,
    string StartupReceiptRelativePath,
    string CleanupReceiptFullPath,
    string CleanupReceiptRelativePath);
