namespace CanDoItAll.AgentFramework.Models;

public sealed record ProjectStructureAgentIdentityDescriptor(
    string AgentId,
    string AgentName,
    string MachineName,
    string RepositoryRoot,
    string BranchName,
    string SessionId)
{
    public bool HasLeaseOwnerIdentity =>
        !string.IsNullOrWhiteSpace(AgentId) &&
        !string.IsNullOrWhiteSpace(MachineName);
}
