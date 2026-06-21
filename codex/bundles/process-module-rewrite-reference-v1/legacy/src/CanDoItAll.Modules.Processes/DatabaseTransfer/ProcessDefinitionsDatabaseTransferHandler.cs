using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Processes;

public sealed class ProcessDefinitionsDatabaseTransferHandler : IDatabaseTransferHandler
{
    public DatabaseTransferItemDescriptor Descriptor { get; } = new(
        "processes",
        "Processes",
        "Copies process definitions and design-time configuration without copying runtime runs or launch history.",
        SortOrder: 40);

    public async Task<DatabaseTransferItemPreview> PreviewAsync(
        DatabaseTransferContext context,
        CancellationToken cancellationToken = default)
    {
        var sourceDefinitions = await context.SourceDbContext.Set<ProcessDefinition>()
            .CountAsync(cancellationToken);
        var sourceVersions = await context.SourceDbContext.Set<ProcessDefinitionVersion>()
            .CountAsync(cancellationToken);
        var targetDefinitions = await context.TargetDbContext.Set<ProcessDefinition>()
            .CountAsync(cancellationToken);

        return new DatabaseTransferItemPreview(
            Descriptor,
            sourceDefinitions > 0,
            $"{sourceDefinitions} process definition(s) and {sourceVersions} version(s) are available.",
            sourceDefinitions == 0 ? "The source database does not contain process definitions." : null,
            sourceDefinitions + sourceVersions,
            targetDefinitions);
    }

    public async Task<DatabaseTransferItemResult> TransferAsync(
        DatabaseTransferContext context,
        CancellationToken cancellationToken = default)
    {
        var definitions = await context.SourceDbContext.Set<ProcessDefinition>()
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        if (definitions.Count == 0)
        {
            return new DatabaseTransferItemResult(Descriptor.Key, Descriptor.Label, false, "The source database has no process definitions to transfer.", 0);
        }

        var versions = await LoadAsync<ProcessDefinitionVersion>(context, cancellationToken);
        var roleRequirements = await LoadAsync<ProcessRoleRequirement>(context, cancellationToken);
        var roleSkillRequirements = await LoadAsync<ProcessRoleSkillRequirement>(context, cancellationToken);
        var messagingPolicies = await LoadAsync<ProcessRoleMessagingPolicyDefinition>(context, cancellationToken);
        var steps = await LoadAsync<ProcessStepDefinition>(context, cancellationToken);
        var dependencies = await LoadAsync<ProcessStepDependencyDefinition>(context, cancellationToken);
        var branchOutcomes = await LoadAsync<ProcessStepBranchOutcomeDefinition>(context, cancellationToken);
        var stepRoleRequirements = await LoadAsync<ProcessStepRoleAssignmentRequirement>(context, cancellationToken);
        var artifactExpectations = await LoadAsync<ProcessArtifactExpectation>(context, cancellationToken);
        var artifactInputs = await LoadAsync<ProcessStepArtifactInputDefinition>(context, cancellationToken);

        await ClearTargetAsync(context.TargetDbContext, cancellationToken);

        var definitionsWithoutActiveVersion = definitions
            .Select(CloneWithoutActivePublishedVersion)
            .ToList();

        await context.TargetDbContext.Set<ProcessDefinition>().AddRangeAsync(definitionsWithoutActiveVersion, cancellationToken);
        await context.TargetDbContext.SaveChangesAsync(cancellationToken);

        await AddAndSaveAsync(context.TargetDbContext, versions, cancellationToken);
        await AddAndSaveAsync(context.TargetDbContext, roleRequirements, cancellationToken);
        await AddAndSaveAsync(context.TargetDbContext, roleSkillRequirements, cancellationToken);
        await AddAndSaveAsync(context.TargetDbContext, messagingPolicies, cancellationToken);
        await AddAndSaveAsync(context.TargetDbContext, steps, cancellationToken);
        await AddAndSaveAsync(context.TargetDbContext, branchOutcomes, cancellationToken);
        await AddAndSaveAsync(context.TargetDbContext, artifactExpectations, cancellationToken);
        await AddAndSaveAsync(context.TargetDbContext, stepRoleRequirements, cancellationToken);
        await AddAndSaveAsync(context.TargetDbContext, dependencies, cancellationToken);
        await AddAndSaveAsync(context.TargetDbContext, artifactInputs, cancellationToken);
        await RestoreActivePublishedVersionsAsync(context.TargetDbContext, definitions, cancellationToken);

        var copied = definitions.Count + versions.Count + roleRequirements.Count + roleSkillRequirements.Count +
            messagingPolicies.Count + steps.Count + dependencies.Count + branchOutcomes.Count +
            stepRoleRequirements.Count + artifactExpectations.Count + artifactInputs.Count;

        return new DatabaseTransferItemResult(
            Descriptor.Key,
            Descriptor.Label,
            true,
            $"Copied {definitions.Count} process definition(s) without runtime history.",
            copied);
    }

