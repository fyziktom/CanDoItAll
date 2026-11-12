namespace CanDoItAll.Processes.Runtime;

public static class ProcessRuntimeTerminalStates {
    public static bool IsRunTerminal(ProcessRuntimeStatus status)
        => status is ProcessRuntimeStatus.Completed or ProcessRuntimeStatus.Failed or ProcessRuntimeStatus.Cancelled;

    public static bool IsStepTerminal(ProcessRuntimeStepStatus status)
        => status is ProcessRuntimeStepStatus.Completed or ProcessRuntimeStepStatus.Failed or ProcessRuntimeStepStatus.Cancelled or ProcessRuntimeStepStatus.Skipped;

    public static bool IsChildRunStopped(ProcessRuntimeStatus status)
        => IsRunTerminal(status) || status == ProcessRuntimeStatus.Blocked;

    public static ProcessRuntimeStateSnapshot CompleteRunIfTerminal(ProcessRuntimeStateSnapshot state, DateTimeOffset occurredAtUtc) {
        var hasOpenExecutableSteps = false;
        var hasFailedStep = false;
        var hasCancelledStep = false;
        foreach (var step in state.Steps) {
            if (!step.IsExecutable) {
                continue;
            }
            if (step.Status == ProcessRuntimeStepStatus.Failed) {
                hasFailedStep = true;
                break;
            }
            if (step.Status == ProcessRuntimeStepStatus.Cancelled) {
                hasCancelledStep = true;
            }
            if (!IsStepTerminal(step.Status)) {
                hasOpenExecutableSteps = true;
            }
        }
        if (hasFailedStep) {
            return state with { Status = ProcessRuntimeStatus.Failed, UpdatedAtUtc = occurredAtUtc };
        }
        if (hasCancelledStep && !hasOpenExecutableSteps) {
            return state with { Status = ProcessRuntimeStatus.Cancelled, UpdatedAtUtc = occurredAtUtc };
        }
        return !hasOpenExecutableSteps
            ? state with { Status = ProcessRuntimeStatus.Completed, UpdatedAtUtc = occurredAtUtc }
            : state;
    }
}
