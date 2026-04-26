using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Processes;

internal sealed class ProcessRuntimeBindingCatalog
{
    public required IReadOnlyDictionary<string, Guid> RoleRequirementIdsByKey { get; init; }

    public required IReadOnlyDictionary<string, Guid> StepDefinitionIdsByKey { get; init; }

    public required IReadOnlyDictionary<string, Guid> BranchOutcomeIdsByCompositeKey { get; init; }
}

public sealed partial class ProcessesService
{
    internal async Task<ProcessRuntimeBindingCatalog?> GetRuntimeBindingCatalogAsync(
        Guid runId,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var run = await dbContext.Set<ProcessRun>()
            .SingleOrDefaultAsync(item => item.Id == runId, cancellationToken);
        if (run is null)
        {
            return null;
        }

        var roles = await dbContext.Set<ProcessRoleRequirement>()
            .Where(item => item.ProcessDefinitionVersionId == run.ProcessDefinitionVersionId)
            .ToListAsync(cancellationToken);
        var steps = await dbContext.Set<ProcessStepDefinition>()
            .Where(item => item.ProcessDefinitionVersionId == run.ProcessDefinitionVersionId)
            .ToListAsync(cancellationToken);
        var stepIds = steps.Select(item => item.Id).ToList();
        var branchOutcomes = stepIds.Count == 0
            ? []
            : await dbContext.Set<ProcessStepBranchOutcomeDefinition>()
                .Where(item => stepIds.Contains(item.StepDefinitionId))
                .ToListAsync(cancellationToken);

        var stepKeysById = steps
            .Where(item => !string.IsNullOrWhiteSpace(item.Key))
            .ToDictionary(item => item.Id, item => item.Key, comparer: EqualityComparer<Guid>.Default);

        return new ProcessRuntimeBindingCatalog
        {
            RoleRequirementIdsByKey = roles
                .Where(item => !string.IsNullOrWhiteSpace(item.Key))
                .ToDictionary(item => item.Key, item => item.Id, StringComparer.OrdinalIgnoreCase),
            StepDefinitionIdsByKey = steps
                .Where(item => !string.IsNullOrWhiteSpace(item.Key))
                .ToDictionary(item => item.Key, item => item.Id, StringComparer.OrdinalIgnoreCase),
            BranchOutcomeIdsByCompositeKey = branchOutcomes
                .Where(item =>
                    !string.IsNullOrWhiteSpace(item.Key) &&
                    stepKeysById.ContainsKey(item.StepDefinitionId))
                .ToDictionary(
                    item => BuildCompositeBranchKey(stepKeysById[item.StepDefinitionId], item.Key),
                    item => item.Id,
                    StringComparer.OrdinalIgnoreCase)
        };
    }

    private static string BuildCompositeBranchKey(string stepKey, string branchOutcomeKey)
    {
        return stepKey.Trim() + "|" + branchOutcomeKey.Trim();
    }
}
