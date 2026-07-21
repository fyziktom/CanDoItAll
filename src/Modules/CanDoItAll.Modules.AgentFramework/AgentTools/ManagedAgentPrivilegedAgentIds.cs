using System.Collections.Frozen;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Modules.AgentFramework;

public static class ManagedAgentPrivilegedAgentIds
{
    public static IReadOnlySet<Guid> All { get; } = new[]
    {
        HrAgentIdentity.AgentId,
        PromptsCuratorAgentIdentity.AgentId,
        WorkflowCuratorAgentIdentity.AgentId,
        CapabilityCuratorAgentIdentity.AgentId,
        SchedulerAgentIdentity.AgentId
    }.ToFrozenSet();
}
