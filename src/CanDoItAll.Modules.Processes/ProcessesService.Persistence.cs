using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Infrastructure.Search;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Processes;

public sealed partial class ProcessesService
{
    private const int DefinitionSlugRetryLimit = 3;

    public async Task<Result<Guid>> SaveAsync(
        ProcessDefinitionEditorModel model,
        CancellationToken cancellationToken = default) {
        return await SaveAsync(model, importMetadata: null, cancellationToken);
    }

    private async Task<Result<Guid>> SaveAsync(
        ProcessDefinitionEditorModel model,
        ProcessImportMetadata? importMetadata,
        CancellationToken cancellationToken) {
        NormalizeDefinitionEditorForSave(model);
        var validationError = ValidateDefinitionEditor(model);
        if (validationError is not null) {
            return Result<Guid>.Failure(validationError);
        }

        for (var slugRetryAttempt = 0; ; slugRetryAttempt++) {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

            ProcessDefinition definition;
            ProcessDefinitionVersion workingVersion;
            Guid outboxId;
            var isNew = false;
            try {
                definition = model.Id.HasValue
                    ? await dbContext.Set<ProcessDefinition>().SingleOrDefaultAsync(item => item.Id == model.Id.Value, cancellationToken)
                    : null;

                isNew = definition is null;
                if (definition is null) {
                    definition = new ProcessDefinition {
                        CreatedAtUtc = clock.GetUtcNow()
                    };

                    await dbContext.Set<ProcessDefinition>().AddAsync(definition, cancellationToken);
                } else if (HasConcurrencyTokenMismatch(model.DefinitionConcurrencyToken, definition.ConcurrencyToken)) {
                    return Result<Guid>.Failure(CreateDefinitionSaveConflictError());
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

                workingVersion = model.WorkingVersionId.HasValue
                    ? await dbContext.Set<ProcessDefinitionVersion>()
                        .SingleOrDefaultAsync(item => item.Id == model.WorkingVersionId.Value, cancellationToken)
                    : null;

                if (workingVersion is null) {
                    workingVersion = await GetWorkingVersionAsync(dbContext, definition.Id, cancellationToken);
                }

                if (workingVersion is not null && HasConcurrencyTokenMismatch(model.WorkingVersionConcurrencyToken, workingVersion.ConcurrencyToken)) {
                    return Result<Guid>.Failure(CreateDefinitionSaveConflictError());
                }

                if (workingVersion is not null && workingVersion.Status == ProcessVersionStatus.Published) {
                    return Result<Guid>.Failure(Error.Validation("Published versions are immutable. Save into a draft version instead.", "processes.immutable-published-version"));
                }

                if (workingVersion is null) {
                    await EnsureNextVersionNumberAheadOfExistingVersionsAsync(dbContext, definition, cancellationToken);
                    workingVersion = new ProcessDefinitionVersion {
                        ProcessDefinitionId = definition.Id,
                        VersionNumber = AllocateNextVersionNumber(definition),
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
                outboxId = await processOutboxService.EnqueueDefinitionSaveAsync(
                    dbContext,
                    definition,
                    workingVersion,
                    isNew,
                    cancellationToken);
                await dbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                await processOutboxService.ProcessAsync(outboxId, cancellationToken);
                return Result<Guid>.Success(definition.Id);
            }
            catch (DbUpdateConcurrencyException) {
                await transaction.RollbackAsync(cancellationToken);
                return Result<Guid>.Failure(CreateDefinitionSaveConflictError());
            }
            catch (DbUpdateException exception) when (DbUpdateExceptionClassifier.IsUniqueConstraintViolation(exception) &&
                                                      IsDefinitionSlugConflict(exception) &&
                                                      slugRetryAttempt < DefinitionSlugRetryLimit - 1) {
                await transaction.RollbackAsync(cancellationToken);
                continue;
            }
            catch (DbUpdateException exception) when (DbUpdateExceptionClassifier.IsUniqueConstraintViolation(exception)) {
                await transaction.RollbackAsync(cancellationToken);
                return Result<Guid>.Failure(CreateDefinitionSaveUniqueConflictError(exception));
            }
        }
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
        var existingSteps = await dbContext.Set<ProcessStepDefinition>()
            .Where(item => item.ProcessDefinitionVersionId == workingVersionId)
            .ToListAsync(cancellationToken);
        var existingRoleIds = existingRoles.Select(item => item.Id).ToHashSet();
        var existingStepIds = existingSteps.Select(item => item.Id).ToHashSet();
        var existingRoleSkills = await dbContext.Set<ProcessRoleSkillRequirement>()
            .Where(item => existingRoleIds.Contains(item.RoleRequirementId))
            .ToListAsync(cancellationToken);
        var existingDependencies = await dbContext.Set<ProcessStepDependencyDefinition>()
            .Where(item => existingStepIds.Contains(item.StepDefinitionId))
            .ToListAsync(cancellationToken);
        var existingAssignments = await dbContext.Set<ProcessStepRoleAssignmentRequirement>()
            .Where(item => existingStepIds.Contains(item.StepDefinitionId))
            .ToListAsync(cancellationToken);
        var existingArtifactExpectations = await dbContext.Set<ProcessArtifactExpectation>()
            .Where(item => existingStepIds.Contains(item.StepDefinitionId))
            .ToListAsync(cancellationToken);
        var existingArtifactInputs = await dbContext.Set<ProcessStepArtifactInputDefinition>()
            .Where(item => existingStepIds.Contains(item.StepDefinitionId))
            .ToListAsync(cancellationToken);
        var existingBranchOutcomes = await dbContext.Set<ProcessStepBranchOutcomeDefinition>()
            .Where(item => existingStepIds.Contains(item.StepDefinitionId))
            .ToListAsync(cancellationToken);

        var rolesById = existingRoles.ToDictionary(item => item.Id);
        var stepsById = existingSteps.ToDictionary(item => item.Id);
        var roleSkillsById = existingRoleSkills.ToDictionary(item => item.Id);
        var dependenciesById = existingDependencies.ToDictionary(item => item.Id);
        var assignmentsById = existingAssignments.ToDictionary(item => item.Id);
        var artifactExpectationsById = existingArtifactExpectations.ToDictionary(item => item.Id);
        var artifactInputsById = existingArtifactInputs.ToDictionary(item => item.Id);
        var branchOutcomesById = existingBranchOutcomes.ToDictionary(item => item.Id);

        var roleIdMap = new Dictionary<Guid, Guid>();
        var stepIdMap = new Dictionary<Guid, Guid>();
        var branchOutcomeIdMap = new Dictionary<Guid, Guid>();
        var artifactExpectationIdMap = new Dictionary<Guid, Guid>();

        var assignedRoleIds = new HashSet<Guid>();
        var assignedStepIds = new HashSet<Guid>();
        var assignedBranchOutcomeIds = new HashSet<Guid>();
        var assignedDependencyIds = new HashSet<Guid>();
        var assignedAssignmentIds = new HashSet<Guid>();
        var assignedArtifactExpectationIds = new HashSet<Guid>();
        var assignedArtifactInputIds = new HashSet<Guid>();

        var retainedRoleIds = new HashSet<Guid>();
        var retainedRoleSkillIds = new HashSet<Guid>();
        var retainedStepIds = new HashSet<Guid>();
        var retainedBranchOutcomeIds = new HashSet<Guid>();
        var retainedDependencyIds = new HashSet<Guid>();
        var retainedAssignmentIds = new HashSet<Guid>();
        var retainedArtifactExpectationIds = new HashSet<Guid>();
        var retainedArtifactInputIds = new HashSet<Guid>();

        var resolvedRoles = new List<(Guid RoleId, ProcessRoleEditorModel Model)>(model.Roles.Count);
        for (var index = 0; index < model.Roles.Count; index++) {
            var roleModel = model.Roles[index];
            var roleId = ResolveStableChildId(roleModel.Id, assignedRoleIds, "role");
            if (roleModel.Id.HasValue && roleModel.Id.Value != Guid.Empty) {
                roleIdMap[roleModel.Id.Value] = roleId;
            }

            if (!rolesById.TryGetValue(roleId, out var role)) {
                role = new ProcessRoleRequirement {
                    Id = roleId,
                    ProcessDefinitionVersionId = workingVersionId
                };

                await dbContext.Set<ProcessRoleRequirement>().AddAsync(role, cancellationToken);
                rolesById[roleId] = role;
            }

            role.ProcessDefinitionVersionId = workingVersionId;
            role.Key = string.IsNullOrWhiteSpace(roleModel.Key)
                ? BuildKey(roleModel.DisplayName, $"role-{index + 1}")
                : BuildKey(roleModel.Key, $"role-{index + 1}");
            role.DisplayName = roleModel.DisplayName.Trim();
            role.Purpose = roleModel.Purpose.Trim();
            role.StaffingIntent = roleModel.StaffingIntent.Trim();
            role.PreferredExecutorKind = roleModel.PreferredExecutorKind.Trim();
            role.PreferredProjectAssignmentRole = roleModel.PreferredProjectAssignmentRole;
            role.IsRequired = roleModel.IsRequired;
            role.AllowsFallback = roleModel.AllowsFallback;
            role.RequiresExplicitApproval = roleModel.RequiresExplicitApproval;
            role.DefaultAllocationPercent = Math.Clamp(roleModel.DefaultAllocationPercent, 0, 100);
            role.RoleTemplateSourceKey = roleModel.RoleTemplateSourceKey.Trim();
            role.RoleTemplateSnapshotName = roleModel.RoleTemplateSnapshotName.Trim();
            role.SnapshotSummary = roleModel.SnapshotSummary.Trim();
            role.DisplayOrder = index;
            role.CanvasX = roleModel.CanvasX;
            role.CanvasY = roleModel.CanvasY;

            retainedRoleIds.Add(roleId);
            resolvedRoles.Add((roleId, roleModel));
        }

        foreach (var resolvedRole in resolvedRoles) {
            foreach (var skillId in resolvedRole.Model.RequiredSkillIds.Distinct()) {
                if (skillId == Guid.Empty) {
                    continue;
                }

                var existingRoleSkill = existingRoleSkills.FirstOrDefault(item =>
                    item.RoleRequirementId == resolvedRole.RoleId &&
                    item.SkillId == skillId);
                if (existingRoleSkill is null) {
                    existingRoleSkill = new ProcessRoleSkillRequirement {
                        RoleRequirementId = resolvedRole.RoleId,
                        SkillId = skillId,
                        IsRequired = true
                    };

                    await dbContext.Set<ProcessRoleSkillRequirement>().AddAsync(existingRoleSkill, cancellationToken);
                    existingRoleSkills.Add(existingRoleSkill);
                    roleSkillsById[existingRoleSkill.Id] = existingRoleSkill;
                } else {
                    existingRoleSkill.IsRequired = true;
                }

                retainedRoleSkillIds.Add(existingRoleSkill.Id);
            }
        }

        var resolvedSteps = new List<(Guid StepId, bool ReusesExistingEntity, ProcessStepDefinition Entity, ProcessStepEditorModel Model, IReadOnlyList<ProcessStepDependencyEditorModel> Dependencies)>(model.Steps.Count);
        for (var index = 0; index < model.Steps.Count; index++) {
            var stepModel = model.Steps[index];
            var stepId = ResolveStableChildId(stepModel.Id, assignedStepIds, "step");
            if (stepModel.Id.HasValue && stepModel.Id.Value != Guid.Empty) {
                stepIdMap[stepModel.Id.Value] = stepId;
            }

            var reusesExistingEntity = stepsById.TryGetValue(stepId, out var step);
            if (!reusesExistingEntity) {
                step = new ProcessStepDefinition {
                    Id = stepId,
                    ProcessDefinitionVersionId = workingVersionId
                };

                await dbContext.Set<ProcessStepDefinition>().AddAsync(step, cancellationToken);
                stepsById[stepId] = step;
            }

            step.ProcessDefinitionVersionId = workingVersionId;
            step.Key = string.IsNullOrWhiteSpace(stepModel.Key)
                ? BuildKey(stepModel.Title, $"step-{index + 1}")
                : BuildKey(stepModel.Key, $"step-{index + 1}");
            step.Title = stepModel.Title.Trim();
            step.Subtitle = stepModel.Subtitle.Trim();
            step.Notes = stepModel.Notes.Trim();
            step.StepKind = stepModel.StepKind;
            step.AllowsManualSkip = stepModel.AllowsManualSkip;
            step.AllowsSafeRefusal = stepModel.AllowsSafeRefusal;
            step.RequiresApproval = stepModel.RequiresApproval;
            step.RequiresDecisionRecord = stepModel.RequiresDecisionRecord;
            step.InputContractSummary = stepModel.InputContractSummary.Trim();
            step.OutputContractSummary = stepModel.OutputContractSummary.Trim();
            step.EvidenceContractSummary = stepModel.EvidenceContractSummary.Trim();
            step.DecisionRightsSummary = stepModel.DecisionRightsSummary.Trim();
            step.ExceptionPolicySummary = stepModel.ExceptionPolicySummary.Trim();
            step.TargetLeadHours = Math.Max(0, stepModel.TargetLeadHours);
            step.OrderIndex = index;
            step.DecisionRoleRequirementId = stepModel.DecisionRoleRequirementId.HasValue &&
                roleIdMap.TryGetValue(stepModel.DecisionRoleRequirementId.Value, out var remappedDecisionRoleId)
                ? remappedDecisionRoleId
                : stepModel.DecisionRoleRequirementId;
            step.CanvasX = stepModel.CanvasX;
            step.CanvasY = stepModel.CanvasY;
            step.BranchCanvasX = stepModel.BranchCanvasX;
            step.BranchCanvasY = stepModel.BranchCanvasY;

            retainedStepIds.Add(stepId);
            resolvedSteps.Add((
                stepId,
                reusesExistingEntity,
                step,
                stepModel,
                ProcessCanvasBranching.GetOrderedDependencies(stepModel)
                    .Select(dependency => new ProcessStepDependencyEditorModel {
                        Id = dependency.Id,
                        DependsOnStepId = dependency.DependsOnStepId,
                        DependsOnBranchOutcomeId = dependency.DependsOnBranchOutcomeId
                    })
                    .ToList()));
        }

        foreach (var resolvedStep in resolvedSteps) {
            var existingOutcomesForStep = existingBranchOutcomes
                .Where(item => item.StepDefinitionId == resolvedStep.StepId)
                .ToList();
            var existingOutcomesByKey = existingOutcomesForStep
                .GroupBy(item => item.Key, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.Ordinal);

            for (var outcomeIndex = 0; outcomeIndex < resolvedStep.Model.BranchOutcomes.Count; outcomeIndex++) {
                var outcomeModel = resolvedStep.Model.BranchOutcomes[outcomeIndex];
                var resolvedKey = string.IsNullOrWhiteSpace(outcomeModel.Key)
                    ? BuildKey(outcomeModel.Title, $"outcome-{outcomeIndex + 1}")
                    : BuildKey(outcomeModel.Key, $"outcome-{outcomeIndex + 1}");
                ProcessStepBranchOutcomeDefinition? branchOutcome = null;
                var requestedOutcomeId = ResolveStableChildId(
                    resolvedStep.ReusesExistingEntity
                        ? outcomeModel.Id
                        : null,
                    assignedBranchOutcomeIds,
                    "branch outcome");
                if (outcomeModel.Id.HasValue && outcomeModel.Id.Value != Guid.Empty) {
                    branchOutcomeIdMap[outcomeModel.Id.Value] = requestedOutcomeId;
                }

                if (resolvedStep.ReusesExistingEntity &&
                    branchOutcomesById.TryGetValue(requestedOutcomeId, out var existingOutcome)) {
                    branchOutcome = existingOutcome;
                } else if ((!outcomeModel.Id.HasValue || outcomeModel.Id.Value == Guid.Empty) &&
                           existingOutcomesByKey.TryGetValue(resolvedKey, out var matchingOutcomes)) {
                    branchOutcome = matchingOutcomes.FirstOrDefault(candidate => !retainedBranchOutcomeIds.Contains(candidate.Id));
                }

                if (branchOutcome is null) {
                    branchOutcome = new ProcessStepBranchOutcomeDefinition {
                        Id = requestedOutcomeId
                    };

                    await dbContext.Set<ProcessStepBranchOutcomeDefinition>().AddAsync(branchOutcome, cancellationToken);
                    existingBranchOutcomes.Add(branchOutcome);
                    branchOutcomesById[branchOutcome.Id] = branchOutcome;
                }

                branchOutcome.StepDefinitionId = resolvedStep.StepId;
                branchOutcome.Key = resolvedKey;
                branchOutcome.Title = outcomeModel.Title.Trim();
                branchOutcome.Description = outcomeModel.Description.Trim();
                branchOutcome.DisplayOrder = outcomeIndex;

                branchOutcomeIdMap[branchOutcome.Id] = branchOutcome.Id;
                retainedBranchOutcomeIds.Add(branchOutcome.Id);
            }
        }

        foreach (var resolvedStep in resolvedSteps) {
            var existingDependenciesForStep = existingDependencies
                .Where(item => item.StepDefinitionId == resolvedStep.StepId)
                .ToList();
            var existingDependenciesByShape = existingDependenciesForStep
                .GroupBy(item => (item.DependsOnStepId, item.DependsOnBranchOutcomeId))
                .ToDictionary(group => group.Key, group => group.ToList());
            var orderedDependencies = new List<ProcessStepDependencyDefinition>();

            for (var dependencyIndex = 0; dependencyIndex < resolvedStep.Dependencies.Count; dependencyIndex++) {
                var dependencyModel = resolvedStep.Dependencies[dependencyIndex];
                if (!dependencyModel.DependsOnStepId.HasValue || dependencyModel.DependsOnStepId.Value == Guid.Empty) {
                    continue;
                }

                var remappedDependsOnStepId = stepIdMap.TryGetValue(dependencyModel.DependsOnStepId.Value, out var mappedDependsOnStepId)
                    ? mappedDependsOnStepId
                    : dependencyModel.DependsOnStepId.Value;
                if (!stepsById.ContainsKey(remappedDependsOnStepId)) {
                    throw new InvalidOperationException($"Dependency step '{dependencyModel.DependsOnStepId.Value:D}' could not be resolved during save.");
                }

                Guid? remappedDependsOnBranchOutcomeId = null;
                if (dependencyModel.DependsOnBranchOutcomeId.HasValue) {
                    remappedDependsOnBranchOutcomeId = branchOutcomeIdMap.TryGetValue(dependencyModel.DependsOnBranchOutcomeId.Value, out var mappedOutcomeId)
                        ? mappedOutcomeId
                        : dependencyModel.DependsOnBranchOutcomeId.Value;
                    if (remappedDependsOnBranchOutcomeId.HasValue && !branchOutcomesById.ContainsKey(remappedDependsOnBranchOutcomeId.Value)) {
                        throw new InvalidOperationException($"Dependency branch outcome '{dependencyModel.DependsOnBranchOutcomeId.Value:D}' could not be resolved during save.");
                    }
                }

                ProcessStepDependencyDefinition? dependency = null;
                var requestedDependencyId = ResolveStableChildId(
                    resolvedStep.ReusesExistingEntity
                        ? dependencyModel.Id
                        : null,
                    assignedDependencyIds,
                    "step dependency");
                if (resolvedStep.ReusesExistingEntity &&
                    dependencyModel.Id.HasValue &&
                    dependencyModel.Id.Value != Guid.Empty &&
                    dependenciesById.TryGetValue(requestedDependencyId, out var existingDependency)) {
                    dependency = existingDependency;
                } else if ((!dependencyModel.Id.HasValue || dependencyModel.Id.Value == Guid.Empty) &&
                           existingDependenciesByShape.TryGetValue((remappedDependsOnStepId, remappedDependsOnBranchOutcomeId), out var matchingDependencies)) {
                    dependency = matchingDependencies.FirstOrDefault(candidate => !retainedDependencyIds.Contains(candidate.Id));
                }

                if (dependency is null) {
                    dependency = new ProcessStepDependencyDefinition {
                        Id = requestedDependencyId
                    };

                    await dbContext.Set<ProcessStepDependencyDefinition>().AddAsync(dependency, cancellationToken);
                    existingDependencies.Add(dependency);
                    dependenciesById[dependency.Id] = dependency;
                }

                dependency.StepDefinitionId = resolvedStep.StepId;
                dependency.DependsOnStepId = remappedDependsOnStepId;
                dependency.DependsOnBranchOutcomeId = remappedDependsOnBranchOutcomeId;
                dependency.DisplayOrder = dependencyIndex;

                retainedDependencyIds.Add(dependency.Id);
                orderedDependencies.Add(dependency);
            }

        }

        foreach (var resolvedStep in resolvedSteps) {
            var existingAssignmentsForStep = existingAssignments
                .Where(item => item.StepDefinitionId == resolvedStep.StepId)
                .ToList();
            var existingAssignmentsByShape = existingAssignmentsForStep
                .GroupBy(item => (item.RoleRequirementId, item.ResponsibilityKind))
                .ToDictionary(group => group.Key, group => group.ToList());

            foreach (var roleAssignmentModel in resolvedStep.Model.RoleAssignments) {
                if (!roleAssignmentModel.RoleRequirementId.HasValue || roleAssignmentModel.RoleRequirementId.Value == Guid.Empty) {
                    continue;
                }

                var resolvedRoleId = roleIdMap.TryGetValue(roleAssignmentModel.RoleRequirementId.Value, out var remappedRoleId)
                    ? remappedRoleId
                    : roleAssignmentModel.RoleRequirementId.Value;
                if (!rolesById.ContainsKey(resolvedRoleId)) {
                    throw new InvalidOperationException($"Role requirement '{roleAssignmentModel.RoleRequirementId.Value:D}' could not be resolved during save.");
                }

                ProcessStepRoleAssignmentRequirement? assignment = null;
                var requestedAssignmentId = ResolveStableChildId(
                    resolvedStep.ReusesExistingEntity
                        ? roleAssignmentModel.Id
                        : null,
                    assignedAssignmentIds,
                    "step role assignment");
                if (resolvedStep.ReusesExistingEntity &&
                    roleAssignmentModel.Id.HasValue &&
                    roleAssignmentModel.Id.Value != Guid.Empty &&
                    assignmentsById.TryGetValue(requestedAssignmentId, out var existingAssignment)) {
                    assignment = existingAssignment;
                } else if ((!roleAssignmentModel.Id.HasValue || roleAssignmentModel.Id.Value == Guid.Empty) &&
                           existingAssignmentsByShape.TryGetValue((resolvedRoleId, roleAssignmentModel.ResponsibilityKind), out var matchingAssignments)) {
                    assignment = matchingAssignments.FirstOrDefault(candidate => !retainedAssignmentIds.Contains(candidate.Id));
                }

                if (assignment is null) {
                    assignment = new ProcessStepRoleAssignmentRequirement {
                        Id = requestedAssignmentId
                    };

                    await dbContext.Set<ProcessStepRoleAssignmentRequirement>().AddAsync(assignment, cancellationToken);
                    existingAssignments.Add(assignment);
                    assignmentsById[assignment.Id] = assignment;
                }

                assignment.StepDefinitionId = resolvedStep.StepId;
                assignment.RoleRequirementId = resolvedRoleId;
                assignment.ResponsibilityKind = roleAssignmentModel.ResponsibilityKind;
                assignment.IsRequired = roleAssignmentModel.IsRequired;
                assignment.FallbackOrder = Math.Max(0, roleAssignmentModel.FallbackOrder);
                assignment.RebindPolicySummary = roleAssignmentModel.RebindPolicySummary.Trim();

                retainedAssignmentIds.Add(assignment.Id);
            }

            foreach (var artifactModel in resolvedStep.Model.ArtifactExpectations) {
                if (string.IsNullOrWhiteSpace(artifactModel.Title)) {
                    continue;
                }

                ProcessArtifactExpectation? artifactExpectation = null;
                var requestedArtifactExpectationId = ResolveStableChildId(
                    resolvedStep.ReusesExistingEntity
                        ? artifactModel.Id
                        : null,
                    assignedArtifactExpectationIds,
                    "artifact expectation");
                if (artifactModel.Id.HasValue && artifactModel.Id.Value != Guid.Empty) {
                    artifactExpectationIdMap[artifactModel.Id.Value] = requestedArtifactExpectationId;
                }

                if (resolvedStep.ReusesExistingEntity &&
                    artifactModel.Id.HasValue &&
                    artifactModel.Id.Value != Guid.Empty &&
                    artifactExpectationsById.TryGetValue(requestedArtifactExpectationId, out var existingArtifactExpectation)) {
                    artifactExpectation = existingArtifactExpectation;
                }

                if (artifactExpectation is null) {
                    artifactExpectation = new ProcessArtifactExpectation {
                        Id = requestedArtifactExpectationId
                    };

                    await dbContext.Set<ProcessArtifactExpectation>().AddAsync(artifactExpectation, cancellationToken);
                    existingArtifactExpectations.Add(artifactExpectation);
                    artifactExpectationsById[artifactExpectation.Id] = artifactExpectation;
                }

                artifactExpectation.StepDefinitionId = resolvedStep.StepId;
                artifactExpectation.ArtifactKind = artifactModel.ArtifactKind;
                artifactExpectation.Title = artifactModel.Title.Trim();
                artifactExpectation.IsRequired = artifactModel.IsRequired;
                artifactExpectation.TrustRequirement = artifactModel.TrustRequirement;
                artifactExpectation.SensitivityLevel = artifactModel.SensitivityLevel;
                artifactExpectation.RetentionDays = Math.Max(0, artifactModel.RetentionDays);
                artifactExpectation.AllowedFutureUsageSummary = artifactModel.AllowedFutureUsageSummary.Trim();
                artifactExpectation.ValidationRequirementSummary = artifactModel.ValidationRequirementSummary.Trim();

                artifactExpectationIdMap[artifactExpectation.Id] = artifactExpectation.Id;
                retainedArtifactExpectationIds.Add(artifactExpectation.Id);
            }
        }

        foreach (var resolvedStep in resolvedSteps) {
            var existingArtifactInputsForStep = existingArtifactInputs
                .Where(item => item.StepDefinitionId == resolvedStep.StepId)
                .ToList();
            var existingArtifactInputsByArtifactId = existingArtifactInputsForStep
                .GroupBy(item => item.ArtifactExpectationId)
                .ToDictionary(group => group.Key, group => group.ToList());

            for (var artifactInputIndex = 0; artifactInputIndex < resolvedStep.Model.ArtifactInputs.Count; artifactInputIndex++) {
                var artifactInputModel = resolvedStep.Model.ArtifactInputs[artifactInputIndex];
                if (!artifactInputModel.ArtifactExpectationId.HasValue || artifactInputModel.ArtifactExpectationId.Value == Guid.Empty) {
                    continue;
                }

                var remappedArtifactExpectationId = artifactExpectationIdMap.TryGetValue(artifactInputModel.ArtifactExpectationId.Value, out var mappedArtifactExpectationId)
                    ? mappedArtifactExpectationId
                    : artifactInputModel.ArtifactExpectationId.Value;
                if (!artifactExpectationsById.ContainsKey(remappedArtifactExpectationId)) {
                    throw new InvalidOperationException($"Artifact expectation '{artifactInputModel.ArtifactExpectationId.Value:D}' could not be resolved during save.");
                }

                ProcessStepArtifactInputDefinition? artifactInput = null;
                var requestedArtifactInputId = ResolveStableChildId(
                    resolvedStep.ReusesExistingEntity
                        ? artifactInputModel.Id
                        : null,
                    assignedArtifactInputIds,
                    "artifact input");
                if (resolvedStep.ReusesExistingEntity &&
                    artifactInputModel.Id.HasValue &&
                    artifactInputModel.Id.Value != Guid.Empty &&
                    artifactInputsById.TryGetValue(requestedArtifactInputId, out var existingArtifactInput)) {
                    artifactInput = existingArtifactInput;
                } else if ((!artifactInputModel.Id.HasValue || artifactInputModel.Id.Value == Guid.Empty) &&
                           existingArtifactInputsByArtifactId.TryGetValue(remappedArtifactExpectationId, out var matchingArtifactInputs)) {
                    artifactInput = matchingArtifactInputs.FirstOrDefault(candidate => !retainedArtifactInputIds.Contains(candidate.Id));
                }

                if (artifactInput is null) {
                    artifactInput = new ProcessStepArtifactInputDefinition {
                        Id = requestedArtifactInputId
                    };

                    await dbContext.Set<ProcessStepArtifactInputDefinition>().AddAsync(artifactInput, cancellationToken);
                    existingArtifactInputs.Add(artifactInput);
                    artifactInputsById[artifactInput.Id] = artifactInput;
                }

                artifactInput.StepDefinitionId = resolvedStep.StepId;
                artifactInput.ArtifactExpectationId = remappedArtifactExpectationId;
                artifactInput.DisplayOrder = artifactInputIndex;

                retainedArtifactInputIds.Add(artifactInput.Id);
            }
        }

        dbContext.RemoveRange(existingArtifactInputs.Where(item => !retainedArtifactInputIds.Contains(item.Id)).ToList());
        dbContext.RemoveRange(existingAssignments.Where(item => !retainedAssignmentIds.Contains(item.Id)).ToList());
        dbContext.RemoveRange(existingDependencies.Where(item => !retainedDependencyIds.Contains(item.Id)).ToList());
        dbContext.RemoveRange(existingBranchOutcomes.Where(item => !retainedBranchOutcomeIds.Contains(item.Id)).ToList());
        dbContext.RemoveRange(existingArtifactExpectations.Where(item => !retainedArtifactExpectationIds.Contains(item.Id)).ToList());
        dbContext.RemoveRange(existingRoleSkills.Where(item => !retainedRoleSkillIds.Contains(item.Id)).ToList());
        dbContext.RemoveRange(existingSteps.Where(item => !retainedStepIds.Contains(item.Id)).ToList());
        dbContext.RemoveRange(existingRoles.Where(item => !retainedRoleIds.Contains(item.Id)).ToList());

        static Guid ResolveStableChildId(Guid? requestedId, HashSet<Guid> assignedIds, string childKind) {
            if (requestedId.HasValue && requestedId.Value != Guid.Empty) {
                if (!assignedIds.Add(requestedId.Value)) {
                    throw new InvalidOperationException($"Duplicate {childKind} id '{requestedId.Value:D}' detected during save.");
                }

                return requestedId.Value;
            }

            Guid generatedId;
            do {
                generatedId = Guid.NewGuid();
            } while (!assignedIds.Add(generatedId));

            return generatedId;
        }
    }

    private async Task<ProcessDefinitionVersion?> GetWorkingVersionAsync(
        AppDbContext dbContext,
        Guid definitionId,
        CancellationToken cancellationToken) {
        return await dbContext.Set<ProcessDefinitionVersion>()
            .SingleOrDefaultAsync(
                item => item.ProcessDefinitionId == definitionId &&
                item.Status == ProcessVersionStatus.Draft,
                cancellationToken);
    }

    private static async Task EnsureNextVersionNumberAheadOfExistingVersionsAsync(
        AppDbContext dbContext,
        ProcessDefinition definition,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(definition);

        var highestExistingVersionNumber = await dbContext.Set<ProcessDefinitionVersion>()
            .Where(item => item.ProcessDefinitionId == definition.Id)
            .Select(item => (int?)item.VersionNumber)
            .MaxAsync(cancellationToken);
        var nextAvailableVersionNumber = (highestExistingVersionNumber ?? 0) + 1;
        if (definition.NextVersionNumber < nextAvailableVersionNumber) {
            definition.NextVersionNumber = nextAvailableVersionNumber;
        }
    }

    private static int AllocateNextVersionNumber(ProcessDefinition definition) {
        ArgumentNullException.ThrowIfNull(definition);

        var nextVersionNumber = Math.Max(1, definition.NextVersionNumber);
        definition.NextVersionNumber = nextVersionNumber + 1;
        return nextVersionNumber;
    }

    private static string BuildSlug(string input) {
        return FileSafeSlugBuilder.Build(input);
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
