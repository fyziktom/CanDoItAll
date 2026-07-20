namespace CanDoItAll.Modules.AgentFramework;

public static class ManagedAgentPrivilegedCapabilityKeys
{
    public static IReadOnlySet<string> All { get; } = new HashSet<string>(
        HrAgentCapabilityKeys.PrivilegedKeys
            .Concat(PromptsCuratorAgentCapabilityKeys.PrivilegedKeys)
            .Concat(WorkflowCuratorAgentCapabilityKeys.PrivilegedKeys),
        StringComparer.OrdinalIgnoreCase);
}
