using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Modules.AgentFramework;

public interface IAgentDiagnosticsReads {
    Task<SandboxDashboardSnapshot> ReadDashboardAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<AgentDefinition>> ReadAgentsAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<ExecutionRunRecord>> ReadRecentRunsAsync(CancellationToken cancellationToken);
}

public sealed class AgentDiagnosticsReads(IAgentFrameworkWorkspaceService workspace) : IAgentDiagnosticsReads {
    public Task<SandboxDashboardSnapshot> ReadDashboardAsync(CancellationToken cancellationToken)
        => workspace.GetDashboardAsync(cancellationToken);
    public Task<IReadOnlyList<AgentDefinition>> ReadAgentsAsync(CancellationToken cancellationToken)
        => workspace.ListAgentsAsync(includeTemplates: false, cancellationToken);
    public Task<IReadOnlyList<ExecutionRunRecord>> ReadRecentRunsAsync(CancellationToken cancellationToken)
        => workspace.ListExecutionRunsAsync(new ExecutionRunQuery(Take: 12), cancellationToken);
}
