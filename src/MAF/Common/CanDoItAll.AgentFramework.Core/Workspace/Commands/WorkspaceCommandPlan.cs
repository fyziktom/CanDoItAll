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
    ToolExecutionSideEffectMode DeclaredSideEffectMode = ToolExecutionSideEffectMode.Unspecified);
