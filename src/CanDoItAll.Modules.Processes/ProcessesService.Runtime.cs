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
}

