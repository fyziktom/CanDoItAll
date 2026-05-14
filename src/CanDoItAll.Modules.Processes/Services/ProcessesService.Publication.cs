using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Processes;

public sealed partial class ProcessesService
{
    private static readonly ProcessDefinitionDraftCloneEngine DraftCloneEngine = new();

    public Task<Result> PublishAsync(Guid definitionId, CancellationToken cancellationToken = default) {
        return PublishAsync(
            new ProcessDefinitionPublishRequest {
                DefinitionId = definitionId
            },
            cancellationToken);
    }

    public async Task<Result> PublishAsync(ProcessDefinitionPublishRequest request, CancellationToken cancellationToken = default) {
        if (request.DefinitionId == Guid.Empty) {
            return Result.Failure(Error.Validation("Process definition was not found.", "processes.definition-not-found"));
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        ProcessDefinition definition;
        ProcessDefinitionVersion draftVersion;
        Guid outboxId;
        try {
            var publicationContextResult = await LoadPublicationContextAsync(dbContext, request, cancellationToken);
            if (publicationContextResult.IsFailure) {
                return Result.Failure(publicationContextResult.Errors);
            }

            var publicationContext = publicationContextResult.Value!;
            definition = publicationContext.Definition;
            draftVersion = publicationContext.DraftVersion;

            var publishError = ValidatePublish(
                definition,
                draftVersion,
                publicationContext.CloneSource.Roles,
                publicationContext.CloneSource.MessagingPolicies,
                publicationContext.CloneSource.Steps,
                publicationContext.CloneSource.StepRoleRequirements,
                publicationContext.CloneSource.BranchOutcomes,
                publicationContext.CloneSource.StepDependencies,
                publicationContext.CloneSource.ArtifactExpectations,
                publicationContext.CloneSource.ArtifactInputs);
            if (publishError is not null) {
                return Result.Failure(publishError);
            }

            var publishedVersions = await dbContext.Set<ProcessDefinitionVersion>()
                .Where(item => item.ProcessDefinitionId == request.DefinitionId && item.Status == ProcessVersionStatus.Published)
                .ToListAsync(cancellationToken);
            var now = clock.GetUtcNow();
            RetirePublishedVersions(publishedVersions, now);
            if (publishedVersions.Count > 0) {
                await dbContext.SaveChangesAsync(cancellationToken);
            }

            PromoteDraftVersion(definition, draftVersion, now);
            await dbContext.SaveChangesAsync(cancellationToken);

            await ProvisionNextDraftFromPublishedVersionAsync(
                dbContext,
                definition,
                draftVersion,
                publicationContext.CloneSource,
                now,
                cancellationToken);
            outboxId = await processOutboxService.EnqueueDefinitionPublishAsync(
                dbContext,
                definition,
                draftVersion,
                cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException) {
            await transaction.RollbackAsync(cancellationToken);
            return Result.Failure(CreateDefinitionPublishConflictError());
        }
        catch (DbUpdateException exception) when (DbUpdateExceptionClassifier.IsUniqueConstraintViolation(exception)) {
            await transaction.RollbackAsync(cancellationToken);
            return Result.Failure(CreateDefinitionPublishUniqueConflictError());
        }

        await processOutboxService.ProcessAsync(outboxId, cancellationToken);
        NotifyDefinitionObservationChanged(definition.ProjectId, definition.Id);
        return Result.Success();
    }

    public async Task DeleteAsync(Guid definitionId, CancellationToken cancellationToken = default) {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var definition = await dbContext.Set<ProcessDefinition>()
            .SingleOrDefaultAsync(item => item.Id == definitionId, cancellationToken);
        if (definition is null) {
            return;
        }

        var versionIds = await dbContext.Set<ProcessDefinitionVersion>()
            .Where(item => item.ProcessDefinitionId == definitionId)
            .Select(item => item.Id)
            .ToListAsync(cancellationToken);
        var roleIds = versionIds.Count == 0
            ? []
            : await dbContext.Set<ProcessRoleRequirement>()
                .Where(item => versionIds.Contains(item.ProcessDefinitionVersionId))
                .Select(item => item.Id)
                .ToListAsync(cancellationToken);
        var stepIds = versionIds.Count == 0
            ? []
            : await dbContext.Set<ProcessStepDefinition>()
                .Where(item => versionIds.Contains(item.ProcessDefinitionVersionId))
                .Select(item => item.Id)
                .ToListAsync(cancellationToken);
        var runIds = await dbContext.Set<ProcessRun>()
            .Where(item => item.ProcessDefinitionId == definitionId)
            .Select(item => item.Id)
            .ToListAsync(cancellationToken);

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        Guid outboxId;

        if (roleIds.Count > 0) {
            await dbContext.Set<ProcessRoleSkillRequirement>()
                .Where(item => roleIds.Contains(item.RoleRequirementId))
                .ExecuteDeleteAsync(cancellationToken);
        }

        if (versionIds.Count > 0) {
            await dbContext.Set<ProcessRoleMessagingPolicyDefinition>()
                .Where(item => versionIds.Contains(item.ProcessDefinitionVersionId))
                .ExecuteDeleteAsync(cancellationToken);
        }

        if (stepIds.Count > 0) {
            await dbContext.Set<ProcessStepDependencyDefinition>()
                .Where(item => stepIds.Contains(item.StepDefinitionId))
                .ExecuteDeleteAsync(cancellationToken);
            await dbContext.Set<ProcessStepRoleAssignmentRequirement>()
                .Where(item => stepIds.Contains(item.StepDefinitionId))
                .ExecuteDeleteAsync(cancellationToken);
            await dbContext.Set<ProcessStepArtifactInputDefinition>()
                .Where(item => stepIds.Contains(item.StepDefinitionId))
                .ExecuteDeleteAsync(cancellationToken);
            await dbContext.Set<ProcessArtifactExpectation>()
                .Where(item => stepIds.Contains(item.StepDefinitionId))
                .ExecuteDeleteAsync(cancellationToken);
            await dbContext.Set<ProcessStepBranchOutcomeDefinition>()
                .Where(item => stepIds.Contains(item.StepDefinitionId))
                .ExecuteDeleteAsync(cancellationToken);
        }

        if (runIds.Count > 0) {
            await dbContext.Set<ProcessStepRun>()
                .Where(item => runIds.Contains(item.ProcessRunId))
                .ExecuteDeleteAsync(cancellationToken);
            await dbContext.Set<ProcessRunAssignment>()
                .Where(item => runIds.Contains(item.ProcessRunId))
                .ExecuteDeleteAsync(cancellationToken);
            await dbContext.Set<ProcessWorkBrief>()
                .Where(item => runIds.Contains(item.ProcessRunId))
                .ExecuteDeleteAsync(cancellationToken);
            await dbContext.Set<ProcessDecisionRecord>()
                .Where(item => runIds.Contains(item.ProcessRunId))
                .ExecuteDeleteAsync(cancellationToken);
            await dbContext.Set<ProcessArtifactRecord>()
                .Where(item => runIds.Contains(item.ProcessRunId))
                .ExecuteDeleteAsync(cancellationToken);
            await dbContext.Set<ProcessJournalEntry>()
                .Where(item => runIds.Contains(item.ProcessRunId))
                .ExecuteDeleteAsync(cancellationToken);
            await dbContext.Set<ProcessConformanceObservation>()
                .Where(item => runIds.Contains(item.ProcessRunId))
                .ExecuteDeleteAsync(cancellationToken);
        }

        await dbContext.Set<ProcessImprovementCandidate>()
            .Where(item => item.ProcessDefinitionId == definitionId)
            .ExecuteDeleteAsync(cancellationToken);

        await dbContext.Set<ProcessDefinition>()
            .Where(item => item.Id == definitionId)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(item => item.ActivePublishedVersionId, (Guid?)null),
                cancellationToken);

        if (runIds.Count > 0) {
            await dbContext.Set<ProcessRun>()
                .Where(item => runIds.Contains(item.Id))
                .ExecuteDeleteAsync(cancellationToken);
        }

        if (stepIds.Count > 0) {
            await dbContext.Set<ProcessStepDefinition>()
                .Where(item => stepIds.Contains(item.Id))
                .ExecuteDeleteAsync(cancellationToken);
        }

        if (roleIds.Count > 0) {
            await dbContext.Set<ProcessRoleRequirement>()
                .Where(item => roleIds.Contains(item.Id))
                .ExecuteDeleteAsync(cancellationToken);
        }

        if (versionIds.Count > 0) {
            await dbContext.Set<ProcessDefinitionVersion>()
                .Where(item => versionIds.Contains(item.Id))
                .ExecuteDeleteAsync(cancellationToken);
        }

        await dbContext.Set<ProcessDefinition>()
            .Where(item => item.Id == definitionId)
            .ExecuteDeleteAsync(cancellationToken);

        outboxId = await processOutboxService.EnqueueDefinitionDeleteAsync(
            dbContext,
            definitionId,
            definition.ProjectId,
            cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        await processOutboxService.ProcessAsync(outboxId, cancellationToken);
        NotifyDefinitionObservationChanged(definition.ProjectId, definitionId);
    }

    private async Task<Result<ProcessPublicationContext>> LoadPublicationContextAsync(
        AppDbContext dbContext,
        ProcessDefinitionPublishRequest request,
        CancellationToken cancellationToken) {
        var definition = await dbContext.Set<ProcessDefinition>()
            .SingleOrDefaultAsync(item => item.Id == request.DefinitionId, cancellationToken);
        if (definition is null) {
            return Result<ProcessPublicationContext>.Failure(
                Error.Validation("Process definition was not found.", "processes.definition-not-found"));
        }

        if (HasConcurrencyTokenMismatch(request.DefinitionConcurrencyToken, definition.ConcurrencyToken)) {
            return Result<ProcessPublicationContext>.Failure(CreateDefinitionPublishConflictError());
        }

        var draftVersion = await dbContext.Set<ProcessDefinitionVersion>()
            .SingleOrDefaultAsync(
                item => item.ProcessDefinitionId == request.DefinitionId &&
                    item.Status == ProcessVersionStatus.Draft,
                cancellationToken);
        if (draftVersion is null) {
            return Result<ProcessPublicationContext>.Failure(
                Error.Validation("No draft version is available to publish.", "processes.draft-version-required"));
        }

        if (HasConcurrencyTokenMismatch(request.DraftVersionConcurrencyToken, draftVersion.ConcurrencyToken)) {
            return Result<ProcessPublicationContext>.Failure(CreateDefinitionPublishConflictError());
        }

        var roles = await dbContext.Set<ProcessRoleRequirement>()
            .Where(item => item.ProcessDefinitionVersionId == draftVersion.Id)
            .OrderBy(item => item.DisplayOrder)
            .ToListAsync(cancellationToken);
        var steps = await dbContext.Set<ProcessStepDefinition>()
            .Where(item => item.ProcessDefinitionVersionId == draftVersion.Id)
            .OrderBy(item => item.OrderIndex)
            .ToListAsync(cancellationToken);
        var roleIds = roles.Select(item => item.Id).ToList();
        var stepIds = steps.Select(item => item.Id).ToList();
        IReadOnlyList<ProcessRoleSkillRequirement> roleSkills = roleIds.Count == 0
            ? []
            : await dbContext.Set<ProcessRoleSkillRequirement>()
                .Where(item => roleIds.Contains(item.RoleRequirementId))
                .ToListAsync(cancellationToken);
        IReadOnlyList<ProcessRoleMessagingPolicyDefinition> messagingPolicies = roleIds.Count == 0
            ? []
            : await dbContext.Set<ProcessRoleMessagingPolicyDefinition>()
                .Where(item => item.ProcessDefinitionVersionId == draftVersion.Id)
                .OrderBy(item => item.DisplayOrder)
                .ToListAsync(cancellationToken);
        IReadOnlyList<ProcessStepRoleAssignmentRequirement> stepRoleRequirements = stepIds.Count == 0
            ? []
            : await dbContext.Set<ProcessStepRoleAssignmentRequirement>()
                .Where(item => stepIds.Contains(item.StepDefinitionId))
                .ToListAsync(cancellationToken);
        IReadOnlyList<ProcessStepBranchOutcomeDefinition> branchOutcomes = stepIds.Count == 0
            ? []
            : await dbContext.Set<ProcessStepBranchOutcomeDefinition>()
                .Where(item => stepIds.Contains(item.StepDefinitionId))
                .ToListAsync(cancellationToken);
        IReadOnlyList<ProcessStepDependencyDefinition> stepDependencies = stepIds.Count == 0
            ? []
            : await dbContext.Set<ProcessStepDependencyDefinition>()
                .Where(item => stepIds.Contains(item.StepDefinitionId))
                .ToListAsync(cancellationToken);
        IReadOnlyList<ProcessArtifactExpectation> artifactExpectations = stepIds.Count == 0
            ? []
            : await dbContext.Set<ProcessArtifactExpectation>()
                .Where(item => stepIds.Contains(item.StepDefinitionId))
                .ToListAsync(cancellationToken);
        IReadOnlyList<ProcessStepArtifactInputDefinition> artifactInputs = stepIds.Count == 0
            ? []
            : await dbContext.Set<ProcessStepArtifactInputDefinition>()
                .Where(item => stepIds.Contains(item.StepDefinitionId))
                .ToListAsync(cancellationToken);

        return Result<ProcessPublicationContext>.Success(
            new ProcessPublicationContext(
                definition,
                draftVersion,
                new ProcessDefinitionDraftCloneSource(
                    roles,
                    roleSkills,
                    messagingPolicies,
                    steps,
                    stepRoleRequirements,
                    branchOutcomes,
                    stepDependencies,
                    artifactExpectations,
                    artifactInputs)));
    }

    private async Task ProvisionNextDraftFromPublishedVersionAsync(
        AppDbContext dbContext,
        ProcessDefinition definition,
        ProcessDefinitionVersion publishedVersion,
        ProcessDefinitionDraftCloneSource cloneSource,
        DateTimeOffset now,
        CancellationToken cancellationToken) {
        await EnsureNextVersionNumberAheadOfExistingVersionsAsync(dbContext, definition, cancellationToken);
        var nextDraft = CreateNextDraftVersion(definition, publishedVersion, now);
        await dbContext.Set<ProcessDefinitionVersion>().AddAsync(nextDraft, cancellationToken);
        await DraftCloneEngine.CloneAsync(
            dbContext,
            nextDraft,
            cloneSource,
            cancellationToken);
    }

    private static void RetirePublishedVersions(
        IReadOnlyList<ProcessDefinitionVersion> publishedVersions,
        DateTimeOffset now) {
        foreach (var publishedVersion in publishedVersions) {
            publishedVersion.Status = ProcessVersionStatus.Superseded;
            publishedVersion.UpdatedAtUtc = now;
        }
    }

    private static void PromoteDraftVersion(
        ProcessDefinition definition,
        ProcessDefinitionVersion draftVersion,
        DateTimeOffset now) {
        draftVersion.Status = ProcessVersionStatus.Published;
        draftVersion.PublishedAtUtc = now;
        draftVersion.PublishedBy = DefaultActor;
        draftVersion.UpdatedAtUtc = now;
        definition.Status = ProcessDefinitionStatus.Published;
        definition.ActivePublishedVersionId = draftVersion.Id;
        definition.UpdatedAtUtc = now;
    }

    private static ProcessDefinitionVersion CreateNextDraftVersion(
        ProcessDefinition definition,
        ProcessDefinitionVersion publishedVersion,
        DateTimeOffset now) {
        return new ProcessDefinitionVersion {
            ProcessDefinitionId = definition.Id,
            VersionNumber = AllocateNextVersionNumber(definition),
            Status = ProcessVersionStatus.Draft,
            ChangeSummary = $"Draft created from published v{publishedVersion.VersionNumber}.",
            GovernancePolicySummary = publishedVersion.GovernancePolicySummary,
            ConstitutionRuleSummary = publishedVersion.ConstitutionRuleSummary,
            OperatingModeSummary = publishedVersion.OperatingModeSummary,
            SimulationReadinessSummary = publishedVersion.SimulationReadinessSummary,
            ManagerAgentOverrideId = publishedVersion.ManagerAgentOverrideId,
            ManagerAgentOverrideName = publishedVersion.ManagerAgentOverrideName,
            ImportedFrom = publishedVersion.ImportedFrom,
            ImportWarnings = publishedVersion.ImportWarnings,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
    }

    private sealed record ProcessPublicationContext(
        ProcessDefinition Definition,
        ProcessDefinitionVersion DraftVersion,
        ProcessDefinitionDraftCloneSource CloneSource);
}
