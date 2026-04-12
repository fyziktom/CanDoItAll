namespace CanDoItAll.Modules.Processes;

public sealed partial class ProcessesService
{
    private static void ActivatePendingStepRun(ProcessStepRun stepRun, ProcessStepDefinition stepDefinition, DateTimeOffset now) {
        if (stepRun.Status != ProcessStepRunStatus.Pending) {
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
        DateTimeOffset now) {
        if (!stepRunsByDefinitionId.TryGetValue(stepDefinition.Id, out var stepRun) ||
            stepRun.Status != ProcessStepRunStatus.Pending) {
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
        DateTimeOffset now) {
        foreach (var dependentStep in stepDefinitionsById.Values
                     .Where(item => GetPersistedDependencies(item, stepDependenciesByStepId)
                         .Any(dependency => dependency.DependsOnStepId == stepDefinition.Id))
                     .OrderBy(item => item.OrderIndex)) {
            CascadeSkipStepRun(dependentStep, stepDefinitionsById, stepRunsByDefinitionId, stepDependenciesByStepId, reason, now);
        }
    }

    private static bool AreAllDependenciesSatisfied(
        ProcessStepDefinition stepDefinition,
        IReadOnlyDictionary<Guid, ProcessStepRun> stepRunsByDefinitionId,
        IReadOnlyDictionary<Guid, List<ProcessStepDependencyDefinition>> stepDependenciesByStepId) {
        foreach (var dependency in GetPersistedDependencies(stepDefinition, stepDependenciesByStepId)) {
            if (!stepRunsByDefinitionId.TryGetValue(dependency.DependsOnStepId, out var sourceStepRun) ||
                sourceStepRun.Status != ProcessStepRunStatus.Completed) {
                return false;
            }

            if (dependency.DependsOnBranchOutcomeId.HasValue &&
                sourceStepRun.SelectedBranchOutcomeId != dependency.DependsOnBranchOutcomeId) {
                return false;
            }
        }

        return true;
    }

    private static bool TryResolveImpossibleDependencyReason(
        ProcessStepDefinition stepDefinition,
        IReadOnlyDictionary<Guid, ProcessStepDefinition> stepDefinitionsById,
        IReadOnlyDictionary<Guid, ProcessStepRun> stepRunsByDefinitionId,
        IReadOnlyDictionary<Guid, List<ProcessStepDependencyDefinition>> stepDependenciesByStepId,
        out string reason) {
        foreach (var dependency in GetPersistedDependencies(stepDefinition, stepDependenciesByStepId)) {
            if (!stepRunsByDefinitionId.TryGetValue(dependency.DependsOnStepId, out var sourceStepRun) ||
                !stepDefinitionsById.TryGetValue(dependency.DependsOnStepId, out var sourceStepDefinition)) {
                continue;
            }

            if (sourceStepRun.Status == ProcessStepRunStatus.Skipped) {
                reason = $"Skipped because upstream step '{sourceStepDefinition.Title}' was skipped.";
                return true;
            }

            if (dependency.DependsOnBranchOutcomeId.HasValue &&
                sourceStepRun.Status == ProcessStepRunStatus.Completed &&
                sourceStepRun.SelectedBranchOutcomeId != dependency.DependsOnBranchOutcomeId) {
                reason = $"Skipped because upstream step '{sourceStepDefinition.Title}' selected a different branch outcome.";
                return true;
            }
        }

        reason = string.Empty;
        return false;
    }

    private static string BuildTransitionJournalDescription(
        string stepTitle,
        ProcessStepRunStatus targetStatus,
        string reason,
        string? selectedBranchOutcomeTitle) {
        var description = $"{stepTitle} moved to {targetStatus}.";
        if (!string.IsNullOrWhiteSpace(selectedBranchOutcomeTitle)) {
            description += $" Selected branch outcome: {selectedBranchOutcomeTitle}.";
        }

        if (!string.IsNullOrWhiteSpace(reason)) {
            description += $" {reason.Trim()}";
        }

        return description.Trim();
    }
}
