using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

internal sealed class AgentDecisionDataPolicyOwner(
    ISandboxWorkspaceCatalogStore store,
    AgentExecutionActivityWorkspaceIdentity workspace,
    IAgentExecutionProfileGenerationSource generationSource) {
    public async Task<AgentDecisionDataPolicySnapshot> ReadAsync(Guid agentId,
        CancellationToken cancellationToken = default) {
        if (agentId == Guid.Empty) {
            throw new ArgumentException("An Agent identity is required.", nameof(agentId));
        }
        RequireCurrent(workspace);
        var snapshot = await store.LoadCatalogSnapshotAsync(cancellationToken);
        RequireCurrent(workspace);
        var agent = snapshot.Catalog.Agents.SingleOrDefault(candidate => candidate.Id == agentId)
            ?? throw new KeyNotFoundException("The Agent no longer exists in this workspace.");
        return new(new(workspace, agentId, snapshot.Revision),
            AgentDecisionDataPolicyMetadata.Read(agent.ConfigurationJson));
    }

    public async Task<AgentDecisionDataPolicySaveOutcome> ReplaceAsync(ReplaceAgentDecisionDataPolicyCommand command,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(command);
        try {
            RequireCurrent(command.Expectation.Workspace);
        } catch (AgentDecisionDataPolicyContextChangedException) {
            return new AgentDecisionDataPolicySaveOutcome.Rejected(AgentDecisionDataPolicyRejection.ContextChanged);
        }
        try {
            return await store.ReplaceAgentDecisionDataPolicyAsync(command,
                () => RequireCurrent(command.Expectation.Workspace), cancellationToken);
        } catch (Exception failure) {
            return new AgentDecisionDataPolicySaveOutcome.Unconfirmed(command,
                AgentDecisionDataPolicyFailure.Capture(AgentDecisionDataPolicyFailureStage.OwnerDispatch, failure));
        }
    }

    private void RequireCurrent(AgentExecutionActivityWorkspaceIdentity original) {
        if (original != workspace || generationSource.GetGeneration() != workspace.DatabaseProfileGeneration) {
            throw new AgentDecisionDataPolicyContextChangedException();
        }
    }
}
