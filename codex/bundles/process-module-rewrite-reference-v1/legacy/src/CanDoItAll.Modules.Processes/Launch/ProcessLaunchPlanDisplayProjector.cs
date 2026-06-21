namespace CanDoItAll.Modules.Processes;

internal static class ProcessLaunchPlanDisplayProjector
{
    public static ProcessLaunchPlanDisplayProjection Resolve(ProcessLaunchPlanStatus planningStatus, ProcessRunStatus? generatedRunStatus)
    {
        if (!generatedRunStatus.HasValue)
        {
            return new ProcessLaunchPlanDisplayProjection(
                planningStatus.ToString(),
                ResolvePlanningTone(planningStatus),
                string.Empty,
                string.Empty);
        }

        return generatedRunStatus.Value switch
        {
            ProcessRunStatus.Active => new ProcessLaunchPlanDisplayProjection(
                "Run active",
                ResolveRunTone(generatedRunStatus.Value),
                BuildPlanningStatusBadgeText(planningStatus),
                "The generated runtime run is still executing, so run state is authoritative for this launch."),
            ProcessRunStatus.Blocked => new ProcessLaunchPlanDisplayProjection(
                "Run blocked",
                ResolveRunTone(generatedRunStatus.Value),
                BuildPlanningStatusBadgeText(planningStatus),
                "The generated runtime run is currently blocked, so run state is authoritative for this launch."),
            ProcessRunStatus.Completed => new ProcessLaunchPlanDisplayProjection(
                "Run completed",
                ResolveRunTone(generatedRunStatus.Value),
                BuildPlanningStatusBadgeText(planningStatus),
                "The generated runtime run completed, so run state is authoritative for this launch."),
            ProcessRunStatus.Cancelled => new ProcessLaunchPlanDisplayProjection(
                "Run cancelled",
                ResolveRunTone(generatedRunStatus.Value),
                BuildPlanningStatusBadgeText(planningStatus),
                "The generated runtime run was cancelled, so run state is authoritative for this launch."),
            ProcessRunStatus.Failed => new ProcessLaunchPlanDisplayProjection(
                "Run failed",
                ResolveRunTone(generatedRunStatus.Value),
                BuildPlanningStatusBadgeText(planningStatus),
                "The generated runtime run failed, so run state is authoritative for this launch."),
            _ => new ProcessLaunchPlanDisplayProjection(
                "Run draft",
                ResolveRunTone(generatedRunStatus.Value),
                BuildPlanningStatusBadgeText(planningStatus),
                "The generated runtime run exists but has not started executing yet.")
        };
    }

    private static string BuildPlanningStatusBadgeText(ProcessLaunchPlanStatus planningStatus)
    {
        return $"Launch {planningStatus}";
    }

    private static string ResolvePlanningTone(ProcessLaunchPlanStatus status)
    {
        return status switch
        {
            ProcessLaunchPlanStatus.Ready => "success",
            ProcessLaunchPlanStatus.Completed => "mint",
            ProcessLaunchPlanStatus.PendingApproval => "warning",
            ProcessLaunchPlanStatus.Approved => "info",
            ProcessLaunchPlanStatus.Executing => "info",
            ProcessLaunchPlanStatus.Rejected => "danger",
            ProcessLaunchPlanStatus.Cancelled => "neutral",
            _ => "neutral"
        };
    }

    private static string ResolveRunTone(ProcessRunStatus status)
    {
        return status switch
        {
            ProcessRunStatus.Completed => "mint",
            ProcessRunStatus.Active => "info",
            ProcessRunStatus.Blocked => "warning",
            ProcessRunStatus.Failed => "danger",
            ProcessRunStatus.Cancelled => "neutral",
            _ => "neutral"
        };
    }
}

internal sealed record ProcessLaunchPlanDisplayProjection(
    string StatusBadgeText,
    string StatusTone,
    string PlanningStatusBadgeText,
    string StatusDetail);
