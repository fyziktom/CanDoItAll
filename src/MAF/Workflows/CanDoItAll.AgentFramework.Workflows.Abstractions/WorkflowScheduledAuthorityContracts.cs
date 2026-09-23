using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Workflows.Abstractions;

public interface IWorkflowScheduledAuthorityPolicy {
    bool AllowsScheduling(AgentExecutionGovernanceSnapshot authority);
    Task RequireCurrentAsync(WorkflowStructureSchedulerAuthority authority, CancellationToken cancellationToken = default);
    Task RequireCurrentForMutationAsync(WorkflowStructureSchedulerAuthority authority, CancellationToken cancellationToken = default);
}
