using CanDoItAll.AgentFramework.Core;
using static CanDoItAll.AgentFramework.Core.ToolCapabilityMetadataFactory;

namespace CanDoItAll.Modules.SchedulerPlanner;

public static class SchedulerToolPolicy {
    public const string SchedulerWorkflowTargetsSearch = "scheduler_workflow_targets_search";
    public const string SchedulerWorkflowSchedulesSearch = "scheduler_workflow_schedules_search";
    public const string SchedulerWorkflowScheduleCreate = "scheduler_workflow_schedule_create";

    public static IReadOnlyList<ToolCapabilityMetadata> Capabilities { get; } = Array.AsReadOnly<ToolCapabilityMetadata>([
        Read(SchedulerWorkflowTargetsSearch, ToolCapabilitySideEffectKind.InternalDataRead) with { BusinessArgumentRetentionScheme = "scheduler-approval-redacted-v1" },
        Read(SchedulerWorkflowSchedulesSearch, ToolCapabilitySideEffectKind.InternalDataRead) with { BusinessArgumentRetentionScheme = "scheduler-approval-redacted-v1" },
        Mutation(SchedulerWorkflowScheduleCreate, ToolCapabilitySideEffectKind.InternalStateMutation) with { BusinessArgumentRetentionScheme = "scheduler-approval-redacted-v1" }
    ]);
}
