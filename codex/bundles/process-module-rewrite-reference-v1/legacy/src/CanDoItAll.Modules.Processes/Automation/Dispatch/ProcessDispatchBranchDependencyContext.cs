using DispatchBranchOutcome = CanDoItAll.Modules.Processes.ProcessRunAutomationDispatchService.DispatchBranchOutcome;

namespace CanDoItAll.Modules.Processes;

internal sealed record ProcessDispatchBranchDependencyContext(
    IReadOnlyList<DispatchBranchOutcome> BranchOutcomes,
    bool RequiresExplicitBranchOutcomeSelection)
{
    public static ProcessDispatchBranchDependencyContext Create(
        ProcessStepRun stepRun,
        IReadOnlyDictionary<Guid, IReadOnlyList<ProcessStepBranchOutcomeDefinition>> branchOutcomesByStepDefinitionId,
        IReadOnlyDictionary<Guid, HashSet<Guid>> conditionalDependencyOutcomeIdsByStepDefinitionId)
    {
        ArgumentNullException.ThrowIfNull(stepRun);

        var branchOutcomes = branchOutcomesByStepDefinitionId.TryGetValue(stepRun.StepDefinitionId, out var configuredBranchOutcomes)
            ? configuredBranchOutcomes
                .Select(item => new DispatchBranchOutcome(item.Id, item.Key, item.Title, item.Description))
                .ToList()
            : [];
        var requiresExplicitBranchOutcomeSelection =
            conditionalDependencyOutcomeIdsByStepDefinitionId.TryGetValue(stepRun.StepDefinitionId, out var requiredBranchOutcomeIds) &&
            branchOutcomes.Any(item => requiredBranchOutcomeIds.Contains(item.Id));

        return new ProcessDispatchBranchDependencyContext(branchOutcomes, requiresExplicitBranchOutcomeSelection);
    }
}
