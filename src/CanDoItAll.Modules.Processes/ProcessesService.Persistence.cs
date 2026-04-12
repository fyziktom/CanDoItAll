using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Infrastructure.Search;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Processes;

public sealed partial class ProcessesService
{
    public async Task<Result<Guid>> SaveAsync(
        ProcessDefinitionEditorModel model,
        CancellationToken cancellationToken = default) {
        return await SaveAsync(model, importMetadata: null, cancellationToken);
    }

    private async Task<Result<Guid>> SaveAsync(
        ProcessDefinitionEditorModel model,
        ProcessImportMetadata? importMetadata,
        CancellationToken cancellationToken) {
        var validationError = ValidateDefinitionEditor(model);
        if (validationError is not null) {
            return Result<Guid>.Failure(validationError);
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var definition = model.Id.HasValue
            ? await dbContext.Set<ProcessDefinition>().SingleOrDefaultAsync(item => item.Id == model.Id.Value, cancellationToken)
            : null;

        var isNew = definition is null;
        if (definition is null) {
            definition = new ProcessDefinition {
                CreatedAtUtc = clock.GetUtcNow()
            };

            await dbContext.Set<ProcessDefinition>().AddAsync(definition, cancellationToken);
        }

        definition.ProjectId = model.ProjectId;
        definition.Name = model.Name.Trim();
        definition.Slug = await BuildUniqueSlugAsync(dbContext, model.Name, model.ProjectId, definition.Id, cancellationToken);
        definition.Summary = model.Summary.Trim();
        definition.ValueStatement = model.ValueStatement.Trim();
        definition.CustomerName = model.CustomerName.Trim();
        definition.OwnerName = model.OwnerName.Trim();
        definition.InterfaceContractSummary = model.InterfaceContractSummary.Trim();
        definition.GovernanceNotes = model.GovernanceNotes.Trim();
        definition.Criticality = model.Criticality;
        definition.AutonomyLevel = model.AutonomyLevel;
        definition.Status = model.Status;
        definition.UpdatedAtUtc = clock.GetUtcNow();

        var workingVersion = model.WorkingVersionId.HasValue
            ? await dbContext.Set<ProcessDefinitionVersion>()
                .SingleOrDefaultAsync(item => item.Id == model.WorkingVersionId.Value, cancellationToken)
            : null;

        if (workingVersion is null) {
            workingVersion = await GetWorkingVersionAsync(dbContext, definition.Id, cancellationToken);
        }

        if (workingVersion is not null && workingVersion.Status == ProcessVersionStatus.Published) {
            return Result<Guid>.Failure(Error.Validation("Published versions are immutable. Save into a draft version instead.", "processes.immutable-published-version"));
        }

        if (workingVersion is null) {
            workingVersion = new ProcessDefinitionVersion {
                ProcessDefinitionId = definition.Id,
                VersionNumber = await GetNextVersionNumberAsync(dbContext, definition.Id, cancellationToken),
                Status = ProcessVersionStatus.Draft,
                CreatedAtUtc = clock.GetUtcNow()
            };

            await dbContext.Set<ProcessDefinitionVersion>().AddAsync(workingVersion, cancellationToken);
        }

        workingVersion.ChangeSummary = model.ChangeSummary.Trim();
        workingVersion.GovernancePolicySummary = model.GovernancePolicySummary.Trim();
        workingVersion.ConstitutionRuleSummary = model.ConstitutionRuleSummary.Trim();
        workingVersion.OperatingModeSummary = model.OperatingModeSummary.Trim();
        workingVersion.SimulationReadinessSummary = model.SimulationReadinessSummary.Trim();
        if (importMetadata is not null) {
            workingVersion.ImportedFrom = importMetadata.SourceFormat;
            workingVersion.ImportWarnings = importMetadata.WarningSummary;
        }

        workingVersion.UpdatedAtUtc = clock.GetUtcNow();

        await SaveDefinitionChildrenAsync(dbContext, workingVersion.Id, model, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        var route = definition.ProjectId.HasValue
            ? $"/projects/{definition.ProjectId.Value:D}/processes?processId={definition.Id:D}"
            : $"/processes?processId={definition.Id:D}";
        await searchIndexService.UpsertAsync(
            new SearchDocumentInput(
                "process-definition",
                definition.Id.ToString(),
                "Processes",
                definition.Name,
                definition.Summary,
                $"{definition.ValueStatement}\nCustomer: {definition.CustomerName}\nOwner: {definition.OwnerName}\nVersion: {workingVersion.VersionNumber}",
                route,
                definition.ProjectId),
            cancellationToken);
        await activityStream.RecordAsync(
            new ActivityWriteRequest(
                "processes",
                isNew ? "create-definition" : "update-definition",
                isNew ? "Created process definition" : "Updated process definition",
                definition.Name,
                definition.ProjectId,
                "process-definition",
                definition.Id,
                route,
                DefaultActor),
            cancellationToken);
        return Result<Guid>.Success(definition.Id);
    }

    private sealed record ProcessImportMetadata(
        string SourceFormat,
        string WarningSummary);

    private async Task SaveDefinitionChildrenAsync(
        AppDbContext dbContext,
        Guid workingVersionId,
        ProcessDefinitionEditorModel model,
        CancellationToken cancellationToken) {
        var existingRoles = await dbContext.Set<ProcessRoleRequirement>()
            .Where(item => item.ProcessDefinitionVersionId == workingVersionId)
            .ToListAsync(cancellationToken);
        var existingRoleIds = existingRoles.Select(item => item.Id).ToHashSet();
        var existingSteps = await dbContext.Set<ProcessStepDefinition>()
            .Where(item => item.ProcessDefinitionVersionId == workingVersionId)
            .ToListAsync(cancellationToken);
        var existingStepIds = existingSteps.Select(item => item.Id).ToHashSet();

        dbContext.RemoveRange(await dbContext.Set<ProcessRoleSkillRequirement>().Where(item => existingRoleIds.Contains(item.RoleRequirementId)).ToListAsync(cancellationToken));
        dbContext.RemoveRange(await dbContext.Set<ProcessStepDependencyDefinition>().Where(item => existingStepIds.Contains(item.StepDefinitionId)).ToListAsync(cancellationToken));
        dbContext.RemoveRange(await dbContext.Set<ProcessStepRoleAssignmentRequirement>().Where(item => existingStepIds.Contains(item.StepDefinitionId)).ToListAsync(cancellationToken));
        dbContext.RemoveRange(await dbContext.Set<ProcessArtifactExpectation>().Where(item => existingStepIds.Contains(item.StepDefinitionId)).ToListAsync(cancellationToken));
        dbContext.RemoveRange(await dbContext.Set<ProcessStepArtifactInputDefinition>().Where(item => existingStepIds.Contains(item.StepDefinitionId)).ToListAsync(cancellationToken));
        dbContext.RemoveRange(await dbContext.Set<ProcessStepBranchOutcomeDefinition>().Where(item => existingStepIds.Contains(item.StepDefinitionId)).ToListAsync(cancellationToken));
        dbContext.RemoveRange(existingRoles);
        dbContext.RemoveRange(existingSteps);

        var roleIdMap = new Dictionary<Guid, Guid>();
        for (var index = 0; index < model.Roles.Count; index++) {
            var roleModel = model.Roles[index];
            var roleId = Guid.NewGuid();
            if (roleModel.Id.HasValue) {
                roleIdMap[roleModel.Id.Value] = roleId;
            }

            await dbContext.Set<ProcessRoleRequirement>().AddAsync(
                new ProcessRoleRequirement {
                    Id = roleId,
                    ProcessDefinitionVersionId = workingVersionId,
                    Key = string.IsNullOrWhiteSpace(roleModel.Key) ? BuildKey(roleModel.DisplayName, $"role-{index + 1}") : BuildKey(roleModel.Key, $"role-{index + 1}"),
                    DisplayName = roleModel.DisplayName.Trim(),
                    Purpose = roleModel.Purpose.Trim(),
                    StaffingIntent = roleModel.StaffingIntent.Trim(),
                    PreferredExecutorKind = roleModel.PreferredExecutorKind.Trim(),
                    PreferredProjectAssignmentRole = roleModel.PreferredProjectAssignmentRole,
                    IsRequired = roleModel.IsRequired,
                    AllowsFallback = roleModel.AllowsFallback,
                    RequiresExplicitApproval = roleModel.RequiresExplicitApproval,
                    DefaultAllocationPercent = Math.Clamp(roleModel.DefaultAllocationPercent, 0, 100),
                    RoleTemplateSourceKey = roleModel.RoleTemplateSourceKey.Trim(),
                    RoleTemplateSnapshotName = roleModel.RoleTemplateSnapshotName.Trim(),
                    SnapshotSummary = roleModel.SnapshotSummary.Trim(),
                    DisplayOrder = index,
                    CanvasX = roleModel.CanvasX,
                    CanvasY = roleModel.CanvasY
                },
                cancellationToken);

            foreach (var skillId in roleModel.RequiredSkillIds.Distinct()) {
                if (skillId == Guid.Empty) {
                    continue;
                }

                await dbContext.Set<ProcessRoleSkillRequirement>().AddAsync(
                    new ProcessRoleSkillRequirement {
                        RoleRequirementId = roleId,
                        SkillId = skillId,
                        IsRequired = true
                    },
                    cancellationToken);
            }
        }

        var stepIdMap = new Dictionary<Guid, Guid>();
        var branchOutcomeIdMap = new Dictionary<Guid, Guid>();
        var artifactExpectationIdMap = new Dictionary<Guid, Guid>();
        var persistedStepIds = new List<Guid>(model.Steps.Count);
        var tempDependencies = new List<(Guid StepId, List<ProcessStepDependencyEditorModel> Dependencies)>();
        var tempArtifactInputs = new List<(Guid StepId, List<ProcessStepArtifactInputEditorModel> ArtifactInputs)>();
        for (var index = 0; index < model.Steps.Count; index++) {
            var stepModel = model.Steps[index];
            var normalizedDependencies = ProcessCanvasBranching.GetOrderedDependencies(stepModel)
                .Select(dependency => new ProcessStepDependencyEditorModel {
                    Id = dependency.Id,
                    DependsOnStepId = dependency.DependsOnStepId,
                    DependsOnBranchOutcomeId = dependency.DependsOnBranchOutcomeId
                })
                .ToList();
            var stepId = Guid.NewGuid();
            persistedStepIds.Add(stepId);
            if (stepModel.Id.HasValue) {
                stepIdMap[stepModel.Id.Value] = stepId;
            }

            tempDependencies.Add((stepId, normalizedDependencies));
            tempArtifactInputs.Add((stepId, stepModel.ArtifactInputs
                .Select(item => new ProcessStepArtifactInputEditorModel {
                    Id = item.Id,
                    ArtifactExpectationId = item.ArtifactExpectationId
                })
                .ToList()));
            await dbContext.Set<ProcessStepDefinition>().AddAsync(
                new ProcessStepDefinition {
                    Id = stepId,
                    ProcessDefinitionVersionId = workingVersionId,
                    Key = string.IsNullOrWhiteSpace(stepModel.Key) ? BuildKey(stepModel.Title, $"step-{index + 1}") : BuildKey(stepModel.Key, $"step-{index + 1}"),
                    Title = stepModel.Title.Trim(),
                    Subtitle = stepModel.Subtitle.Trim(),
                    Notes = stepModel.Notes.Trim(),
                    StepKind = stepModel.StepKind,
                    AllowsManualSkip = stepModel.AllowsManualSkip,
                    AllowsSafeRefusal = stepModel.AllowsSafeRefusal,
                    RequiresApproval = stepModel.RequiresApproval,
                    RequiresDecisionRecord = stepModel.RequiresDecisionRecord,
                    InputContractSummary = stepModel.InputContractSummary.Trim(),
                    OutputContractSummary = stepModel.OutputContractSummary.Trim(),
                    EvidenceContractSummary = stepModel.EvidenceContractSummary.Trim(),
                    DecisionRightsSummary = stepModel.DecisionRightsSummary.Trim(),
                    ExceptionPolicySummary = stepModel.ExceptionPolicySummary.Trim(),
                    TargetLeadHours = Math.Max(0, stepModel.TargetLeadHours),
                    OrderIndex = index,
                    DecisionRoleRequirementId = stepModel.DecisionRoleRequirementId.HasValue &&
                        roleIdMap.TryGetValue(stepModel.DecisionRoleRequirementId.Value, out var remappedDecisionRoleId)
                        ? remappedDecisionRoleId
                        : stepModel.DecisionRoleRequirementId,
                    CanvasX = stepModel.CanvasX,
                    CanvasY = stepModel.CanvasY,
                    BranchCanvasX = stepModel.BranchCanvasX,
                    BranchCanvasY = stepModel.BranchCanvasY
                },
                cancellationToken);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        for (var stepIndex = 0; stepIndex < model.Steps.Count; stepIndex++) {
            var stepModel = model.Steps[stepIndex];
            var stepId = persistedStepIds[stepIndex];

            for (var outcomeIndex = 0; outcomeIndex < stepModel.BranchOutcomes.Count; outcomeIndex++) {
                var outcomeModel = stepModel.BranchOutcomes[outcomeIndex];
                var outcomeId = Guid.NewGuid();
                if (outcomeModel.Id.HasValue) {
                    branchOutcomeIdMap[outcomeModel.Id.Value] = outcomeId;
                }

                await dbContext.Set<ProcessStepBranchOutcomeDefinition>().AddAsync(
                    new ProcessStepBranchOutcomeDefinition {
                        Id = outcomeId,
                        StepDefinitionId = stepId,
                        Key = string.IsNullOrWhiteSpace(outcomeModel.Key) ? BuildKey(outcomeModel.Title, $"outcome-{outcomeIndex + 1}") : BuildKey(outcomeModel.Key, $"outcome-{outcomeIndex + 1}"),
                        Title = outcomeModel.Title.Trim(),
                        Description = outcomeModel.Description.Trim(),
                        DisplayOrder = outcomeIndex
                    },
                    cancellationToken);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        var persistedSteps = await dbContext.Set<ProcessStepDefinition>()
            .Where(item => item.ProcessDefinitionVersionId == workingVersionId)
            .ToDictionaryAsync(item => item.Id, cancellationToken);

        foreach (var dependency in tempDependencies) {
            if (!persistedSteps.TryGetValue(dependency.StepId, out var step)) {
                continue;
            }

            var remappedDependencies = dependency.Dependencies
                .Where(item => item.DependsOnStepId.HasValue &&
                    stepIdMap.ContainsKey(item.DependsOnStepId.Value))
                .Select((item, index) => new ProcessStepDependencyDefinition {
                    StepDefinitionId = dependency.StepId,
                    DependsOnStepId = stepIdMap[item.DependsOnStepId!.Value],
                    DependsOnBranchOutcomeId = item.DependsOnBranchOutcomeId.HasValue &&
                        branchOutcomeIdMap.TryGetValue(item.DependsOnBranchOutcomeId.Value, out var remappedOutcomeId)
                            ? remappedOutcomeId
                            : null,
                    DisplayOrder = index
                })
                .ToList();

            var primaryDependency = remappedDependencies.FirstOrDefault();
            step.DependsOnStepId = primaryDependency?.DependsOnStepId;
            step.DependsOnBranchOutcomeId = primaryDependency?.DependsOnBranchOutcomeId;

            foreach (var remappedDependency in remappedDependencies) {
                await dbContext.Set<ProcessStepDependencyDefinition>().AddAsync(remappedDependency, cancellationToken);
            }
        }

        for (var stepIndex = 0; stepIndex < model.Steps.Count; stepIndex++) {
            var stepModel = model.Steps[stepIndex];
            var stepId = persistedStepIds[stepIndex];

            foreach (var roleRequirementModel in stepModel.RoleAssignments) {
                if (!roleRequirementModel.RoleRequirementId.HasValue) {
                    continue;
                }

                var resolvedRoleId = roleIdMap.TryGetValue(roleRequirementModel.RoleRequirementId.Value, out var remappedRoleId)
                    ? remappedRoleId
                    : roleRequirementModel.RoleRequirementId.Value;
                await dbContext.Set<ProcessStepRoleAssignmentRequirement>().AddAsync(
                    new ProcessStepRoleAssignmentRequirement {
                        StepDefinitionId = stepId,
                        RoleRequirementId = resolvedRoleId,
                        ResponsibilityKind = roleRequirementModel.ResponsibilityKind,
                        IsRequired = roleRequirementModel.IsRequired,
                        FallbackOrder = Math.Max(0, roleRequirementModel.FallbackOrder),
                        RebindPolicySummary = roleRequirementModel.RebindPolicySummary.Trim()
                    },
                    cancellationToken);
            }

            foreach (var artifactModel in stepModel.ArtifactExpectations) {
                if (string.IsNullOrWhiteSpace(artifactModel.Title)) {
                    continue;
                }

                var artifactExpectationId = Guid.NewGuid();
                if (artifactModel.Id.HasValue) {
                    artifactExpectationIdMap[artifactModel.Id.Value] = artifactExpectationId;
                }

                await dbContext.Set<ProcessArtifactExpectation>().AddAsync(
                    new ProcessArtifactExpectation {
                        Id = artifactExpectationId,
                        StepDefinitionId = stepId,
                        ArtifactKind = artifactModel.ArtifactKind,
                        Title = artifactModel.Title.Trim(),
                        IsRequired = artifactModel.IsRequired,
                        TrustRequirement = artifactModel.TrustRequirement,
                        SensitivityLevel = artifactModel.SensitivityLevel,
                        RetentionDays = Math.Max(0, artifactModel.RetentionDays),
                        AllowedFutureUsageSummary = artifactModel.AllowedFutureUsageSummary.Trim(),
                        ValidationRequirementSummary = artifactModel.ValidationRequirementSummary.Trim()
                    },
                    cancellationToken);
            }

            var artifactInputsForStep = tempArtifactInputs
                .FirstOrDefault(item => item.StepId == stepId)
                .ArtifactInputs;
            for (var artifactInputIndex = 0; artifactInputIndex < artifactInputsForStep.Count; artifactInputIndex++) {
                var artifactInputModel = artifactInputsForStep[artifactInputIndex];
                if (!artifactInputModel.ArtifactExpectationId.HasValue ||
                    !artifactExpectationIdMap.TryGetValue(artifactInputModel.ArtifactExpectationId.Value, out var remappedArtifactExpectationId)) {
                    continue;
                }

                await dbContext.Set<ProcessStepArtifactInputDefinition>().AddAsync(
                    new ProcessStepArtifactInputDefinition {
                        StepDefinitionId = stepId,
                        ArtifactExpectationId = remappedArtifactExpectationId,
                        DisplayOrder = artifactInputIndex
                    },
                    cancellationToken);
            }
        }
    }

    private async Task<ProcessDefinitionVersion?> GetWorkingVersionAsync(
        AppDbContext dbContext,
        Guid definitionId,
        CancellationToken cancellationToken) {
        return await dbContext.Set<ProcessDefinitionVersion>()
            .Where(item => item.ProcessDefinitionId == definitionId)
            .OrderBy(item => item.Status == ProcessVersionStatus.Draft ? 0 : 1)
            .ThenByDescending(item => item.VersionNumber)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<int> GetNextVersionNumberAsync(AppDbContext dbContext, Guid definitionId, CancellationToken cancellationToken) {
        var existingVersionNumber = await dbContext.Set<ProcessDefinitionVersion>()
            .Where(item => item.ProcessDefinitionId == definitionId)
            .Select(item => (int?)item.VersionNumber)
            .MaxAsync(cancellationToken);
        return (existingVersionNumber ?? 0) + 1;
    }

    private static string BuildSlug(string input) {
        var slug = input.Trim().ToLowerInvariant();
        foreach (var character in Path.GetInvalidFileNameChars()) {
            slug = slug.Replace(character.ToString(), string.Empty, StringComparison.Ordinal);
        }

        slug = slug.Replace(' ', '-');
        return string.IsNullOrWhiteSpace(slug) ? Guid.NewGuid().ToString("N") : slug;
    }

    private static async Task<string> BuildUniqueSlugAsync(
        AppDbContext dbContext,
        string input,
        Guid? projectId,
        Guid currentDefinitionId,
        CancellationToken cancellationToken)
    {
        var baseSlug = BuildSlug(input);
        var scopeSuffix = projectId?.ToString("N")[..8];
        var alternateBaseSlug = projectId.HasValue
            ? $"{baseSlug}-{scopeSuffix}"
            : baseSlug;
        var candidate = baseSlug;

        if (await SlugExistsAsync(dbContext, candidate, currentDefinitionId, cancellationToken)) {
            candidate = projectId.HasValue ? alternateBaseSlug : $"{baseSlug}-2";
        }

        var suffixBase = projectId.HasValue ? alternateBaseSlug : baseSlug;
        var suffix = projectId.HasValue ? 2 : 3;
        while (await SlugExistsAsync(dbContext, candidate, currentDefinitionId, cancellationToken)) {
            candidate = $"{suffixBase}-{suffix}";
            suffix++;
        }

        return candidate;
    }

    private static Task<bool> SlugExistsAsync(
        AppDbContext dbContext,
        string slug,
        Guid currentDefinitionId,
        CancellationToken cancellationToken)
    {
        return dbContext.Set<ProcessDefinition>()
            .AnyAsync(item => item.Id != currentDefinitionId && item.Slug == slug, cancellationToken);
    }

    private static string BuildKey(string input, string fallback) {
        var normalized = BuildSlug(input);
        return string.IsNullOrWhiteSpace(normalized) ? fallback : normalized;
    }

    private static int Average(IEnumerable<int> values) {
        var materialized = values.ToList();
        return materialized.Count == 0 ? 0 : (int)Math.Round(materialized.Average(), MidpointRounding.AwayFromZero);
    }
}
