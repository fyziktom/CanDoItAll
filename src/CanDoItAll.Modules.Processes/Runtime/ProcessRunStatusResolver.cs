namespace CanDoItAll.Modules.Processes;

internal static class ProcessRunStatusResolver
{
    public static ProcessRunStatus Resolve(IReadOnlyList<ProcessStepRun> persistedStepRuns, ProcessStepRun currentStepRun)
    {
        ArgumentNullException.ThrowIfNull(persistedStepRuns);
        ArgumentNullException.ThrowIfNull(currentStepRun);

        var stepRuns = persistedStepRuns
            .Where(item => item.Id != currentStepRun.Id)
            .Append(currentStepRun)
            .ToList();
        if (stepRuns.All(item => item.Status == ProcessStepRunStatus.Completed || item.Status == ProcessStepRunStatus.Skipped))
        {
            return ProcessRunStatus.Completed;
        }

        if (stepRuns.Any(item => item.Status == ProcessStepRunStatus.Failed))
        {
            return ProcessRunStatus.Failed;
        }

        if (stepRuns.Any(item => item.Status == ProcessStepRunStatus.Blocked))
        {
            return ProcessRunStatus.Blocked;
        }

        return ProcessRunStatus.Active;
    }
}