    private static Task<List<T>> LoadAsync<T>(
        DatabaseTransferContext context,
        CancellationToken cancellationToken)
        where T : class
    {
        return context.SourceDbContext.Set<T>()
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    private static async Task AddAndSaveAsync<T>(
        AppDbContext dbContext,
        IReadOnlyCollection<T> entities,
        CancellationToken cancellationToken)
        where T : class
    {
        if (entities.Count == 0)
        {
            return;
        }

        await dbContext.Set<T>().AddRangeAsync(entities, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static async Task ClearTargetAsync(
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        await RemoveAndSaveAsync<ProcessStepArtifactInputDefinition>(dbContext, cancellationToken);
        await RemoveAndSaveAsync<ProcessArtifactExpectation>(dbContext, cancellationToken);
        await RemoveAndSaveAsync<ProcessStepRoleAssignmentRequirement>(dbContext, cancellationToken);
        await RemoveAndSaveAsync<ProcessStepDependencyDefinition>(dbContext, cancellationToken);
        await RemoveAndSaveAsync<ProcessStepBranchOutcomeDefinition>(dbContext, cancellationToken);
        await RemoveAndSaveAsync<ProcessStepDefinition>(dbContext, cancellationToken);
        await RemoveAndSaveAsync<ProcessRoleMessagingPolicyDefinition>(dbContext, cancellationToken);
        await RemoveAndSaveAsync<ProcessRoleSkillRequirement>(dbContext, cancellationToken);
        await RemoveAndSaveAsync<ProcessRoleRequirement>(dbContext, cancellationToken);
        await RemoveAndSaveAsync<ProcessDefinitionVersion>(dbContext, cancellationToken);
        await RemoveAndSaveAsync<ProcessDefinition>(dbContext, cancellationToken);
    }

    private static async Task RemoveAndSaveAsync<T>(
        AppDbContext dbContext,
        CancellationToken cancellationToken)
        where T : class
    {
        var entities = await dbContext.Set<T>().ToListAsync(cancellationToken);
        if (entities.Count == 0)
        {
            return;
        }

        dbContext.RemoveRange(entities);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static async Task RestoreActivePublishedVersionsAsync(
        AppDbContext dbContext,
        IReadOnlyCollection<ProcessDefinition> sourceDefinitions,
        CancellationToken cancellationToken)
    {
        var activeVersionIds = sourceDefinitions
            .Where(definition => definition.ActivePublishedVersionId.HasValue)
            .ToDictionary(definition => definition.Id, definition => definition.ActivePublishedVersionId);
        if (activeVersionIds.Count == 0)
        {
            return;
        }

        var definitionIds = activeVersionIds.Keys.ToList();
        var targetDefinitions = await dbContext.Set<ProcessDefinition>()
            .Where(definition => definitionIds.Contains(definition.Id))
            .ToListAsync(cancellationToken);
        foreach (var definition in targetDefinitions)
        {
            definition.ActivePublishedVersionId = activeVersionIds[definition.Id];
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static ProcessDefinition CloneWithoutActivePublishedVersion(ProcessDefinition source)
    {
        return new ProcessDefinition
        {
            Id = source.Id,
            ProjectId = source.ProjectId,
            Name = source.Name,
            Slug = source.Slug,
            Summary = source.Summary,
            ValueStatement = source.ValueStatement,
            CustomerName = source.CustomerName,
            OwnerName = source.OwnerName,
            InterfaceContractSummary = source.InterfaceContractSummary,
            GovernanceNotes = source.GovernanceNotes,
            Criticality = source.Criticality,
            AutonomyLevel = source.AutonomyLevel,
            Status = source.Status,
            ActivePublishedVersionId = null,
            NextVersionNumber = source.NextVersionNumber,
            CreatedAtUtc = source.CreatedAtUtc,
            UpdatedAtUtc = source.UpdatedAtUtc,
            ConcurrencyToken = source.ConcurrencyToken
        };
    }
}
