using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Infrastructure.Search;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Processes;

public sealed partial class ProcessesService {
    public async Task<Result<Guid>> StartRunAsync(ProcessRunStartRequest request, CancellationToken cancellationToken = default) {
        if (request.ProcessDefinitionId == Guid.Empty) {
            return Result<Guid>.Failure(Error.Validation("Process definition is required.", "processes.run.definition-required"));
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        ProcessRun run;
        Guid outboxId;
        try {
            var definition = await dbContext.Set<ProcessDefinition>()
                .SingleOrDefaultAsync(item => item.Id == request.ProcessDefinitionId, cancellationToken);
            if (definition is null || !definition.ActivePublishedVersionId.HasValue) {
                return Result<Guid>.Failure(Error.Validation("Publish a process definition before starting a run.", "processes.run.published-version-required"));
            }

            var publishedVersion = await dbContext.Set<ProcessDefinitionVersion>()
                .SingleAsync(
                    item => item.ProcessDefinitionId == definition.Id &&
                        item.Id == definition.ActivePublishedVersionId.Value &&
                        item.Status == ProcessVersionStatus.Published,
                    cancellationToken);
            var roles = await dbContext.Set<ProcessRoleRequirement>()
                .Where(item => item.ProcessDefinitionVersionId == publishedVersion.Id)
                .OrderBy(item => item.DisplayOrder)
                .ToListAsync(cancellationToken);
            var steps = await dbContext.Set<ProcessStepDefinition>()
                .Where(item => item.ProcessDefinitionVersionId == publishedVersion.Id)
                .OrderBy(item => item.OrderIndex)
                .ToListAsync(cancellationToken);
            var stepDependencies = await dbContext.Set<ProcessStepDependencyDefinition>()
                .Where(item => steps.Select(step => step.Id).Contains(item.StepDefinitionId))
                .OrderBy(item => item.DisplayOrder)
                .ToListAsync(cancellationToken);
            var stepRoleRequirements = await dbContext.Set<ProcessStepRoleAssignmentRequirement>()
                .Where(item => steps.Select(step => step.Id).Contains(item.StepDefinitionId))
                .ToListAsync(cancellationToken);
            var artifactExpectations = await dbContext.Set<ProcessArtifactExpectation>()
                .Where(item => steps.Select(step => step.Id).Contains(item.StepDefinitionId))
                .ToListAsync(cancellationToken);

            run = new ProcessRun {
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
            var stepDependenciesByStepId = stepDependencies
                .GroupBy(item => item.StepDefinitionId)
                .ToDictionary(group => group.Key, group => group.OrderBy(item => item.DisplayOrder).ToList());
            var rootStepIds = steps
                .Where(step => GetPersistedDependencies(step, stepDependenciesByStepId).Count == 0)
                .Select(step => step.Id)
                .ToHashSet();
            if (rootStepIds.Count == 0 && steps.Count > 0) {
                rootStepIds.Add(steps[0].Id);
            }

            for (var index = 0; index < steps.Count; index++) {
                var step = steps[index];
                var stepRoleIds = stepRoleRequirements
                    .Where(item => item.StepDefinitionId == step.Id)
                    .Select(item => item.RoleRequirementId)
                    .Distinct()
                    .ToHashSet();
                var hasCapabilityGap = stepRoleIds.Count > 0 && stepRoleIds.Any(unresolvedRoleIds.Contains);
                var status = rootStepIds.Contains(step.Id)
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

            outboxId = await processOutboxService.EnqueueRunStartAsync(dbContext, run, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException) {
            await transaction.RollbackAsync(cancellationToken);
            return Result<Guid>.Failure(CreateRunStartConflictError());
        }
        catch (DbUpdateException exception) when (DbUpdateExceptionClassifier.IsUniqueConstraintViolation(exception)) {
            await transaction.RollbackAsync(cancellationToken);
            return Result<Guid>.Failure(CreateRunStartConflictError());
        }

        await processOutboxService.ProcessAsync(outboxId, cancellationToken);
        return Result<Guid>.Success(run.Id);
    }

    public async Task<Result> TransitionStepAsync(ProcessStepTransitionRequest request, CancellationToken cancellationToken = default) {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        try {
            var transitionContextResult = await LoadTransitionContextAsync(dbContext, request, cancellationToken);
            if (transitionContextResult.IsFailure) {
                return Result.Failure(transitionContextResult.Errors);
            }

            var transitionContext = transitionContextResult.Value!;
            var availableBranchOutcomes = transitionContext.BranchOutcomesByStepId.GetValueOrDefault(transitionContext.StepRun.StepDefinitionId) ?? [];
            var transitionResolutionResult = ProcessStepTransitionGuard.ValidateAndResolve(
                request,
                transitionContext.StepRun,
                transitionContext.CurrentStepDefinition,
                transitionContext.StepDefinitions,
                transitionContext.StepDependenciesByStepId,
                availableBranchOutcomes);
            if (transitionResolutionResult.IsFailure) {
                return Result.Failure(transitionResolutionResult.Errors);
            }

            var selectedBranchOutcome = transitionResolutionResult.Value!.SelectedBranchOutcome;
            var trimmedReason = request.Reason.Trim();
            var now = clock.GetUtcNow();

            ApplyStepRunTransitionState(transitionContext.StepRun, request, selectedBranchOutcome, trimmedReason, now);

            var stepRunsByDefinitionId = transitionContext.PersistedStepRuns.ToDictionary(item => item.StepDefinitionId);
            stepRunsByDefinitionId[transitionContext.StepRun.StepDefinitionId] = transitionContext.StepRun;

            ProcessRuntimeProgressionPlanner.ApplyTransitionConsequences(
                request.TargetStatus,
                transitionContext.CurrentStepDefinition,
                transitionContext.StepDefinitionsById,
                stepRunsByDefinitionId,
                transitionContext.StepDependenciesByStepId,
                now);

            transitionContext.Run.UpdatedAtUtc = now;
            transitionContext.Run.Status = ProcessRunStatusCalculator.Resolve(transitionContext.PersistedStepRuns, transitionContext.StepRun);
            if (transitionContext.Run.Status is ProcessRunStatus.Completed or ProcessRunStatus.Failed or ProcessRunStatus.Cancelled) {
                transitionContext.Run.CompletedAtUtc = now;
            }

            var decisionKind = request.TargetStatus switch {
                ProcessStepRunStatus.Completed when selectedBranchOutcome is not null => ProcessDecisionKind.Variant,
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
                    ProcessRunId = transitionContext.Run.Id,
                    StepRunId = transitionContext.StepRun.Id,
                    DecisionKind = decisionKind,
                    Outcome = decisionOutcome,
                    Title = selectedBranchOutcome is null
                        ? $"{transitionContext.StepRun.Title} -> {request.TargetStatus}"
                        : $"{transitionContext.StepRun.Title} -> {request.TargetStatus} / {selectedBranchOutcome.Title}",
                    Reason = trimmedReason,
                    PolicyEvaluation = transitionContext.StepRun.DecisionSummary,
                    BranchOutcomeId = selectedBranchOutcome?.Id,
                    BranchOutcomeTitle = selectedBranchOutcome?.Title ?? string.Empty,
                    DecidedBy = string.IsNullOrWhiteSpace(request.DecidedBy) ? DefaultActor : request.DecidedBy.Trim(),
                    OperatingMode = transitionContext.Run.OperatingMode,
                    CreatedAtUtc = now
                },
                cancellationToken);
            await dbContext.Set<ProcessJournalEntry>().AddAsync(
                BuildJournalEntry(
                    transitionContext.Run.Id,
                    transitionContext.StepRun.Id,
                    $"step-{request.TargetStatus.ToString().ToLowerInvariant()}",
                    $"Step {request.TargetStatus}",
                    BuildTransitionJournalDescription(transitionContext.StepRun.Title, request.TargetStatus, trimmedReason, selectedBranchOutcome?.Title),
                    transitionContext.Run.OperatingMode,
                    $"definition-version:{transitionContext.Run.ProcessDefinitionVersionId:D}",
                    trimmedReason),
                cancellationToken);

            if (request.TargetStatus is ProcessStepRunStatus.Blocked or ProcessStepRunStatus.Refused or ProcessStepRunStatus.Failed) {
                await dbContext.Set<ProcessConformanceObservation>().AddAsync(
                    new ProcessConformanceObservation {
                        ProcessRunId = transitionContext.Run.Id,
                        StepRunId = transitionContext.StepRun.Id,
                        Severity = request.TargetStatus == ProcessStepRunStatus.Failed
                            ? ProcessConformanceSeverity.High
                            : ProcessConformanceSeverity.Moderate,
                        Category = request.TargetStatus.ToString(),
                        Observation = $"{transitionContext.StepRun.Title} resulted in {request.TargetStatus}.",
                        DeviationReason = trimmedReason,
                        IsSafeNonAction = request.TargetStatus == ProcessStepRunStatus.Refused,
                        ContainsSensitiveAssessment = false,
                        CreatedAtUtc = now
                    },
                    cancellationToken);

                await MaybeCreateImprovementCandidateAsync(dbContext, transitionContext.Run, transitionContext.StepRun, request, cancellationToken);
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Result.Success();
        }
        catch (DbUpdateConcurrencyException) {
            await transaction.RollbackAsync(cancellationToken);
            return Result.Failure(CreateStepTransitionConflictError());
        }
        catch (DbUpdateException exception) when (DbUpdateExceptionClassifier.IsUniqueConstraintViolation(exception)) {
            await transaction.RollbackAsync(cancellationToken);
            return Result.Failure(CreateStepTransitionConflictError());
        }
    }

    private async Task<Result<ProcessRuntimeTransitionContext>> LoadTransitionContextAsync(
        AppDbContext dbContext,
        ProcessStepTransitionRequest request,
        CancellationToken cancellationToken) {
        var stepRun = await dbContext.Set<ProcessStepRun>()
            .SingleOrDefaultAsync(item => item.Id == request.StepRunId, cancellationToken);
        if (stepRun is null) {
            return Result<ProcessRuntimeTransitionContext>.Failure(
                Error.Validation("Process step run was not found.", "processes.step-run-not-found"));
        }

        if (HasConcurrencyTokenMismatch(request.StepRunConcurrencyToken, stepRun.ConcurrencyToken)) {
            return Result<ProcessRuntimeTransitionContext>.Failure(CreateStepTransitionConflictError());
        }

        var run = await dbContext.Set<ProcessRun>()
            .SingleAsync(item => item.Id == stepRun.ProcessRunId, cancellationToken);
        var stepDefinitions = await dbContext.Set<ProcessStepDefinition>()
            .Where(item => item.ProcessDefinitionVersionId == run.ProcessDefinitionVersionId)
            .ToListAsync(cancellationToken);
        var stepIds = stepDefinitions.Select(item => item.Id).ToList();
        IReadOnlyList<ProcessStepDependencyDefinition> stepDependencies = stepIds.Count == 0
            ? []
            : await dbContext.Set<ProcessStepDependencyDefinition>()
                .Where(item => stepIds.Contains(item.StepDefinitionId))
                .OrderBy(item => item.DisplayOrder)
                .ToListAsync(cancellationToken);
        IReadOnlyList<ProcessStepBranchOutcomeDefinition> branchOutcomes = stepIds.Count == 0
            ? []
            : await dbContext.Set<ProcessStepBranchOutcomeDefinition>()
                .Where(item => stepIds.Contains(item.StepDefinitionId))
                .ToListAsync(cancellationToken);
        var persistedStepRuns = await dbContext.Set<ProcessStepRun>()
            .Where(item => item.ProcessRunId == run.Id)
            .ToListAsync(cancellationToken);

        var currentStepDefinition = stepDefinitions.Single(item => item.Id == stepRun.StepDefinitionId);
        var branchOutcomesByStepId = branchOutcomes
            .GroupBy(item => item.StepDefinitionId)
            .ToDictionary(group => group.Key, group => group.OrderBy(item => item.DisplayOrder).ToList());
        var stepDefinitionsById = stepDefinitions.ToDictionary(item => item.Id);
        var stepDependenciesByStepId = stepDependencies
            .GroupBy(item => item.StepDefinitionId)
            .ToDictionary(group => group.Key, group => group.OrderBy(item => item.DisplayOrder).ToList());

        return Result<ProcessRuntimeTransitionContext>.Success(
            new ProcessRuntimeTransitionContext(
                stepRun,
                run,
                currentStepDefinition,
                stepDefinitions,
                stepDefinitionsById,
                stepDependenciesByStepId,
                branchOutcomesByStepId,
                persistedStepRuns));
    }

    private static void ApplyStepRunTransitionState(
        ProcessStepRun stepRun,
        ProcessStepTransitionRequest request,
        ProcessStepBranchOutcomeDefinition? selectedBranchOutcome,
        string trimmedReason,
        DateTimeOffset now) {
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
            stepRun.BlockedReason = trimmedReason;
            stepRun.BlockedMinutes = Math.Max(stepRun.BlockedMinutes, 15);
        }

        if (request.TargetStatus == ProcessStepRunStatus.Refused) {
            stepRun.RefusalReason = trimmedReason;
            stepRun.DecisionSummary = "Safe refusal recorded.";
        }

        if (request.TargetStatus == ProcessStepRunStatus.Failed) {
            stepRun.ExceptionSummary = trimmedReason;
            stepRun.ReworkCount += 1;
        }

        if (selectedBranchOutcome is not null) {
            stepRun.SelectedBranchOutcomeId = selectedBranchOutcome.Id;
            stepRun.SelectedBranchOutcomeTitle = selectedBranchOutcome.Title;
            stepRun.DecisionSummary = $"Selected branch outcome: {selectedBranchOutcome.Title}.";
        }

        if (request.TargetStatus == ProcessStepRunStatus.Skipped) {
            stepRun.DecisionSummary = string.IsNullOrWhiteSpace(trimmedReason)
                ? "Skipped."
                : trimmedReason;
        }

        stepRun.Status = request.TargetStatus;
    }

    private sealed record ProcessRuntimeTransitionContext(
        ProcessStepRun StepRun,
        ProcessRun Run,
        ProcessStepDefinition CurrentStepDefinition,
        IReadOnlyList<ProcessStepDefinition> StepDefinitions,
        IReadOnlyDictionary<Guid, ProcessStepDefinition> StepDefinitionsById,
        IReadOnlyDictionary<Guid, List<ProcessStepDependencyDefinition>> StepDependenciesByStepId,
        IReadOnlyDictionary<Guid, List<ProcessStepBranchOutcomeDefinition>> BranchOutcomesByStepId,
        IReadOnlyList<ProcessStepRun> PersistedStepRuns);
}

