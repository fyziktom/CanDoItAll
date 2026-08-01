using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Processes.Core;

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
    string FailureEvidencePrefix,
    WorkspaceManagedScriptPlanExecutionPolicy ExecutionPolicy);

internal sealed record WorkspaceManagedScriptPlanExecutionPolicy(
    ProcessToolOperationIdempotencyPolicy Idempotency,
    ProcessToolOperationFailureReconciliationPolicy FailureReconciliation)
{
    public static WorkspaceManagedScriptPlanExecutionPolicy FailClosed { get; } = new(
        ProcessToolOperationIdempotencyPolicy.Unspecified,
        ProcessToolOperationFailureReconciliationPolicy.None);
}

internal sealed record WorkspaceManagedScriptReadbackCheck(
    IReadOnlyList<string> PathCandidates,
    IReadOnlyList<IReadOnlyList<string>> RequiredTextAnyGroups,
    bool MustExist);

internal sealed record WorkspaceManagedScriptPlanExecutionResult(
    bool Succeeded,
    IReadOnlyList<ToolExecutionReceiptRecord> ToolReceipts,
    string Summary,
    string Evidence,
    ProcessRuntimeOwnedStepFailure? Failure);
