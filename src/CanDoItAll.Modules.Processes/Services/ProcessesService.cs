using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.Collaboration;
using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.Processes;

public sealed partial class ProcessesService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    IClock clock,
    ProcessOutboxService processOutboxService,
    IProcessDefinitionListQueryService definitionListQueryService,
    IProcessRuntimeReadQueryService runtimeReadQueryService,
    IProjectPartyIntegrationBridge projectPartyIntegrationBridge,
    IProcessExecutorRegistryBridge executorRegistryBridge,
    IProcessProjectStructureBridge projectStructureBridge,
    IWorkflowCatalogService workflowCatalogService,
    IAgentFrameworkWorkspaceService agentWorkspaceService,
    HrService hrService,
    AiAgentService aiAgentService,
    CollaborationService collaborationService,
    IProcessObservationInvalidator processObservationInvalidator,
    ILogger<ProcessesService> logger)
{
    private const string DefaultActor = "process-management";

    public async Task<IReadOnlyList<ProcessDefinitionListItem>> ListDefinitionsAsync(
        Guid? projectId = null,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await definitionListQueryService.ListAsync(dbContext, projectId, cancellationToken);
    }

    public async Task<ProcessDefinitionEditorModel> GetEditorAsync(
        Guid? definitionId,
        Guid? projectId = null,
        CancellationToken cancellationToken = default) {
        if (!definitionId.HasValue) {
            return new ProcessDefinitionEditorModel {
                ProjectId = projectId
            };
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var definition = await dbContext.Set<ProcessDefinition>()
            .SingleOrDefaultAsync(item => item.Id == definitionId.Value, cancellationToken);
        if (definition is null) {
            return new ProcessDefinitionEditorModel {
                ProjectId = projectId
            };
        }

        var workingVersion = await GetWorkingVersionAsync(dbContext, definition.Id, cancellationToken);
        if (workingVersion is null) {
            return new ProcessDefinitionEditorModel {
                Id = definition.Id,
                ProjectId = definition.ProjectId,
                DefinitionConcurrencyToken = definition.ConcurrencyToken,
                Name = definition.Name,
                Summary = definition.Summary,
                ValueStatement = definition.ValueStatement,
                CustomerName = definition.CustomerName,
                OwnerName = definition.OwnerName,
                InterfaceContractSummary = definition.InterfaceContractSummary,
                GovernanceNotes = definition.GovernanceNotes,
                Criticality = definition.Criticality,
                AutonomyLevel = definition.AutonomyLevel,
                Status = definition.Status
            };
        }

        var roles = await dbContext.Set<ProcessRoleRequirement>()
            .Where(item => item.ProcessDefinitionVersionId == workingVersion.Id)
            .OrderBy(item => item.DisplayOrder)
            .ToListAsync(cancellationToken);
        var messagingPolicies = await dbContext.Set<ProcessRoleMessagingPolicyDefinition>()
            .Where(item => item.ProcessDefinitionVersionId == workingVersion.Id)
            .OrderBy(item => item.DisplayOrder)
            .ToListAsync(cancellationToken);
        var roleSkills = await dbContext.Set<ProcessRoleSkillRequirement>()
            .Where(item => roles.Select(role => role.Id).Contains(item.RoleRequirementId))
            .ToListAsync(cancellationToken);
        var steps = await dbContext.Set<ProcessStepDefinition>()
            .Where(item => item.ProcessDefinitionVersionId == workingVersion.Id)
            .OrderBy(item => item.OrderIndex)
            .ToListAsync(cancellationToken);
        var stepRoleRequirements = await dbContext.Set<ProcessStepRoleAssignmentRequirement>()
            .Where(item => steps.Select(step => step.Id).Contains(item.StepDefinitionId))
            .ToListAsync(cancellationToken);
        var artifactExpectations = await dbContext.Set<ProcessArtifactExpectation>()
            .Where(item => steps.Select(step => step.Id).Contains(item.StepDefinitionId))
            .ToListAsync(cancellationToken);
        var artifactInputs = await dbContext.Set<ProcessStepArtifactInputDefinition>()
            .Where(item => steps.Select(step => step.Id).Contains(item.StepDefinitionId))
            .OrderBy(item => item.DisplayOrder)
            .ToListAsync(cancellationToken);
        var branchOutcomes = await dbContext.Set<ProcessStepBranchOutcomeDefinition>()
            .Where(item => steps.Select(step => step.Id).Contains(item.StepDefinitionId))
            .ToListAsync(cancellationToken);
        var stepDependencies = await dbContext.Set<ProcessStepDependencyDefinition>()
            .Where(item => steps.Select(step => step.Id).Contains(item.StepDefinitionId))
            .OrderBy(item => item.DisplayOrder)
            .ToListAsync(cancellationToken);

        return new ProcessDefinitionEditorModel {
            Id = definition.Id,
            ProjectId = definition.ProjectId,
            WorkingVersionId = workingVersion.Id,
            DefinitionConcurrencyToken = definition.ConcurrencyToken,
            WorkingVersionConcurrencyToken = workingVersion.ConcurrencyToken,
            WorkingVersionNumber = workingVersion.VersionNumber,
            Name = definition.Name,
            Summary = definition.Summary,
            ValueStatement = definition.ValueStatement,
            CustomerName = definition.CustomerName,
            OwnerName = definition.OwnerName,
            InterfaceContractSummary = definition.InterfaceContractSummary,
            GovernanceNotes = definition.GovernanceNotes,
            ChangeSummary = workingVersion.ChangeSummary,
            GovernancePolicySummary = workingVersion.GovernancePolicySummary,
            ConstitutionRuleSummary = workingVersion.ConstitutionRuleSummary,
            OperatingModeSummary = workingVersion.OperatingModeSummary,
            SimulationReadinessSummary = workingVersion.SimulationReadinessSummary,
            ManagerAgentOverrideId = workingVersion.ManagerAgentOverrideId,
            ManagerAgentOverrideName = workingVersion.ManagerAgentOverrideName,
            Criticality = definition.Criticality,
            AutonomyLevel = definition.AutonomyLevel,
            Status = definition.Status,
            Roles = roles.Select(role => new ProcessRoleEditorModel {
                Id = role.Id,
                Key = role.Key,
                DisplayName = role.DisplayName,
                Purpose = role.Purpose,
                StaffingIntent = role.StaffingIntent,
                PreferredExecutorKind = role.PreferredExecutorKind,
                PreferredWorkflowDefinitionId = role.PreferredWorkflowDefinitionId,
                PreferredWorkflowVersionId = role.PreferredWorkflowVersionId,
                PreferredProjectAssignmentRole = role.PreferredProjectAssignmentRole,
                IsRequired = role.IsRequired,
                AllowsFallback = role.AllowsFallback,
                RequiresExplicitApproval = role.RequiresExplicitApproval,
                DefaultAllocationPercent = role.DefaultAllocationPercent,
                RoleTemplateSourceKey = role.RoleTemplateSourceKey,
                RoleTemplateSnapshotName = role.RoleTemplateSnapshotName,
                SnapshotSummary = role.SnapshotSummary,
                CanvasX = role.CanvasX,
                CanvasY = role.CanvasY,
                RequiredSkillIds = roleSkills
                    .Where(item => item.RoleRequirementId == role.Id)
                    .Select(item => item.SkillId)
                    .ToList()
            }).ToList(),
            MessagingPolicies = messagingPolicies.Select(item => new ProcessRoleMessagingPolicyEditorModel {
                Id = item.Id,
                SourceRoleRequirementId = item.SourceRoleRequirementId,
                TargetRoleRequirementId = item.TargetRoleRequirementId
            }).ToList(),
            Steps = steps.Select(step => {
                var editorStep = new ProcessStepEditorModel {
                    Id = step.Id,
                    Key = step.Key,
                    Title = step.Title,
                    Subtitle = step.Subtitle,
                    Notes = step.Notes,
                    StepKind = step.StepKind,
                    SubprocessDefinitionId = step.SubprocessDefinitionId,
                    SubprocessDefinitionSnapshotName = step.SubprocessDefinitionSnapshotName,
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
                    DecisionRoleRequirementId = step.DecisionRoleRequirementId,
                    CanvasX = step.CanvasX,
                    CanvasY = step.CanvasY,
                    BranchCanvasX = step.BranchCanvasX,
                    BranchCanvasY = step.BranchCanvasY,
                    BranchOutcomes = branchOutcomes
                        .Where(item => item.StepDefinitionId == step.Id)
                        .OrderBy(item => item.DisplayOrder)
                        .Select(item => new ProcessStepBranchOutcomeEditorModel {
                            Id = item.Id,
                            Key = item.Key,
                            Title = item.Title,
                            Description = item.Description
                        })
                        .ToList(),
                    RoleAssignments = stepRoleRequirements
                        .Where(item => item.StepDefinitionId == step.Id)
                        .OrderBy(item => item.FallbackOrder)
                        .ThenBy(item => item.ResponsibilityKind)
                        .Select(item => new ProcessStepRoleRequirementEditorModel {
                            Id = item.Id,
                            RoleRequirementId = item.RoleRequirementId,
                            ResponsibilityKind = item.ResponsibilityKind,
                            IsRequired = item.IsRequired,
                            FallbackOrder = item.FallbackOrder,
                            RebindPolicySummary = item.RebindPolicySummary
                        })
                        .ToList(),
                    ArtifactExpectations = artifactExpectations
                        .Where(item => item.StepDefinitionId == step.Id)
                        .Select(item => new ProcessArtifactExpectationEditorModel {
                            Id = item.Id,
                            ArtifactKind = item.ArtifactKind,
                            Title = item.Title,
                            IsRequired = item.IsRequired,
                            TrustRequirement = item.TrustRequirement,
                            SensitivityLevel = item.SensitivityLevel,
                            RetentionDays = item.RetentionDays,
                            AllowedFutureUsageSummary = item.AllowedFutureUsageSummary,
                            ValidationRequirementSummary = item.ValidationRequirementSummary
                        })
                        .ToList(),
                    ArtifactInputs = BuildEditorArtifactInputs(step, artifactInputs)
                };
                ProcessStepDependencyCollection.SetEditorDependencies(editorStep, BuildEditorDependencies(step, stepDependencies));
                return editorStep;
            }).ToList()
        };
    }
}
