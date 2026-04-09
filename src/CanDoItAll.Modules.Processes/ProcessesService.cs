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

        return definitions
            .OrderByDescending(definition => definition.UpdatedAtUtc)
            .Select(definition => {
                var definitionVersions = versions.Where(version => version.ProcessDefinitionId == definition.Id).ToList();
                var latestVersionNumber = definitionVersions.Count == 0 ? 0 : definitionVersions.Max(version => version.VersionNumber);
                var activeVersionIds = definitionVersions.Select(version => version.Id).ToHashSet();
                var roleIds = roles
                    .Where(role => activeVersionIds.Contains(role.ProcessDefinitionVersionId))
                    .Select(role => role.Id)
                    .ToHashSet();
                var definitionRuns = runs.Where(run => run.ProcessDefinitionId == definition.Id).ToList();
                return new ProcessDefinitionListItem(
                    definition.Id,
                    definition.ProjectId,
                    definition.Name,
                    definition.Status,
                    latestVersionNumber,
                    definition.ActivePublishedVersionId.HasValue,
                    roles.Count(role => activeVersionIds.Contains(role.ProcessDefinitionVersionId)),
                    steps.Count(step => activeVersionIds.Contains(step.ProcessDefinitionVersionId)),
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
                CanvasX = step.CanvasX,
                CanvasY = step.CanvasY,
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
                    .ToList()
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
        var publishError = ValidatePublish(definition, draftVersion, roles, steps, stepRoleRequirements);
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

        await ClonePublishedVersionIntoNextDraftAsync(dbContext, definitionId, draftVersion, roles, steps, stepRoleRequirements, cancellationToken);
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
        dbContext.RemoveRange(await dbContext.Set<ProcessStepRoleAssignmentRequirement>().Where(item => stepIds.Contains(item.StepDefinitionId)).ToListAsync(cancellationToken));
        dbContext.RemoveRange(await dbContext.Set<ProcessArtifactExpectation>().Where(item => stepIds.Contains(item.StepDefinitionId)).ToListAsync(cancellationToken));
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

    public async Task<IReadOnlyList<ProcessRunListItem>> ListRunsAsync(
        Guid? definitionId = null,
        Guid? projectId = null,
        CancellationToken cancellationToken = default) {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var runsQuery = dbContext.Set<ProcessRun>().AsQueryable();
        if (definitionId.HasValue) {
            runsQuery = runsQuery.Where(run => run.ProcessDefinitionId == definitionId.Value);
        }

        if (projectId.HasValue) {
            runsQuery = runsQuery.Where(run => run.ProjectId == projectId.Value);
        }

        var runs = await runsQuery.ToListAsync(cancellationToken);
        var stepRuns = await dbContext.Set<ProcessStepRun>()
            .Where(stepRun => runs.Select(run => run.Id).Contains(stepRun.ProcessRunId))
            .ToListAsync(cancellationToken);

        return runs
            .OrderByDescending(run => run.UpdatedAtUtc)
            .Select(run => {
                var runStepRuns = stepRuns.Where(stepRun => stepRun.ProcessRunId == run.Id).ToList();
                return new ProcessRunListItem(
                    run.Id,
                    run.ProcessDefinitionId,
                    run.ProcessDefinitionVersionId,
                    run.ProjectId,
                    run.Name,
                    run.Status,
                    run.OperatingMode,
                    runStepRuns.Count(stepRun => stepRun.Status == ProcessStepRunStatus.Completed),
                    runStepRuns.Count,
                    runStepRuns.Count(stepRun => stepRun.Status == ProcessStepRunStatus.Blocked),
                    runStepRuns.Count(stepRun => stepRun.CapabilityGapSeverity != ProcessCapabilityGapSeverity.None),
                    run.EstimatedCost,
                    run.ActualCost,
                    run.UpdatedAtUtc);
            })
            .ToList();
    }

    public async Task<IReadOnlyList<ProcessStepRunViewModel>> ListStepRunsAsync(Guid runId, CancellationToken cancellationToken = default) {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.Set<ProcessStepRun>()
            .Where(item => item.ProcessRunId == runId)
            .OrderBy(item => item.Sequence)
            .Select(item => new ProcessStepRunViewModel(
                item.Id,
                item.StepDefinitionId,
                item.Sequence,
                item.Title,
                item.StepKind,
                item.Status,
                item.CurrentExecutorName,
                item.DecisionSummary,
                item.BlockedReason,
                item.RefusalReason,
                item.WaitMinutes,
                item.TouchMinutes,
                item.BlockedMinutes,
                item.ReworkCount,
                item.CapabilityGapSeverity))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ProcessDecisionViewModel>> ListDecisionRecordsAsync(Guid runId, CancellationToken cancellationToken = default) {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var items = await dbContext.Set<ProcessDecisionRecord>()
            .Where(item => item.ProcessRunId == runId)
            .Select(item => new ProcessDecisionViewModel(
                item.Id,
                item.DecisionKind,
                item.Outcome,
                item.Title,
                item.Reason,
                item.DecidedBy,
                item.CreatedAtUtc))
            .ToListAsync(cancellationToken);
        return items
            .OrderByDescending(item => item.CreatedAtUtc)
            .ToList();
    }

    public async Task<IReadOnlyList<ProcessArtifactViewModel>> ListArtifactsAsync(Guid runId, CancellationToken cancellationToken = default) {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var items = await dbContext.Set<ProcessArtifactRecord>()
            .Where(item => item.ProcessRunId == runId)
            .Select(item => new ProcessArtifactViewModel(
                item.Id,
                item.ArtifactKind,
                item.Title,
                item.TrustStatus,
                item.SensitivityLevel,
                item.ProvenanceSummary,
                item.AllowedFutureUsageSummary,
                item.CreatedAtUtc))
            .ToListAsync(cancellationToken);
        return items
            .OrderByDescending(item => item.CreatedAtUtc)
            .ToList();
    }

    public async Task<IReadOnlyList<ProcessRunAssignmentViewModel>> ListAssignmentsAsync(Guid runId, CancellationToken cancellationToken = default) {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.Set<ProcessRunAssignment>()
            .Where(item => item.ProcessRunId == runId)
            .OrderBy(item => item.DisplayName)
            .Select(item => new ProcessRunAssignmentViewModel(
                item.Id,
                item.RoleRequirementId,
                item.StepDefinitionId,
                item.PartyId,
                item.DisplayName,
                item.ExecutorKind,
                item.BindingReason,
                item.SourceRegistryKey,
                item.SnapshotSummary,
                item.IsFallback,
                item.IsCapabilityGap))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ProcessWorkBriefViewModel>> ListWorkBriefsAsync(Guid runId, CancellationToken cancellationToken = default) {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var items = await dbContext.Set<ProcessWorkBrief>()
            .Where(item => item.ProcessRunId == runId)
            .Select(item => new ProcessWorkBriefViewModel(
                item.Id,
                item.StepRunId,
                item.Title,
                item.WorkBriefText,
                item.HandoffSummary,
                item.AssignmentReason,
                item.ExpectedOutcome,
                item.EvidenceExpectationSummary,
                item.CreatedAtUtc))
            .ToListAsync(cancellationToken);
        return items
            .OrderBy(item => item.CreatedAtUtc)
            .ToList();
    }

    public async Task<IReadOnlyList<ProcessConformanceObservationViewModel>> ListConformanceObservationsAsync(Guid runId, CancellationToken cancellationToken = default) {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var items = await dbContext.Set<ProcessConformanceObservation>()
            .Where(item => item.ProcessRunId == runId)
            .Select(item => new ProcessConformanceObservationViewModel(
                item.Id,
                item.StepRunId,
                item.Severity,
                item.Category,
                item.Observation,
                item.DeviationReason,
                item.IsSafeNonAction,
                item.ContainsSensitiveAssessment,
                item.CreatedAtUtc))
            .ToListAsync(cancellationToken);
        return items
            .OrderByDescending(item => item.CreatedAtUtc)
            .ToList();
    }

    public async Task<IReadOnlyList<ProcessImprovementViewModel>> ListImprovementsAsync(
        Guid? definitionId = null,
        CancellationToken cancellationToken = default) {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var query = dbContext.Set<ProcessImprovementCandidate>().AsQueryable();
        if (definitionId.HasValue) {
            query = query.Where(item => item.ProcessDefinitionId == definitionId.Value);
        }

        var items = await query
            .Select(item => new ProcessImprovementViewModel(
                item.Id,
                item.Title,
                item.Category,
                item.ProblemSummary,
                item.Status,
                item.IsTrainingOpportunity,
                item.RequiresGovernanceReview))
            .ToListAsync(cancellationToken);
        return items;
    }

    public async Task<ProcessAnalyticsSummary> GetAnalyticsAsync(
        Guid? definitionId = null,
        Guid? projectId = null,
        CancellationToken cancellationToken = default) {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var runsQuery = dbContext.Set<ProcessRun>().AsQueryable();
        if (definitionId.HasValue) {
            runsQuery = runsQuery.Where(run => run.ProcessDefinitionId == definitionId.Value);
        }

        if (projectId.HasValue) {
            runsQuery = runsQuery.Where(run => run.ProjectId == projectId.Value);
        }

        var runs = await runsQuery.ToListAsync(cancellationToken);
        var runIds = runs.Select(run => run.Id).ToHashSet();
        var stepRuns = await dbContext.Set<ProcessStepRun>()
            .Where(stepRun => runIds.Contains(stepRun.ProcessRunId))
            .ToListAsync(cancellationToken);
        var conformanceObservations = await dbContext.Set<ProcessConformanceObservation>()
            .Where(item => runIds.Contains(item.ProcessRunId))
            .ToListAsync(cancellationToken);
        var improvementCount = await dbContext.Set<ProcessImprovementCandidate>()
            .CountAsync(item => !definitionId.HasValue || item.ProcessDefinitionId == definitionId.Value, cancellationToken);

        return new ProcessAnalyticsSummary(
            runs.Count,
            runs.Count(run => run.Status == ProcessRunStatus.Active),
            runs.Count(run => run.Status == ProcessRunStatus.Completed),
            runs.Count(run => run.Status == ProcessRunStatus.Blocked),
            stepRuns.Count(stepRun => stepRun.CapabilityGapSeverity != ProcessCapabilityGapSeverity.None),
            improvementCount,
            conformanceObservations.Count,
            conformanceObservations.Count(item => item.IsSafeNonAction),
            Average(stepRuns.Select(item => item.WaitMinutes + item.TouchMinutes + item.BlockedMinutes)),
            Average(stepRuns.Select(item => item.WaitMinutes)),
            Average(stepRuns.Select(item => item.BlockedMinutes)),
            runs.Sum(run => run.EstimatedCost),
            runs.Sum(run => run.ActualCost));
    }

    public async Task<IReadOnlyList<ProjectPartyOption>> ListPartyOptionsAsync(Guid projectId, CancellationToken cancellationToken = default) {
        return await projectPartyIntegrationBridge.ListPartyOptionsAsync(projectId, cancellationToken);
    }

    public async Task<Result<Guid>> StartRunAsync(ProcessRunStartRequest request, CancellationToken cancellationToken = default) {
        if (request.ProcessDefinitionId == Guid.Empty) {
            return Result<Guid>.Failure(Error.Validation("Process definition is required.", "processes.run.definition-required"));
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var definition = await dbContext.Set<ProcessDefinition>()
            .SingleOrDefaultAsync(item => item.Id == request.ProcessDefinitionId, cancellationToken);
        if (definition is null || !definition.ActivePublishedVersionId.HasValue) {
            return Result<Guid>.Failure(Error.Validation("Publish a process definition before starting a run.", "processes.run.published-version-required"));
        }

        var publishedVersion = await dbContext.Set<ProcessDefinitionVersion>()
            .SingleAsync(item => item.Id == definition.ActivePublishedVersionId.Value, cancellationToken);
        var roles = await dbContext.Set<ProcessRoleRequirement>()
            .Where(item => item.ProcessDefinitionVersionId == publishedVersion.Id)
            .OrderBy(item => item.DisplayOrder)
            .ToListAsync(cancellationToken);
        var steps = await dbContext.Set<ProcessStepDefinition>()
            .Where(item => item.ProcessDefinitionVersionId == publishedVersion.Id)
            .OrderBy(item => item.OrderIndex)
            .ToListAsync(cancellationToken);
        var stepRoleRequirements = await dbContext.Set<ProcessStepRoleAssignmentRequirement>()
            .Where(item => steps.Select(step => step.Id).Contains(item.StepDefinitionId))
            .ToListAsync(cancellationToken);
        var artifactExpectations = await dbContext.Set<ProcessArtifactExpectation>()
            .Where(item => steps.Select(step => step.Id).Contains(item.StepDefinitionId))
            .ToListAsync(cancellationToken);

        var run = new ProcessRun {
            ProcessDefinitionId = definition.Id,
            ProcessDefinitionVersionId = publishedVersion.Id,
            ProjectId = request.ProjectId ?? definition.ProjectId,
            Name = string.IsNullOrWhiteSpace(request.RunName)
                ? $"{definition.Name} / {clock.GetUtcNow():yyyy-MM-dd HH:mm}"
                : request.RunName.Trim(),
            Status = ProcessRunStatus.Active,
            OperatingMode = request.OperatingMode,
            TriggerReason = request.TriggerReason.Trim(),
            GovernanceSnapshot = publishedVersion.GovernancePolicySummary,
            PolicySnapshot = publishedVersion.ConstitutionRuleSummary,
            ExecutorSnapshotSummary = publishedVersion.OperatingModeSummary,
            CreatedAtUtc = clock.GetUtcNow(),
            UpdatedAtUtc = clock.GetUtcNow(),
            StartedAtUtc = clock.GetUtcNow(),
            EstimatedCost = steps.Sum(step => step.TargetLeadHours) * 40m
        };
        await dbContext.Set<ProcessRun>().AddAsync(run, cancellationToken);

        var projectAssignments = run.ProjectId.HasValue
            ? await projectPartyIntegrationBridge.ListAssignmentsDetailedAsync(run.ProjectId.Value, cancellationToken)
            : [];
        var projectAssignmentLookup = projectAssignments
            .GroupBy(item => item.Role)
            .ToDictionary(group => group.Key, group => group.OrderByDescending(item => item.IsPrimary).ToList());

        var resolvedAssignments = new List<ProcessRunAssignment>();
        foreach (var role in roles) {
            projectAssignmentLookup.TryGetValue(role.PreferredProjectAssignmentRole ?? ProjectPartyAssignmentRole.TeamMember, out var candidates);
            var candidate = candidates?.FirstOrDefault();
            var assignment = new ProcessRunAssignment {
                ProcessRunId = run.Id,
                RoleRequirementId = role.Id,
                PartyId = candidate?.PartyId,
                DisplayName = candidate?.PartyDisplayName ?? "Unassigned role",
                ExecutorKind = candidate is not null ? candidate.PartyTypeLabel : role.PreferredExecutorKind,
                BindingReason = candidate is not null
                    ? $"Matched project portfolio role {candidate.Role}."
                    : "No eligible project assignment was pre-bound to this role.",
                SnapshotSummary = role.SnapshotSummary,
                IsFallback = false,
                IsCapabilityGap = candidate is null
            };
            resolvedAssignments.Add(assignment);
            await dbContext.Set<ProcessRunAssignment>().AddAsync(assignment, cancellationToken);

            await dbContext.Set<ProcessDecisionRecord>().AddAsync(
                new ProcessDecisionRecord {
                    ProcessRunId = run.Id,
                    DecisionKind = ProcessDecisionKind.Assignment,
                    Outcome = candidate is null ? ProcessDecisionOutcome.Escalated : ProcessDecisionOutcome.Accepted,
                    Title = $"Assignment for {role.DisplayName}",
                    Reason = assignment.BindingReason,
                    PolicyEvaluation = role.RequiresExplicitApproval
                        ? "Role requires explicit approval before irreversible work."
                        : "Role can proceed under standard governance.",
                    DecidedBy = DefaultActor,
                    OperatingMode = run.OperatingMode,
                    CreatedAtUtc = clock.GetUtcNow()
                },
                cancellationToken);
        }

        var unresolvedRoleIds = resolvedAssignments
            .Where(assignment => assignment.IsCapabilityGap)
            .Select(assignment => assignment.RoleRequirementId)
            .ToHashSet();

        for (var index = 0; index < steps.Count; index++) {
            var step = steps[index];
            var stepRoleIds = stepRoleRequirements
                .Where(item => item.StepDefinitionId == step.Id)
                .Select(item => item.RoleRequirementId)
                .Distinct()
                .ToHashSet();
            var hasCapabilityGap = stepRoleIds.Count > 0 && stepRoleIds.Any(unresolvedRoleIds.Contains);
            var status = index == 0
                ? (step.RequiresApproval ? ProcessStepRunStatus.WaitingApproval : ProcessStepRunStatus.Ready)
                : ProcessStepRunStatus.Pending;
            var currentAssignment = stepRoleRequirements
                .Where(item => item.StepDefinitionId == step.Id && item.ResponsibilityKind == ProcessResponsibilityKind.Responsible)
                .Join(
                    resolvedAssignments,
                    requirement => requirement.RoleRequirementId,
                    assignment => assignment.RoleRequirementId,
                    (_, assignment) => assignment)
                .FirstOrDefault();

            var stepRun = new ProcessStepRun {
                ProcessRunId = run.Id,
                StepDefinitionId = step.Id,
                Sequence = index,
                Title = step.Title,
                StepKind = step.StepKind,
                Status = status,
                RoleSnapshotSummary = step.DecisionRightsSummary,
                CurrentExecutorName = currentAssignment?.DisplayName ?? string.Empty,
                CurrentExecutorPartyId = currentAssignment?.PartyId,
                DecisionSummary = step.RequiresDecisionRecord ? "Decision record required." : string.Empty,
                ReadyAtUtc = status is ProcessStepRunStatus.Ready or ProcessStepRunStatus.WaitingApproval ? clock.GetUtcNow() : null,
                CapabilityGapSeverity = hasCapabilityGap ? ProcessCapabilityGapSeverity.Attention : ProcessCapabilityGapSeverity.None
            };
            await dbContext.Set<ProcessStepRun>().AddAsync(stepRun, cancellationToken);

            await dbContext.Set<ProcessWorkBrief>().AddAsync(
                new ProcessWorkBrief {
                    ProcessRunId = run.Id,
                    StepRunId = stepRun.Id,
                    Title = $"{step.Title} brief",
                    WorkBriefText = BuildWorkBrief(definition, step, currentAssignment?.DisplayName),
                    HandoffSummary = step.InputContractSummary,
                    AssignmentReason = currentAssignment?.BindingReason ?? "No executor is currently bound to the required role.",
                    ExpectedOutcome = step.OutputContractSummary,
                    EvidenceExpectationSummary = string.Join(
                        "; ",
                        artifactExpectations.Where(item => item.StepDefinitionId == step.Id).Select(item => item.Title)),
                    CreatedAtUtc = clock.GetUtcNow()
                },
                cancellationToken);
        }

        await dbContext.Set<ProcessJournalEntry>().AddAsync(
            BuildJournalEntry(
                run.Id,
                null,
                "run-created",
                "Created process run",
                $"{run.Name} started from process version {publishedVersion.VersionNumber}.",
                run.OperatingMode,
                $"v{publishedVersion.VersionNumber}",
                run.TriggerReason),
            cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);
        await activityStream.RecordAsync(
            new ActivityWriteRequest(
                "processes",
                "start-run",
                "Started process run",
                run.Name,
                run.ProjectId,
                "process-run",
                run.Id,
                BuildRunRoute(run),
                DefaultActor),
            cancellationToken);
        return Result<Guid>.Success(run.Id);
    }

    public async Task<Result> TransitionStepAsync(ProcessStepTransitionRequest request, CancellationToken cancellationToken = default) {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var stepRun = await dbContext.Set<ProcessStepRun>()
            .SingleOrDefaultAsync(item => item.Id == request.StepRunId, cancellationToken);
        if (stepRun is null) {
            return Result.Failure(Error.Validation("Process step run was not found.", "processes.step-run-not-found"));
        }

        var run = await dbContext.Set<ProcessRun>()
            .SingleAsync(item => item.Id == stepRun.ProcessRunId, cancellationToken);
        if (!IsTransitionAllowed(stepRun.Status, request.TargetStatus)) {
            return Result.Failure(Error.Validation(
                $"Cannot move step from {stepRun.Status} to {request.TargetStatus}.",
                "processes.invalid-step-transition"));
        }

        var now = clock.GetUtcNow();
        if (request.TargetStatus == ProcessStepRunStatus.InProgress) {
            stepRun.StartedAtUtc ??= now;
            stepRun.WaitMinutes = stepRun.ReadyAtUtc.HasValue
                ? Math.Max(0, (int)(now - stepRun.ReadyAtUtc.Value).TotalMinutes)
                : stepRun.WaitMinutes;
        }

        if (request.TargetStatus is ProcessStepRunStatus.Completed or ProcessStepRunStatus.Refused or ProcessStepRunStatus.Failed or ProcessStepRunStatus.Skipped) {
            stepRun.CompletedAtUtc = now;
            stepRun.TouchMinutes = stepRun.StartedAtUtc.HasValue
                ? Math.Max(0, (int)(now - stepRun.StartedAtUtc.Value).TotalMinutes)
                : stepRun.TouchMinutes;
        }

        if (request.TargetStatus == ProcessStepRunStatus.Blocked) {
            stepRun.BlockedReason = request.Reason.Trim();
            stepRun.BlockedMinutes = Math.Max(stepRun.BlockedMinutes, 15);
        }

        if (request.TargetStatus == ProcessStepRunStatus.Refused) {
            stepRun.RefusalReason = request.Reason.Trim();
            stepRun.DecisionSummary = "Safe refusal recorded.";
        }

        if (request.TargetStatus == ProcessStepRunStatus.Failed) {
            stepRun.ExceptionSummary = request.Reason.Trim();
            stepRun.ReworkCount += 1;
        }

        stepRun.Status = request.TargetStatus;

        if (request.TargetStatus == ProcessStepRunStatus.Completed) {
            var nextStep = await dbContext.Set<ProcessStepRun>()
                .Where(item => item.ProcessRunId == run.Id && item.Sequence > stepRun.Sequence)
                .OrderBy(item => item.Sequence)
                .FirstOrDefaultAsync(cancellationToken);
            if (nextStep is not null && nextStep.Status == ProcessStepRunStatus.Pending) {
                nextStep.Status = nextStep.StepKind == ProcessStepKind.Approval
                    ? ProcessStepRunStatus.WaitingApproval
                    : ProcessStepRunStatus.Ready;
                nextStep.ReadyAtUtc = now;
            }
        }

        run.UpdatedAtUtc = now;
        var persistedStepRuns = await dbContext.Set<ProcessStepRun>()
            .Where(item => item.ProcessRunId == run.Id)
            .ToListAsync(cancellationToken);
        run.Status = ResolveRunStatus(persistedStepRuns, stepRun);
        if (run.Status is ProcessRunStatus.Completed or ProcessRunStatus.Failed or ProcessRunStatus.Cancelled) {
            run.CompletedAtUtc = now;
        }

        var decisionKind = request.TargetStatus switch {
            ProcessStepRunStatus.WaitingApproval => ProcessDecisionKind.Approval,
            ProcessStepRunStatus.Blocked => ProcessDecisionKind.Escalation,
            ProcessStepRunStatus.Refused => ProcessDecisionKind.Refusal,
            ProcessStepRunStatus.Failed => ProcessDecisionKind.Exception,
            _ => ProcessDecisionKind.Assignment
        };
        var decisionOutcome = request.TargetStatus switch {
            ProcessStepRunStatus.Blocked => ProcessDecisionOutcome.Escalated,
            ProcessStepRunStatus.Refused => ProcessDecisionOutcome.Refused,
            ProcessStepRunStatus.Completed => ProcessDecisionOutcome.Accepted,
            ProcessStepRunStatus.Failed => ProcessDecisionOutcome.Rejected,
            _ => ProcessDecisionOutcome.Recorded
        };

        await dbContext.Set<ProcessDecisionRecord>().AddAsync(
            new ProcessDecisionRecord {
                ProcessRunId = run.Id,
                StepRunId = stepRun.Id,
                DecisionKind = decisionKind,
                Outcome = decisionOutcome,
                Title = $"{stepRun.Title} -> {request.TargetStatus}",
                Reason = request.Reason.Trim(),
                PolicyEvaluation = stepRun.DecisionSummary,
                DecidedBy = string.IsNullOrWhiteSpace(request.DecidedBy) ? DefaultActor : request.DecidedBy.Trim(),
                OperatingMode = run.OperatingMode,
                CreatedAtUtc = now
            },
            cancellationToken);
        await dbContext.Set<ProcessJournalEntry>().AddAsync(
            BuildJournalEntry(
                run.Id,
                stepRun.Id,
                $"step-{request.TargetStatus.ToString().ToLowerInvariant()}",
                $"Step {request.TargetStatus}",
                $"{stepRun.Title} moved to {request.TargetStatus}. {request.Reason}".Trim(),
                run.OperatingMode,
                $"definition-version:{run.ProcessDefinitionVersionId:D}",
                request.Reason),
            cancellationToken);

        if (request.TargetStatus is ProcessStepRunStatus.Blocked or ProcessStepRunStatus.Refused or ProcessStepRunStatus.Failed) {
            await dbContext.Set<ProcessConformanceObservation>().AddAsync(
                new ProcessConformanceObservation {
                    ProcessRunId = run.Id,
                    StepRunId = stepRun.Id,
                    Severity = request.TargetStatus == ProcessStepRunStatus.Failed
                        ? ProcessConformanceSeverity.High
                        : ProcessConformanceSeverity.Moderate,
                    Category = request.TargetStatus.ToString(),
                    Observation = $"{stepRun.Title} resulted in {request.TargetStatus}.",
                    DeviationReason = request.Reason.Trim(),
                    IsSafeNonAction = request.TargetStatus == ProcessStepRunStatus.Refused,
                    ContainsSensitiveAssessment = false,
                    CreatedAtUtc = now
                },
                cancellationToken);

            await MaybeCreateImprovementCandidateAsync(dbContext, run, stepRun, request, cancellationToken);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> ResolveAssignmentAsync(ProcessAssignmentResolutionRequest request, CancellationToken cancellationToken = default) {
        if (request.ProcessRunId == Guid.Empty || request.RoleRequirementId == Guid.Empty) {
            return Result.Failure(Error.Validation("Run and role are required for assignment resolution.", "processes.assignment.run-role-required"));
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var run = await dbContext.Set<ProcessRun>()
            .SingleOrDefaultAsync(item => item.Id == request.ProcessRunId, cancellationToken);
        if (run is null) {
            return Result.Failure(Error.Validation("Process run was not found.", "processes.assignment.run-not-found"));
        }

        var assignment = await dbContext.Set<ProcessRunAssignment>()
            .FirstOrDefaultAsync(
                item => item.ProcessRunId == request.ProcessRunId &&
                    item.RoleRequirementId == request.RoleRequirementId &&
                    item.StepDefinitionId == request.StepDefinitionId,
                cancellationToken);
        if (assignment is null) {
            assignment = new ProcessRunAssignment {
                ProcessRunId = request.ProcessRunId,
                RoleRequirementId = request.RoleRequirementId,
                StepDefinitionId = request.StepDefinitionId
            };

            await dbContext.Set<ProcessRunAssignment>().AddAsync(assignment, cancellationToken);
        }

        assignment.PartyId = request.PartyId;
        assignment.DisplayName = string.IsNullOrWhiteSpace(request.DisplayName) ? "Unassigned role" : request.DisplayName.Trim();
        assignment.ExecutorKind = request.ExecutorKind.Trim();
        assignment.BindingReason = request.BindingReason.Trim();
        assignment.IsFallback = request.IsFallback;
        assignment.IsCapabilityGap = !request.PartyId.HasValue && string.IsNullOrWhiteSpace(request.DisplayName);

        if (request.StepDefinitionId.HasValue) {
            var stepRuns = await dbContext.Set<ProcessStepRun>()
                .Where(item => item.ProcessRunId == request.ProcessRunId && item.StepDefinitionId == request.StepDefinitionId.Value)
                .ToListAsync(cancellationToken);
            foreach (var stepRun in stepRuns) {
                stepRun.CurrentExecutorPartyId = request.PartyId;
                stepRun.CurrentExecutorName = assignment.DisplayName;
                stepRun.CapabilityGapSeverity = assignment.IsCapabilityGap
                    ? ProcessCapabilityGapSeverity.Attention
                    : ProcessCapabilityGapSeverity.None;
            }
        }

        await dbContext.Set<ProcessDecisionRecord>().AddAsync(
            new ProcessDecisionRecord {
                ProcessRunId = request.ProcessRunId,
                DecisionKind = ProcessDecisionKind.Assignment,
                Outcome = assignment.IsCapabilityGap ? ProcessDecisionOutcome.Escalated : ProcessDecisionOutcome.Accepted,
                Title = $"Resolved role assignment {assignment.DisplayName}",
                Reason = assignment.BindingReason,
                PolicyEvaluation = assignment.IsFallback ? "Fallback assignment was used." : "Primary assignment was used.",
                DecidedBy = DefaultActor,
                OperatingMode = run.OperatingMode,
                CreatedAtUtc = clock.GetUtcNow()
            },
            cancellationToken);
        await dbContext.Set<ProcessJournalEntry>().AddAsync(
            BuildJournalEntry(
                request.ProcessRunId,
                null,
                "assignment-resolved",
                "Resolved process assignment",
                assignment.BindingReason,
                run.OperatingMode,
                $"definition-version:{run.ProcessDefinitionVersionId:D}",
                assignment.DisplayName),
            cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result<Guid>> RecordArtifactAsync(ProcessArtifactRecordRequest request, CancellationToken cancellationToken = default) {
        if (request.ProcessRunId == Guid.Empty || string.IsNullOrWhiteSpace(request.Title)) {
            return Result<Guid>.Failure(Error.Validation("Run and title are required for artifact records.", "processes.artifact.required"));
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var run = await dbContext.Set<ProcessRun>()
            .SingleOrDefaultAsync(item => item.Id == request.ProcessRunId, cancellationToken);
        if (run is null) {
            return Result<Guid>.Failure(Error.Validation("Process run was not found.", "processes.artifact.run-not-found"));
        }

        var artifact = new ProcessArtifactRecord {
            ProcessRunId = request.ProcessRunId,
            StepRunId = request.StepRunId,
            ArtifactKind = request.ArtifactKind,
            Title = request.Title.Trim(),
            TrustStatus = request.TrustStatus,
            SensitivityLevel = request.SensitivityLevel,
            ProvenanceSummary = request.ProvenanceSummary.Trim(),
            AllowedFutureUsageSummary = request.AllowedFutureUsageSummary.Trim(),
            ReviewSummary = request.ReviewSummary.Trim(),
            ManagedStoragePath = request.ManagedStoragePath.Trim(),
            CreatedAtUtc = clock.GetUtcNow()
        };
        await dbContext.Set<ProcessArtifactRecord>().AddAsync(artifact, cancellationToken);
        await dbContext.Set<ProcessJournalEntry>().AddAsync(
            BuildJournalEntry(
                request.ProcessRunId,
                request.StepRunId,
                "artifact-recorded",
                "Recorded process artifact",
                artifact.Title,
                run.OperatingMode,
                $"definition-version:{run.ProcessDefinitionVersionId:D}",
                artifact.ManagedStoragePath),
            cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result<Guid>.Success(artifact.Id);
    }

    public async Task<ProcessImportExportEnvelope> ExportAsync(Guid definitionId, CancellationToken cancellationToken = default) {
        return new ProcessImportExportEnvelope {
            Definition = await GetEditorAsync(definitionId, null, cancellationToken),
            Warnings = [],
            SourceFormat = "CanDoItAll.ProcessDefinition/v1"
        };
    }

    public async Task<Result<Guid>> ImportAsync(ProcessImportExportEnvelope envelope, CancellationToken cancellationToken = default) {
        var saveResult = await SaveAsync(envelope.Definition, cancellationToken);
        if (saveResult.IsFailure) {
            return saveResult;
        }

        if (envelope.Warnings.Count == 0) {
            return saveResult;
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var definition = await dbContext.Set<ProcessDefinition>()
            .SingleAsync(item => item.Id == saveResult.Value, cancellationToken);
        var version = await GetWorkingVersionAsync(dbContext, definition.Id, cancellationToken);
        if (version is not null) {
            version.ImportedFrom = envelope.SourceFormat;
            version.ImportWarnings = string.Join(Environment.NewLine, envelope.Warnings);
            version.UpdatedAtUtc = clock.GetUtcNow();
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return saveResult;
    }

    public async Task<IReadOnlyList<ProcessExecutorRegistryOption>> ListExecutorOptionsAsync(CancellationToken cancellationToken = default) {
        return await executorRegistryBridge.ListOptionsAsync(cancellationToken);
    }

    private async Task ClonePublishedVersionIntoNextDraftAsync(
        AppDbContext dbContext,
        Guid definitionId,
        ProcessDefinitionVersion publishedVersion,
        IReadOnlyList<ProcessRoleRequirement> roles,
        IReadOnlyList<ProcessStepDefinition> steps,
        IReadOnlyList<ProcessStepRoleAssignmentRequirement> stepRoleRequirements,
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
        var artifactExpectations = await dbContext.Set<ProcessArtifactExpectation>()
            .Where(item => steps.Select(step => step.Id).Contains(item.StepDefinitionId))
            .ToListAsync(cancellationToken);

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
                    DisplayOrder = role.DisplayOrder
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
            await dbContext.Set<ProcessStepDefinition>().AddAsync(
                new ProcessStepDefinition {
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
                    DependsOnStepId = step.DependsOnStepId.HasValue ? stepIdMap.GetValueOrDefault(step.DependsOnStepId.Value) : null,
                    CanvasX = step.CanvasX,
                    CanvasY = step.CanvasY
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

        foreach (var artifactExpectation in artifactExpectations) {
            if (!stepIdMap.TryGetValue(artifactExpectation.StepDefinitionId, out var nextStepId)) {
                continue;
            }

            await dbContext.Set<ProcessArtifactExpectation>().AddAsync(
                new ProcessArtifactExpectation {
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
        dbContext.RemoveRange(await dbContext.Set<ProcessStepRoleAssignmentRequirement>().Where(item => existingStepIds.Contains(item.StepDefinitionId)).ToListAsync(cancellationToken));
        dbContext.RemoveRange(await dbContext.Set<ProcessArtifactExpectation>().Where(item => existingStepIds.Contains(item.StepDefinitionId)).ToListAsync(cancellationToken));
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
                    DisplayOrder = index
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
        var persistedStepIds = new List<Guid>(model.Steps.Count);
        var tempDependencies = new List<(Guid StepId, Guid? OriginalDependsOnId)>();
        for (var index = 0; index < model.Steps.Count; index++) {
            var stepModel = model.Steps[index];
            var stepId = Guid.NewGuid();
            persistedStepIds.Add(stepId);
            if (stepModel.Id.HasValue) {
                stepIdMap[stepModel.Id.Value] = stepId;
            }
            tempDependencies.Add((stepId, stepModel.DependsOnStepId));
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
                    CanvasX = stepModel.CanvasX,
                    CanvasY = stepModel.CanvasY
                },
                cancellationToken);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        var persistedSteps = await dbContext.Set<ProcessStepDefinition>()
            .Where(item => item.ProcessDefinitionVersionId == workingVersionId)
            .ToDictionaryAsync(item => item.Id, cancellationToken);

        foreach (var dependency in tempDependencies) {
            if (!dependency.OriginalDependsOnId.HasValue ||
                !stepIdMap.TryGetValue(dependency.OriginalDependsOnId.Value, out var dependsOnStepId) ||
                !persistedSteps.TryGetValue(dependency.StepId, out var step)) {
                continue;
            }

            step.DependsOnStepId = dependsOnStepId;
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

                await dbContext.Set<ProcessArtifactExpectation>().AddAsync(
                    new ProcessArtifactExpectation {
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

        return null;
    }

    private static Error? ValidatePublish(
        ProcessDefinition definition,
        ProcessDefinitionVersion version,
        IReadOnlyList<ProcessRoleRequirement> roles,
        IReadOnlyList<ProcessStepDefinition> steps,
        IReadOnlyList<ProcessStepRoleAssignmentRequirement> stepRoleRequirements) {
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

        return null;
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

    private static bool IsTransitionAllowed(ProcessStepRunStatus currentStatus, ProcessStepRunStatus targetStatus) {
        if (currentStatus == targetStatus) {
            return true;
        }

        return currentStatus switch {
            ProcessStepRunStatus.Pending => targetStatus == ProcessStepRunStatus.Ready,
            ProcessStepRunStatus.Ready => targetStatus is ProcessStepRunStatus.InProgress or ProcessStepRunStatus.Blocked or ProcessStepRunStatus.Refused or ProcessStepRunStatus.Skipped or ProcessStepRunStatus.WaitingApproval,
            ProcessStepRunStatus.WaitingApproval => targetStatus is ProcessStepRunStatus.InProgress or ProcessStepRunStatus.Completed or ProcessStepRunStatus.Blocked or ProcessStepRunStatus.Refused,
            ProcessStepRunStatus.InProgress => targetStatus is ProcessStepRunStatus.Completed or ProcessStepRunStatus.Blocked or ProcessStepRunStatus.Refused or ProcessStepRunStatus.Failed,
            ProcessStepRunStatus.Blocked => targetStatus is ProcessStepRunStatus.Ready or ProcessStepRunStatus.InProgress or ProcessStepRunStatus.Refused or ProcessStepRunStatus.Failed,
            _ => false
        };
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
}
