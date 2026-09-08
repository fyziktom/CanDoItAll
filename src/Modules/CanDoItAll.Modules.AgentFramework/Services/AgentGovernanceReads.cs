using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Modules.AgentFramework;

public interface IAgentGovernanceReads {
    Task<IReadOnlyList<AgentDefinition>> ReadAgentsAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<ExecutionRunRecord>> ReadRunsAsync(Guid? agentId, CancellationToken cancellationToken);
    Task<ExecutionRunDetail> ReadDetailAsync(Guid runId, CancellationToken cancellationToken);
}

public sealed class AgentGovernanceReads(IAgentFrameworkWorkspaceService workspace) : IAgentGovernanceReads {
    public Task<IReadOnlyList<AgentDefinition>> ReadAgentsAsync(CancellationToken cancellationToken)
        => workspace.ListAgentsAsync(includeTemplates: false, cancellationToken);

    public Task<IReadOnlyList<ExecutionRunRecord>> ReadRunsAsync(Guid? agentId, CancellationToken cancellationToken) {
        if (agentId == Guid.Empty) {
            throw new ArgumentException("An explicit agent identity cannot be empty.", nameof(agentId));
        }
        return workspace.ListExecutionRunsAsync(new ExecutionRunQuery(AgentId: agentId, Take: 30), cancellationToken);
    }

    public Task<ExecutionRunDetail> ReadDetailAsync(Guid runId, CancellationToken cancellationToken)
        => workspace.GetExecutionRunDetailAsync(runId, cancellationToken);
}
