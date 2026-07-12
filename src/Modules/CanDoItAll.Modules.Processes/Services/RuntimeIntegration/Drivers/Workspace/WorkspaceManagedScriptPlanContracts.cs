using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Modules.Processes;

internal sealed record WorkspaceManagedScriptPlanExecutionRequest(
    Guid ExecutionRunId,
    string ScriptRef,
    string Script,
    string SideEffectManifest,
    string WorkingDirectory,
    string OutputPath,
    string ProductRoot,
    IReadOnlyList<WorkspaceManagedScriptReadbackCheck> ReadbackChecks,
    string FailureEvidencePrefix);

internal sealed record WorkspaceManagedScriptReadbackCheck(
    IReadOnlyList<string> PathCandidates,
    IReadOnlyList<IReadOnlyList<string>> RequiredTextAnyGroups,
    bool MustExist);

internal sealed record WorkspaceManagedScriptPlanExecutionResult(
    bool Succeeded,
    IReadOnlyList<ToolExecutionReceiptRecord> ToolReceipts,
    string Summary,
    string Evidence);
