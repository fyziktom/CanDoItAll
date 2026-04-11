using System.Text.Json;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Infrastructure.Search;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Processes;

public sealed partial class ProcessesService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    IClock clock,
    IActivityStream activityStream,
    ISearchIndexService searchIndexService,
    IProjectPartyIntegrationBridge projectPartyIntegrationBridge,
    IProcessExecutorRegistryBridge executorRegistryBridge) {
    private const string DefaultActor = "process-management";

    public async Task<IReadOnlyList<ProcessDefinitionListItem>> ListDefinitionsAsync(
        Guid? projectId = null,
        CancellationToken cancellationToken = default) {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var definitionsQuery = dbContext.Set<ProcessDefinition>().AsQueryable();
        if (projectId.HasValue) {
            definitionsQuery = definitionsQuery.Where(definition => definition.ProjectId == projectId.Value);
        }

        var definitions = await definitionsQuery.ToListAsync(cancellationToken);
        var versions = await dbContext.Set<ProcessDefinitionVersion>().ToListAsync(cancellationToken);
        var roles = await dbContext.Set<ProcessRoleRequirement>().ToListAsync(cancellationToken);
        var steps = await dbContext.Set<ProcessStepDefinition>().ToListAsync(cancellationToken);
        var runs = await dbContext.Set<ProcessRun>().ToListAsync(cancellationToken);
        var assignments = await dbContext.Set<ProcessRunAssignment>().ToListAsync(cancellationToken);
        var projectNames = await LoadProjectNamesAsync(dbContext, cancellationToken);
        var versionsByDefinitionId = versions
            .GroupBy(version => version.ProcessDefinitionId)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<ProcessDefinitionVersion>)group.ToList());
        var roleIdsByVersionId = roles
            .GroupBy(role => role.ProcessDefinitionVersionId)
            .ToDictionary(group => group.Key, group => group.Select(role => role.Id).ToHashSet());
        var stepCountByVersionId = steps
            .GroupBy(step => step.ProcessDefinitionVersionId)
            .ToDictionary(group => group.Key, group => group.Count());

        return definitions
            .OrderByDescending(definition => definition.UpdatedAtUtc)
            .Select(definition => {
                var definitionVersions = versionsByDefinitionId.GetValueOrDefault(definition.Id) ?? Array.Empty<ProcessDefinitionVersion>();
                var summaryVersion = ResolveDefinitionSummaryVersion(definitionVersions);
                var summaryVersionId = summaryVersion?.Id;
                var roleIds = summaryVersionId.HasValue && roleIdsByVersionId.TryGetValue(summaryVersionId.Value, out var summaryRoleIds)
                    ? summaryRoleIds
                    : new HashSet<Guid>();
                var definitionRuns = runs.Where(run => run.ProcessDefinitionId == definition.Id).ToList();
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
                    definitionRuns.Count(run => run.Status == ProcessRunStatus.Active || run.Status == ProcessRunStatus.Blocked),
                    assignments.Count(assignment =>
                        definitionRuns.Any(run => run.Id == assignment.ProcessRunId) &&
                        roleIds.Contains(assignment.RoleRequirementId) &&
                        assignment.IsCapabilityGap),
                    definition.Summary,
                    definition.ValueStatement,
                    definition.ProjectId.HasValue ? projectNames.GetValueOrDefault(definition.ProjectId.Value) ?? string.Empty : string.Empty,
                    definition.UpdatedAtUtc);
            })
            .ToList();
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
            Steps = steps.Select(step => new ProcessStepEditorModel {
                Id = step.Id,
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
                DependsOnStepId = step.DependsOnStepId,
                DependsOnBranchOutcomeId = step.DependsOnBranchOutcomeId,
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
                Dependencies = BuildEditorDependencies(step, stepDependencies),
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
            }).ToList()
        };
    }

    public async Task<Result<Guid>> SaveAsync(
        ProcessDefinitionEditorModel model,
        CancellationToken cancellationToken = default) {
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
        var definition = await dbContext.Set<ProcessDefinition>()
            .SingleOrDefaultAsync(item => item.Id == definitionId, cancellationToken);
        if (definition is null) {
            return;
        }

        var versions = await dbContext.Set<ProcessDefinitionVersion>()
            .Where(item => item.ProcessDefinitionId == definitionId)
            .ToListAsync(cancellationToken);
        var versionIds = versions.Select(item => item.Id).ToHashSet();
        var roles = await dbContext.Set<ProcessRoleRequirement>()
            .Where(item => versionIds.Contains(item.ProcessDefinitionVersionId))
            .ToListAsync(cancellationToken);
        var roleIds = roles.Select(item => item.Id).ToHashSet();
        var steps = await dbContext.Set<ProcessStepDefinition>()
            .Where(item => versionIds.Contains(item.ProcessDefinitionVersionId))
            .ToListAsync(cancellationToken);
        var stepIds = steps.Select(item => item.Id).ToHashSet();
        var runs = await dbContext.Set<ProcessRun>()
            .Where(item => item.ProcessDefinitionId == definitionId)
            .ToListAsync(cancellationToken);
        var runIds = runs.Select(item => item.Id).ToHashSet();

        dbContext.RemoveRange(await dbContext.Set<ProcessRoleSkillRequirement>().Where(item => roleIds.Contains(item.RoleRequirementId)).ToListAsync(cancellationToken));
        dbContext.RemoveRange(await dbContext.Set<ProcessStepDependencyDefinition>().Where(item => stepIds.Contains(item.StepDefinitionId)).ToListAsync(cancellationToken));
        dbContext.RemoveRange(await dbContext.Set<ProcessStepRoleAssignmentRequirement>().Where(item => stepIds.Contains(item.StepDefinitionId)).ToListAsync(cancellationToken));
        dbContext.RemoveRange(await dbContext.Set<ProcessArtifactExpectation>().Where(item => stepIds.Contains(item.StepDefinitionId)).ToListAsync(cancellationToken));
        dbContext.RemoveRange(await dbContext.Set<ProcessStepArtifactInputDefinition>().Where(item => stepIds.Contains(item.StepDefinitionId)).ToListAsync(cancellationToken));
        dbContext.RemoveRange(await dbContext.Set<ProcessStepRun>().Where(item => runIds.Contains(item.ProcessRunId)).ToListAsync(cancellationToken));
        dbContext.RemoveRange(await dbContext.Set<ProcessRunAssignment>().Where(item => runIds.Contains(item.ProcessRunId)).ToListAsync(cancellationToken));
        dbContext.RemoveRange(await dbContext.Set<ProcessWorkBrief>().Where(item => runIds.Contains(item.ProcessRunId)).ToListAsync(cancellationToken));
        dbContext.RemoveRange(await dbContext.Set<ProcessDecisionRecord>().Where(item => runIds.Contains(item.ProcessRunId)).ToListAsync(cancellationToken));
        dbContext.RemoveRange(await dbContext.Set<ProcessArtifactRecord>().Where(item => runIds.Contains(item.ProcessRunId)).ToListAsync(cancellationToken));
        dbContext.RemoveRange(await dbContext.Set<ProcessJournalEntry>().Where(item => runIds.Contains(item.ProcessRunId)).ToListAsync(cancellationToken));
        dbContext.RemoveRange(await dbContext.Set<ProcessConformanceObservation>().Where(item => runIds.Contains(item.ProcessRunId)).ToListAsync(cancellationToken));
        dbContext.RemoveRange(await dbContext.Set<ProcessImprovementCandidate>().Where(item => item.ProcessDefinitionId == definitionId).ToListAsync(cancellationToken));
        dbContext.RemoveRange(runs);
        dbContext.RemoveRange(roles);
        dbContext.RemoveRange(steps);
        dbContext.RemoveRange(versions);
        dbContext.Remove(definition);
        await dbContext.SaveChangesAsync(cancellationToken);
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
                .Select(item => new ProcessStepArtifactInputEditorModel
                {
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

    private static Error? ValidateDefinitionEditor(ProcessDefinitionEditorModel model) {
        ProcessCanvasBranching.NormalizeDefinitionEditor(model);

        if (string.IsNullOrWhiteSpace(model.Name)) {
            return Error.Validation("Process name is required.", "processes.name-required");
        }

        if (string.IsNullOrWhiteSpace(model.ValueStatement)) {
            return Error.Validation("Value statement is required.", "processes.value-statement-required");
        }

        if (string.IsNullOrWhiteSpace(model.OwnerName)) {
            return Error.Validation("Owner name is required.", "processes.owner-required");
        }

        if (model.Roles.Count == 0) {
            return Error.Validation("At least one role is required.", "processes.role-required");
        }

        if (model.Steps.Count == 0) {
            return Error.Validation("At least one step is required.", "processes.step-required");
        }

        if (model.Steps.Any(step => string.IsNullOrWhiteSpace(step.Title))) {
            return Error.Validation("Every step requires a title.", "processes.step-title-required");
        }

        var stepsById = model.Steps
            .Where(step => step.Id.HasValue)
            .ToDictionary(step => step.Id!.Value);
        foreach (var step in model.Steps) {
            if (step.BranchOutcomes.Any(outcome => string.IsNullOrWhiteSpace(outcome.Title))) {
                return Error.Validation("Every branch outcome requires a title.", "processes.branch-outcome-title-required");
            }

            if (step.BranchOutcomes.Count > 0 && !step.DecisionRoleRequirementId.HasValue) {
                return Error.Validation("Branching steps require an explicit decision-maker role.", "processes.branch-decision-role-required");
            }

            if (step.DecisionRoleRequirementId.HasValue &&
                model.Roles.All(role => role.Id != step.DecisionRoleRequirementId.Value)) {
                return Error.Validation("Decision-maker role must reference a process role in the same definition.", "processes.branch-decision-role-invalid");
            }

            foreach (var dependency in ProcessCanvasBranching.GetOrderedDependencies(step)) {
                if (!dependency.DependsOnStepId.HasValue) {
                    return Error.Validation("Every dependency must resolve to an upstream step.", "processes.branch-dependency-step-required");
                }

                if (!stepsById.TryGetValue(dependency.DependsOnStepId.Value, out var dependencyStep)) {
                    return Error.Validation("Dependencies must reference a step in the same definition.", "processes.branch-dependency-step-invalid");
                }

                if (!dependency.DependsOnBranchOutcomeId.HasValue) {
                    continue;
                }

                if (dependencyStep.BranchOutcomes.All(outcome => outcome.Id != dependency.DependsOnBranchOutcomeId.Value)) {
                    return Error.Validation("Dependency outcome must belong to the selected dependency step.", "processes.branch-dependency-outcome-invalid");
                }
            }
        }

        return ValidateArtifactInputs(model);
    }

    private static Error? ValidatePublish(
        ProcessDefinition definition,
        ProcessDefinitionVersion version,
        IReadOnlyList<ProcessRoleRequirement> roles,
        IReadOnlyList<ProcessStepDefinition> steps,
        IReadOnlyList<ProcessStepRoleAssignmentRequirement> stepRoleRequirements,
        IReadOnlyList<ProcessStepBranchOutcomeDefinition> branchOutcomes,
        IReadOnlyList<ProcessStepDependencyDefinition> stepDependencies,
        IReadOnlyList<ProcessArtifactExpectation> artifactExpectations,
        IReadOnlyList<ProcessStepArtifactInputDefinition> artifactInputs) {
        if (string.IsNullOrWhiteSpace(definition.OwnerName) ||
            string.IsNullOrWhiteSpace(definition.CustomerName) ||
            string.IsNullOrWhiteSpace(definition.ValueStatement) ||
            string.IsNullOrWhiteSpace(version.GovernancePolicySummary)) {
            return Error.Validation(
                "Publishing requires owner, customer, value statement, and governance policy summary.",
                "processes.publish-governance-required");
        }

        if (roles.Count == 0 || steps.Count == 0) {
            return Error.Validation("Publishing requires at least one role and one step.", "processes.publish-shape-required");
        }

        if (steps.Any(step => !stepRoleRequirements.Any(requirement => requirement.StepDefinitionId == step.Id))) {
            return Error.Validation("Every step must have at least one explicit role requirement before publication.", "processes.publish-step-role-required");
        }

        var branchOutcomesByStepId = branchOutcomes
            .GroupBy(item => item.StepDefinitionId)
            .ToDictionary(group => group.Key, group => group.OrderBy(item => item.DisplayOrder).ToList());
        var stepDependenciesByStepId = stepDependencies
            .GroupBy(item => item.StepDefinitionId)
            .ToDictionary(group => group.Key, group => group.OrderBy(item => item.DisplayOrder).ToList());

        var branchingError = ValidatePublishBranching(definition, roles, steps, branchOutcomesByStepId, stepDependenciesByStepId);
        if (branchingError is not null) {
            return branchingError;
        }

        return ValidatePublishedArtifactInputs(steps, artifactExpectations, artifactInputs, stepDependenciesByStepId);
    }

    private static Error? ValidatePublishBranching(
        ProcessDefinition definition,
        IReadOnlyList<ProcessRoleRequirement> roles,
        IReadOnlyList<ProcessStepDefinition> steps,
        IReadOnlyDictionary<Guid, List<ProcessStepBranchOutcomeDefinition>> branchOutcomesByStepId,
        IReadOnlyDictionary<Guid, List<ProcessStepDependencyDefinition>> stepDependenciesByStepId) {
        var stepsById = steps.ToDictionary(step => step.Id);

        foreach (var step in steps) {
            branchOutcomesByStepId.TryGetValue(step.Id, out var stepBranchOutcomes);
            stepBranchOutcomes ??= [];

            if (stepBranchOutcomes.Count > 0 && !step.DecisionRoleRequirementId.HasValue) {
                return Error.Validation("Publishing requires a decision-maker role for branching steps.", "processes.publish-branch-decision-role-required");
            }

            if (step.DecisionRoleRequirementId.HasValue && roles.All(role => role.Id != step.DecisionRoleRequirementId.Value)) {
                return Error.Validation("Branch decision-maker roles must resolve to a published process role.", "processes.publish-branch-decision-role-invalid");
            }

            foreach (var dependency in GetPersistedDependencies(step, stepDependenciesByStepId)) {
                if (!stepsById.ContainsKey(dependency.DependsOnStepId)) {
                    return Error.Validation("Publishing requires each dependency to resolve to a published upstream step.", "processes.publish-branch-dependency-step-required");
                }

                if (!dependency.DependsOnBranchOutcomeId.HasValue) {
                    continue;
                }

                branchOutcomesByStepId.TryGetValue(dependency.DependsOnStepId, out var dependencyOutcomes);
                dependencyOutcomes ??= [];

                if (dependencyOutcomes.All(outcome => outcome.Id != dependency.DependsOnBranchOutcomeId.Value)) {
                    return Error.Validation("Dependency outcomes must belong to the selected dependency step before publication.", "processes.publish-branch-dependency-outcome-invalid");
                }
            }
        }

        foreach (var step in steps) {
            if (!branchOutcomesByStepId.TryGetValue(step.Id, out var stepBranchOutcomes) || stepBranchOutcomes.Count == 0) {
                continue;
            }

            foreach (var branchOutcome in stepBranchOutcomes) {
                if (ProcessCanvasBranching.IsSystemOutcome(branchOutcome)) {
                    continue;
                }

                var hasDependent = steps.Any(candidate =>
                    GetPersistedDependencies(candidate, stepDependenciesByStepId)
                        .Any(dependency => dependency.DependsOnStepId == step.Id &&
                            dependency.DependsOnBranchOutcomeId == branchOutcome.Id));
                if (!hasDependent) {
                    return Error.Validation(
                        $"Branch outcome '{branchOutcome.Title}' on process '{definition.Name}' is not routed to any downstream step.",
                        "processes.publish-branch-outcome-unused");
                }
            }
        }

        return null;
    }

    private static List<ProcessStepDependencyEditorModel> BuildEditorDependencies(
        ProcessStepDefinition step,
        IReadOnlyList<ProcessStepDependencyDefinition> allDependencies) {
        var dependencies = allDependencies
            .Where(item => item.StepDefinitionId == step.Id)
            .OrderBy(item => item.DisplayOrder)
            .Select(item => new ProcessStepDependencyEditorModel {
                Id = item.Id,
                DependsOnStepId = item.DependsOnStepId,
                DependsOnBranchOutcomeId = item.DependsOnBranchOutcomeId
            })
            .ToList();
        if (dependencies.Count > 0) {
            return dependencies;
        }

        if (!step.DependsOnStepId.HasValue) {
            return [];
        }

        return
        [
            new ProcessStepDependencyEditorModel {
                Id = Guid.NewGuid(),
                DependsOnStepId = step.DependsOnStepId,
                DependsOnBranchOutcomeId = step.DependsOnBranchOutcomeId
            }
        ];
    }

    private static IReadOnlyList<ProcessStepDependencyDefinition> GetPersistedDependencies(
        ProcessStepDefinition step,
        IReadOnlyDictionary<Guid, List<ProcessStepDependencyDefinition>> dependenciesByStepId) {
        if (dependenciesByStepId.TryGetValue(step.Id, out var dependencies) && dependencies.Count > 0) {
            return dependencies;
        }

        if (!step.DependsOnStepId.HasValue) {
            return [];
        }

        return
        [
            new ProcessStepDependencyDefinition {
                StepDefinitionId = step.Id,
                DependsOnStepId = step.DependsOnStepId.Value,
                DependsOnBranchOutcomeId = step.DependsOnBranchOutcomeId
            }
        ];
    }

    private async Task<int> GetNextVersionNumberAsync(AppDbContext dbContext, Guid definitionId, CancellationToken cancellationToken) {
        var existingVersionNumber = await dbContext.Set<ProcessDefinitionVersion>()
            .Where(item => item.ProcessDefinitionId == definitionId)
            .Select(item => (int?)item.VersionNumber)
            .MaxAsync(cancellationToken);
        return (existingVersionNumber ?? 0) + 1;
    }

    private static ProcessRunStatus ResolveRunStatus(IReadOnlyList<ProcessStepRun> persistedStepRuns, ProcessStepRun currentStepRun) {
        var stepRuns = persistedStepRuns
            .Where(item => item.Id != currentStepRun.Id)
            .Append(currentStepRun)
            .ToList();
        if (stepRuns.All(item => item.Status == ProcessStepRunStatus.Completed || item.Status == ProcessStepRunStatus.Skipped)) {
            return ProcessRunStatus.Completed;
        }

        if (stepRuns.Any(item => item.Status == ProcessStepRunStatus.Failed)) {
            return ProcessRunStatus.Failed;
        }

        if (stepRuns.Any(item => item.Status == ProcessStepRunStatus.Blocked)) {
            return ProcessRunStatus.Blocked;
        }

        return ProcessRunStatus.Active;
    }

    private static bool IsTransitionAllowed(ProcessStepRunStatus currentStatus, ProcessStepRunStatus targetStatus)
    {
        return ProcessStepRunTransitions.IsAllowed(currentStatus, targetStatus);
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

    private static string BuildWorkBrief(ProcessDefinition definition, ProcessStepDefinition step, string? executorName) {
        return $"{definition.Name}: {step.Title}{Environment.NewLine}" +
            $"Customer value: {definition.ValueStatement}{Environment.NewLine}" +
            $"Owner: {definition.OwnerName}{Environment.NewLine}" +
            $"Executor: {(string.IsNullOrWhiteSpace(executorName) ? "Unassigned" : executorName)}{Environment.NewLine}" +
            $"Inputs: {step.InputContractSummary}{Environment.NewLine}" +
            $"Outputs: {step.OutputContractSummary}{Environment.NewLine}" +
            $"Evidence: {step.EvidenceContractSummary}";
    }

    private static string BuildRunRoute(ProcessRun run) {
        return run.ProjectId.HasValue
            ? $"/projects/{run.ProjectId.Value:D}/processes?runId={run.Id:D}"
            : $"/processes?runId={run.Id:D}";
    }

    private ProcessJournalEntry BuildJournalEntry(
        Guid runId,
        Guid? stepRunId,
        string eventType,
        string title,
        string description,
        ProcessOperatingMode operatingMode,
        string policyVersion,
        string replaySummary) {
        return new ProcessJournalEntry {
            ProcessRunId = runId,
            StepRunId = stepRunId,
            EventType = eventType,
            Title = title,
            Description = description,
            CorrelationId = Guid.NewGuid().ToString("N"),
            OperatingMode = operatingMode,
            PolicyVersion = policyVersion,
            EnvironmentMode = operatingMode.ToString(),
            ReplayContextJson = JsonSerializer.Serialize(new {
                RunId = runId,
                StepRunId = stepRunId,
                Summary = replaySummary
            }),
            OccurredAtUtc = clock.GetUtcNow()
        };
    }

    private async Task MaybeCreateImprovementCandidateAsync(
        AppDbContext dbContext,
        ProcessRun run,
        ProcessStepRun stepRun,
        ProcessStepTransitionRequest request,
        CancellationToken cancellationToken) {
        await dbContext.Set<ProcessImprovementCandidate>().AddAsync(
            new ProcessImprovementCandidate {
                ProcessDefinitionId = run.ProcessDefinitionId,
                ProcessRunId = run.Id,
                Title = request.TargetStatus switch {
                    ProcessStepRunStatus.Refused => $"Review refusal path in {stepRun.Title}",
                    ProcessStepRunStatus.Blocked => $"Reduce blocking in {stepRun.Title}",
                    ProcessStepRunStatus.Failed => $"Stabilize failure-prone step {stepRun.Title}",
                    _ => $"Improve {stepRun.Title}"
                },
                Category = request.TargetStatus.ToString(),
                ProblemSummary = request.Reason.Trim(),
                EvidenceSummary = $"{stepRun.Title} / {request.TargetStatus}",
                Status = ProcessImprovementStatus.Open,
                IsTrainingOpportunity = request.TargetStatus == ProcessStepRunStatus.Refused,
                RequiresGovernanceReview = true,
                CreatedAtUtc = clock.GetUtcNow()
            },
            cancellationToken);
    }

    private static async Task<IReadOnlyDictionary<Guid, string>> LoadProjectNamesAsync(
        AppDbContext dbContext,
        CancellationToken cancellationToken) {
        return await dbContext.Set<Project>()
            .ToDictionaryAsync(item => item.Id, item => item.Name, cancellationToken);
    }

    private static ProcessDefinitionVersion? ResolveDefinitionSummaryVersion(IReadOnlyList<ProcessDefinitionVersion> versions) {
        return versions
            .OrderBy(version => version.Status == ProcessVersionStatus.Draft ? 0 : 1)
            .ThenByDescending(version => version.VersionNumber)
            .FirstOrDefault();
    }
}

