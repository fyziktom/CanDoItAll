using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Processes;

internal sealed record ProcessDispatchCandidateHydrationSnapshot(
    ProcessRun Run,
    ProcessDefinition Definition,
    IReadOnlyList<ProcessStepRun> DispatchableSteps,
    IReadOnlyDictionary<Guid, ProcessWorkBrief> WorkBriefsByStepRunId,
    IReadOnlyDictionary<Guid, IReadOnlyList<ProcessStepRun>> StepRunsByDefinitionId,
    IReadOnlyList<ProcessArtifactRecord> ExistingArtifacts,
    HashSet<string> ExternalReferenceKeys,
    IReadOnlyDictionary<Guid, ProcessStepDefinition> ReadyStepDefinitionsById,
    IReadOnlyDictionary<Guid, IReadOnlyList<ProcessStepRoleAssignmentRequirement>> StepRoleRequirementsByStepDefinitionId,
    IReadOnlyDictionary<Guid, ProcessRoleRequirement> RoleRequirementsById,
    IReadOnlyList<ProcessRunAssignment> RunAssignments,
    IReadOnlyDictionary<Guid, IReadOnlyList<ProcessStepArtifactInputDefinition>> ArtifactInputsByStepDefinitionId,
    IReadOnlyDictionary<Guid, ProcessArtifactExpectation> ArtifactExpectationsById,
    IReadOnlyDictionary<Guid, ProcessStepDefinition> SourceStepsById,
    IReadOnlyDictionary<Guid, IReadOnlyList<ProcessStepBranchOutcomeDefinition>> BranchOutcomesByStepDefinitionId,
    IReadOnlyDictionary<Guid, HashSet<Guid>> ConditionalDependencyOutcomeIdsByStepDefinitionId);

internal static class ProcessDispatchCandidateHydrationLoader
{
    public static async Task<ProcessDispatchCandidateHydrationSnapshot?> LoadAsync(
        AppDbContext dbContext,
        Guid processRunId,
        Guid claimedStepRunId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        var run = await dbContext.Set<ProcessRun>()
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == processRunId, cancellationToken);
        if (run is null || !ProcessDispatchRouteEligibility.IsRunEligibleForDispatchCandidate(run.Status))
        {
            return null;
        }

        var definition = await dbContext.Set<ProcessDefinition>()
            .AsNoTracking()
            .SingleAsync(item => item.Id == run.ProcessDefinitionId, cancellationToken);
        var dispatchableSteps = await dbContext.Set<ProcessStepRun>()
            .AsNoTracking()
            .Where(item => item.ProcessRunId == processRunId &&
                item.Id == claimedStepRunId &&
                (item.Status == ProcessStepRunStatus.Ready ||
                 item.Status == ProcessStepRunStatus.WaitingApproval ||
                 item.Status == ProcessStepRunStatus.InProgress))
            .OrderBy(item => item.Sequence)
            .ToListAsync(cancellationToken);
        dispatchableSteps = dispatchableSteps
            .Where(item => ProcessDispatchRouteEligibility.IsStepStatusDispatchableForRun(run.Status, item.Status))
            .ToList();
        if (dispatchableSteps.Count == 0)
        {
            return null;
        }

