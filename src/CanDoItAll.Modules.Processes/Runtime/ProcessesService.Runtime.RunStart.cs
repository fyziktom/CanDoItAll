using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.Processes;

internal sealed record ProcessSubprocessRunStartResult(
    Guid RunId,
    string RunName,
    ProcessRunStatus Status);

public sealed partial class ProcessesService
{
    private const int MaxProcessRunHierarchyDepth = 12;
    private const string DefaultProcessManagerName = "Default process manager";
    private const string ConfiguredProcessManagerName = "Configured process manager";
    private static readonly HashSet<string> ProcessManagerRoleTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        "manager",
        "lead",
        "orchestrator"
    };

    public async Task<Result<Guid>> StartRunAsync(ProcessRunStartRequest request, CancellationToken cancellationToken = default)
    {
        if (request.ProcessDefinitionId == Guid.Empty && !request.LaunchPlanId.HasValue)
        {
            return Result<Guid>.Failure(Error.Validation("Process definition is required.", "processes.run.definition-required"));
        }

        await SynchronizeAiDirectoryProjectionForProcessAsync("process run start", cancellationToken);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await BeginCoordinatedTransactionAsync(dbContext, cancellationToken);

        ProcessRun run;
        Guid outboxId;
        Guid automationDispatchOutboxId = Guid.Empty;
        try
        {
            var contextResult = await LoadRunStartContextAsync(dbContext, request, cancellationToken);
            if (contextResult.IsFailure)
            {
                return Result<Guid>.Failure(contextResult.Errors);
            }

            var context = contextResult.Value!;
            if (context.ExistingSubprocessRunId.HasValue)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result<Guid>.Success(context.ExistingSubprocessRunId.Value);
            }

            var now = clock.GetUtcNow();
            var runId = Guid.NewGuid();
            var managerSnapshot = ResolveRunManagerSnapshot(context);
            run = new ProcessRun
            {
                Id = runId,
                ProcessDefinitionId = context.Definition.Id,
                ProcessDefinitionVersionId = context.PublishedVersion.Id,
                ParentRunId = context.ParentRun?.Id,
                ParentStepRunId = context.ParentStepRun?.Id,
                RootRunId = context.ParentRun is null
                    ? runId
                    : context.ParentRun.RootRunId ?? context.ParentRun.Id,
                HierarchyDepth = context.ParentRun is null
                    ? 0
                    : context.ParentRun.HierarchyDepth + 1,
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
                ManagerAgentId = managerSnapshot.AgentId,
                ManagerAgentName = managerSnapshot.DisplayName,
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

            var stepRoleRequirementsByStepId = BuildStepRoleRequirementsByStepId(context.StepRoleRequirements);
            var artifactExpectationTitlesByStepId = BuildArtifactExpectationTitlesByStepId(context.ArtifactExpectations);
            for (var index = 0; index < context.Steps.Count; index++)
            {
                var step = context.Steps[index];
                var stepRoleRequirements = stepRoleRequirementsByStepId.GetValueOrDefault(step.Id) ?? [];
                var effectiveAssignmentsByRoleRequirementId = BuildEffectiveAssignmentsByRoleRequirementId(step.Id, resolvedAssignments);
                var currentAssignment = ResolveCurrentExecutorAssignment(step, stepRoleRequirements, effectiveAssignmentsByRoleRequirementId);
                var capabilityGapSeverity = ResolveStepCapabilityGapSeverity(stepRoleRequirements, effectiveAssignmentsByRoleRequirementId);
                var evidenceExpectationSummary = artifactExpectationTitlesByStepId.TryGetValue(step.Id, out var artifactExpectationTitles)
                    ? string.Join("; ", artifactExpectationTitles)
                    : string.Empty;
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
                    DecisionSummary = step.RequiresDecisionRecord ? PendingDecisionRecordSummary : string.Empty,
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
                        EvidenceExpectationSummary = evidenceExpectationSummary,
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

            if (context.ParentStepRun is not null)
            {
                await dbContext.Set<ProcessJournalEntry>().AddAsync(
                    BuildJournalEntry(
                        run.Id,
                        null,
                        ProcessRuntimeEventTypes.SubprocessRunCreated,
                        "Created subprocess run",
                        $"Started as subprocess for parent step '{context.ParentStepRun.Title}' in run '{context.ParentRun!.Name}'.",
                        run.OperatingMode,
                        $"parent-run:{context.ParentRun.Id:D};parent-step:{context.ParentStepRun.Id:D}",
                        run.TriggerReason),
                    cancellationToken);
            }

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
            automationDispatchOutboxId = await processOutboxService.EnqueueAutomationDispatchAsync(
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
        NotifyRunObservationChanged(run.ProjectId, run.ProcessDefinitionId, run.Id);

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
        IReadOnlyDictionary<Guid, ProcessLaunchCandidate> directAiCandidatesByRoleRequirementId = new Dictionary<Guid, ProcessLaunchCandidate>();
        IReadOnlyDictionary<Guid, ProcessLaunchCandidate> directWorkflowCandidatesByRoleRequirementId = new Dictionary<Guid, ProcessLaunchCandidate>();
        IReadOnlyDictionary<Guid, InheritedRunAssignmentCandidate> inheritedAssignmentsByRoleRequirementId = new Dictionary<Guid, InheritedRunAssignmentCandidate>();
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
            if (projectStructureContext is null && projectId.HasValue)
            {
                projectStructureContext = await projectStructureBridge.TryResolveLaunchContextAsync(
                    dbContext,
                    projectId.Value,
                    definition.Id,
                    cancellationToken);
                if (projectStructureContext is not null)
                {
                    triggerReason = ProcessProjectStructureContextFormatter.AppendToTriggerReason(
                        triggerReason,
                        projectStructureContext);
                }
            }
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
            if (projectStructureContext is null && projectId.HasValue)
            {
                projectStructureContext = await projectStructureBridge.TryResolveLaunchContextAsync(
                    dbContext,
                    projectId.Value,
                    definition.Id,
                    cancellationToken);
            }

            triggerReason = ProcessProjectStructureContextFormatter.AppendToTriggerReason(
                request.TriggerReason,
                projectStructureContext);
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
        var branchOutcomes = stepIds.Count == 0
            ? []
            : await dbContext.Set<ProcessStepBranchOutcomeDefinition>()
                .Where(item => stepIds.Contains(item.StepDefinitionId))
                .OrderBy(item => item.DisplayOrder)
                .ToListAsync(cancellationToken);
        var artifactExpectations = stepIds.Count == 0
            ? []
            : await dbContext.Set<ProcessArtifactExpectation>()
                .Where(item => stepIds.Contains(item.StepDefinitionId))
                .ToListAsync(cancellationToken);
        var lintMode = ResolveEffectiveLintMode(request.LintMode, definition, publishedVersion.ContractMode);
        var lintResult = ProcessDefinitionLinter.Analyze(
            BuildLintEditorModel(
                definition,
                publishedVersion,
                roles,
                steps,
                stepRoleRequirements,
                branchOutcomes,
                artifactExpectations),
            lintMode);
        var strictLintError = CreateStrictLintGateError(lintResult, "run-start");
        if (strictLintError is not null)
        {
            return Result<RunStartContext>.Failure(strictLintError);
        }

        var parentContextResult = await LoadParentRunStartContextAsync(dbContext, request, definition, cancellationToken);
        if (parentContextResult.IsFailure)
        {
            return Result<RunStartContext>.Failure(parentContextResult.Errors);
        }

        var parentContext = parentContextResult.Value!;
        if (parentContext.ParentRun is not null)
        {
            projectId = parentContext.ParentRun.ProjectId;
            operatingMode = parentContext.ParentRun.OperatingMode;
            inheritedAssignmentsByRoleRequirementId = await BuildInheritedSubprocessAssignmentsAsync(
                dbContext,
                roles,
                parentContext.ParentRun.Id,
                cancellationToken);
        }

        if (projectId.HasValue)
        {
            projectAssignmentLookup = (await projectPartyIntegrationBridge.ListAssignmentsDetailedAsync(projectId.Value, cancellationToken))
                .GroupBy(item => item.Role)
                .ToDictionary(group => group.Key, group => group.OrderByDescending(item => item.IsPrimary).ToList());
        }

        if (launchPlan is null &&
            (RequiresTechnicalAgentBinding(operatingMode) || publishedVersion.ManagerAgentOverrideId.HasValue))
        {
            directAiCandidatesByRoleRequirementId = await BuildDirectRunAiCandidateAssignmentsAsync(
                dbContext,
                definition,
                publishedVersion,
                roles,
                projectId,
                operatingMode,
                triggerReason,
                projectStructureContext,
                cancellationToken);
        }

        if (launchPlan is null)
        {
            directWorkflowCandidatesByRoleRequirementId = await BuildDirectRunWorkflowCandidateAssignmentsAsync(
                roles,
                cancellationToken);
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
                branchOutcomes,
                artifactExpectations,
                projectId,
                operatingMode,
                triggerReason,
                projectStructureContext,
                defaultRunName,
                selectedCandidatesByRoleRequirementId ?? [],
                directAiCandidatesByRoleRequirementId,
                directWorkflowCandidatesByRoleRequirementId,
                inheritedAssignmentsByRoleRequirementId,
                projectAssignmentLookup ?? [],
                parentContext.ParentRun,
                parentContext.ParentStepRun,
                parentContext.ExistingSubprocessRunId));
    }

    private async Task<Result<ParentRunStartContext>> LoadParentRunStartContextAsync(
        AppDbContext dbContext,
        ProcessRunStartRequest request,
        ProcessDefinition definition,
        CancellationToken cancellationToken)
    {
        var hasParentRun = request.ParentRunId.HasValue && request.ParentRunId.Value != Guid.Empty;
        var hasParentStepRun = request.ParentStepRunId.HasValue && request.ParentStepRunId.Value != Guid.Empty;
        if (!hasParentRun && !hasParentStepRun)
        {
            return Result<ParentRunStartContext>.Success(ParentRunStartContext.Empty);
        }

        if (hasParentRun != hasParentStepRun)
        {
            return Result<ParentRunStartContext>.Failure(
                Error.Validation(
                    "Subprocess runs require both parent run and parent step run identifiers.",
                    "processes.subprocess-parent-incomplete"));
        }

        if (request.LaunchPlanId.HasValue)
        {
            return Result<ParentRunStartContext>.Failure(
                Error.Validation(
                    "Subprocess runs must start from their parent subprocess step, not from a launch plan.",
                    "processes.subprocess-launch-plan-not-supported"));
        }

        var parentRun = await dbContext.Set<ProcessRun>()
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == request.ParentRunId!.Value, cancellationToken);
        if (parentRun is null)
        {
            return Result<ParentRunStartContext>.Failure(
                Error.Validation("Parent process run was not found.", "processes.subprocess-parent-run-not-found"));
        }

        if (parentRun.Status is ProcessRunStatus.Completed or ProcessRunStatus.Cancelled or ProcessRunStatus.Failed)
        {
            return Result<ParentRunStartContext>.Failure(
                Error.Validation(
                    $"Parent process run '{parentRun.Name}' is {parentRun.Status} and cannot start subprocesses.",
                    "processes.subprocess-parent-run-terminal"));
        }

        if (parentRun.HierarchyDepth + 1 > MaxProcessRunHierarchyDepth)
        {
            return Result<ParentRunStartContext>.Failure(
                Error.Validation(
                    $"Subprocess hierarchy depth cannot exceed {MaxProcessRunHierarchyDepth}.",
                    "processes.subprocess-depth-limit"));
        }

        var parentStepRun = await dbContext.Set<ProcessStepRun>()
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == request.ParentStepRunId!.Value, cancellationToken);
        if (parentStepRun is null || parentStepRun.ProcessRunId != parentRun.Id)
        {
            return Result<ParentRunStartContext>.Failure(
                Error.Validation("Parent subprocess step run was not found for the parent run.", "processes.subprocess-parent-step-not-found"));
        }

        if (parentStepRun.StepKind != ProcessStepKind.Subprocess)
        {
            return Result<ParentRunStartContext>.Failure(
                Error.Validation("Parent step must be a subprocess step.", "processes.subprocess-parent-step-kind"));
        }

        var parentStepDefinition = await dbContext.Set<ProcessStepDefinition>()
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == parentStepRun.StepDefinitionId, cancellationToken);
        if (parentStepDefinition is null || parentStepDefinition.ProcessDefinitionVersionId != parentRun.ProcessDefinitionVersionId)
        {
            return Result<ParentRunStartContext>.Failure(
                Error.Validation("Parent subprocess step definition no longer matches the parent run version.", "processes.subprocess-parent-step-definition-mismatch"));
        }

        if (!parentStepDefinition.SubprocessDefinitionId.HasValue ||
            parentStepDefinition.SubprocessDefinitionId.Value != definition.Id)
        {
            return Result<ParentRunStartContext>.Failure(
                Error.Validation(
                    $"Parent subprocess step '{parentStepRun.Title}' does not target process definition '{definition.Name}'.",
                    "processes.subprocess-definition-mismatch"));
        }

        var cycleError = await ValidateSubprocessDefinitionCycleAsync(dbContext, parentRun, definition.Id, cancellationToken);
        if (cycleError is not null)
        {
            return Result<ParentRunStartContext>.Failure(cycleError);
        }

        var existingSubprocessRunId = await dbContext.Set<ProcessRun>()
            .AsNoTracking()
            .Where(item => item.ParentStepRunId == parentStepRun.Id)
            .Select(item => (Guid?)item.Id)
            .SingleOrDefaultAsync(cancellationToken);

        return Result<ParentRunStartContext>.Success(
            new ParentRunStartContext(parentRun, parentStepRun, existingSubprocessRunId));
    }

    private static ProcessRunManagerSnapshot ResolveRunManagerSnapshot(RunStartContext context)
    {
        if (context.PublishedVersion.ManagerAgentOverrideId.HasValue)
        {
            return new ProcessRunManagerSnapshot(
                context.PublishedVersion.ManagerAgentOverrideId,
                string.IsNullOrWhiteSpace(context.PublishedVersion.ManagerAgentOverrideName)
                    ? ConfiguredProcessManagerName
                    : context.PublishedVersion.ManagerAgentOverrideName.Trim());
        }

        if (context.ParentRun is not null &&
            (context.ParentRun.ManagerAgentId.HasValue || !string.IsNullOrWhiteSpace(context.ParentRun.ManagerAgentName)))
        {
            return new ProcessRunManagerSnapshot(
                context.ParentRun.ManagerAgentId,
                string.IsNullOrWhiteSpace(context.ParentRun.ManagerAgentName)
                    ? DefaultProcessManagerName
                    : context.ParentRun.ManagerAgentName.Trim());
        }

        return new ProcessRunManagerSnapshot(null, DefaultProcessManagerName);
    }

    private async Task<Error?> ValidateSubprocessDefinitionCycleAsync(
        AppDbContext dbContext,
        ProcessRun parentRun,
        Guid childDefinitionId,
        CancellationToken cancellationToken)
    {
        var currentRun = parentRun;
        while (true)
        {
            if (currentRun.ProcessDefinitionId == childDefinitionId)
            {
                return Error.Validation(
                    "Subprocess hierarchy cannot contain the same process definition as one of its ancestors.",
                    "processes.subprocess-definition-cycle");
            }

            if (!currentRun.ParentRunId.HasValue)
            {
                return null;
            }

            var nextRun = await dbContext.Set<ProcessRun>()
                .AsNoTracking()
                .SingleOrDefaultAsync(item => item.Id == currentRun.ParentRunId.Value, cancellationToken);
            if (nextRun is null)
            {
                return Error.Validation(
                    $"Parent run chain for run '{parentRun.Name}' is incomplete.",
                    "processes.subprocess-parent-chain-broken");
            }

            currentRun = nextRun;
        }
    }

    internal async Task<Result<ProcessSubprocessRunStartResult>> EnsureSubprocessRunForStepAsync(
        Guid stepRunId,
        CancellationToken cancellationToken = default)
    {
        if (stepRunId == Guid.Empty)
        {
            return Result<ProcessSubprocessRunStartResult>.Failure(
                Error.Validation("Process step run is required before starting a subprocess.", "processes.subprocess.step-run-required"));
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var parentStepRun = await dbContext.Set<ProcessStepRun>()
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == stepRunId, cancellationToken);
        if (parentStepRun is null)
        {
            return Result<ProcessSubprocessRunStartResult>.Failure(
                Error.Validation("Process step run was not found.", "processes.step-run-not-found"));
        }

        if (parentStepRun.StepKind != ProcessStepKind.Subprocess)
        {
            return Result<ProcessSubprocessRunStartResult>.Failure(
                Error.Validation("Only subprocess steps can start subprocess runs.", "processes.subprocess.step-kind-required"));
        }

        var parentRun = await dbContext.Set<ProcessRun>()
            .AsNoTracking()
            .SingleAsync(item => item.Id == parentStepRun.ProcessRunId, cancellationToken);
        var parentStepDefinition = await dbContext.Set<ProcessStepDefinition>()
            .AsNoTracking()
            .SingleAsync(item => item.Id == parentStepRun.StepDefinitionId, cancellationToken);
        if (!parentStepDefinition.SubprocessDefinitionId.HasValue || parentStepDefinition.SubprocessDefinitionId.Value == Guid.Empty)
        {
            return Result<ProcessSubprocessRunStartResult>.Failure(
                Error.Validation(
                    $"Subprocess step '{parentStepRun.Title}' does not reference a process definition.",
                    "processes.subprocess.definition-required"));
        }

        var existingRun = await dbContext.Set<ProcessRun>()
            .AsNoTracking()
            .Where(item => item.ParentStepRunId == parentStepRun.Id)
            .Select(item => new ProcessSubprocessRunStartResult(item.Id, item.Name, item.Status))
            .SingleOrDefaultAsync(cancellationToken);
        if (existingRun is not null)
        {
            return Result<ProcessSubprocessRunStartResult>.Success(await ReconcileExistingSubprocessRunStatusAsync(
                dbContext,
                existingRun,
                cancellationToken));
        }

        var startResult = await StartRunAsync(
            new ProcessRunStartRequest
            {
                ProcessDefinitionId = parentStepDefinition.SubprocessDefinitionId.Value,
                ProjectId = parentRun.ProjectId,
                RunName = $"{parentStepRun.Title} / {clock.GetUtcNow():yyyy-MM-dd HH:mm}",
                OperatingMode = parentRun.OperatingMode,
                TriggerReason = $"Subprocess step '{parentStepRun.Title}' from parent run '{parentRun.Name}'.",
                ParentRunId = parentRun.Id,
                ParentStepRunId = parentStepRun.Id
            },
            cancellationToken);
        if (startResult.IsFailure)
        {
            existingRun = await dbContext.Set<ProcessRun>()
                .AsNoTracking()
                .Where(item => item.ParentStepRunId == parentStepRun.Id)
                .Select(item => new ProcessSubprocessRunStartResult(item.Id, item.Name, item.Status))
                .SingleOrDefaultAsync(cancellationToken);
            return existingRun is not null
                ? Result<ProcessSubprocessRunStartResult>.Success(existingRun)
                : Result<ProcessSubprocessRunStartResult>.Failure(startResult.Errors);
        }

        var startedRunId = startResult.Value;
        var startedRun = await dbContext.Set<ProcessRun>()
            .AsNoTracking()
            .Where(item => item.Id == startedRunId)
            .Select(item => new ProcessSubprocessRunStartResult(item.Id, item.Name, item.Status))
            .SingleAsync(cancellationToken);

        return Result<ProcessSubprocessRunStartResult>.Success(startedRun);
    }

    private async Task<ProcessSubprocessRunStartResult> ReconcileExistingSubprocessRunStatusAsync(
        AppDbContext dbContext,
        ProcessSubprocessRunStartResult existingRun,
        CancellationToken cancellationToken)
    {
        if (existingRun.Status != ProcessRunStatus.Active)
        {
            return existingRun;
        }

        var stepRuns = await dbContext.Set<ProcessStepRun>()
            .AsNoTracking()
            .Where(item => item.ProcessRunId == existingRun.RunId)
            .ToListAsync(cancellationToken);
        var resolvedStatus = ProcessRunStatusResolver.Resolve(stepRuns);
        if (resolvedStatus == existingRun.Status)
        {
            return existingRun;
        }

        var run = await dbContext.Set<ProcessRun>()
            .SingleAsync(item => item.Id == existingRun.RunId, cancellationToken);
        var now = clock.GetUtcNow();
        run.Status = resolvedStatus;
        run.UpdatedAtUtc = now;
        run.CompletedAtUtc = resolvedStatus is ProcessRunStatus.Completed or ProcessRunStatus.Failed or ProcessRunStatus.Cancelled
            ? now
            : null;
        run.ConcurrencyToken = Guid.NewGuid();

        logger.LogWarning(
            "Reconciled stale active subprocess run {RunId} to {Status} from its step ledger.",
            existingRun.RunId,
            resolvedStatus);

        await dbContext.SaveChangesAsync(cancellationToken);
        return existingRun with
        {
            Status = resolvedStatus
        };
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
                WorkflowDefinitionId = launchCandidate.WorkflowDefinitionId,
                WorkflowVersionId = launchCandidate.WorkflowVersionId,
                DisplayName = launchCandidate.DisplayName,
                ExecutorKind = ProcessExecutorKindNames.Normalize(launchCandidate.ExecutorKind),
                BindingReason = string.IsNullOrWhiteSpace(launchCandidate.RecommendationSummary)
                    ? "Bound from the approved launch plan."
                    : launchCandidate.RecommendationSummary,
                SourceRegistryKey = launchCandidate.SourceRegistryKey,
                SnapshotSummary = role.SnapshotSummary,
                IsFallback = false,
                IsCapabilityGap = launchCandidate.CandidateKind == ProcessLaunchCandidateKind.Gap || !HasExecutableTarget(launchCandidate),
                AllowsDirectMessaging = launchCandidate.AllowsDirectMessaging && launchCandidate.CandidateKind != ProcessLaunchCandidateKind.Gap
            };
        }

        if (context.DirectWorkflowCandidatesByRoleRequirementId.TryGetValue(role.Id, out var directWorkflowCandidate))
        {
            return new ProcessRunAssignment
            {
                ProcessRunId = processRunId,
                RoleRequirementId = role.Id,
                WorkflowDefinitionId = directWorkflowCandidate.WorkflowDefinitionId,
                WorkflowVersionId = directWorkflowCandidate.WorkflowVersionId,
                DisplayName = directWorkflowCandidate.DisplayName,
                ExecutorKind = ProcessExecutorKindNames.Workflow,
                BindingReason = string.IsNullOrWhiteSpace(directWorkflowCandidate.RecommendationSummary)
                    ? "Matched preferred workflow definition for direct process execution."
                    : directWorkflowCandidate.RecommendationSummary,
                SourceRegistryKey = directWorkflowCandidate.SourceRegistryKey,
                SnapshotSummary = role.SnapshotSummary,
                IsFallback = false,
                IsCapabilityGap = false,
                AllowsDirectMessaging = false
            };
        }

        if (IsWorkflowRole(role))
        {
            return new ProcessRunAssignment
            {
                ProcessRunId = processRunId,
                RoleRequirementId = role.Id,
                WorkflowDefinitionId = role.PreferredWorkflowDefinitionId,
                WorkflowVersionId = role.PreferredWorkflowVersionId,
                DisplayName = string.IsNullOrWhiteSpace(role.DisplayName)
                    ? "Unassigned workflow"
                    : role.DisplayName,
                ExecutorKind = ProcessExecutorKindNames.Workflow,
                BindingReason = "No active workflow definition was selected for direct process execution.",
                SnapshotSummary = role.SnapshotSummary,
                IsFallback = false,
                IsCapabilityGap = true,
                AllowsDirectMessaging = false
            };
        }

        context.ProjectAssignmentLookup.TryGetValue(
            role.PreferredProjectAssignmentRole ?? ProjectPartyAssignmentRole.TeamMember,
            out var candidates);
        var candidate = candidates?.FirstOrDefault();
        if (candidate is null &&
            context.DirectAiCandidatesByRoleRequirementId.TryGetValue(role.Id, out var directAiCandidate))
        {
            return new ProcessRunAssignment
            {
                ProcessRunId = processRunId,
                RoleRequirementId = role.Id,
                PartyId = directAiCandidate.PartyId,
                DisplayName = directAiCandidate.DisplayName,
                ExecutorKind = ProcessExecutorKindNames.Normalize(directAiCandidate.ExecutorKind),
                BindingReason = string.IsNullOrWhiteSpace(directAiCandidate.RecommendationSummary)
                    ? "Matched bound AI resource from the shared agent directory for direct assisted execution."
                    : directAiCandidate.RecommendationSummary,
                SourceRegistryKey = directAiCandidate.SourceRegistryKey,
                SnapshotSummary = role.SnapshotSummary,
                IsFallback = false,
                IsCapabilityGap = false,
                AllowsDirectMessaging = directAiCandidate.AllowsDirectMessaging
            };
        }

        if (context.InheritedAssignmentsByRoleRequirementId.TryGetValue(role.Id, out var inheritedCandidate))
        {
            var parentAssignment = inheritedCandidate.Assignment;
            return new ProcessRunAssignment
            {
                ProcessRunId = processRunId,
                RoleRequirementId = role.Id,
                PartyId = parentAssignment.PartyId,
                WorkflowDefinitionId = parentAssignment.WorkflowDefinitionId,
                WorkflowVersionId = parentAssignment.WorkflowVersionId,
                DisplayName = parentAssignment.DisplayName,
                ExecutorKind = ProcessExecutorKindNames.Normalize(parentAssignment.ExecutorKind),
                BindingReason = BuildInheritedAssignmentReason(inheritedCandidate),
                SourceRegistryKey = string.IsNullOrWhiteSpace(parentAssignment.SourceRegistryKey)
                    ? $"parent-run-assignment:{parentAssignment.Id:D}"
                    : parentAssignment.SourceRegistryKey,
                SnapshotSummary = role.SnapshotSummary,
                IsFallback = parentAssignment.IsFallback,
                IsCapabilityGap = parentAssignment.IsCapabilityGap,
                AllowsDirectMessaging = parentAssignment.AllowsDirectMessaging && !parentAssignment.IsCapabilityGap
            };
        }

        return new ProcessRunAssignment
        {
            ProcessRunId = processRunId,
            RoleRequirementId = role.Id,
            PartyId = candidate?.PartyId,
            DisplayName = candidate?.PartyDisplayName ?? "Unassigned role",
            ExecutorKind = candidate is not null
                ? ProcessExecutorKindNames.Normalize(candidate.PartyTypeLabel)
                : ProcessExecutorKindNames.Normalize(role.PreferredExecutorKind),
            BindingReason = candidate is not null
                ? $"Matched project portfolio role {candidate.Role}."
                : "No eligible project assignment was pre-bound to this role.",
            SnapshotSummary = role.SnapshotSummary,
            IsFallback = false,
            IsCapabilityGap = candidate is null,
            AllowsDirectMessaging = candidate is not null
        };
    }

    private static bool HasExecutableTarget(ProcessLaunchCandidate candidate)
    {
        return candidate.PartyId.HasValue ||
            candidate.WorkflowDefinitionId.HasValue && candidate.WorkflowVersionId.HasValue;
    }

    private sealed record RunStartContext(
        ProcessDefinition Definition,
        ProcessDefinitionVersion PublishedVersion,
        ProcessLaunchPlan? LaunchPlan,
        IReadOnlyList<ProcessRoleRequirement> Roles,
        IReadOnlyList<ProcessStepDefinition> Steps,
        IReadOnlyList<ProcessStepDependencyDefinition> StepDependencies,
        IReadOnlyList<ProcessStepRoleAssignmentRequirement> StepRoleRequirements,
        IReadOnlyList<ProcessStepBranchOutcomeDefinition> BranchOutcomes,
        IReadOnlyList<ProcessArtifactExpectation> ArtifactExpectations,
        Guid? ProjectId,
        ProcessOperatingMode OperatingMode,
        string TriggerReason,
        ProcessProjectStructureContext? ProjectStructureContext,
        string DefaultRunName,
        IReadOnlyDictionary<Guid, ProcessLaunchCandidate> SelectedLaunchCandidatesByRoleRequirementId,
        IReadOnlyDictionary<Guid, ProcessLaunchCandidate> DirectAiCandidatesByRoleRequirementId,
        IReadOnlyDictionary<Guid, ProcessLaunchCandidate> DirectWorkflowCandidatesByRoleRequirementId,
        IReadOnlyDictionary<Guid, InheritedRunAssignmentCandidate> InheritedAssignmentsByRoleRequirementId,
        IReadOnlyDictionary<ProjectPartyAssignmentRole, List<ProjectPartyAssignmentDetail>> ProjectAssignmentLookup,
        ProcessRun? ParentRun,
        ProcessStepRun? ParentStepRun,
        Guid? ExistingSubprocessRunId);

    private sealed record ParentRunStartContext(
        ProcessRun? ParentRun,
        ProcessStepRun? ParentStepRun,
        Guid? ExistingSubprocessRunId)
    {
        public static ParentRunStartContext Empty { get; } = new(null, null, null);
    }

    private sealed record ProcessRunManagerSnapshot(
        Guid? AgentId,
        string DisplayName);

    private sealed record InheritedRunAssignmentCandidate(
        ProcessRunAssignment Assignment,
        ProcessRoleRequirement ParentRole,
        string MatchReason);

    private sealed record InheritedRoleAssignmentSource(
        ProcessRunAssignment Assignment,
        ProcessRoleRequirement ParentRole);

    private static Dictionary<Guid, List<ProcessStepRoleAssignmentRequirement>> BuildStepRoleRequirementsByStepId(
        IReadOnlyList<ProcessStepRoleAssignmentRequirement> stepRoleRequirements)
    {
        var requirementsByStepId = new Dictionary<Guid, List<ProcessStepRoleAssignmentRequirement>>();
        requirementsByStepId.EnsureCapacity(stepRoleRequirements.Count);

        foreach (var requirement in stepRoleRequirements)
        {
            if (!requirementsByStepId.TryGetValue(requirement.StepDefinitionId, out var stepRequirements))
            {
                stepRequirements = [];
                requirementsByStepId[requirement.StepDefinitionId] = stepRequirements;
            }

            stepRequirements.Add(requirement);
        }

        return requirementsByStepId;
    }

    private static Dictionary<Guid, List<string>> BuildArtifactExpectationTitlesByStepId(
        IReadOnlyList<ProcessArtifactExpectation> artifactExpectations)
    {
        var titlesByStepId = new Dictionary<Guid, List<string>>();
        titlesByStepId.EnsureCapacity(artifactExpectations.Count);

        foreach (var artifactExpectation in artifactExpectations)
        {
            if (!titlesByStepId.TryGetValue(artifactExpectation.StepDefinitionId, out var titles))
            {
                titles = [];
                titlesByStepId[artifactExpectation.StepDefinitionId] = titles;
            }

            titles.Add(artifactExpectation.Title);
        }

        return titlesByStepId;
    }

    private async Task<IReadOnlyDictionary<Guid, InheritedRunAssignmentCandidate>> BuildInheritedSubprocessAssignmentsAsync(
        AppDbContext dbContext,
        IReadOnlyList<ProcessRoleRequirement> childRoles,
        Guid parentRunId,
        CancellationToken cancellationToken)
    {
        if (childRoles.Count == 0)
        {
            return new Dictionary<Guid, InheritedRunAssignmentCandidate>();
        }

        var parentAssignments = await dbContext.Set<ProcessRunAssignment>()
            .AsNoTracking()
            .Where(item => item.ProcessRunId == parentRunId && !item.IsCapabilityGap)
            .ToListAsync(cancellationToken);
        parentAssignments = parentAssignments
            .Where(item =>
                !string.IsNullOrWhiteSpace(item.DisplayName) &&
                !string.Equals(item.DisplayName, "Unassigned role", StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (parentAssignments.Count == 0)
        {
            return new Dictionary<Guid, InheritedRunAssignmentCandidate>();
        }

        var parentRoleIds = parentAssignments
            .Select(item => item.RoleRequirementId)
            .Distinct()
            .ToList();
        var parentRolesById = await dbContext.Set<ProcessRoleRequirement>()
            .AsNoTracking()
            .Where(item => parentRoleIds.Contains(item.Id))
            .ToDictionaryAsync(item => item.Id, cancellationToken);

        var parentSources = parentAssignments
            .GroupBy(item => item.RoleRequirementId)
            .Select(SelectInheritableParentAssignment)
            .Where(item => item is not null)
            .Select(item => item!)
            .Where(item => parentRolesById.ContainsKey(item.RoleRequirementId))
            .Select(item => new InheritedRoleAssignmentSource(item, parentRolesById[item.RoleRequirementId]))
            .ToList();
        if (parentSources.Count == 0)
        {
            return new Dictionary<Guid, InheritedRunAssignmentCandidate>();
        }

        var inheritedAssignmentsByRoleId = new Dictionary<Guid, InheritedRunAssignmentCandidate>();
        foreach (var childRole in childRoles)
        {
            var match = ResolveInheritedAssignmentForRole(childRole, parentSources);
            if (match is not null)
            {
                inheritedAssignmentsByRoleId[childRole.Id] = match;
            }
        }

        return inheritedAssignmentsByRoleId;
    }

    private static ProcessRunAssignment? SelectInheritableParentAssignment(
        IGrouping<Guid, ProcessRunAssignment> assignmentsByRole)
    {
        var runScopedAssignments = assignmentsByRole
            .Where(item => !item.StepDefinitionId.HasValue)
            .ToList();
        if (runScopedAssignments.Count == 1)
        {
            return runScopedAssignments[0];
        }

        if (runScopedAssignments.Count > 1)
        {
            return null;
        }

        var stepScopedAssignments = assignmentsByRole.ToList();
        return stepScopedAssignments.Count == 1 ? stepScopedAssignments[0] : null;
    }

    private static InheritedRunAssignmentCandidate? ResolveInheritedAssignmentForRole(
        ProcessRoleRequirement childRole,
        IReadOnlyList<InheritedRoleAssignmentSource> parentSources)
    {
        var matches = parentSources
            .Select(source => new
            {
                Source = source,
                Match = ScoreInheritedAssignmentMatch(childRole, source.ParentRole)
            })
            .Where(item => item.Match.Score > 0)
            .OrderByDescending(item => item.Match.Score)
            .ThenBy(item => item.Source.ParentRole.DisplayOrder)
            .ToList();
        if (matches.Count == 0)
        {
            return null;
        }

        var bestScore = matches[0].Match.Score;
        var bestMatches = matches
            .Where(item => item.Match.Score == bestScore)
            .ToList();
        if (bestMatches.Count != 1)
        {
            return null;
        }

        var bestMatch = bestMatches[0];
        return new InheritedRunAssignmentCandidate(
            bestMatch.Source.Assignment,
            bestMatch.Source.ParentRole,
            bestMatch.Match.Reason);
    }

    private static (int Score, string Reason) ScoreInheritedAssignmentMatch(
        ProcessRoleRequirement childRole,
        ProcessRoleRequirement parentRole)
    {
        if (EqualsNonEmpty(childRole.RoleTemplateSourceKey, parentRole.RoleTemplateSourceKey))
        {
            return (300, $"matching role template '{childRole.RoleTemplateSourceKey.Trim()}'");
        }

        if (EqualsNonEmpty(childRole.Key, parentRole.Key))
        {
            return (250, $"matching role key '{childRole.Key.Trim()}'");
        }

        if (EqualsNonEmpty(childRole.RoleTemplateSnapshotName, parentRole.RoleTemplateSnapshotName))
        {
            return (200, $"matching role template snapshot '{childRole.RoleTemplateSnapshotName.Trim()}'");
        }

        if (EqualsNormalizedText(childRole.DisplayName, parentRole.DisplayName))
        {
            return (150, $"matching role display name '{childRole.DisplayName.Trim()}'");
        }

        return (0, string.Empty);
    }

    private static bool EqualsNonEmpty(string left, string right)
    {
        return !string.IsNullOrWhiteSpace(left) &&
            !string.IsNullOrWhiteSpace(right) &&
            string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private static bool EqualsNormalizedText(string left, string right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
        {
            return false;
        }

        return string.Equals(
            NormalizeRoleText(left),
            NormalizeRoleText(right),
            StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeRoleText(string value)
    {
        var tokens = value.Split(
            ['-', '_', ' ', '/', '\\', ':'],
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return string.Join(" ", tokens);
    }

    private static string BuildInheritedAssignmentReason(InheritedRunAssignmentCandidate inheritedCandidate)
    {
        var parentAssignment = inheritedCandidate.Assignment;
        var parentBinding = string.IsNullOrWhiteSpace(parentAssignment.BindingReason)
            ? "Parent binding did not provide a reason."
            : parentAssignment.BindingReason.Trim();

        return $"Inherited subprocess role binding from parent role '{inheritedCandidate.ParentRole.DisplayName}' by {inheritedCandidate.MatchReason}. Parent binding: {parentBinding}";
    }

    private async Task<IReadOnlyDictionary<Guid, ProcessLaunchCandidate>> BuildDirectRunWorkflowCandidateAssignmentsAsync(
        IReadOnlyList<ProcessRoleRequirement> roles,
        CancellationToken cancellationToken)
    {
        var workflowRoles = roles
            .Where(role => IsWorkflowRole(role) && role.PreferredWorkflowDefinitionId.HasValue)
            .ToList();
        if (workflowRoles.Count == 0)
        {
            return new Dictionary<Guid, ProcessLaunchCandidate>();
        }

        var activeWorkflows = (await workflowCatalogService.ListDefinitionsAsync(cancellationToken))
            .Where(item => item.Status == WorkflowLifecycleStatus.Active)
            .ToList();
        if (activeWorkflows.Count == 0)
        {
            return new Dictionary<Guid, ProcessLaunchCandidate>();
        }

        var selectedCandidatesByRoleId = new Dictionary<Guid, ProcessLaunchCandidate>();
        foreach (var role in workflowRoles)
        {
            var workflow = activeWorkflows.FirstOrDefault(item =>
                item.Id.Value == role.PreferredWorkflowDefinitionId!.Value &&
                (!role.PreferredWorkflowVersionId.HasValue || item.VersionId.Value == role.PreferredWorkflowVersionId.Value));
            if (workflow is null)
            {
                continue;
            }

            selectedCandidatesByRoleId[role.Id] = new ProcessLaunchCandidate
            {
                CandidateKind = ProcessLaunchCandidateKind.Workflow,
                WorkflowDefinitionId = workflow.Id.Value,
                WorkflowVersionId = workflow.VersionId.Value,
                DisplayName = workflow.Name,
                ExecutorKind = ProcessExecutorKindNames.Workflow,
                Score = 104m,
                IsRecommended = true,
                AllowsDirectMessaging = false,
                RequiresProvisioning = false,
                RecommendationSummary = $"Matched preferred active workflow '{workflow.Name}' for direct process execution.",
                AvailabilitySummary = $"{workflow.Status} / {workflow.PreferredBackend}",
                SourceRegistryKey = $"workflow:{workflow.Id.Value:D}:{workflow.VersionId.Value:D}",
                MetadataJson = "{}",
                CreatedAtUtc = clock.GetUtcNow()
            };
        }

        return selectedCandidatesByRoleId;
    }

    private async Task<IReadOnlyDictionary<Guid, ProcessLaunchCandidate>> BuildDirectRunAiCandidateAssignmentsAsync(
        AppDbContext dbContext,
        ProcessDefinition definition,
        ProcessDefinitionVersion publishedVersion,
        IReadOnlyList<ProcessRoleRequirement> roles,
        Guid? projectId,
        ProcessOperatingMode operatingMode,
        string triggerReason,
        ProcessProjectStructureContext? projectStructureContext,
        CancellationToken cancellationToken)
    {
        var assignableRoles = roles
            .Where(IsAiRole)
            .ToList();
        if (assignableRoles.Count == 0)
        {
            return new Dictionary<Guid, ProcessLaunchCandidate>();
        }

        var aiDirectorySnapshot = await LoadLaunchAiDirectorySnapshotAsync(dbContext, cancellationToken);
        var aiDirectory = aiDirectorySnapshot.Directory
            .Where(HasRunnableTechnicalAgent)
            .OrderBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (aiDirectory.Count == 0)
        {
            return new Dictionary<Guid, ProcessLaunchCandidate>();
        }

        var roleIds = assignableRoles
            .Select(item => item.Id)
            .ToList();
        var roleSkillRequirements = await dbContext.Set<ProcessRoleSkillRequirement>()
            .AsNoTracking()
            .Where(item => roleIds.Contains(item.RoleRequirementId))
            .ToListAsync(cancellationToken);
        var requiredSkillIdsByRoleId = roleSkillRequirements
            .Where(item => item.IsRequired)
            .GroupBy(item => item.RoleRequirementId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<Guid>)group.Select(item => item.SkillId).Distinct().ToList());
        var allRequiredSkillIds = requiredSkillIdsByRoleId.Values
            .SelectMany(item => item)
            .Distinct()
            .ToList();
        var skillNamesById = allRequiredSkillIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await dbContext.Set<SkillDefinition>()
                .AsNoTracking()
                .Where(item => allRequiredSkillIds.Contains(item.Id))
                .ToDictionaryAsync(item => item.Id, item => item.Name, cancellationToken);
        var aiMatchedSkillsByPartyId = await LoadMatchedSkillsByPartyIdAsync(
            dbContext,
            aiDirectory.Select(item => item.PartyId).Distinct().ToList(),
            allRequiredSkillIds,
            cancellationToken);
        var aiFactsByPartyId = aiDirectorySnapshot.StaffingFactsByPartyId;
        var project = projectId.HasValue
            ? await dbContext.Set<Project>()
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.Id == projectId.Value, cancellationToken)
            : null;
        var transientPlan = new ProcessLaunchPlan
        {
            ProcessDefinitionId = definition.Id,
            ProcessDefinitionVersionId = publishedVersion.Id,
            ProjectId = projectId,
            Name = definition.Name,
            OperatingMode = operatingMode,
            TriggerReason = triggerReason
        };
        var launchContext = await BuildLaunchRoleContextAsync(
            dbContext,
            transientPlan,
            project,
            projectStructureContext,
            cancellationToken);
        var selectedCandidatesByRoleId = new Dictionary<Guid, ProcessLaunchCandidate>();

        foreach (var role in assignableRoles)
        {
            var requiredSkillIds = requiredSkillIdsByRoleId.GetValueOrDefault(role.Id) ?? [];
            var managerOverrideCandidate = TryBuildDirectRunManagerOverrideCandidate(
                publishedVersion,
                role,
                requiredSkillIds,
                aiDirectory,
                aiMatchedSkillsByPartyId);
            if (managerOverrideCandidate is not null)
            {
                selectedCandidatesByRoleId[role.Id] = managerOverrideCandidate;
                continue;
            }

            var selectedCandidate = aiDirectory
                .Select(aiResource => BuildDirectRunAiCandidate(aiResource, requiredSkillIds, aiMatchedSkillsByPartyId))
                .Select(candidate =>
                {
                    candidate.Score = ScoreCandidateForHrManager(
                        role,
                        candidate,
                        requiredSkillIds,
                        skillNamesById,
                        aiFactsByPartyId,
                        launchContext);
                    return candidate;
                })
                .Where(candidate => candidate.Score >= MinimumDirectAiCandidateScore)
                .OrderByDescending(candidate => candidate.Score)
                .ThenBy(candidate => candidate.DisplayName, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();

            if (selectedCandidate is not null)
            {
                selectedCandidatesByRoleId[role.Id] = selectedCandidate;
            }
        }

        return selectedCandidatesByRoleId;
    }

    private ProcessLaunchCandidate? TryBuildDirectRunManagerOverrideCandidate(
        ProcessDefinitionVersion publishedVersion,
        ProcessRoleRequirement role,
        IReadOnlyList<Guid> requiredSkillIds,
        IReadOnlyList<AiAgentListItemModel> aiDirectory,
        IReadOnlyDictionary<Guid, HashSet<Guid>> aiMatchedSkillsByPartyId)
    {
        if (!publishedVersion.ManagerAgentOverrideId.HasValue || !IsProcessManagerRole(role))
        {
            return null;
        }

        var overrideId = publishedVersion.ManagerAgentOverrideId.Value;
        var aiResource = aiDirectory.FirstOrDefault(item =>
            item.PartyId == overrideId ||
            item.TechnicalAgentId == overrideId);
        if (aiResource is null)
        {
            logger.LogWarning(
                "Process definition version {VersionId} has manager override {ManagerAgentOverrideId}, but no runnable AI resource with that party or technical agent id was found for manager role {RoleId}.",
                publishedVersion.Id,
                overrideId,
                role.Id);
            return null;
        }

        var candidate = BuildDirectRunAiCandidate(aiResource, requiredSkillIds, aiMatchedSkillsByPartyId);
        candidate.Score = Math.Max(candidate.Score, 1_000m);
        candidate.RecommendationSummary = $"Configured manager override for process role '{role.DisplayName}'.";
        candidate.SourceRegistryKey = $"process-manager-override:{overrideId:D}";

        return candidate;
    }

    private static bool IsProcessManagerRole(ProcessRoleRequirement role)
    {
        return ContainsProcessManagerRoleToken(role.Key) ||
            ContainsProcessManagerRoleToken(role.DisplayName);
    }

    private static bool ContainsProcessManagerRoleToken(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var tokens = value.Split(
            ['-', '_', ' ', '/', '\\', ':'],
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return tokens.Any(ProcessManagerRoleTokens.Contains);
    }

    private ProcessLaunchCandidate BuildDirectRunAiCandidate(
        AiAgentListItemModel aiResource,
        IReadOnlyList<Guid> requiredSkillIds,
        IReadOnlyDictionary<Guid, HashSet<Guid>> matchedSkillsByPartyId)
    {
        matchedSkillsByPartyId.TryGetValue(aiResource.PartyId, out var matchedSkillSet);
        var matchedSkillCount = matchedSkillSet?.Count ?? 0;

        return new ProcessLaunchCandidate
        {
            CandidateKind = ProcessLaunchCandidateKind.AiResource,
            PartyId = aiResource.PartyId,
            TechnicalAgentId = aiResource.TechnicalAgentId,
            DisplayName = aiResource.DisplayName,
            ExecutorKind = "AI agent",
            Score = ResolveAiResourceScore(aiResource, matchedSkillCount, requiredSkillIds.Count),
            IsRecommended = true,
            AllowsDirectMessaging = true,
            RequiresProvisioning = false,
            RecommendationSummary = BuildAiResourceRecommendationSummary(aiResource, matchedSkillCount, requiredSkillIds.Count),
            AvailabilitySummary = string.IsNullOrWhiteSpace(aiResource.ProviderName)
                ? "AI resource is available in the shared agent directory."
                : $"{aiResource.ProviderName} / {aiResource.DefaultModel}",
            SourceRegistryKey = $"crmhr-ai-agent:{aiResource.PartyId:D}",
            MetadataJson = "{}",
            CreatedAtUtc = clock.GetUtcNow()
        };
    }

    private static ProcessRunAssignment? ResolveCurrentExecutorAssignment(
        ProcessStepDefinition stepDefinition,
        IReadOnlyList<ProcessStepRoleAssignmentRequirement> stepRoleRequirements,
        IReadOnlyDictionary<Guid, ProcessRunAssignment> assignmentsByRoleRequirementId)
    {
        if (stepRoleRequirements.Count == 0 || assignmentsByRoleRequirementId.Count == 0)
        {
            return null;
        }

        foreach (var responsibilityKind in GetExecutorPriority(stepDefinition.StepKind))
        {
            var candidate = ResolveAssignmentByResponsibility(
                stepRoleRequirements,
                assignmentsByRoleRequirementId,
                responsibilityKind);
            if (candidate is not null)
            {
                return candidate;
            }
        }

        return ResolveFallbackAssignment(stepRoleRequirements, assignmentsByRoleRequirementId);
    }

    private static ProcessRunAssignment? ResolveAssignmentByResponsibility(
        IReadOnlyList<ProcessStepRoleAssignmentRequirement> stepRoleRequirements,
        IReadOnlyDictionary<Guid, ProcessRunAssignment> assignmentsByRoleRequirementId,
        ProcessResponsibilityKind responsibilityKind)
    {
        ProcessRunAssignment? selectedAssignment = null;
        var selectedFallbackOrder = 0;

        foreach (var requirement in stepRoleRequirements)
        {
            if (requirement.ResponsibilityKind != responsibilityKind ||
                !assignmentsByRoleRequirementId.TryGetValue(requirement.RoleRequirementId, out var assignment))
            {
                continue;
            }

            if (selectedAssignment is null || requirement.FallbackOrder < selectedFallbackOrder)
            {
                selectedAssignment = assignment;
                selectedFallbackOrder = requirement.FallbackOrder;
            }
        }

        return selectedAssignment;
    }

    private static ProcessRunAssignment? ResolveFallbackAssignment(
        IReadOnlyList<ProcessStepRoleAssignmentRequirement> stepRoleRequirements,
        IReadOnlyDictionary<Guid, ProcessRunAssignment> assignmentsByRoleRequirementId)
    {
        ProcessRunAssignment? selectedAssignment = null;
        var selectedIsRequired = false;
        var selectedFallbackOrder = 0;

        foreach (var requirement in stepRoleRequirements)
        {
            if (!assignmentsByRoleRequirementId.TryGetValue(requirement.RoleRequirementId, out var assignment))
            {
                continue;
            }

            if (selectedAssignment is null ||
                requirement.IsRequired && !selectedIsRequired ||
                requirement.IsRequired == selectedIsRequired && requirement.FallbackOrder < selectedFallbackOrder)
            {
                selectedAssignment = assignment;
                selectedIsRequired = requirement.IsRequired;
                selectedFallbackOrder = requirement.FallbackOrder;
            }
        }

        return selectedAssignment;
    }

    private static ProcessCapabilityGapSeverity ResolveStepCapabilityGapSeverity(
        IReadOnlyList<ProcessStepRoleAssignmentRequirement> stepRoleRequirements,
        IReadOnlyDictionary<Guid, ProcessRunAssignment> assignmentsByRoleRequirementId)
    {
        if (stepRoleRequirements.Count == 0)
        {
            return ProcessCapabilityGapSeverity.None;
        }

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
        var assignmentsByRoleRequirementId = new Dictionary<Guid, ProcessRunAssignment>();
        assignmentsByRoleRequirementId.EnsureCapacity(runAssignments.Count);

        foreach (var assignment in runAssignments)
        {
            if (assignment.StepDefinitionId.HasValue && assignment.StepDefinitionId.Value != stepDefinitionId)
            {
                continue;
            }

            if (!assignmentsByRoleRequirementId.TryGetValue(assignment.RoleRequirementId, out var currentAssignment) ||
                IsPreferredRunAssignment(stepDefinitionId, assignment, currentAssignment))
            {
                assignmentsByRoleRequirementId[assignment.RoleRequirementId] = assignment;
            }
        }

        return assignmentsByRoleRequirementId;
    }

    private static bool IsPreferredRunAssignment(
        Guid stepDefinitionId,
        ProcessRunAssignment candidate,
        ProcessRunAssignment current)
    {
        var candidateIsStepScoped = candidate.StepDefinitionId == stepDefinitionId;
        var currentIsStepScoped = current.StepDefinitionId == stepDefinitionId;
        if (candidateIsStepScoped != currentIsStepScoped)
        {
            return candidateIsStepScoped;
        }

        var candidateHasExecutableTarget = HasExecutableTarget(candidate);
        var currentHasExecutableTarget = HasExecutableTarget(current);
        if (candidateHasExecutableTarget != currentHasExecutableTarget)
        {
            return candidateHasExecutableTarget;
        }

        return false;
    }

    private static bool HasExecutableTarget(ProcessRunAssignment assignment)
    {
        return assignment.PartyId.HasValue ||
            assignment.WorkflowDefinitionId.HasValue && assignment.WorkflowVersionId.HasValue;
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
