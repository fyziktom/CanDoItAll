using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Projects;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Processes;

public interface IProcessDefinitionListQueryService
{
    Task<IReadOnlyList<ProcessDefinitionListItem>> ListAsync(
        AppDbContext dbContext,
        Guid? projectId,
        CancellationToken cancellationToken);
}

public sealed class ProcessDefinitionListQueryService : IProcessDefinitionListQueryService
{
    public async Task<IReadOnlyList<ProcessDefinitionListItem>> ListAsync(
        AppDbContext dbContext,
        Guid? projectId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        var definitionsQuery = dbContext.Set<ProcessDefinition>()
            .AsNoTracking()
            .AsQueryable();
        if (projectId.HasValue)
        {
            definitionsQuery = definitionsQuery.Where(definition => definition.ProjectId == projectId.Value);
        }

        var definitions = (await definitionsQuery
            .Select(
                definition => new ProcessDefinitionListProjection(
                    definition.Id,
                    definition.ProjectId,
                    definition.Name,
                    definition.Status,
                    definition.ActivePublishedVersionId,
                    definition.Summary,
                    definition.ValueStatement,
                    definition.UpdatedAtUtc))
            .ToListAsync(cancellationToken))
            .OrderByDescending(definition => definition.UpdatedAtUtc)
            .ToList();
        if (definitions.Count == 0)
        {
            return [];
        }

        var definitionIds = definitions.Select(definition => definition.Id).ToList();
        var versions = await dbContext.Set<ProcessDefinitionVersion>()
            .AsNoTracking()
            .Where(item => definitionIds.Contains(item.ProcessDefinitionId))
            .Select(
                item => new ProcessDefinitionVersionProjection(
                    item.Id,
                    item.ProcessDefinitionId,
                    item.VersionNumber,
                    item.Status))
            .ToListAsync(cancellationToken);
        var summaryVersionsByDefinitionId = versions
            .GroupBy(item => item.ProcessDefinitionId)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderBy(item => item.Status == ProcessVersionStatus.Draft ? 0 : 1)
                    .ThenByDescending(item => item.VersionNumber)
                    .First());
        var summaryVersionIds = summaryVersionsByDefinitionId.Values
            .Select(item => item.Id)
            .Distinct()
            .ToList();

        var roleIdsByVersionId = summaryVersionIds.Count == 0
            ? new Dictionary<Guid, HashSet<Guid>>()
            : (await dbContext.Set<ProcessRoleRequirement>()
                    .AsNoTracking()
                    .Where(item => summaryVersionIds.Contains(item.ProcessDefinitionVersionId))
                    .Select(item => new ProcessRoleVersionProjection(item.ProcessDefinitionVersionId, item.Id))
                    .ToListAsync(cancellationToken))
                .GroupBy(item => item.ProcessDefinitionVersionId)
                .ToDictionary(
                    group => group.Key,
                    group => group
                        .Select(item => item.RoleRequirementId)
                        .ToHashSet());
        var stepCountByVersionId = summaryVersionIds.Count == 0
            ? new Dictionary<Guid, int>()
            : await dbContext.Set<ProcessStepDefinition>()
                .AsNoTracking()
                .Where(item => summaryVersionIds.Contains(item.ProcessDefinitionVersionId))
                .GroupBy(item => item.ProcessDefinitionVersionId)
                .Select(group => new ProcessVersionCountProjection(group.Key, group.Count()))
                .ToDictionaryAsync(item => item.ProcessDefinitionVersionId, item => item.Count, cancellationToken);