        var stepRunIds = dispatchableSteps.Select(item => item.Id).ToList();
        var workBriefsByStepRunId = (await dbContext.Set<ProcessWorkBrief>()
                .AsNoTracking()
                .Where(item => item.ProcessRunId == processRunId && item.StepRunId.HasValue && stepRunIds.Contains(item.StepRunId.Value))
                .ToListAsync(cancellationToken))
            .OrderByDescending(item => item.CreatedAtUtc)
            .GroupBy(item => item.StepRunId!.Value)
            .ToDictionary(group => group.Key, group => group.First());
        var allStepRuns = await dbContext.Set<ProcessStepRun>()
            .AsNoTracking()
            .Where(item => item.ProcessRunId == processRunId)
            .ToListAsync(cancellationToken);
        var stepRunsByDefinitionId = allStepRuns
            .GroupBy(item => item.StepDefinitionId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<ProcessStepRun>)group
                    .OrderByDescending(item => item.Sequence)
                    .ToList());
        var existingArtifacts = (await dbContext.Set<ProcessArtifactRecord>()
                .AsNoTracking()
                .Where(item => item.ProcessRunId == processRunId)
                .ToListAsync(cancellationToken))
            .OrderByDescending(item => item.CreatedAtUtc)
            .ToList();
        var externalReferenceKeys = existingArtifacts
            .Select(item => item.ExternalReferenceKey)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var readyStepDefinitionIds = dispatchableSteps
            .Select(item => item.StepDefinitionId)
            .Distinct()
            .ToList();
        var readyStepDefinitionsById = readyStepDefinitionIds.Count == 0
            ? new Dictionary<Guid, ProcessStepDefinition>()
            : await dbContext.Set<ProcessStepDefinition>()
                .AsNoTracking()
                .Where(item => readyStepDefinitionIds.Contains(item.Id))
                .ToDictionaryAsync(item => item.Id, cancellationToken);
        var stepRoleRequirements = readyStepDefinitionIds.Count == 0
            ? []
            : await dbContext.Set<ProcessStepRoleAssignmentRequirement>()
                .AsNoTracking()
                .Where(item => readyStepDefinitionIds.Contains(item.StepDefinitionId))
                .OrderBy(item => item.FallbackOrder)
                .ToListAsync(cancellationToken);
        var stepRoleRequirementsByStepDefinitionId = stepRoleRequirements
            .GroupBy(item => item.StepDefinitionId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<ProcessStepRoleAssignmentRequirement>)group.ToList());
        var roleRequirementIds = stepRoleRequirements
            .Select(item => item.RoleRequirementId)
            .Distinct()
            .ToList();
        var roleRequirementsById = roleRequirementIds.Count == 0
            ? new Dictionary<Guid, ProcessRoleRequirement>()
            : await dbContext.Set<ProcessRoleRequirement>()
                .AsNoTracking()
                .Where(item => roleRequirementIds.Contains(item.Id))
                .ToDictionaryAsync(item => item.Id, cancellationToken);
        var runAssignments = await dbContext.Set<ProcessRunAssignment>()
            .AsNoTracking()
            .Where(item => item.ProcessRunId == processRunId)
            .ToListAsync(cancellationToken);
        var artifactInputs = readyStepDefinitionIds.Count == 0
            ? []
            : await dbContext.Set<ProcessStepArtifactInputDefinition>()
                .AsNoTracking()
                .Where(item => readyStepDefinitionIds.Contains(item.StepDefinitionId))
                .OrderBy(item => item.DisplayOrder)
                .ToListAsync(cancellationToken);
        var artifactInputsByStepDefinitionId = artifactInputs
            .GroupBy(item => item.StepDefinitionId)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<ProcessStepArtifactInputDefinition>)group.ToList());
        var artifactExpectationIds = artifactInputs
            .Select(item => item.ArtifactExpectationId)
            .Distinct()
            .ToList();
        var branchOutcomes = readyStepDefinitionIds.Count == 0
            ? []
            : await dbContext.Set<ProcessStepBranchOutcomeDefinition>()
                .AsNoTracking()
                .Where(item => readyStepDefinitionIds.Contains(item.StepDefinitionId))
                .OrderBy(item => item.DisplayOrder)
                .ToListAsync(cancellationToken);
        var branchOutcomesByStepDefinitionId = branchOutcomes
            .GroupBy(item => item.StepDefinitionId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<ProcessStepBranchOutcomeDefinition>)group.ToList());
        var conditionalDependencies = readyStepDefinitionIds.Count == 0
            ? []
            : await dbContext.Set<ProcessStepDependencyDefinition>()
                .AsNoTracking()
                .Where(item => readyStepDefinitionIds.Contains(item.DependsOnStepId) && item.DependsOnBranchOutcomeId.HasValue)
                .ToListAsync(cancellationToken);
        var conditionalDependencyOutcomeIdsByStepDefinitionId = conditionalDependencies
            .GroupBy(item => item.DependsOnStepId)
            .ToDictionary(
                group => group.Key,
                group => group
                    .Where(item => item.DependsOnBranchOutcomeId.HasValue)
                    .Select(item => item.DependsOnBranchOutcomeId!.Value)
                    .ToHashSet());
        var artifactExpectationsById = artifactExpectationIds.Count == 0
            ? new Dictionary<Guid, ProcessArtifactExpectation>()
            : await dbContext.Set<ProcessArtifactExpectation>()
                .AsNoTracking()
                .Where(item => artifactExpectationIds.Contains(item.Id))
                .ToDictionaryAsync(item => item.Id, cancellationToken);
        var sourceStepDefinitionIds = artifactExpectationsById.Values
            .Select(item => item.StepDefinitionId)
            .Distinct()
            .ToList();
        var sourceStepsById = sourceStepDefinitionIds.Count == 0
            ? new Dictionary<Guid, ProcessStepDefinition>()
            : await dbContext.Set<ProcessStepDefinition>()
                .AsNoTracking()
                .Where(item => sourceStepDefinitionIds.Contains(item.Id))
                .ToDictionaryAsync(item => item.Id, cancellationToken);

        return new ProcessDispatchCandidateHydrationSnapshot(
            run,
            definition,
            dispatchableSteps,
            workBriefsByStepRunId,
            stepRunsByDefinitionId,
            existingArtifacts,
            externalReferenceKeys,
            readyStepDefinitionsById,
            stepRoleRequirementsByStepDefinitionId,
            roleRequirementsById,
            runAssignments,
            artifactInputsByStepDefinitionId,
            artifactExpectationsById,
            sourceStepsById,
            branchOutcomesByStepDefinitionId,
            conditionalDependencyOutcomeIdsByStepDefinitionId);
    }
}
