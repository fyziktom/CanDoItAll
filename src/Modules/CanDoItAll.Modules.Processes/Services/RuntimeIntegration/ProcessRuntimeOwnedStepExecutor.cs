using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Modules.Processes;

internal interface IProcessRuntimeOwnedStepExecutor
{
    string ExecutorKey { get; }

    ValueTask<ProcessRuntimeOwnedStepExecutionResult?> TryExecuteAsync(
        ProcessRuntimeStepAssignment assignment,
        CancellationToken cancellationToken = default);
}

internal sealed record ProcessRuntimeOwnedStepExecutionResult(
    bool Succeeded,
    ProcessStepOutcomeResult? Output,
    IReadOnlyList<ToolExecutionReceiptRecord> ToolReceipts,
    Guid ExecutionRunId,
    string Summary,
    string Evidence);
