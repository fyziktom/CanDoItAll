namespace CanDoItAll.Modules.Processes;

internal static class ProcessStepDependencyCollection
{
    public static IReadOnlyList<ProcessStepDependencyEditorModel> GetOrderedEditorDependencies(ProcessStepEditorModel step)
    {
        ArgumentNullException.ThrowIfNull(step);

        return [.. step.Dependencies];
    }

    public static void SetEditorDependencies(
        ProcessStepEditorModel step,
        IEnumerable<ProcessStepDependencyEditorModel> dependencies)
    {
        ArgumentNullException.ThrowIfNull(step);
        ArgumentNullException.ThrowIfNull(dependencies);

        step.Dependencies = dependencies
            .Where(dependency => dependency.DependsOnStepId.HasValue && dependency.DependsOnStepId.Value != Guid.Empty)
            .Select(
                dependency => new ProcessStepDependencyEditorModel
                {
                    Id = dependency.Id ?? Guid.NewGuid(),
                    DependsOnStepId = dependency.DependsOnStepId,
                    DependsOnBranchOutcomeId = dependency.DependsOnBranchOutcomeId
                })
            .ToList();
    }

    public static ProcessStepDependencyEditorModel CreateEditorDependency(
        Guid dependsOnStepId,
        Guid? dependsOnBranchOutcomeId)
    {
        return new ProcessStepDependencyEditorModel
        {
            Id = Guid.NewGuid(),
            DependsOnStepId = dependsOnStepId,
            DependsOnBranchOutcomeId = dependsOnBranchOutcomeId
        };
    }

    public static List<ProcessStepDependencyEditorModel> BuildEditorDependencies(
        Guid stepId,
        IReadOnlyList<ProcessStepDependencyDefinition> allDependencies)
    {
        ArgumentNullException.ThrowIfNull(allDependencies);

        return allDependencies
            .Where(item => item.StepDefinitionId == stepId)
            .OrderBy(item => item.DisplayOrder)
            .Select(
                item => new ProcessStepDependencyEditorModel
                {
                    Id = item.Id,
                    DependsOnStepId = item.DependsOnStepId,
                    DependsOnBranchOutcomeId = item.DependsOnBranchOutcomeId
                })
            .ToList();
    }

    public static IReadOnlyList<ProcessStepDependencyDefinition> GetPersistedDependencies(
        Guid stepId,
        IReadOnlyDictionary<Guid, List<ProcessStepDependencyDefinition>> dependenciesByStepId)
    {
        ArgumentNullException.ThrowIfNull(dependenciesByStepId);

        return dependenciesByStepId.TryGetValue(stepId, out var dependencies) && dependencies.Count > 0
            ? dependencies
            : [];
    }

    public static IReadOnlyList<ProcessStepDependencyViewModel> BuildRuntimeDependencies(
        Guid stepId,
        IReadOnlyDictionary<Guid, List<ProcessStepDependencyDefinition>> dependenciesByStepId)
    {
        return GetPersistedDependencies(stepId, dependenciesByStepId)
            .Select(item => new ProcessStepDependencyViewModel(item.DependsOnStepId, item.DependsOnBranchOutcomeId))
            .ToList();
    }
}
