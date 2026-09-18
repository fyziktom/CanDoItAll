using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Agents.SimpleChats;

namespace CanDoItAll.Modules.AgentFramework;

public static class ManagedAgentPrivilegedCapabilityKeys
{
    public static IReadOnlySet<string> All { get; } = new HashSet<string>(
        HrAgentCapabilityKeys.PrivilegedKeys
            .Concat(HrSimpleChatToolPolicy.PrivilegedKeys)
            .Concat(PromptsCuratorAgentCapabilityKeys.PrivilegedKeys)
            .Concat(WorkflowCuratorAgentCapabilityKeys.PrivilegedKeys)
            .Concat(CapabilityCuratorAgentCapabilityKeys.PrivilegedKeys)
            .Concat(SchedulerAgentIdentity.PrivilegedCapabilityKeys),
        StringComparer.OrdinalIgnoreCase);
}
