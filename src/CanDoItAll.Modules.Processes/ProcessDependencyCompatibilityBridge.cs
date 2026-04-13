namespace CanDoItAll.Modules.Processes;

internal static class ProcessDependencyCompatibilityBridge
{
    public static IReadOnlyList<ProcessStepDependencyEditorModel> GetCanonicalEditorDependencies(ProcessStepEditorModel step)
    {
        ArgumentNullException.ThrowIfNull(step);

        if (step.Dependencies.Count > 0)
        {
            return step.Dependencies;
        }

        if (!step.DependsOnStepId.HasValue || step.DependsOnStepId.Value == Guid.Empty)
        {
            return [];
        }

        step.Dependencies =
        [
            new ProcessStepDependencyEditorModel
            {
                Id = Guid.NewGuid(),
                DependsOnStepId = step.DependsOnStepId,
                DependsOnBranchOutcomeId = step.DependsOnBranchOutcomeId
            }
        ];

        return step.Dependencies;
    }

    public static void SetCanonicalEditorDependencies(
        ProcessStepEditorModel step,
        IEnumerable<ProcessStepDependencyEditorModel> dependencies)
    {
        ArgumentNullException.ThrowIfNull(step);
        ArgumentNullException.ThrowIfNull(dependencies);

        var materialized = dependencies
            .Where(dependency => dependency.DependsOnStepId.HasValue && dependency.DependsOnStepId.Value != Guid.Empty)
            .Select(dependency => new ProcessStepDependencyEditorModel
            {
                Id = dependency.Id ?? Guid.NewGuid(),
                DependsOnStepId = dependency.DependsOnStepId,
                DependsOnBranchOutcomeId = dependency.DependsOnBranchOutcomeId
            })
            .ToList();

        step.Dependencies = materialized;
        SyncLegacyEditorPrimaryDependency(step);
    }

    public static void SetLegacyEditorPrimaryDependency(
        ProcessStepEditorModel step,
        Guid? dependsOnStepId,
        Guid? dependsOnBranchOutcomeId)
    {
        ArgumentNullException.ThrowIfNull(step);

        if (!dependsOnStepId.HasValue || dependsOnStepId.Value == Guid.Empty)
        {
            SetCanonicalEditorDependencies(step, []);
            return;
        }

        SetCanonicalEditorDependencies(
            step,
            [
                new ProcessStepDependencyEditorModel
                {
                    Id = Guid.NewGuid(),
                    DependsOnStepId = dependsOnStepId,
                    DependsOnBranchOutcomeId = dependsOnBranchOutcomeId
                }
            ]);
    }

    public static List<ProcessStepDependencyEditorModel> BuildEditorDependencies(
        ProcessStepDefinition step,
        IReadOnlyList<ProcessStepDependencyDefinition> allDependencies)
    {
        ArgumentNullException.ThrowIfNull(step);
        ArgumentNullException.ThrowIfNull(allDependencies);

        var dependencies = allDependencies
            .Where(item => item.StepDefinitionId == step.Id)
            .OrderBy(item => item.DisplayOrder)
            .Select(item => new ProcessStepDependencyEditorModel
            {
                Id = item.Id,
                DependsOnStepId = item.DependsOnStepId,
                DependsOnBranchOutcomeId = item.DependsOnBranchOutcomeId
            })
            .ToList();
        if (dependencies.Count > 0)
        {
            return dependencies;
        }

        if (!step.DependsOnStepId.HasValue)
        {
            return [];
        }

        return
        [
            new ProcessStepDependencyEditorModel
            {
                Id = Guid.NewGuid(),
                DependsOnStepId = step.DependsOnStepId,
                DependsOnBranchOutcomeId = step.DependsOnBranchOutcomeId
            }
        ];
    }

    public static IReadOnlyList<ProcessStepDependencyDefinition> GetCanonicalPersistedDependencies(
        ProcessStepDefinition step,
        IReadOnlyDictionary<Guid, List<ProcessStepDependencyDefinition>> dependenciesByStepId)
    {
        ArgumentNullException.ThrowIfNull(step);
        ArgumentNullException.ThrowIfNull(dependenciesByStepId);

        if (dependenciesByStepId.TryGetValue(step.Id, out var dependencies) && dependencies.Count > 0)
        {
            return dependencies;
        }

        if (!step.DependsOnStepId.HasValue)
        {
            return [];
        }

        return
        [
            new ProcessStepDependencyDefinition
            {
                StepDefinitionId = step.Id,
                DependsOnStepId = step.DependsOnStepId.Value,
                DependsOnBranchOutcomeId = step.DependsOnBranchOutcomeId
            }
        ];
    }

    public static IReadOnlyList<ProcessStepDependencyViewModel> BuildRuntimeDependencies(
        ProcessStepDefinition step,
        IReadOnlyDictionary<Guid, List<ProcessStepDependencyDefinition>> dependenciesByStepId)
    {
        return GetCanonicalPersistedDependencies(step, dependenciesByStepId)
            .Select(item => new ProcessStepDependencyViewModel(item.DependsOnStepId, item.DependsOnBranchOutcomeId))
            .ToList();
    }

    public static void SyncLegacyEditorPrimaryDependency(ProcessStepEditorModel step)
    {
        ArgumentNullException.ThrowIfNull(step);

        var primaryDependency = step.Dependencies
            .FirstOrDefault(dependency => dependency.DependsOnStepId.HasValue && dependency.DependsOnStepId.Value != Guid.Empty);
        step.DependsOnStepId = primaryDependency?.DependsOnStepId;
        step.DependsOnBranchOutcomeId = primaryDependency?.DependsOnBranchOutcomeId;
    }

    public static void SyncLegacyPersistedPrimaryDependency(
        ProcessStepDefinition step,
        IEnumerable<ProcessStepDependencyDefinition> dependencies)
    {
        ArgumentNullException.ThrowIfNull(step);
        ArgumentNullException.ThrowIfNull(dependencies);

        var primaryDependency = dependencies.FirstOrDefault();
        step.DependsOnStepId = primaryDependency?.DependsOnStepId;
        step.DependsOnBranchOutcomeId = primaryDependency?.DependsOnBranchOutcomeId;
    }
}
