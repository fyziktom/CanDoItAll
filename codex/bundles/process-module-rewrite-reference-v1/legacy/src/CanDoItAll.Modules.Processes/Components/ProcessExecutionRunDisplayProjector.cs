using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Modules.Processes;

internal static class ProcessExecutionRunDisplayProjector
{
    public static ProcessExecutionRunDisplayProjection Resolve(
        ExecutionRunRecord run,
        ProcessStepRunViewModel? stepRun,
        bool isLatestRunForStep)
    {
        ArgumentNullException.ThrowIfNull(run);

        var rawBadgeText = BuildRawBadge(run.State, run.Outcome);
        var rawTone = ResolveRawTone(run.State, run.Outcome);
        if (stepRun is null || !isLatestRunForStep)
        {
            return new ProcessExecutionRunDisplayProjection(rawBadgeText, rawTone, string.Empty, string.Empty);
        }

        var rawStepStatus = MapRawStepStatus(run);
        if (rawStepStatus == stepRun.Status || !ShouldProjectGovernedStatus(stepRun.Status))
        {
            return new ProcessExecutionRunDisplayProjection(rawBadgeText, rawTone, string.Empty, string.Empty);
        }

        return new ProcessExecutionRunDisplayProjection(
            BuildGovernedBadge(stepRun.Status),
            ResolveGovernedTone(stepRun.Status),
            $"Raw {rawBadgeText}",
            BuildGovernedStatusDetail(stepRun.Status));
    }

    internal static string BuildRawBadge(ExecutionState state, RunOutcome? outcome)
    {
        return outcome.HasValue
            ? $"{state} / {outcome.Value}"
            : state.ToString();
    }

    internal static string ResolveRawTone(ExecutionState state, RunOutcome? outcome)
    {
        if (outcome == RunOutcome.Succeeded)
        {
            return "mint";
        }

        if (outcome == RunOutcome.Failed || state == ExecutionState.Failed)
        {
            return "danger";
        }

        return state switch
        {
            ExecutionState.WaitingOnTool => "warning",
            ExecutionState.Running or ExecutionState.Preparing or ExecutionState.Persisting => "info",
            _ => "neutral"
        };
    }

    private static bool ShouldProjectGovernedStatus(ProcessStepRunStatus status)
    {
        return status is
            ProcessStepRunStatus.Completed or
            ProcessStepRunStatus.WaitingApproval or
            ProcessStepRunStatus.Blocked or
            ProcessStepRunStatus.Refused or
            ProcessStepRunStatus.Failed;
    }

    private static ProcessStepRunStatus MapRawStepStatus(ExecutionRunRecord run)
    {
        if (run.PendingApprovals.Count > 0 || run.State == ExecutionState.WaitingOnTool)
        {
            return ProcessStepRunStatus.WaitingApproval;
        }

        if (run.State == ExecutionState.Failed || run.Outcome is RunOutcome.Failed or RunOutcome.Cancelled)
        {
            return ProcessStepRunStatus.Failed;
        }

        if (run.State == ExecutionState.Completed && run.Outcome == RunOutcome.Succeeded)
        {
            return ProcessStepRunStatus.Completed;
        }

        return ProcessStepRunStatus.InProgress;
    }

    private static string BuildGovernedBadge(ProcessStepRunStatus status)
    {
        return status switch
        {
            ProcessStepRunStatus.WaitingApproval => "Process waiting approval",
            ProcessStepRunStatus.Blocked => "Process blocked",
            ProcessStepRunStatus.Refused => "Process refused",
            ProcessStepRunStatus.Failed => "Process failed",
            ProcessStepRunStatus.Completed => "Process completed",
            _ => status.ToString()
        };
    }

    private static string ResolveGovernedTone(ProcessStepRunStatus status)
    {
        return status switch
        {
            ProcessStepRunStatus.Completed => "mint",
            ProcessStepRunStatus.WaitingApproval => "warning",
            ProcessStepRunStatus.Blocked => "warning",
            ProcessStepRunStatus.Refused => "neutral",
            ProcessStepRunStatus.Failed => "danger",
            _ => "neutral"
        };
    }

    private static string BuildGovernedStatusDetail(ProcessStepRunStatus status)
    {
        return status switch
        {
            ProcessStepRunStatus.Completed => "The latest governed process evaluation completed this step, so process state is authoritative for this attempt.",
            ProcessStepRunStatus.WaitingApproval => "The latest governed process evaluation is waiting on approval for this step, so process state is authoritative for this attempt.",
            ProcessStepRunStatus.Blocked => "The latest governed process evaluation blocked this step, so process state is authoritative for this attempt.",
            ProcessStepRunStatus.Refused => "The latest governed process evaluation refused this step, so process state is authoritative for this attempt.",
            ProcessStepRunStatus.Failed => "The latest governed process evaluation failed this step, so process state is authoritative for this attempt.",
            _ => string.Empty
        };
    }
}

internal sealed record ProcessExecutionRunDisplayProjection(
    string StatusBadgeText,
    string StatusTone,
    string RawStatusBadgeText,
    string StatusDetail);
