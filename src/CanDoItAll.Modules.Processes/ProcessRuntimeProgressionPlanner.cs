namespace CanDoItAll.Modules.Processes;

internal static class ProcessRuntimeProgressionPlanner
{
    public static void ApplyTransitionConsequences(
        ProcessStepRunStatus targetStatus,
        ProcessStepDefinition currentStepDefinition,
        IReadOnlyDictionary<Guid, ProcessStepDefinition> stepDefinitionsById,
        IDictionary<Guid, ProcessStepRun> stepRunsByDefinitionId,
        IReadOnlyDictionary<Guid, List<ProcessStepDependencyDefinition>> stepDependenciesByStepId,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(currentStepDefinition);
        ArgumentNullException.ThrowIfNull(stepDefinitionsById);
        ArgumentNullException.ThrowIfNull(stepRunsByDefinitionId);
        ArgumentNullException.ThrowIfNull(stepDependenciesByStepId);

        if (targetStatus == ProcessStepRunStatus.Completed)
        {
            foreach (var dependentStep in GetDependentSteps(currentStepDefinition, stepDefinitionsById, stepDependenciesByStepId))
            {
                if (!stepRunsByDefinitionId.TryGetValue(dependentStep.Id, out var dependentStepRun))
                {
                    continue;
                }

                if (TryResolveImpossibleDependencyReason(
                    dependentStep,
                    stepDefinitionsById,
                    stepRunsByDefinitionId,
                    stepDependenciesByStepId,
                    out var impossibleReason))
                {
                    CascadeSkipStepRun(
                        dependentStep,
                        stepDefinitionsById,
                        stepRunsByDefinitionId,
                        stepDependenciesByStepId,
                        impossibleReason,
                        now);
                    continue;
                }

                if (dependentStepRun.Status == ProcessStepRunStatus.Pending &&
                    AreAllDependenciesSatisfied(dependentStep, stepRunsByDefinitionId, stepDependenciesByStepId))
                {
                    ActivatePendingStepRun(dependentStepRun, dependentStep, now);
                }
            }
        }

        if (targetStatus == ProcessStepRunStatus.Skipped)
        {
            var reason = $"Skipped because upstream step '{currentStepDefinition.Title}' was skipped.";
            CascadeSkipDependents(
                currentStepDefinition,
                stepDefinitionsById,
                stepRunsByDefinitionId,
                stepDependenciesByStepId,
                reason,
                now);
        }
    }

    private static IEnumerable<ProcessStepDefinition> GetDependentSteps(
        ProcessStepDefinition stepDefinition,
        IReadOnlyDictionary<Guid, ProcessStepDefinition> stepDefinitionsById,
        IReadOnlyDictionary<Guid, List<ProcessStepDependencyDefinition>> stepDependenciesByStepId)
    {
        return stepDefinitionsById.Values
            .Where(
                item => ProcessStepDependencyCollection.GetPersistedDependencies(item.Id, stepDependenciesByStepId)
                    .Any(dependency => dependency.DependsOnStepId == stepDefinition.Id))
            .OrderBy(item => item.OrderIndex);
    }

    private static void ActivatePendingStepRun(ProcessStepRun stepRun, ProcessStepDefinition stepDefinition, DateTimeOffset now)
    {
        if (stepRun.Status != ProcessStepRunStatus.Pending)
        {
            return;
        }

        stepRun.Status = stepDefinition.RequiresApproval || stepDefinition.StepKind == ProcessStepKind.Approval
            ? ProcessStepRunStatus.WaitingApproval
            : ProcessStepRunStatus.Ready;
        stepRun.ReadyAtUtc = now;
    }

    private static void CascadeSkipStepRun(
        ProcessStepDefinition stepDefinition,
        IReadOnlyDictionary<Guid, ProcessStepDefinition> stepDefinitionsById,
        IDictionary<Guid, ProcessStepRun> stepRunsByDefinitionId,
        IReadOnlyDictionary<Guid, List<ProcessStepDependencyDefinition>> stepDependenciesByStepId,
        string reason,
        DateTimeOffset now)
    {
        if (!stepRunsByDefinitionId.TryGetValue(stepDefinition.Id, out var stepRun) ||
            stepRun.Status != ProcessStepRunStatus.Pending)
        {
            return;
        }

        stepRun.Status = ProcessStepRunStatus.Skipped;
        stepRun.CompletedAtUtc = now;
        stepRun.DecisionSummary = reason;

        CascadeSkipDependents(stepDefinition, stepDefinitionsById, stepRunsByDefinitionId, stepDependenciesByStepId, reason, now);
    }

    private static void CascadeSkipDependents(
        ProcessStepDefinition stepDefinition,
        IReadOnlyDictionary<Guid, ProcessStepDefinition> stepDefinitionsById,
        IDictionary<Guid, ProcessStepRun> stepRunsByDefinitionId,
        IReadOnlyDictionary<Guid, List<ProcessStepDependencyDefinition>> stepDependenciesByStepId,
        string reason,
        DateTimeOffset now)
    {
        foreach (var dependentStep in GetDependentSteps(stepDefinition, stepDefinitionsById, stepDependenciesByStepId))
        {
            CascadeSkipStepRun(dependentStep, stepDefinitionsById, stepRunsByDefinitionId, stepDependenciesByStepId, reason, now);
        }
    }

    private static bool AreAllDependenciesSatisfied(
        ProcessStepDefinition stepDefinition,
        IDictionary<Guid, ProcessStepRun> stepRunsByDefinitionId,
        IReadOnlyDictionary<Guid, List<ProcessStepDependencyDefinition>> stepDependenciesByStepId)
    {
        foreach (var dependency in ProcessStepDependencyCollection.GetPersistedDependencies(stepDefinition.Id, stepDependenciesByStepId))
        {
            if (!stepRunsByDefinitionId.TryGetValue(dependency.DependsOnStepId, out var sourceStepRun) ||
                sourceStepRun.Status != ProcessStepRunStatus.Completed)
            {
                return false;
            }

            if (dependency.DependsOnBranchOutcomeId.HasValue &&
                sourceStepRun.SelectedBranchOutcomeId != dependency.DependsOnBranchOutcomeId)
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryResolveImpossibleDependencyReason(
        ProcessStepDefinition stepDefinition,
        IReadOnlyDictionary<Guid, ProcessStepDefinition> stepDefinitionsById,
        IDictionary<Guid, ProcessStepRun> stepRunsByDefinitionId,
        IReadOnlyDictionary<Guid, List<ProcessStepDependencyDefinition>> stepDependenciesByStepId,
        out string reason)
    {
        foreach (var dependency in ProcessStepDependencyCollection.GetPersistedDependencies(stepDefinition.Id, stepDependenciesByStepId))
        {
            if (!stepRunsByDefinitionId.TryGetValue(dependency.DependsOnStepId, out var sourceStepRun) ||
                !stepDefinitionsById.TryGetValue(dependency.DependsOnStepId, out var sourceStepDefinition))
            {
                continue;
            }

            if (sourceStepRun.Status == ProcessStepRunStatus.Skipped)
            {
                reason = $"Skipped because upstream step '{sourceStepDefinition.Title}' was skipped.";
                return true;
            }

            if (dependency.DependsOnBranchOutcomeId.HasValue &&
                sourceStepRun.Status == ProcessStepRunStatus.Completed &&
                sourceStepRun.SelectedBranchOutcomeId != dependency.DependsOnBranchOutcomeId)
            {
                reason = $"Skipped because upstream step '{sourceStepDefinition.Title}' selected a different branch outcome.";
                return true;
            }
        }

        reason = string.Empty;
        return false;
    }
}
