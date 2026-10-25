using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Workflows.Abstractions;

public enum WorkflowProcessCancellationStatus { Closed, Pending, ReconciliationRequired }

public interface IWorkflowProcessCancellationRecovery {
    Task<WorkflowRunId?> FindNextAsync(WorkflowProcessRunId processRunId, WorkflowRunId? after, CancellationToken cancellationToken);
    Task<WorkflowProcessCancellationStatus> StopAsync(WorkflowProcessRunId processRunId, WorkflowRunId runId, CancellationToken cancellationToken);
}
