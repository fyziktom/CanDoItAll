namespace CanDoItAll.Modules.Processes;

internal static class ProcessRunStatusResolver
{
    public static ProcessRunStatus Resolve(IReadOnlyList<ProcessStepRun> stepRuns)
    {
        ArgumentNullException.ThrowIfNull(stepRuns);

        if (stepRuns.Count == 0)
        {
            return ProcessRunStatus.Active;
        }

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

    public static ProcessRunStatus Resolve(
        IReadOnlyList<ProcessStepRun> stepRuns,
        IReadOnlyDictionary<Guid, List<ProcessStepDependencyDefinition>> stepDependenciesByStepId,
        IReadOnlyDictionary<Guid, List<ProcessStepBranchOutcomeDefinition>> branchOutcomesByStepId)
    {
        ArgumentNullException.ThrowIfNull(stepRuns);
        ArgumentNullException.ThrowIfNull(stepDependenciesByStepId);
        ArgumentNullException.ThrowIfNull(branchOutcomesByStepId);

        var status = Resolve(stepRuns);
        return status == ProcessRunStatus.Completed &&
            HasUnhandledExceptionBranch(stepRuns, stepDependenciesByStepId, branchOutcomesByStepId)
                ? ProcessRunStatus.Blocked
                : status;
    }

    public static ProcessRunStatus Resolve(IReadOnlyList<ProcessStepRun> persistedStepRuns, ProcessStepRun currentStepRun)
    {
        ArgumentNullException.ThrowIfNull(persistedStepRuns);
        ArgumentNullException.ThrowIfNull(currentStepRun);

        var stepRuns = persistedStepRuns
            .Where(item => item.Id != currentStepRun.Id)
            .Append(currentStepRun)
            .ToList();
        return Resolve(stepRuns);
    }

    public static ProcessRunStatus Resolve(
        IReadOnlyList<ProcessStepRun> persistedStepRuns,
        ProcessStepRun currentStepRun,
        IReadOnlyDictionary<Guid, List<ProcessStepDependencyDefinition>> stepDependenciesByStepId,
        IReadOnlyDictionary<Guid, List<ProcessStepBranchOutcomeDefinition>> branchOutcomesByStepId)
    {
        ArgumentNullException.ThrowIfNull(persistedStepRuns);
        ArgumentNullException.ThrowIfNull(currentStepRun);
        ArgumentNullException.ThrowIfNull(stepDependenciesByStepId);
        ArgumentNullException.ThrowIfNull(branchOutcomesByStepId);

        var stepRuns = persistedStepRuns
            .Where(item => item.Id != currentStepRun.Id)
            .Append(currentStepRun)
            .ToList();
        return Resolve(stepRuns, stepDependenciesByStepId, branchOutcomesByStepId);
    }

    private static bool HasUnhandledExceptionBranch(
        IReadOnlyList<ProcessStepRun> stepRuns,
        IReadOnlyDictionary<Guid, List<ProcessStepDependencyDefinition>> stepDependenciesByStepId,
        IReadOnlyDictionary<Guid, List<ProcessStepBranchOutcomeDefinition>> branchOutcomesByStepId)
    {
        var stepRunsByDefinitionId = stepRuns.ToDictionary(item => item.StepDefinitionId);
        var branchOutcomesById = branchOutcomesByStepId
            .SelectMany(item => item.Value)
            .ToDictionary(item => item.Id);

        foreach (var stepRun in stepRuns)
        {
            if (stepRun.Status != ProcessStepRunStatus.Completed ||
                !stepRun.SelectedBranchOutcomeId.HasValue ||
                !branchOutcomesById.TryGetValue(stepRun.SelectedBranchOutcomeId.Value, out var selectedBranchOutcome) ||
                !ProcessBranchOutcomeRouting.IsExceptionRoutingBranchOutcome(selectedBranchOutcome))
            {
                continue;
            }

            var exceptionBranchDependentStepIds = stepDependenciesByStepId
                .Where(item => item.Value.Any(dependency =>
                    dependency.DependsOnStepId == stepRun.StepDefinitionId &&
                    dependency.DependsOnBranchOutcomeId == selectedBranchOutcome.Id))
                .Select(item => item.Key)
                .ToArray();

            if (exceptionBranchDependentStepIds.Length == 0)
            {
                return true;
            }

            var hasHandledDependent = exceptionBranchDependentStepIds.Any(stepDefinitionId =>
                stepRunsByDefinitionId.TryGetValue(stepDefinitionId, out var dependentRun) &&
                dependentRun.Status != ProcessStepRunStatus.Skipped);
            if (!hasHandledDependent)
            {
                return true;
            }
        }

        return false;
    }
}
