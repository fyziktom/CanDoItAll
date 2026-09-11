using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Workflows.Abstractions;

public interface IWorkflowStructureAuthorityFactory {
    Task<WorkflowStructureAuthority> CaptureLocalOperatorAsync(WorkflowStructureOperatorSurface surface,
        CancellationToken cancellationToken = default);
    Task<WorkflowStructureAuthority> CaptureAuthenticatedOperatorAsync(string subject, DateTimeOffset expiresAtUtc,
        CancellationToken cancellationToken = default);
    WorkflowStructureAuthority CaptureAgent(AgentDefinition agent, AgentExecutionGovernanceSnapshot governance);
    Task<WorkflowStructureAuthority> CaptureAgentAsync(AgentDefinition agent, AgentExecutionGovernanceSnapshot governance,
        CancellationToken cancellationToken = default) => Task.FromResult(CaptureAgent(agent, governance));
}
