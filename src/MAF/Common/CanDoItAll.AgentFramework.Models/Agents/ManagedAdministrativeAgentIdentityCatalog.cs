using System.Collections.Frozen;

namespace CanDoItAll.AgentFramework.Models;

public static class ManagedAdministrativeAgentIdentityCatalog
{
    public static IReadOnlySet<Guid> AgentIds { get; } = new[]
    {
        HrAgentIdentity.AgentId,
        PromptsCuratorAgentIdentity.AgentId,
        WorkflowCuratorAgentIdentity.AgentId,
        CapabilityCuratorAgentIdentity.AgentId,
        SchedulerAgentIdentity.AgentId
    }.ToFrozenSet();
}
