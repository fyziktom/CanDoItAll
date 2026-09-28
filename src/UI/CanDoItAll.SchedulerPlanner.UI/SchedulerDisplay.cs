using CanDoItAll.Modules.SchedulerPlanner;

namespace CanDoItAll.SchedulerPlanner.UI;

public enum ScheduleStateFilter { All, Enabled, Paused }

public static class SchedulerDisplay {
    public static string FormatScheduleStateFilter(ScheduleStateFilter filter) {
        return filter switch {
            ScheduleStateFilter.Enabled => "Enabled",
            ScheduleStateFilter.Paused => "Paused",
            _ => "All states"
        };
    }

    public static string ResolveScheduleCardClass(SchedulerPlanSummary plan) {
        return plan.IsEnabled
            ? "scheduler-plan-card scheduler-plan-card--enabled"
            : "scheduler-plan-card scheduler-plan-card--paused";
    }

    public static IReadOnlyList<string> BuildTargetTags(SchedulerTargetOption target) {
        var tags = new List<string> {
            target.Kind.ToString()
        };

        if (!string.IsNullOrWhiteSpace(target.Status)) {
            tags.Add(target.Status.Trim());
        }

        if (target.VersionId.HasValue) {
            tags.Add("Versioned");
        }

        return tags
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static string ResolveTargetDescription(SchedulerTargetOption target) {
        return string.IsNullOrWhiteSpace(target.Description)
            ? "No description is configured for this target."
            : target.Description.Trim();
    }

    public static string ResolveTargetInitials(SchedulerTargetOption target) {
        return target.Kind == SchedulerPlanTargetKind.Workflow
            ? "WF"
            : "PR";
    }

    public static string ResolveTargetRole(SchedulerTargetOption target) {
        return target.Kind == SchedulerPlanTargetKind.Workflow
            ? "Workflow definition"
            : "Process definition";
    }

    public static string ResolveTargetKindTone(SchedulerPlanTargetKind kind) {
        return kind == SchedulerPlanTargetKind.Workflow
            ? "secondary"
            : "info";
    }

    public static string ResolveTargetStatusTone(string status) {
        if (status.Contains("active", StringComparison.OrdinalIgnoreCase) ||
            status.Contains("published", StringComparison.OrdinalIgnoreCase) ||
            status.Contains("ready", StringComparison.OrdinalIgnoreCase)) {
            return "success";
        }

        if (status.Contains("draft", StringComparison.OrdinalIgnoreCase) ||
            status.Contains("paused", StringComparison.OrdinalIgnoreCase)) {
            return "warning";
        }

        if (status.Contains("archived", StringComparison.OrdinalIgnoreCase) ||
            status.Contains("disabled", StringComparison.OrdinalIgnoreCase)) {
            return "neutral";
        }

        return "info";
    }

    public static string FormatNextFire(DateTimeOffset? nextFireAtUtc) {
        return nextFireAtUtc.HasValue
            ? $"Next {nextFireAtUtc.Value.LocalDateTime:g}"
            : "No next fire";
    }

    public static string FormatLastFire(DateTimeOffset? lastFireAtUtc) {
        return lastFireAtUtc.HasValue
            ? lastFireAtUtc.Value.LocalDateTime.ToString("g")
            : "Never";
    }

    public static string FormatUpdatedAt(DateTimeOffset? updatedAtUtc) {
        return updatedAtUtc.HasValue
            ? updatedAtUtc.Value.LocalDateTime.ToString("g")
            : "Not saved";
    }

    public static string ResolveRunTone(SchedulerPlanRunDispatchStatus status) {
        return status switch {
            SchedulerPlanRunDispatchStatus.Dispatched => "success",
            SchedulerPlanRunDispatchStatus.Failed => "danger",
            SchedulerPlanRunDispatchStatus.NoMessages => "neutral",
            SchedulerPlanRunDispatchStatus.WaitingForApproval => "warning",
            SchedulerPlanRunDispatchStatus.Dispatching => "info",
            _ => "neutral"
        };
    }

    public static string ResolveRouteTone(SchedulerPlanRunSummary run) {
        return run.Status switch {
            SchedulerPlanRunDispatchStatus.Failed => "danger",
            SchedulerPlanRunDispatchStatus.NoMessages => "neutral",
            SchedulerPlanRunDispatchStatus.WaitingForApproval => "warning",
            SchedulerPlanRunDispatchStatus.Dispatched => "success",
            _ => "info"
        };
    }

    public static string FormatRunRoute(string route)
        => string.IsNullOrWhiteSpace(route)
            ? SchedulerPlanRunRoutes.Processed
            : route;

    public static string FormatRunPolicy(SchedulerPlanRunRetryCategory retryCategory) {
        return retryCategory switch {
            SchedulerPlanRunRetryCategory.NoAction => "No retry: no matching message.",
            SchedulerPlanRunRetryCategory.WorkflowWaitingForApproval => "No retry: waiting for approval.",
            SchedulerPlanRunRetryCategory.TransientExternalFailure => "Retry scheduled: external dependency.",
            SchedulerPlanRunRetryCategory.ProjectWriteFailure => "Retry scheduled: project write.",
            SchedulerPlanRunRetryCategory.WorkflowFailure => "Retry scheduled: workflow failure.",
            SchedulerPlanRunRetryCategory.SchedulerFailure => "Retry scheduled: scheduler failure.",
            _ => string.Empty
        };
    }

    public static string ResolveRunResult(SchedulerPlanRunSummary run) {
        if (!string.IsNullOrWhiteSpace(run.ErrorMessage)) {
            return run.ErrorMessage;
        }

        return string.IsNullOrWhiteSpace(run.Summary)
            ? run.TargetRunId?.ToString("D") ?? "No target run id"
            : run.Summary;
    }
}
