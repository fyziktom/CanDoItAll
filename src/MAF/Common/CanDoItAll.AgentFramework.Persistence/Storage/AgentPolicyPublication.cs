using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Persistence;

internal sealed class AgentPolicyPublication(ReplaceAgentDecisionDataPolicyCommand command, DateTimeOffset acceptedAtUtc) {
    private AgentDecisionDataPolicySaveOutcome.Accepted? preparedAcknowledgement;
    public bool WriteEntered { get; private set; }
    public AgentDecisionDataPolicySaveOutcome.Accepted? Acknowledgement { get; private set; }

    public SandboxWorkspaceCatalog Apply(SandboxWorkspaceCatalog catalog) => catalog with {
        Agents = catalog.Agents.Select(agent => agent.Id == command.Expectation.AgentId
            ? agent with { ConfigurationJson = AgentDecisionDataPolicyMetadata.Replace(agent.ConfigurationJson, command.Policy) }
            : agent).ToArray(),
        AgentDecisionDataPolicyReceipts = (catalog.AgentDecisionDataPolicyReceipts ?? AgentDecisionDataPolicyReceiptLog.Empty)
            .Append(command, acceptedAtUtc)
    };

    public void EnterWrite(SandboxWorkspaceCatalog catalog) {
        var agent = catalog.Agents.Single(candidate => candidate.Id == command.Expectation.AgentId);
        var snapshot = new AgentDecisionDataPolicySnapshot(new(command.Expectation.Workspace, agent.Id, catalog.CatalogDataRevision),
            AgentDecisionDataPolicyMetadata.Read(agent.ConfigurationJson));
        if (catalog.AgentDecisionDataPolicyReceipts?.Find(command, acceptedAtUtc) is not AgentDecisionDataPolicyLookup.Accepted receipt ||
            receipt.Receipt.Snapshot != snapshot) {
            throw new InvalidOperationException("The Agent policy publication lacks its exact historical receipt.");
        }
        preparedAcknowledgement = new(command, snapshot);
        WriteEntered = true;
    }

    public void Acknowledge() => Acknowledgement = preparedAcknowledgement;
}
