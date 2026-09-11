using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Workflows.Abstractions;

public interface IWorkflowStructureOutputStore {
    Task<WorkflowProjectLifetime?> FindProjectLifetimeAsync(WorkflowRunId runId, Guid projectId, CancellationToken cancellationToken = default);
    Task RequireForMutationAsync(WorkflowStructureOutputPlan plan, CancellationToken cancellationToken = default);
    Task<WorkflowStructureOutput?> FindAsync(WorkflowStructureOutputIdentity identity, CancellationToken cancellationToken = default);
    Task<WorkflowStructureOutput> PrepareAsync(WorkflowStructureOutputPlan plan, CancellationToken cancellationToken = default);
    Task CompleteAsync(WorkflowStructureOutputReceipt receipt, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WorkflowStructureOutput>> ListAsync(WorkflowRunId runId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WorkflowStructureOutput>> ListPendingAsync(int take, CancellationToken cancellationToken = default);
    Task DeferInspectionAsync(WorkflowStructureOutputIdentity identity, CancellationToken cancellationToken = default);
    Task<bool> TryBeginAssetDispatchAsync(WorkflowStructureOutputIdentity identity, CancellationToken cancellationToken = default);
}

public sealed class WorkflowStructureOutputConflictException : InvalidOperationException {
    public WorkflowStructureOutputConflictException()
        : base("This workflow output occurrence was already prepared with different content or target binding.") { }
}

public sealed class WorkflowStructureLegacyLineageException : InvalidOperationException {
    public WorkflowStructureLegacyLineageException()
        : base("The saved workflow has no trusted output occurrence or admission authority. Its historical state remains available; creating further outputs requires explicit reconciliation.") { }
}

public sealed class WorkflowStructureOutputCancelledException : InvalidOperationException {
    public WorkflowStructureOutputCancelledException()
        : base("A cancelled Workflow cannot deliver a new native effect.") { }
}
