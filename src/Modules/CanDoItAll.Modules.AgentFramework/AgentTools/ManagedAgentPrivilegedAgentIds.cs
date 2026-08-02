using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Modules.AgentFramework;

public static class ManagedAgentPrivilegedAgentIds
{
    public static IReadOnlySet<Guid> All => ManagedAdministrativeAgentIdentityCatalog.AgentIds;
}