        var runs = await dbContext.Set<ProcessRun>()
            .AsNoTracking()
            .Where(item => definitionIds.Contains(item.ProcessDefinitionId))
            .Select(item => new ProcessDefinitionRunProjection(item.Id, item.ProcessDefinitionId, item.Status))
            .ToListAsync(cancellationToken);
        var runsByDefinitionId = runs
            .GroupBy(item => item.ProcessDefinitionId)
            .ToDictionary(group => group.Key, group => group.ToList());
        var runIds = runs.Select(item => item.Id).ToList();
        var capabilityGapAssignmentsByRunId = runIds.Count == 0
            ? new Dictionary<Guid, List<Guid>>()
            : (await dbContext.Set<ProcessRunAssignment>()
                    .AsNoTracking()
                    .Where(item => runIds.Contains(item.ProcessRunId) && item.IsCapabilityGap)
                    .Select(item => new ProcessCapabilityGapProjection(item.ProcessRunId, item.RoleRequirementId))
                    .ToListAsync(cancellationToken))
                .GroupBy(item => item.ProcessRunId)
                .ToDictionary(
                    group => group.Key,
                    group => group
                        .Select(item => item.RoleRequirementId)
                        .ToList());

        var projectIds = definitions
            .Where(item => item.ProjectId.HasValue)
            .Select(item => item.ProjectId!.Value)
            .Distinct()
            .ToList();
        var projectNames = projectIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await dbContext.Set<Project>()
                .AsNoTracking()
                .Where(item => projectIds.Contains(item.Id))
                .ToDictionaryAsync(item => item.Id, item => item.Name, cancellationToken);

        return definitions
            .Select(
                definition =>
                {
                    var summaryVersion = summaryVersionsByDefinitionId.GetValueOrDefault(definition.Id);
                    var summaryVersionId = summaryVersion?.Id;
                    var roleIds = summaryVersionId.HasValue &&
                                  roleIdsByVersionId.TryGetValue(summaryVersionId.Value, out var resolvedRoleIds)
                        ? resolvedRoleIds
                        : [];
                    var definitionRuns = runsByDefinitionId.GetValueOrDefault(definition.Id) ?? [];
                    var capabilityGapCount = roleIds.Count == 0
                        ? 0
                        : definitionRuns.Sum(
                            run => capabilityGapAssignmentsByRunId.TryGetValue(run.Id, out var gapRoleIds)
                                ? gapRoleIds.Count(roleIds.Contains)
                                : 0);

                    return new ProcessDefinitionListItem(
                        definition.Id,
                        definition.ProjectId,
                        definition.Name,
                        definition.Status,
                        summaryVersion?.VersionNumber ?? 0,
                        definition.ActivePublishedVersionId.HasValue,
                        roleIds.Count,
                        summaryVersionId.HasValue && stepCountByVersionId.TryGetValue(summaryVersionId.Value, out var stepCount)
                            ? stepCount
                            : 0,
                        definitionRuns.Count(
                            run => run.Status == ProcessRunStatus.Active || run.Status == ProcessRunStatus.Blocked),
                        capabilityGapCount,
                        definition.Summary,
                        definition.ValueStatement,
                        definition.ProjectId.HasValue
                            ? projectNames.GetValueOrDefault(definition.ProjectId.Value) ?? string.Empty
                            : string.Empty,
                        definition.UpdatedAtUtc);
                })
            .ToList();
    }

    private sealed record ProcessDefinitionListProjection(
        Guid Id,
        Guid? ProjectId,
        string Name,
        ProcessDefinitionStatus Status,
        Guid? ActivePublishedVersionId,
        string Summary,
        string ValueStatement,
        DateTimeOffset UpdatedAtUtc);

    private sealed record ProcessDefinitionVersionProjection(
        Guid Id,
        Guid ProcessDefinitionId,
        int VersionNumber,
        ProcessVersionStatus Status);

    private sealed record ProcessRoleVersionProjection(Guid ProcessDefinitionVersionId, Guid RoleRequirementId);

    private sealed record ProcessVersionCountProjection(Guid ProcessDefinitionVersionId, int Count);

    private sealed record ProcessDefinitionRunProjection(Guid Id, Guid ProcessDefinitionId, ProcessRunStatus Status);

    private sealed record ProcessCapabilityGapProjection(Guid ProcessRunId, Guid RoleRequirementId);
}
