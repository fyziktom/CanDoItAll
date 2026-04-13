using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Processes;

internal sealed class ProcessDefinitionDraftCloneEngine
{
    public async Task CloneAsync(
        AppDbContext dbContext,
        ProcessDefinitionVersion nextDraft,
        ProcessDefinitionDraftCloneSource source,
        CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(nextDraft);
        ArgumentNullException.ThrowIfNull(source);

        var roleIdMap = source.Roles.ToDictionary(item => item.Id, _ => Guid.NewGuid());
        var stepIdMap = source.Steps.ToDictionary(item => item.Id, _ => Guid.NewGuid());
        var branchOutcomeIdMap = source.BranchOutcomes.ToDictionary(item => item.Id, _ => Guid.NewGuid());
        var artifactExpectationIdMap = source.ArtifactExpectations.ToDictionary(item => item.Id, _ => Guid.NewGuid());
        var sourceDependenciesByStepId = source.StepDependencies
            .GroupBy(item => item.StepDefinitionId)
            .ToDictionary(group => group.Key, group => group.OrderBy(item => item.DisplayOrder).ToList());
        var clonedStepsById = new Dictionary<Guid, ProcessStepDefinition>();
        var clonedDependenciesByStepId = new Dictionary<Guid, List<ProcessStepDependencyDefinition>>();

        foreach (var role in source.Roles.OrderBy(item => item.DisplayOrder)) {
            await dbContext.Set<ProcessRoleRequirement>().AddAsync(
                new ProcessRoleRequirement {
                    Id = roleIdMap[role.Id],
                    ProcessDefinitionVersionId = nextDraft.Id,
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

        foreach (var roleSkill in source.RoleSkills) {
            if (!roleIdMap.TryGetValue(roleSkill.RoleRequirementId, out var nextRoleId)) {
                throw new InvalidOperationException(
                    $"Role skill '{roleSkill.Id:D}' references missing role '{roleSkill.RoleRequirementId:D}' during draft clone.");
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

        foreach (var step in source.Steps.OrderBy(item => item.OrderIndex)) {
            Guid? decisionRoleRequirementId = null;
            if (step.DecisionRoleRequirementId.HasValue) {
                if (!roleIdMap.TryGetValue(step.DecisionRoleRequirementId.Value, out var nextDecisionRoleRequirementId)) {
                    throw new InvalidOperationException(
                        $"Step '{step.Id:D}' references missing decision role '{step.DecisionRoleRequirementId.Value:D}' during draft clone.");
                }

                decisionRoleRequirementId = nextDecisionRoleRequirementId;
            }

            var clonedStep = new ProcessStepDefinition {
                Id = stepIdMap[step.Id],
                ProcessDefinitionVersionId = nextDraft.Id,
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
                DecisionRoleRequirementId = decisionRoleRequirementId,
                CanvasX = step.CanvasX,
                CanvasY = step.CanvasY,
                BranchCanvasX = step.BranchCanvasX,
                BranchCanvasY = step.BranchCanvasY
            };

            clonedStepsById[clonedStep.Id] = clonedStep;
            await dbContext.Set<ProcessStepDefinition>().AddAsync(clonedStep, cancellationToken);
        }

        foreach (var branchOutcome in source.BranchOutcomes.OrderBy(item => item.DisplayOrder)) {
            if (!stepIdMap.TryGetValue(branchOutcome.StepDefinitionId, out var nextStepId)) {
                throw new InvalidOperationException(
                    $"Branch outcome '{branchOutcome.Id:D}' references missing step '{branchOutcome.StepDefinitionId:D}' during draft clone.");
            }

            await dbContext.Set<ProcessStepBranchOutcomeDefinition>().AddAsync(
                new ProcessStepBranchOutcomeDefinition {
                    Id = branchOutcomeIdMap[branchOutcome.Id],
                    StepDefinitionId = nextStepId,
                    Key = branchOutcome.Key,
                    Title = branchOutcome.Title,
                    Description = branchOutcome.Description,
                    DisplayOrder = branchOutcome.DisplayOrder
                },
                cancellationToken);
        }

        foreach (var stepRoleRequirement in source.StepRoleRequirements) {
            if (!stepIdMap.TryGetValue(stepRoleRequirement.StepDefinitionId, out var nextStepId)) {
                throw new InvalidOperationException(
                    $"Step role assignment '{stepRoleRequirement.Id:D}' references missing step '{stepRoleRequirement.StepDefinitionId:D}' during draft clone.");
            }

            if (!roleIdMap.TryGetValue(stepRoleRequirement.RoleRequirementId, out var nextRoleId)) {
                throw new InvalidOperationException(
                    $"Step role assignment '{stepRoleRequirement.Id:D}' references missing role '{stepRoleRequirement.RoleRequirementId:D}' during draft clone.");
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

        foreach (var step in source.Steps.OrderBy(item => item.OrderIndex)) {
            var canonicalDependencies = ProcessDependencyCompatibilityBridge.GetCanonicalPersistedDependencies(step, sourceDependenciesByStepId);
            var nextStepId = stepIdMap[step.Id];
            var clonedDependencies = new List<ProcessStepDependencyDefinition>(canonicalDependencies.Count);

            foreach (var dependency in canonicalDependencies.OrderBy(item => item.DisplayOrder)) {
                if (!stepIdMap.TryGetValue(dependency.DependsOnStepId, out var nextDependsOnStepId)) {
                    throw new InvalidOperationException(
                        $"Step dependency '{dependency.Id:D}' references missing upstream step '{dependency.DependsOnStepId:D}' during draft clone.");
                }

                Guid? nextDependsOnBranchOutcomeId = null;
                if (dependency.DependsOnBranchOutcomeId.HasValue) {
                    if (!branchOutcomeIdMap.TryGetValue(dependency.DependsOnBranchOutcomeId.Value, out var mappedOutcomeId)) {
                        throw new InvalidOperationException(
                            $"Step dependency '{dependency.Id:D}' references missing branch outcome '{dependency.DependsOnBranchOutcomeId.Value:D}' during draft clone.");
                    }

                    nextDependsOnBranchOutcomeId = mappedOutcomeId;
                }

                var clonedDependency = new ProcessStepDependencyDefinition {
                    StepDefinitionId = nextStepId,
                    DependsOnStepId = nextDependsOnStepId,
                    DependsOnBranchOutcomeId = nextDependsOnBranchOutcomeId,
                    DisplayOrder = clonedDependencies.Count
                };

                clonedDependencies.Add(clonedDependency);
                await dbContext.Set<ProcessStepDependencyDefinition>().AddAsync(clonedDependency, cancellationToken);
            }

            clonedDependenciesByStepId[nextStepId] = clonedDependencies;
        }

        foreach (var clonedStep in clonedStepsById.Values) {
            clonedDependenciesByStepId.TryGetValue(clonedStep.Id, out var clonedDependencies);
            ProcessDependencyCompatibilityBridge.SyncLegacyPersistedPrimaryDependency(clonedStep, clonedDependencies ?? []);
        }

        foreach (var artifactExpectation in source.ArtifactExpectations) {
            if (!stepIdMap.TryGetValue(artifactExpectation.StepDefinitionId, out var nextStepId)) {
                throw new InvalidOperationException(
                    $"Artifact expectation '{artifactExpectation.Id:D}' references missing step '{artifactExpectation.StepDefinitionId:D}' during draft clone.");
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

        foreach (var artifactInput in source.ArtifactInputs.OrderBy(item => item.DisplayOrder)) {
            if (!stepIdMap.TryGetValue(artifactInput.StepDefinitionId, out var nextStepId)) {
                throw new InvalidOperationException(
                    $"Artifact input '{artifactInput.Id:D}' references missing step '{artifactInput.StepDefinitionId:D}' during draft clone.");
            }

            if (!artifactExpectationIdMap.TryGetValue(artifactInput.ArtifactExpectationId, out var nextArtifactExpectationId)) {
                throw new InvalidOperationException(
                    $"Artifact input '{artifactInput.Id:D}' references missing artifact expectation '{artifactInput.ArtifactExpectationId:D}' during draft clone.");
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

internal sealed record ProcessDefinitionDraftCloneSource(
    IReadOnlyList<ProcessRoleRequirement> Roles,
    IReadOnlyList<ProcessRoleSkillRequirement> RoleSkills,
    IReadOnlyList<ProcessStepDefinition> Steps,
    IReadOnlyList<ProcessStepRoleAssignmentRequirement> StepRoleRequirements,
    IReadOnlyList<ProcessStepBranchOutcomeDefinition> BranchOutcomes,
    IReadOnlyList<ProcessStepDependencyDefinition> StepDependencies,
    IReadOnlyList<ProcessArtifactExpectation> ArtifactExpectations,
    IReadOnlyList<ProcessStepArtifactInputDefinition> ArtifactInputs);
