using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Workflows.Abstractions;

public interface IWorkflowProviderInputAdmission {
    ValueTask RequireAsync(WorkflowDefinition definition, WorkflowNode node, WorkflowNodeInput input,
        CancellationToken cancellationToken = default);
}

public interface IWorkflowProviderDisclosurePolicy {
    WorkflowDisclosureOwnerId Owner { get; }

    bool RequiresEvidence(WorkflowNode node);

    ValueTask RequireCurrentAsync(WorkflowRunSnapshot run, WorkflowDefinition definition,
        IReadOnlyList<WorkflowCompletedNodeRead> reads, CancellationToken cancellationToken = default);
}
