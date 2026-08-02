namespace CanDoItAll.AgentFramework.Models;

public static class SchedulerAgentIdentity
{
    public const string StableIdKey = "agents/scheduler-agent";
    public const string TemplateKey = "scheduler-agent";
    public const string DefaultDisplayName = "Scheduler Agent";
    public const string DefaultAvatarImageUrl = AgentAvatarImageCatalog.BundledAvatarBasePath + "avatar-03.jpg";
    public const string AgentSkillCapabilityKey = "scheduler-agent-inline-skill";
    public const string WorkflowTargetsSearchCapabilityKey = "scheduler-workflow-targets-search";
    public const string WorkflowSchedulesSearchCapabilityKey = "scheduler-workflow-schedules-search";
    public const string WorkflowScheduleCreateCapabilityKey = "scheduler-workflow-schedule-create";
    public const string SchedulingAccessVersionPropertyName = "schedulerSchedulingAccessVersion";
    public const string CurrentSchedulingAccessVersion = "2026-08-scheduler-scheduling-access-v1";

    public static Guid AgentId { get; } = new("c81de15b-c260-425d-8727-adbc1fb5d598");

    public static IReadOnlySet<string> PrivilegedCapabilityKeys { get; } = new HashSet<string>(
        [
            AgentSkillCapabilityKey,
            WorkflowTargetsSearchCapabilityKey,
            WorkflowSchedulesSearchCapabilityKey,
            WorkflowScheduleCreateCapabilityKey
        ],
        StringComparer.OrdinalIgnoreCase);

    public static bool Matches(AgentDefinition? agent)
    {
        return agent is not null &&
               agent.Id == AgentId &&
               string.Equals(agent.TemplateKey, TemplateKey, StringComparison.Ordinal);
    }
}
