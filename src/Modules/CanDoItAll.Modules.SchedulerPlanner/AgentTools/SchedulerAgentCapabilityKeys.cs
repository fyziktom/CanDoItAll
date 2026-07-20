using System.Collections.Frozen;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Modules.SchedulerPlanner;

public static class SchedulerAgentCapabilityKeys
{
    public const string AgentSkill = SchedulerAgentIdentity.AgentSkillCapabilityKey;
    public const string WorkflowTargetsSearch = SchedulerAgentIdentity.WorkflowTargetsSearchCapabilityKey;
    public const string WorkflowSchedulesSearch = SchedulerAgentIdentity.WorkflowSchedulesSearchCapabilityKey;
    public const string WorkflowScheduleCreate = SchedulerAgentIdentity.WorkflowScheduleCreateCapabilityKey;

    public static IReadOnlyDictionary<string, string> ToolNameToCapabilityKey { get; } =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [AgentToolInvocationPolicyMetadata.SchedulerWorkflowTargetsSearch] = WorkflowTargetsSearch,
            [AgentToolInvocationPolicyMetadata.SchedulerWorkflowSchedulesSearch] = WorkflowSchedulesSearch,
            [AgentToolInvocationPolicyMetadata.SchedulerWorkflowScheduleCreate] = WorkflowScheduleCreate
        }.ToFrozenDictionary(StringComparer.Ordinal);

    public static IReadOnlySet<string> PrivilegedKeys => SchedulerAgentIdentity.PrivilegedCapabilityKeys;
}
