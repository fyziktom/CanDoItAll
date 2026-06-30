namespace CanDoItAll.Processes.Core;

public enum ProcessRunState
{
    Planned,
    Running,
    Waiting,
    Completed,
    Failed,
    Canceled
}

public enum ProcessStepState
{
    Pending,
    Ready,
    Running,
    Waiting,
    Completed,
    Failed,
    Skipped,
    Canceled
}

public static class ProcessStateTransitionRules
{
    public static bool CanTransition(ProcessRunState from, ProcessRunState to)
    {
        return from switch
        {
            ProcessRunState.Planned => to is ProcessRunState.Running or ProcessRunState.Canceled,
            ProcessRunState.Running => to is ProcessRunState.Waiting or ProcessRunState.Completed or ProcessRunState.Failed or ProcessRunState.Canceled,
            ProcessRunState.Waiting => to is ProcessRunState.Running or ProcessRunState.Failed or ProcessRunState.Canceled,
            ProcessRunState.Completed or ProcessRunState.Failed or ProcessRunState.Canceled => false,
            _ => false
        };
    }

    public static bool CanTransition(ProcessStepState from, ProcessStepState to)
    {
        return from switch
        {
            ProcessStepState.Pending => to is ProcessStepState.Ready or ProcessStepState.Skipped or ProcessStepState.Canceled,
            ProcessStepState.Ready => to is ProcessStepState.Running or ProcessStepState.Skipped or ProcessStepState.Canceled,
            ProcessStepState.Running => to is ProcessStepState.Waiting or ProcessStepState.Completed or ProcessStepState.Failed or ProcessStepState.Canceled,
            ProcessStepState.Waiting => to is ProcessStepState.Running or ProcessStepState.Failed or ProcessStepState.Canceled,
            ProcessStepState.Completed or ProcessStepState.Failed or ProcessStepState.Skipped or ProcessStepState.Canceled => false,
            _ => false
        };
    }

    public static ProcessValidationResult Validate(ProcessRunState from, ProcessRunState to)
    {
        return CanTransition(from, to)
            ? ProcessValidationResult.Success
            : ProcessValidationResult.From(
            [
                new ProcessValidationFailure(
                    "Runtime.InvalidRunTransition",
                    $"Run transition from '{from}' to '{to}' is not allowed.")
            ]);
    }

    public static ProcessValidationResult Validate(ProcessStepState from, ProcessStepState to)
    {
        return CanTransition(from, to)
            ? ProcessValidationResult.Success
            : ProcessValidationResult.From(
            [
                new ProcessValidationFailure(
                    "Runtime.InvalidStepTransition",
                    $"Step transition from '{from}' to '{to}' is not allowed.")
            ]);
    }
}
