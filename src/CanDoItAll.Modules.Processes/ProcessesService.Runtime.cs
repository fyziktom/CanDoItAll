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

        if (request.SelectedBranchOutcomeId.HasValue && request.TargetStatus != ProcessStepRunStatus.Completed) {
            return Result.Failure(Error.Validation(
                "Branch outcomes can only be selected when completing a step.",
                "processes.branch-outcome-invalid-transition"));
        }

        var stepDefinitions = await dbContext.Set<ProcessStepDefinition>()
            .Where(item => item.ProcessDefinitionVersionId == run.ProcessDefinitionVersionId)
            .ToListAsync(cancellationToken);
        var stepDependencies = await dbContext.Set<ProcessStepDependencyDefinition>()
            .Where(item => stepDefinitions.Select(step => step.Id).Contains(item.StepDefinitionId))
            .OrderBy(item => item.DisplayOrder)
            .ToListAsync(cancellationToken);
        var currentStepDefinition = stepDefinitions.Single(item => item.Id == stepRun.StepDefinitionId);
        var branchOutcomes = await dbContext.Set<ProcessStepBranchOutcomeDefinition>()
            .Where(item => stepDefinitions.Select(step => step.Id).Contains(item.StepDefinitionId))
            .ToListAsync(cancellationToken);
        var branchOutcomesByStepId = branchOutcomes
            .GroupBy(item => item.StepDefinitionId)
            .ToDictionary(group => group.Key, group => group.OrderBy(item => item.DisplayOrder).ToList());
        var stepDefinitionsById = stepDefinitions.ToDictionary(item => item.Id);
        var stepDependenciesByStepId = stepDependencies
            .GroupBy(item => item.StepDefinitionId)
            .ToDictionary(group => group.Key, group => group.OrderBy(item => item.DisplayOrder).ToList());

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

        ProcessStepBranchOutcomeDefinition? selectedBranchOutcome = null;
        if (request.TargetStatus == ProcessStepRunStatus.Completed) {
            var availableBranchOutcomes = branchOutcomesByStepId.GetValueOrDefault(stepRun.StepDefinitionId) ?? [];
            var hasConditionalDependents = stepDefinitions.Any(item =>
                GetPersistedDependencies(item, stepDependenciesByStepId)
                    .Any(dependency => dependency.DependsOnStepId == currentStepDefinition.Id &&
                        dependency.DependsOnBranchOutcomeId.HasValue));
            if (hasConditionalDependents && !request.SelectedBranchOutcomeId.HasValue) {
                return Result.Failure(Error.Validation(
                    "Completing this step requires selecting a branch outcome.",
                    "processes.branch-outcome-required"));
            }

            if (request.SelectedBranchOutcomeId.HasValue) {
                selectedBranchOutcome = availableBranchOutcomes.SingleOrDefault(item => item.Id == request.SelectedBranchOutcomeId.Value);
                if (selectedBranchOutcome is null) {
                    return Result.Failure(Error.Validation(
                        "Selected branch outcome is not valid for this step.",
                        "processes.branch-outcome-invalid"));
                }

                stepRun.SelectedBranchOutcomeId = selectedBranchOutcome.Id;
                stepRun.SelectedBranchOutcomeTitle = selectedBranchOutcome.Title;
                stepRun.DecisionSummary = $"Selected branch outcome: {selectedBranchOutcome.Title}.";
            }
        }

        if (request.TargetStatus == ProcessStepRunStatus.Skipped) {
            stepRun.DecisionSummary = string.IsNullOrWhiteSpace(request.Reason)
                ? "Skipped."
                : request.Reason.Trim();
        }

        stepRun.Status = request.TargetStatus;

        var persistedStepRuns = await dbContext.Set<ProcessStepRun>()
            .Where(item => item.ProcessRunId == run.Id)
            .ToListAsync(cancellationToken);
        var stepRunsByDefinitionId = persistedStepRuns.ToDictionary(item => item.StepDefinitionId);
        stepRunsByDefinitionId[stepRun.StepDefinitionId] = stepRun;

        if (request.TargetStatus == ProcessStepRunStatus.Completed) {
            foreach (var dependentStep in stepDefinitions
                         .Where(item => GetPersistedDependencies(item, stepDependenciesByStepId)
                             .Any(dependency => dependency.DependsOnStepId == currentStepDefinition.Id))
                         .OrderBy(item => item.OrderIndex)) {
                if (!stepRunsByDefinitionId.TryGetValue(dependentStep.Id, out var dependentStepRun)) {
                    continue;
                }

                if (TryResolveImpossibleDependencyReason(
                    dependentStep,
                    stepDefinitionsById,
                    stepRunsByDefinitionId,
                    stepDependenciesByStepId,
                    out var impossibleReason)) {
                    CascadeSkipStepRun(
                        dependentStep,
                        stepDefinitionsById,
                        stepRunsByDefinitionId,
                        stepDependenciesByStepId,
                        impossibleReason,
                        now);
                    continue;
                }

                if (dependentStepRun.Status == ProcessStepRunStatus.Pending &&
                    AreAllDependenciesSatisfied(dependentStep, stepRunsByDefinitionId, stepDependenciesByStepId)) {
                    ActivatePendingStepRun(dependentStepRun, dependentStep, now);
                }
            }
        }

        if (request.TargetStatus == ProcessStepRunStatus.Skipped) {
            CascadeSkipDependents(
                currentStepDefinition,
                stepDefinitionsById,
                stepRunsByDefinitionId,
                stepDependenciesByStepId,
                $"Skipped because upstream step '{stepRun.Title}' was skipped.",
                now);
        }

        run.UpdatedAtUtc = now;
        run.Status = ResolveRunStatus(persistedStepRuns, stepRun);
        if (run.Status is ProcessRunStatus.Completed or ProcessRunStatus.Failed or ProcessRunStatus.Cancelled) {
            run.CompletedAtUtc = now;
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
                ProcessRunId = run.Id,
                StepRunId = stepRun.Id,
                DecisionKind = decisionKind,
                Outcome = decisionOutcome,
                Title = selectedBranchOutcome is null
                    ? $"{stepRun.Title} -> {request.TargetStatus}"
                    : $"{stepRun.Title} -> {request.TargetStatus} / {selectedBranchOutcome.Title}",
                Reason = request.Reason.Trim(),
                PolicyEvaluation = stepRun.DecisionSummary,
                BranchOutcomeId = selectedBranchOutcome?.Id,
                BranchOutcomeTitle = selectedBranchOutcome?.Title ?? string.Empty,
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
                BuildTransitionJournalDescription(stepRun.Title, request.TargetStatus, request.Reason, selectedBranchOutcome?.Title),
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

}

