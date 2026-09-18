using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Modules.Processes;

internal static class ProcessAgentExecutionAdmission {
    internal static async Task<ExecutionRunResult> ExecuteAsync(IAgentFrameworkWorkspaceService workspace,
        ProcessRuntimeStepAssignment assignment, ExecutionRunRequest request, CancellationToken cancellationToken,
        Action<ProcessExecutionRunId>? observeAdmission = null) {
        var context = request.Context ?? throw new InvalidOperationException("The Process execution has no source context.");
        var source = ExecutionRunSourceKey.ForBackground(context.SourceKind, context.SourceId,
            assignment.RunId.ToString(), assignment.StepInstanceId.ToString());
        var reserved = await workspace.ExecuteSameSourceRunAsync(source, request, cancellationToken).ConfigureAwait(false);
        var executionId = new ProcessExecutionRunId(reserved.Run.Id);
        observeAdmission?.Invoke(executionId);
        if (reserved.Disposition == ExecutionRunSourceDisposition.Created) {
            return reserved.CreatedExecutionResult ?? throw new InvalidOperationException("The newly admitted Process execution returned no result.");
        }
        if (reserved.Disposition == ExecutionRunSourceDisposition.ExistingActive) {
            throw new ProcessRuntimeDispatchInProgressException(executionId);
        }
        if (reserved.Disposition == ExecutionRunSourceDisposition.SourceReconciliationRequired) {
            throw new ProcessSourceExecutionReconciliationException(executionId, "process.adapter.prior_execution_unreconciled");
        }
        if (reserved.Disposition is not (ExecutionRunSourceDisposition.ReusedCompleted or ExecutionRunSourceDisposition.ExistingAdmittedFailure)) {
            throw new InvalidOperationException($"Unsupported admitted Process reservation disposition '{reserved.Disposition}'.");
        }
        try {
            return await workspace.RecoverExecutionRunAsync(reserved.Run.Id, AgentExecutionOperationId.New(), cancellationToken).ConfigureAwait(false);
        } catch (AgentToolAdmissionException exception) {
            throw new ProcessSourceExecutionReconciliationException(executionId, exception.Code, exception);
        }
    }
}

internal sealed class ProcessSourceExecutionReconciliationException(ProcessExecutionRunId executionRunId, string code, Exception? inner = null)
    : Exception("The saved Process execution requires exact journal reconciliation.", inner) {
    public ProcessExecutionRunId ExecutionRunId { get; } = executionRunId;
    public string Code { get; } = code;
}
