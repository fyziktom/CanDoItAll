using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Modules.SchedulerPlanner;

public enum SchedulerAgentChatView
{
    Calendar,
    Schedules,
    NewSchedule
}

public static class SchedulerAgentChatContextBuilder
{
    private static readonly AgentChatContextSource Source = new(
        new AgentChatContextSourceKind("scheduler"),
        new AgentChatContextSourceId("scheduler"));

    public static AgentChatContextSurface Build(
        SchedulerAgentChatView view,
        SchedulerPlanSummary? selectedPlan,
        SchedulerTargetOption? selectedTarget,
        string? overlay)
    {
        if (!Enum.IsDefined(view))
        {
            throw new ArgumentOutOfRangeException(nameof(view), view, "The scheduler agent-chat view is undefined.");
        }

        var viewToken = view switch
        {
            SchedulerAgentChatView.Calendar => "calendar",
            SchedulerAgentChatView.Schedules => "schedules",
            SchedulerAgentChatView.NewSchedule => "new-schedule",
            _ => throw new ArgumentOutOfRangeException(nameof(view), view, "The scheduler agent-chat view is undefined.")
        };
        var primarySelection = selectedPlan is not null
            ? new AgentChatContextEntityReference("schedule", selectedPlan.Id.ToString("D"), selectedPlan.Name)
            : selectedTarget is not null
                ? new AgentChatContextEntityReference("schedule-target", selectedTarget.Id.ToString("D"), selectedTarget.Name)
                : null;
        var selectedEntities = selectedPlan is null
            ? Array.Empty<AgentChatContextEntityReference>()
            :
            [
                new AgentChatContextEntityReference(
                    "schedule-target",
                    selectedPlan.TargetId.ToString("D"),
                    selectedPlan.TargetName)
            ];

        return new AgentChatContextSurface(
            Source,
            "Scheduler and planner",
            new AgentChatSurfacePosition(
                module: "scheduler",
                surface: "planner",
                view: viewToken,
                route: "/scheduler",
                primarySelection,
                selectedEntities,
                BuildFacts(selectedPlan, selectedTarget, overlay)),
            agentAccess:
            [
                new AgentChatContextAgentAccess(
                    SchedulerAgentIdentity.AgentId,
                    AgentChatContextPermission.Read | AgentChatContextPermission.Mutate,
                    "Scheduler")
            ],
            accessMode: AgentChatContextScopeAccessMode.Unrestricted,
            completionRefreshMode: AgentChatContextCompletionRefreshMode.OnSuccessfulRun);
    }

    private static IReadOnlyList<AgentChatContextPositionFact> BuildFacts(
        SchedulerPlanSummary? selectedPlan,
        SchedulerTargetOption? selectedTarget,
        string? overlay)
    {
        if (selectedPlan is not null)
        {
            return string.IsNullOrWhiteSpace(overlay)
                ?
                [
                    new AgentChatContextPositionFact("schedule-state", selectedPlan.IsEnabled ? "enabled" : "paused"),
                    new AgentChatContextPositionFact("target-kind", selectedPlan.TargetKind.ToString())
                ]
                :
                [
                    new AgentChatContextPositionFact("schedule-state", selectedPlan.IsEnabled ? "enabled" : "paused"),
                    new AgentChatContextPositionFact("target-kind", selectedPlan.TargetKind.ToString()),
                    new AgentChatContextPositionFact("overlay", overlay)
                ];
        }

        if (selectedTarget is not null)
        {
            return string.IsNullOrWhiteSpace(overlay)
                ?
                [
                    new AgentChatContextPositionFact("target-kind", selectedTarget.Kind.ToString()),
                    new AgentChatContextPositionFact("target-status", selectedTarget.Status)
                ]
                :
                [
                    new AgentChatContextPositionFact("target-kind", selectedTarget.Kind.ToString()),
                    new AgentChatContextPositionFact("target-status", selectedTarget.Status),
                    new AgentChatContextPositionFact("overlay", overlay)
                ];
        }

        return string.IsNullOrWhiteSpace(overlay)
            ? []
            : [new AgentChatContextPositionFact("overlay", overlay)];
    }
}
