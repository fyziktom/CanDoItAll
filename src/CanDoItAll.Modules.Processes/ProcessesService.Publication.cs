using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Processes;

public sealed partial class ProcessesService
{
    public async Task<Result> PublishAsync(Guid definitionId, CancellationToken cancellationToken = default) {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var definition = await dbContext.Set<ProcessDefinition>()
            .SingleOrDefaultAsync(item => item.Id == definitionId, cancellationToken);
        if (definition is null) {
            return Result.Failure(Error.Validation("Process definition was not found.", "processes.definition-not-found"));
        }

        var draftVersion = await dbContext.Set<ProcessDefinitionVersion>()
            .Where(item => item.ProcessDefinitionId == definitionId && item.Status == ProcessVersionStatus.Draft)
            .OrderByDescending(item => item.VersionNumber)
            .FirstOrDefaultAsync(cancellationToken);
        if (draftVersion is null) {
            return Result.Failure(Error.Validation("No draft version is available to publish.", "processes.draft-version-required"));
        }

        var roles = await dbContext.Set<ProcessRoleRequirement>()
            .Where(item => item.ProcessDefinitionVersionId == draftVersion.Id)
            .ToListAsync(cancellationToken);
        var steps = await dbContext.Set<ProcessStepDefinition>()
            .Where(item => item.ProcessDefinitionVersionId == draftVersion.Id)
            .OrderBy(item => item.OrderIndex)
            .ToListAsync(cancellationToken);
        var stepRoleRequirements = await dbContext.Set<ProcessStepRoleAssignmentRequirement>()
            .Where(item => steps.Select(step => step.Id).Contains(item.StepDefinitionId))
            .ToListAsync(cancellationToken);
        var branchOutcomes = await dbContext.Set<ProcessStepBranchOutcomeDefinition>()
            .Where(item => steps.Select(step => step.Id).Contains(item.StepDefinitionId))
            .ToListAsync(cancellationToken);
        var stepDependencies = await dbContext.Set<ProcessStepDependencyDefinition>()
            .Where(item => steps.Select(step => step.Id).Contains(item.StepDefinitionId))
            .ToListAsync(cancellationToken);
        var artifactExpectations = await dbContext.Set<ProcessArtifactExpectation>()
            .Where(item => steps.Select(step => step.Id).Contains(item.StepDefinitionId))
            .ToListAsync(cancellationToken);
        var artifactInputs = await dbContext.Set<ProcessStepArtifactInputDefinition>()
            .Where(item => steps.Select(step => step.Id).Contains(item.StepDefinitionId))
            .ToListAsync(cancellationToken);
        var publishError = ValidatePublish(definition, draftVersion, roles, steps, stepRoleRequirements, branchOutcomes, stepDependencies, artifactExpectations, artifactInputs);
        if (publishError is not null) {
            return Result.Failure(publishError);
        }

        var publishedVersions = await dbContext.Set<ProcessDefinitionVersion>()
            .Where(item => item.ProcessDefinitionId == definitionId && item.Status == ProcessVersionStatus.Published)
            .ToListAsync(cancellationToken);
        foreach (var publishedVersion in publishedVersions) {
            publishedVersion.Status = ProcessVersionStatus.Superseded;
            publishedVersion.UpdatedAtUtc = clock.GetUtcNow();
        }

        draftVersion.Status = ProcessVersionStatus.Published;
        draftVersion.PublishedAtUtc = clock.GetUtcNow();
        draftVersion.PublishedBy = DefaultActor;
        draftVersion.UpdatedAtUtc = clock.GetUtcNow();
        definition.Status = ProcessDefinitionStatus.Published;
        definition.ActivePublishedVersionId = draftVersion.Id;
        definition.UpdatedAtUtc = clock.GetUtcNow();

        await ClonePublishedVersionIntoNextDraftAsync(
            dbContext,
            definitionId,
            draftVersion,
            roles,
            steps,
            stepRoleRequirements,
            stepDependencies,
            artifactExpectations,
            artifactInputs,
            cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        var route = definition.ProjectId.HasValue
            ? $"/projects/{definition.ProjectId.Value:D}/processes?processId={definition.Id:D}"
            : $"/processes?processId={definition.Id:D}";
        await activityStream.RecordAsync(
            new ActivityWriteRequest(
                "processes",
                "publish-definition",
                "Published process definition",
                $"{definition.Name} v{draftVersion.VersionNumber} is now immutable for runtime use.",
                definition.ProjectId,
                "process-definition",
                definition.Id,
                route,
                DefaultActor),
            cancellationToken);
        return Result.Success();
    }

    public async Task DeleteAsync(Guid definitionId, CancellationToken cancellationToken = default) {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var definitionExists = await dbContext.Set<ProcessDefinition>()
            .AnyAsync(item => item.Id == definitionId, cancellationToken);
        if (!definitionExists) {
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

        if (roleIds.Count > 0) {
            await dbContext.Set<ProcessRoleSkillRequirement>()
                .Where(item => roleIds.Contains(item.RoleRequirementId))
                .ExecuteDeleteAsync(cancellationToken);
        }

        if (stepIds.Count > 0) {
            await dbContext.Set<ProcessStepDependencyDefinition>()
                .Where(item => stepIds.Contains(item.StepDefinitionId))
                .ExecuteDeleteAsync(cancellationToken);
            await dbContext.Set<ProcessStepRoleAssignmentRequirement>()
                .Where(item => stepIds.Contains(item.StepDefinitionId))
                .ExecuteDeleteAsync(cancellationToken);
            await dbContext.Set<ProcessArtifactExpectation>()
                .Where(item => stepIds.Contains(item.StepDefinitionId))
                .ExecuteDeleteAsync(cancellationToken);
            await dbContext.Set<ProcessStepArtifactInputDefinition>()
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

        await transaction.CommitAsync(cancellationToken);
        await searchIndexService.DeleteAsync("process-definition", definitionId.ToString(), cancellationToken);
    }

    private async Task ClonePublishedVersionIntoNextDraftAsync(
        AppDbContext dbContext,
        Guid definitionId,
        ProcessDefinitionVersion publishedVersion,
        IReadOnlyList<ProcessRoleRequirement> roles,
        IReadOnlyList<ProcessStepDefinition> steps,
        IReadOnlyList<ProcessStepRoleAssignmentRequirement> stepRoleRequirements,
        IReadOnlyList<ProcessStepDependencyDefinition> stepDependencies,
        IReadOnlyList<ProcessArtifactExpectation> artifactExpectations,
        IReadOnlyList<ProcessStepArtifactInputDefinition> artifactInputs,
        CancellationToken cancellationToken) {
        var nextDraftVersionId = Guid.NewGuid();
        var roleIdMap = roles.ToDictionary(item => item.Id, _ => Guid.NewGuid());
        var stepIdMap = steps.ToDictionary(item => item.Id, _ => Guid.NewGuid());

        var nextDraft = new ProcessDefinitionVersion {
            Id = nextDraftVersionId,
            ProcessDefinitionId = definitionId,
            VersionNumber = publishedVersion.VersionNumber + 1,
            Status = ProcessVersionStatus.Draft,
            ChangeSummary = $"Draft created from published v{publishedVersion.VersionNumber}.",
            GovernancePolicySummary = publishedVersion.GovernancePolicySummary,
            ConstitutionRuleSummary = publishedVersion.ConstitutionRuleSummary,
            OperatingModeSummary = publishedVersion.OperatingModeSummary,
            SimulationReadinessSummary = publishedVersion.SimulationReadinessSummary,
            ImportedFrom = publishedVersion.ImportedFrom,
            ImportWarnings = publishedVersion.ImportWarnings,
            CreatedAtUtc = clock.GetUtcNow(),
            UpdatedAtUtc = clock.GetUtcNow()
        };
        await dbContext.Set<ProcessDefinitionVersion>().AddAsync(nextDraft, cancellationToken);

        var roleSkills = await dbContext.Set<ProcessRoleSkillRequirement>()
            .Where(item => roles.Select(role => role.Id).Contains(item.RoleRequirementId))
            .ToListAsync(cancellationToken);
        var branchOutcomes = await dbContext.Set<ProcessStepBranchOutcomeDefinition>()
            .Where(item => steps.Select(step => step.Id).Contains(item.StepDefinitionId))
            .ToListAsync(cancellationToken);
        var branchOutcomeIdMap = branchOutcomes.ToDictionary(item => item.Id, _ => Guid.NewGuid());
        var artifactExpectationIdMap = artifactExpectations.ToDictionary(item => item.Id, _ => Guid.NewGuid());
        var stepDependenciesByStepId = stepDependencies
            .GroupBy(item => item.StepDefinitionId)
            .ToDictionary(group => group.Key, group => group.OrderBy(item => item.DisplayOrder).ToList());
        var clonedStepsById = new Dictionary<Guid, ProcessStepDefinition>();

        foreach (var role in roles.OrderBy(item => item.DisplayOrder)) {
            await dbContext.Set<ProcessRoleRequirement>().AddAsync(
                new ProcessRoleRequirement {
                    Id = roleIdMap[role.Id],
                    ProcessDefinitionVersionId = nextDraftVersionId,
                    Key = role.Key,
                    DisplayName = role.DisplayName,
                    Purpose = role.Purpose,
                    StaffingIntent = role.StaffingIntent,
                    PreferredExecutorKind = role.PreferredExecutorKind,
                    PreferredProjectAssignmentRole = role.PreferredProjectAssignmentRole,
                    IsRequired = role.IsRequired,
                    AllowsFallback = role.AllowsFallback,
                    RequiresExplicitApproval = role.RequiresExplicitApproval,
                    DefaultAllocationPercent = role.DefaultAllocationPercent,
                    RoleTemplateSourceKey = role.RoleTemplateSourceKey,
                    RoleTemplateSnapshotName = role.RoleTemplateSnapshotName,
                    SnapshotSummary = role.SnapshotSummary,
                    DisplayOrder = role.DisplayOrder,
                    CanvasX = role.CanvasX,
                    CanvasY = role.CanvasY
                },
                cancellationToken);
        }

        foreach (var roleSkill in roleSkills) {
            if (!roleIdMap.TryGetValue(roleSkill.RoleRequirementId, out var nextRoleId)) {
                continue;
            }

            await dbContext.Set<ProcessRoleSkillRequirement>().AddAsync(
                new ProcessRoleSkillRequirement {
                    RoleRequirementId = nextRoleId,
                    SkillId = roleSkill.SkillId,
                    IsRequired = roleSkill.IsRequired,
                    MinimumYearsExperience = roleSkill.MinimumYearsExperience
                },
                cancellationToken);
        }

        foreach (var step in steps.OrderBy(item => item.OrderIndex)) {
            var clonedStep = new ProcessStepDefinition {
                Id = stepIdMap[step.Id],
                ProcessDefinitionVersionId = nextDraftVersionId,
                Key = step.Key,
                Title = step.Title,
                Subtitle = step.Subtitle,
                Notes = step.Notes,
                StepKind = step.StepKind,
                AllowsManualSkip = step.AllowsManualSkip,
                AllowsSafeRefusal = step.AllowsSafeRefusal,
                RequiresApproval = step.RequiresApproval,
                RequiresDecisionRecord = step.RequiresDecisionRecord,
                InputContractSummary = step.InputContractSummary,
                OutputContractSummary = step.OutputContractSummary,
                EvidenceContractSummary = step.EvidenceContractSummary,
                DecisionRightsSummary = step.DecisionRightsSummary,
                ExceptionPolicySummary = step.ExceptionPolicySummary,
                TargetLeadHours = step.TargetLeadHours,
                OrderIndex = step.OrderIndex,
                DecisionRoleRequirementId = step.DecisionRoleRequirementId.HasValue ? roleIdMap.GetValueOrDefault(step.DecisionRoleRequirementId.Value) : null,
                CanvasX = step.CanvasX,
                CanvasY = step.CanvasY,
                BranchCanvasX = step.BranchCanvasX,
                BranchCanvasY = step.BranchCanvasY
            };
            clonedStepsById[clonedStep.Id] = clonedStep;
            await dbContext.Set<ProcessStepDefinition>().AddAsync(clonedStep, cancellationToken);
        }

        foreach (var branchOutcome in branchOutcomes.OrderBy(item => item.DisplayOrder)) {
            if (!stepIdMap.TryGetValue(branchOutcome.StepDefinitionId, out var nextStepId) ||
                !branchOutcomeIdMap.TryGetValue(branchOutcome.Id, out var nextOutcomeId)) {
                continue;
            }

            await dbContext.Set<ProcessStepBranchOutcomeDefinition>().AddAsync(
                new ProcessStepBranchOutcomeDefinition {
                    Id = nextOutcomeId,
                    StepDefinitionId = nextStepId,
                    Key = branchOutcome.Key,
                    Title = branchOutcome.Title,
                    Description = branchOutcome.Description,
                    DisplayOrder = branchOutcome.DisplayOrder
                },
                cancellationToken);
        }

        foreach (var stepRoleRequirement in stepRoleRequirements) {
            if (!stepIdMap.TryGetValue(stepRoleRequirement.StepDefinitionId, out var nextStepId) ||
                !roleIdMap.TryGetValue(stepRoleRequirement.RoleRequirementId, out var nextRoleId)) {
                continue;
            }

            await dbContext.Set<ProcessStepRoleAssignmentRequirement>().AddAsync(
                new ProcessStepRoleAssignmentRequirement {
                    StepDefinitionId = nextStepId,
                    RoleRequirementId = nextRoleId,
                    ResponsibilityKind = stepRoleRequirement.ResponsibilityKind,
                    IsRequired = stepRoleRequirement.IsRequired,
                    FallbackOrder = stepRoleRequirement.FallbackOrder,
                    RebindPolicySummary = stepRoleRequirement.RebindPolicySummary
                },
                cancellationToken);
        }

        foreach (var stepDependency in stepDependencies.OrderBy(item => item.DisplayOrder)) {
            if (!stepIdMap.TryGetValue(stepDependency.StepDefinitionId, out var nextStepId) ||
                !stepIdMap.TryGetValue(stepDependency.DependsOnStepId, out var nextDependsOnStepId)) {
                continue;
            }

            await dbContext.Set<ProcessStepDependencyDefinition>().AddAsync(
                new ProcessStepDependencyDefinition {
                    StepDefinitionId = nextStepId,
                    DependsOnStepId = nextDependsOnStepId,
                    DependsOnBranchOutcomeId = stepDependency.DependsOnBranchOutcomeId.HasValue
                        ? branchOutcomeIdMap.GetValueOrDefault(stepDependency.DependsOnBranchOutcomeId.Value)
                        : null,
                    DisplayOrder = stepDependency.DisplayOrder
                },
                cancellationToken);
        }

        foreach (var step in steps.OrderBy(item => item.OrderIndex)) {
            if (!stepIdMap.TryGetValue(step.Id, out var nextStepId)) {
                continue;
            }

            var primaryDependency = GetPersistedDependencies(step, stepDependenciesByStepId)
                .FirstOrDefault();
            if (!clonedStepsById.TryGetValue(nextStepId, out var clonedStep)) {
                continue;
            }

            clonedStep.DependsOnStepId = primaryDependency is null
                ? null
                : stepIdMap.GetValueOrDefault(primaryDependency.DependsOnStepId);
            clonedStep.DependsOnBranchOutcomeId = primaryDependency?.DependsOnBranchOutcomeId.HasValue == true
                ? branchOutcomeIdMap.GetValueOrDefault(primaryDependency.DependsOnBranchOutcomeId.Value)
                : null;
        }

        foreach (var artifactExpectation in artifactExpectations) {
            if (!stepIdMap.TryGetValue(artifactExpectation.StepDefinitionId, out var nextStepId)) {
                continue;
            }

            await dbContext.Set<ProcessArtifactExpectation>().AddAsync(
                new ProcessArtifactExpectation {
                    Id = artifactExpectationIdMap[artifactExpectation.Id],
                    StepDefinitionId = nextStepId,
                    ArtifactKind = artifactExpectation.ArtifactKind,
                    Title = artifactExpectation.Title,
                    IsRequired = artifactExpectation.IsRequired,
                    TrustRequirement = artifactExpectation.TrustRequirement,
                    SensitivityLevel = artifactExpectation.SensitivityLevel,
                    RetentionDays = artifactExpectation.RetentionDays,
                    AllowedFutureUsageSummary = artifactExpectation.AllowedFutureUsageSummary,
                    ValidationRequirementSummary = artifactExpectation.ValidationRequirementSummary
                },
                cancellationToken);
        }

        foreach (var artifactInput in artifactInputs.OrderBy(item => item.DisplayOrder)) {
            if (!stepIdMap.TryGetValue(artifactInput.StepDefinitionId, out var nextStepId) ||
                !artifactExpectationIdMap.TryGetValue(artifactInput.ArtifactExpectationId, out var nextArtifactExpectationId)) {
                continue;
            }

            await dbContext.Set<ProcessStepArtifactInputDefinition>().AddAsync(
                new ProcessStepArtifactInputDefinition {
                    StepDefinitionId = nextStepId,
                    ArtifactExpectationId = nextArtifactExpectationId,
                    DisplayOrder = artifactInput.DisplayOrder
                },
                cancellationToken);
        }
    }
}
