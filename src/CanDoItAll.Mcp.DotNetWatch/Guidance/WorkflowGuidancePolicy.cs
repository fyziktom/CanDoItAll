using System.Text.Json;
using CanDoItAll.Mcp.DotNetWatch.Configuration;

namespace CanDoItAll.Mcp.DotNetWatch.Guidance;

public sealed class WorkflowGuidancePolicy(RuntimeConfiguration configuration)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public WorkflowGuidanceData? ForWorkspace(WorkspaceInfoData workspace)
    {
        if (!configuration.WorkflowGuidanceEnabled)
        {
            return null;
        }

        var activeApp = workspace.ActiveAppSessions.FirstOrDefault();
        if (activeApp is not null)
        {
            return ForApp(activeApp);
        }

        if (workspace.ActiveOperations.Count > 0)
        {
            return Budget("build-then-check", "finish-current-op", "operation_status then targeted-check", "avoid-parallel-edits", "OperationRunning");
        }

        return Budget("watch-small-step", "start-default-watch", "wait RevisionConfirmed then browser-check", "stay-nearby", "NoActiveApp");
    }

    public WorkflowGuidanceData? ForApp(AppStatusData status)
    {
        if (!configuration.WorkflowGuidanceEnabled)
        {
            return null;
        }

        if (status.RollbackAvailable)
        {
            return Budget("rollback-available", "validate-current-revision", "rollback if regression appears", null, "RollbackReady");
        }

        if (status.LaneKind == RuntimeLaneKind.SourceWatch && status.State == AppLifecycleState.Healthy && status.Watch?.PendingChange != true)
        {
            return Budget("watch-small-step", "edit-1-nearby-file", "wait RevisionConfirmed then browser-check", "stay-nearby", "WatchHealthy");
        }

        if (status.LaneKind == RuntimeLaneKind.SourceWatch && status.Watch?.PendingChange == true)
        {
            return Budget("watch-validate-now", "wait RevisionConfirmed", "browser-check changed surface", "avoid-broad-edits", "WatchPending");
        }

        if (status.Watch?.State is WatchProcessingState.RestartRequired or WatchProcessingState.BuildFailed or WatchProcessingState.RuntimeFaulted ||
            status.State is AppLifecycleState.Failed or AppLifecycleState.ExitedUnexpectedly)
        {
            return Budget("fix-current-failure", "diagnose-current-runtime", "retest before wider edits", "avoid-broad-edits", "RuntimeFailure");
        }

        if (status.LaneKind is RuntimeLaneKind.PublishedCandidate or RuntimeLaneKind.PublishedActive)
        {
            return Budget("atomic-candidate-next", "validate-candidate-runtime", "commit or rollback after browser-check", null, "AtomicLane");
        }

        return null;
    }

    public WorkflowGuidanceData? ForWait(AppWaitData wait)
    {
        if (!configuration.WorkflowGuidanceEnabled)
        {
            return null;
        }

        if (!wait.Satisfied)
        {
            return Budget("fix-current-failure", "inspect-current-state", "diagnose_start_failure or logs", "avoid-broad-edits", wait.TimedOut ? "WaitTimedOut" : "WaitUnsatisfied");
        }

        if (wait.Condition is AppWaitCondition.RevisionConfirmed or AppWaitCondition.Healthy or AppWaitCondition.WatchSettled)
        {
            return Budget("watch-validate-now", "browser-check-current-change", "only-then-consider-next-edit", "stay-nearby", "WaitSatisfied");
        }

        if (wait.Condition is AppWaitCondition.TransactionCommitted or AppWaitCondition.RollbackCommitted)
        {
            return Budget("rollback-available", "validate-current-revision", "rollback if needed", null, "TransactionResolved");
        }

        return null;
    }

    public WorkflowGuidanceData? ForOperation(OperationStatusData status)
    {
        if (!configuration.WorkflowGuidanceEnabled)
        {
            return null;
        }

        if (status.State == OperationState.Running || status.State == OperationState.Queued)
        {
            return Budget("build-then-check", "wait-current-operation", "review operation_status before next edit", "avoid-parallel-edits", "OperationPending");
        }

        if (status.State == OperationState.Completed)
        {
            return Budget("build-then-check", "inspect-changed-surface", "browser-check or targeted-tests", null, "OperationCompleted");
        }

        if (status.State is OperationState.Failed or OperationState.TimedOut)
        {
            return Budget("fix-current-failure", "fix-current-build-or-test", "rerun focused validation", "avoid-broad-edits", "OperationFailed");
        }

        return null;
    }

    public WorkflowGuidanceData? ForAtomic(AtomicUpdateData data)
    {
        if (!configuration.WorkflowGuidanceEnabled)
        {
            return null;
        }

        if (!data.Committed)
        {
            return Budget("atomic-candidate-next", "validate-candidate-runtime", "commit only after browser-check", null, "CandidateReady");
        }

        return Budget("rollback-available", "validate-committed-runtime", "rollback if regression appears", null, "Committed");
    }

    public WorkflowGuidanceData? ForRollback(AtomicRollbackData data)
    {
        if (!configuration.WorkflowGuidanceEnabled)
        {
            return null;
        }

        return Budget("watch-validate-now", "validate-restored-runtime", "resume-small-step-iteration", "stay-nearby", "RollbackCommitted");
    }

    public WorkflowGuidanceData? ForDiagnosis(DiagnoseStartFailureData data)
    {
        if (!configuration.WorkflowGuidanceEnabled)
        {
            return null;
        }

        return Budget("fix-current-failure", "apply-smallest-fix", "rerun targeted validation", "avoid-broad-edits", data.Category.ToString());
    }

    public bool ShouldEmit(string toolName)
        => configuration.WorkflowGuidanceEnabled && configuration.WorkflowGuidanceToolAllowList.Contains(toolName);

    private WorkflowGuidanceData? Budget(string mode, string next, string verify, string? guard, string? reasonCode)
    {
        var guidance = new WorkflowGuidanceData(mode, next, verify, guard, reasonCode);
        return JsonSerializer.Serialize(guidance, JsonOptions).Length <= configuration.WorkflowGuidanceMaxSerializedCharacters
            ? guidance
            : null;
    }
}
