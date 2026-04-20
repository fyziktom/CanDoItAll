using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Processes;

public sealed partial class ProcessesService
{
    public async Task<Result<Guid>> StartRunAsync(ProcessRunStartRequest request, CancellationToken cancellationToken = default)
    {
        if (request.ProcessDefinitionId == Guid.Empty && !request.LaunchPlanId.HasValue)
        {
            return Result<Guid>.Failure(Error.Validation("Process definition is required.", "processes.run.definition-required"));
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        ProcessRun run;
        Guid outboxId;
        try
        {
            var contextResult = await LoadRunStartContextAsync(dbContext, request, cancellationToken);
            if (contextResult.IsFailure)
            {
                return Result<Guid>.Failure(contextResult.Errors);
            }

            var context = contextResult.Value!;
            var now = clock.GetUtcNow();
            run = new ProcessRun
            {
                ProcessDefinitionId = context.Definition.Id,
                ProcessDefinitionVersionId = context.PublishedVersion.Id,
                ProjectId = context.ProjectId,
                Name = string.IsNullOrWhiteSpace(request.RunName)
                    ? context.DefaultRunName
                    : request.RunName.Trim(),
                Status = ProcessRunStatus.Active,
                OperatingMode = context.OperatingMode,
                TriggerReason = context.TriggerReason,
                GovernanceSnapshot = context.PublishedVersion.GovernancePolicySummary,
                PolicySnapshot = context.PublishedVersion.ConstitutionRuleSummary,
                ExecutorSnapshotSummary = context.PublishedVersion.OperatingModeSummary,
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
                StartedAtUtc = now,
                EstimatedCost = context.Steps.Sum(step => step.TargetLeadHours) * 40m
            };
            await dbContext.Set<ProcessRun>().AddAsync(run, cancellationToken);

            var resolvedAssignments = new List<ProcessRunAssignment>();
            var createdStepRuns = new List<ProcessStepRun>(context.Steps.Count);
            foreach (var role in context.Roles)
            {
                var assignment = ResolveRunAssignment(role, run.Id, context);
                resolvedAssignments.Add(assignment);
                await dbContext.Set<ProcessRunAssignment>().AddAsync(assignment, cancellationToken);

                await dbContext.Set<ProcessDecisionRecord>().AddAsync(
                    new ProcessDecisionRecord
                    {
                        ProcessRunId = run.Id,
                        DecisionKind = ProcessDecisionKind.Assignment,
                        Outcome = assignment.IsCapabilityGap ? ProcessDecisionOutcome.Escalated : ProcessDecisionOutcome.Accepted,
                        Title = $"Assignment for {role.DisplayName}",
                        Reason = assignment.BindingReason,
                        PolicyEvaluation = role.RequiresExplicitApproval
                            ? "Role requires explicit approval before irreversible work."
                            : "Role can proceed under standard governance.",
                        DecidedBy = DefaultActor,
                        OperatingMode = run.OperatingMode,
                        CreatedAtUtc = now
                    },
                    cancellationToken);
            }

            var stepDependenciesByStepId = context.StepDependencies
                .GroupBy(item => item.StepDefinitionId)
                .ToDictionary(group => group.Key, group => group.OrderBy(item => item.DisplayOrder).ToList());
            var graphIssue = FindPublishedGraphIssue(context.Steps, stepDependenciesByStepId);
            if (graphIssue is not null)
            {
                return Result<Guid>.Failure(CreateRunStartGraphError(graphIssue));
            }

            var rootStepIds = context.Steps
                .Where(step => GetPersistedDependencies(step, stepDependenciesByStepId).Count == 0)
                .Select(step => step.Id)
                .ToHashSet();
            if (rootStepIds.Count == 0 && context.Steps.Count > 0)
            {
                return Result<Guid>.Failure(CreateRunStartGraphError());
            }

            for (var index = 0; index < context.Steps.Count; index++)
            {
                var step = context.Steps[index];
                var stepRoleRequirements = context.StepRoleRequirements
                    .Where(item => item.StepDefinitionId == step.Id)
                    .ToList();
                var currentAssignment = ResolveCurrentExecutorAssignment(step, stepRoleRequirements, resolvedAssignments);
                var capabilityGapSeverity = ResolveStepCapabilityGapSeverity(step, stepRoleRequirements, resolvedAssignments);
                var status = rootStepIds.Contains(step.Id)
                    ? (step.RequiresApproval ? ProcessStepRunStatus.WaitingApproval : ProcessStepRunStatus.Ready)
                    : ProcessStepRunStatus.Pending;

                var stepRun = new ProcessStepRun
                {
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
                    ReadyAtUtc = status is ProcessStepRunStatus.Ready or ProcessStepRunStatus.WaitingApproval ? now : null,
                    CapabilityGapSeverity = capabilityGapSeverity
                };
                await dbContext.Set<ProcessStepRun>().AddAsync(stepRun, cancellationToken);
                createdStepRuns.Add(stepRun);

                await dbContext.Set<ProcessWorkBrief>().AddAsync(
                    new ProcessWorkBrief
                    {
                        ProcessRunId = run.Id,
                        StepRunId = stepRun.Id,
                        Title = $"{step.Title} brief",
                        WorkBriefText = BuildWorkBrief(
                            context.Definition,
                            step,
                            currentAssignment?.DisplayName,
                            context.ProjectStructureContext),
                        HandoffSummary = step.InputContractSummary,
                        AssignmentReason = currentAssignment?.BindingReason ?? "No executor is currently bound to the required role.",
                        ExpectedOutcome = step.OutputContractSummary,
                        EvidenceExpectationSummary = string.Join(
                            "; ",
                            context.ArtifactExpectations.Where(item => item.StepDefinitionId == step.Id).Select(item => item.Title)),
                        CreatedAtUtc = now
                    },
                    cancellationToken);
            }

            await dbContext.Set<ProcessJournalEntry>().AddAsync(
                BuildJournalEntry(
                    run.Id,
                    null,
                    "run-created",
                    "Created process run",
                    context.LaunchPlan is null
                        ? $"{run.Name} started from process version {context.PublishedVersion.VersionNumber}."
                        : $"{run.Name} started from approved launch plan '{context.LaunchPlan.Name}'.",
                    run.OperatingMode,
                    $"v{context.PublishedVersion.VersionNumber}",
                    run.TriggerReason),
                cancellationToken);

            if (context.LaunchPlan is not null)
            {
                context.LaunchPlan.GeneratedRunId = run.Id;
                context.LaunchPlan.ExecutedAtUtc = now;
                context.LaunchPlan.UpdatedAtUtc = now;
                context.LaunchPlan.Status = ProcessLaunchPlanStatus.Executing;
            }

            await projectStructureBridge.SyncRunAsync(
                dbContext,
                run,
                createdStepRuns,
                cancellationToken);

            outboxId = await processOutboxService.EnqueueRunStartAsync(dbContext, run, cancellationToken);
            await processOutboxService.EnqueueAutomationDispatchAsync(
                dbContext,
                run.ProjectId,
                run.ProcessDefinitionId,
                run.Id,
                null,
                "run-start",
                cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<Guid>.Failure(CreateRunStartConflictError());
        }
        catch (DbUpdateException exception) when (DbUpdateExceptionClassifier.IsUniqueConstraintViolation(exception))
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<Guid>.Failure(CreateRunStartConflictError());
        }

        await processOutboxService.ProcessAsync(outboxId, cancellationToken);

        return Result<Guid>.Success(run.Id);
    }

    private async Task<Result<RunStartContext>> LoadRunStartContextAsync(
        AppDbContext dbContext,
        ProcessRunStartRequest request,
        CancellationToken cancellationToken)
    {
        ProcessLaunchPlan? launchPlan = null;
        Dictionary<Guid, ProcessLaunchCandidate>? selectedCandidatesByRoleRequirementId = null;
        Dictionary<ProjectPartyAssignmentRole, List<ProjectPartyAssignmentDetail>>? projectAssignmentLookup = null;
        ProcessDefinition? definition;
        ProcessDefinitionVersion? publishedVersion;
        Guid? projectId;
        ProcessOperatingMode operatingMode;
        string triggerReason;
        ProcessProjectStructureContext? projectStructureContext = null;

        if (request.LaunchPlanId.HasValue)
        {
            launchPlan = await dbContext.Set<ProcessLaunchPlan>()
                .SingleOrDefaultAsync(item => item.Id == request.LaunchPlanId.Value, cancellationToken);
            if (launchPlan is null)
            {
                return Result<RunStartContext>.Failure(Error.Validation("Launch plan was not found.", "processes.run.launch-plan-not-found"));
            }

            if (launchPlan.GeneratedRunId.HasValue)
            {
                return Result<RunStartContext>.Failure(Error.Validation(
                    "Launch plan already generated a runtime run.",
                    "processes.run.launch-plan-already-executed"));
            }

            if (launchPlan.Status != ProcessLaunchPlanStatus.Ready)
            {
                return Result<RunStartContext>.Failure(Error.Validation(
                    "Launch plan must be approved and fully provisioned before runtime execution can start.",
                    "processes.run.launch-plan-not-ready"));
            }

            definition = await dbContext.Set<ProcessDefinition>()
                .SingleOrDefaultAsync(item => item.Id == launchPlan.ProcessDefinitionId, cancellationToken);
            publishedVersion = await dbContext.Set<ProcessDefinitionVersion>()
                .SingleOrDefaultAsync(
                    item => item.ProcessDefinitionId == launchPlan.ProcessDefinitionId &&
                        item.Id == launchPlan.ProcessDefinitionVersionId &&
                        item.Status == ProcessVersionStatus.Published,
                    cancellationToken);
            if (definition is null || publishedVersion is null)
            {
                return Result<RunStartContext>.Failure(Error.Validation(
                    "Launch plan no longer points to a published process version.",
                    "processes.run.launch-plan-version-missing"));
            }

            var launchRoles = await dbContext.Set<ProcessLaunchPlanRole>()
                .Where(item => item.LaunchPlanId == launchPlan.Id)
                .ToListAsync(cancellationToken);
            var selectedCandidateIds = launchRoles
                .Where(item => item.SelectedCandidateId.HasValue)
                .Select(item => item.SelectedCandidateId!.Value)
                .Distinct()
                .ToList();
            var selectedCandidates = selectedCandidateIds.Count == 0
                ? []
                : await dbContext.Set<ProcessLaunchCandidate>()
                    .Where(item => selectedCandidateIds.Contains(item.Id))
                    .ToListAsync(cancellationToken);
            var candidateLookup = selectedCandidates.ToDictionary(item => item.Id);
            selectedCandidatesByRoleRequirementId = new Dictionary<Guid, ProcessLaunchCandidate>();
            foreach (var launchRole in launchRoles)
            {
                if (!launchRole.SelectedCandidateId.HasValue ||
                    !candidateLookup.TryGetValue(launchRole.SelectedCandidateId.Value, out var selectedCandidate))
                {
                    if (launchRole.IsRequired)
                    {
                        return Result<RunStartContext>.Failure(Error.Validation(
                            $"Required launch role '{launchRole.DisplayName}' does not have a selected candidate.",
                            "processes.run.launch-role-missing-selection"));
                    }

                    continue;
                }

                if (selectedCandidate.CandidateKind == ProcessLaunchCandidateKind.Gap)
                {
                    return Result<RunStartContext>.Failure(Error.Validation(
                        $"Required launch role '{launchRole.DisplayName}' is still unresolved.",
                        "processes.run.launch-role-gap"));
                }

                if (selectedCandidate.RequiresProvisioning)
                {
                    return Result<RunStartContext>.Failure(Error.Validation(
                        $"Launch role '{launchRole.DisplayName}' still requires provisioning.",
                        "processes.run.launch-role-not-provisioned"));
                }

                selectedCandidatesByRoleRequirementId[launchRole.RoleRequirementId] = selectedCandidate;
            }

            projectId = launchPlan.ProjectId ?? request.ProjectId ?? definition.ProjectId;
            operatingMode = launchPlan.OperatingMode;
            triggerReason = string.IsNullOrWhiteSpace(launchPlan.TriggerReason)
                ? "Executed from an approved launch plan."
                : launchPlan.TriggerReason.Trim();
            ProcessProjectStructureContextFormatter.TryParse(triggerReason, out projectStructureContext);
        }
        else
        {
            definition = await dbContext.Set<ProcessDefinition>()
                .SingleOrDefaultAsync(item => item.Id == request.ProcessDefinitionId, cancellationToken);
            if (definition is null || !definition.ActivePublishedVersionId.HasValue)
            {
                return Result<RunStartContext>.Failure(Error.Validation("Publish a process definition before starting a run.", "processes.run.published-version-required"));
            }

            publishedVersion = await dbContext.Set<ProcessDefinitionVersion>()
                .SingleOrDefaultAsync(
                    item => item.ProcessDefinitionId == definition.Id &&
                        item.Id == definition.ActivePublishedVersionId.Value &&
                        item.Status == ProcessVersionStatus.Published,
                    cancellationToken);
            if (publishedVersion is null)
            {
                return Result<RunStartContext>.Failure(Error.Validation("Publish a process definition before starting a run.", "processes.run.published-version-required"));
            }

            projectId = request.ProjectId ?? definition.ProjectId;
            operatingMode = request.OperatingMode;
            projectStructureContext = request.ProjectStructureContext;
            triggerReason = ProcessProjectStructureContextFormatter.AppendToTriggerReason(
                request.TriggerReason,
                request.ProjectStructureContext);
        }

        var roles = await dbContext.Set<ProcessRoleRequirement>()
            .Where(item => item.ProcessDefinitionVersionId == publishedVersion.Id)
            .OrderBy(item => item.DisplayOrder)
            .ToListAsync(cancellationToken);
        var steps = await dbContext.Set<ProcessStepDefinition>()
            .Where(item => item.ProcessDefinitionVersionId == publishedVersion.Id)
            .OrderBy(item => item.OrderIndex)
            .ToListAsync(cancellationToken);
        var stepIds = steps.Select(item => item.Id).ToList();
        var stepDependencies = stepIds.Count == 0
            ? []
            : await dbContext.Set<ProcessStepDependencyDefinition>()
                .Where(item => stepIds.Contains(item.StepDefinitionId))
                .OrderBy(item => item.DisplayOrder)
                .ToListAsync(cancellationToken);
        var stepRoleRequirements = stepIds.Count == 0
            ? []
            : await dbContext.Set<ProcessStepRoleAssignmentRequirement>()
                .Where(item => stepIds.Contains(item.StepDefinitionId))
                .ToListAsync(cancellationToken);
        var artifactExpectations = stepIds.Count == 0
            ? []
            : await dbContext.Set<ProcessArtifactExpectation>()
                .Where(item => stepIds.Contains(item.StepDefinitionId))
                .ToListAsync(cancellationToken);

        if (projectId.HasValue)
        {
            projectAssignmentLookup = (await projectPartyIntegrationBridge.ListAssignmentsDetailedAsync(projectId.Value, cancellationToken))
                .GroupBy(item => item.Role)
                .ToDictionary(group => group.Key, group => group.OrderByDescending(item => item.IsPrimary).ToList());
        }

        var defaultRunName = launchPlan is not null
            ? launchPlan.Name
            : $"{definition.Name} / {clock.GetUtcNow():yyyy-MM-dd HH:mm}";
        return Result<RunStartContext>.Success(
            new RunStartContext(
                definition,
                publishedVersion,
                launchPlan,
                roles,
                steps,
                stepDependencies,
                stepRoleRequirements,
                artifactExpectations,
                projectId,
                operatingMode,
                triggerReason,
                projectStructureContext,
                defaultRunName,
                selectedCandidatesByRoleRequirementId ?? [],
                projectAssignmentLookup ?? []));
    }

    private static ProcessRunAssignment ResolveRunAssignment(
        ProcessRoleRequirement role,
        Guid processRunId,
        RunStartContext context)
    {
        if (context.LaunchPlan is not null &&
            context.SelectedLaunchCandidatesByRoleRequirementId.TryGetValue(role.Id, out var launchCandidate))
        {
            return new ProcessRunAssignment
            {
                ProcessRunId = processRunId,
                RoleRequirementId = role.Id,
                PartyId = launchCandidate.PartyId,
                DisplayName = launchCandidate.DisplayName,
                ExecutorKind = launchCandidate.ExecutorKind,
                BindingReason = string.IsNullOrWhiteSpace(launchCandidate.RecommendationSummary)
                    ? "Bound from the approved launch plan."
                    : launchCandidate.RecommendationSummary,
                SourceRegistryKey = launchCandidate.SourceRegistryKey,
                SnapshotSummary = role.SnapshotSummary,
                IsFallback = false,
                IsCapabilityGap = launchCandidate.CandidateKind == ProcessLaunchCandidateKind.Gap,
                AllowsDirectMessaging = launchCandidate.AllowsDirectMessaging && launchCandidate.CandidateKind != ProcessLaunchCandidateKind.Gap
            };
        }

        context.ProjectAssignmentLookup.TryGetValue(
            role.PreferredProjectAssignmentRole ?? ProjectPartyAssignmentRole.TeamMember,
            out var candidates);
        var candidate = candidates?.FirstOrDefault();
        return new ProcessRunAssignment
        {
            ProcessRunId = processRunId,
            RoleRequirementId = role.Id,
            PartyId = candidate?.PartyId,
            DisplayName = candidate?.PartyDisplayName ?? "Unassigned role",
            ExecutorKind = candidate is not null ? candidate.PartyTypeLabel : role.PreferredExecutorKind,
            BindingReason = candidate is not null
                ? $"Matched project portfolio role {candidate.Role}."
                : "No eligible project assignment was pre-bound to this role.",
            SnapshotSummary = role.SnapshotSummary,
            IsFallback = false,
            IsCapabilityGap = candidate is null,
            AllowsDirectMessaging = candidate is not null
        };
    }

    private sealed record RunStartContext(
        ProcessDefinition Definition,
        ProcessDefinitionVersion PublishedVersion,
        ProcessLaunchPlan? LaunchPlan,
        IReadOnlyList<ProcessRoleRequirement> Roles,
        IReadOnlyList<ProcessStepDefinition> Steps,
        IReadOnlyList<ProcessStepDependencyDefinition> StepDependencies,
        IReadOnlyList<ProcessStepRoleAssignmentRequirement> StepRoleRequirements,
        IReadOnlyList<ProcessArtifactExpectation> ArtifactExpectations,
        Guid? ProjectId,
        ProcessOperatingMode OperatingMode,
        string TriggerReason,
        ProcessProjectStructureContext? ProjectStructureContext,
        string DefaultRunName,
        IReadOnlyDictionary<Guid, ProcessLaunchCandidate> SelectedLaunchCandidatesByRoleRequirementId,
        IReadOnlyDictionary<ProjectPartyAssignmentRole, List<ProjectPartyAssignmentDetail>> ProjectAssignmentLookup);

    private static ProcessRunAssignment? ResolveCurrentExecutorAssignment(
        ProcessStepDefinition stepDefinition,
        IReadOnlyList<ProcessStepRoleAssignmentRequirement> stepRoleRequirements,
        IReadOnlyList<ProcessRunAssignment> runAssignments)
    {
        if (stepRoleRequirements.Count == 0 || runAssignments.Count == 0)
        {
            return null;
        }

        var assignmentsByRoleRequirementId = BuildEffectiveAssignmentsByRoleRequirementId(stepDefinition.Id, runAssignments);
        foreach (var responsibilityKind in GetExecutorPriority(stepDefinition.StepKind))
        {
            var candidate = stepRoleRequirements
                .Where(item => item.ResponsibilityKind == responsibilityKind)
                .OrderBy(item => item.FallbackOrder)
                .Select(item => assignmentsByRoleRequirementId.GetValueOrDefault(item.RoleRequirementId))
                .FirstOrDefault(item => item is not null);
            if (candidate is not null)
            {
                return candidate;
            }
        }

        return stepRoleRequirements
            .OrderBy(item => item.IsRequired ? 0 : 1)
            .ThenBy(item => item.FallbackOrder)
            .Select(item => assignmentsByRoleRequirementId.GetValueOrDefault(item.RoleRequirementId))
            .FirstOrDefault(item => item is not null);
    }

    private static ProcessCapabilityGapSeverity ResolveStepCapabilityGapSeverity(
        ProcessStepDefinition stepDefinition,
        IReadOnlyList<ProcessStepRoleAssignmentRequirement> stepRoleRequirements,
        IReadOnlyList<ProcessRunAssignment> runAssignments)
    {
        if (stepRoleRequirements.Count == 0)
        {
            return ProcessCapabilityGapSeverity.None;
        }

        var assignmentsByRoleRequirementId = BuildEffectiveAssignmentsByRoleRequirementId(stepDefinition.Id, runAssignments);
        foreach (var requirement in stepRoleRequirements)
        {
            if (!assignmentsByRoleRequirementId.TryGetValue(requirement.RoleRequirementId, out var assignment))
            {
                if (requirement.IsRequired)
                {
                    return ProcessCapabilityGapSeverity.Attention;
                }

                continue;
            }

            if (assignment.IsCapabilityGap)
            {
                return ProcessCapabilityGapSeverity.Attention;
            }
        }

        return ProcessCapabilityGapSeverity.None;
    }

    private static Dictionary<Guid, ProcessRunAssignment> BuildEffectiveAssignmentsByRoleRequirementId(
        Guid stepDefinitionId,
        IReadOnlyList<ProcessRunAssignment> runAssignments)
    {
        return runAssignments
            .Where(item => !item.StepDefinitionId.HasValue || item.StepDefinitionId == stepDefinitionId)
            .GroupBy(item => item.RoleRequirementId)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderByDescending(item => item.StepDefinitionId == stepDefinitionId)
                    .ThenByDescending(item => item.PartyId.HasValue)
                    .First());
    }

    private static IReadOnlyList<ProcessResponsibilityKind> GetExecutorPriority(ProcessStepKind stepKind)
    {
        return stepKind switch
        {
            ProcessStepKind.Approval => [
                ProcessResponsibilityKind.Approver,
                ProcessResponsibilityKind.Responsible,
                ProcessResponsibilityKind.Reviewer,
                ProcessResponsibilityKind.Backup
            ],
            ProcessStepKind.Review => [
                ProcessResponsibilityKind.Responsible,
                ProcessResponsibilityKind.Reviewer,
                ProcessResponsibilityKind.Approver,
                ProcessResponsibilityKind.Backup
            ],
            _ => [
                ProcessResponsibilityKind.Responsible,
                ProcessResponsibilityKind.Approver,
                ProcessResponsibilityKind.Reviewer,
                ProcessResponsibilityKind.Backup
            ]
        };
    }
}
