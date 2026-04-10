using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.Processes;

public sealed partial class ProcessDevelopmentSeedService
{
    private async Task<Result> EnsureScenarioRuntimeStateAsync(
        ProcessSeedScenario scenario,
        Guid runId,
        CancellationToken cancellationToken)
    {
        return scenario.Key switch
        {
            ProcessSeedScenarioKeys.SoftwareDelivery => await SeedSoftwareDeliveryRuntimeAsync(scenario, runId, cancellationToken),
            ProcessSeedScenarioKeys.HotfixRollout => await SeedHotfixRuntimeAsync(scenario, runId, cancellationToken),
            ProcessSeedScenarioKeys.CustomerOnboarding => await SeedCustomerOnboardingRuntimeAsync(scenario, runId, cancellationToken),
            ProcessSeedScenarioKeys.IncidentResponse => await SeedIncidentResponseRuntimeAsync(scenario, runId, cancellationToken),
            _ => Result.Success()
        };
    }

    private async Task<Result> SeedCustomerOnboardingRuntimeAsync(
        ProcessSeedScenario scenario,
        Guid runId,
        CancellationToken cancellationToken)
    {
        var stepRuns = await processesService.ListStepRunsAsync(runId, cancellationToken);
        var assignments = await processesService.ListAssignmentsAsync(runId, cancellationToken);
        var artifacts = await processesService.ListArtifactsAsync(runId, cancellationToken);

        if (stepRuns.Count < 3 || scenario.Roles.Count < 3)
        {
            return Result.Success();
        }

        var accountOwnerRoleId = scenario.Roles[0].Id ?? Guid.Empty;
        var staffingManagerRoleId = scenario.Roles[1].Id ?? Guid.Empty;
        var kickoffLeadRoleId = scenario.Roles[2].Id ?? Guid.Empty;

        var assignmentResult = await EnsureAssignmentAsync(
            runId,
            assignments,
            accountOwnerRoleId,
            stepRuns[0].StepDefinitionId,
            "Lucia Marin",
            "person",
            "Commercial owner for the retained customer account.",
            false,
            cancellationToken);
        if (assignmentResult.IsFailure)
        {
            return assignmentResult;
        }

        assignmentResult = await EnsureAssignmentAsync(
            runId,
            assignments,
            staffingManagerRoleId,
            stepRuns[1].StepDefinitionId,
            "Dmitri Volkov",
            "person",
            "Staffing manager selected for delivery-capacity review.",
            false,
            cancellationToken);
        if (assignmentResult.IsFailure)
        {
            return assignmentResult;
        }

        assignmentResult = await EnsureAssignmentAsync(
            runId,
            assignments,
            kickoffLeadRoleId,
            stepRuns[2].StepDefinitionId,
            "Jana Keller",
            "person",
            "Kickoff lead confirmed as the accountable delivery owner for launch readiness.",
            false,
            cancellationToken);
        if (assignmentResult.IsFailure)
        {
            return assignmentResult;
        }

        var transitionResult = await EnsureStepStatusAsync(
            runId,
            sequence: 0,
            ProcessStepRunStatus.Completed,
            "Commercial intake package was completed with customer target dates, scope exclusions, and stakeholder map.",
            "seed-service/customer-onboarding",
            cancellationToken);
        if (transitionResult.IsFailure)
        {
            return transitionResult;
        }

        transitionResult = await EnsureStepStatusAsync(
            runId,
            sequence: 1,
            ProcessStepRunStatus.Completed,
            "Staffing review finished with a primary delivery lead and fallback staffing recommendation.",
            "seed-service/customer-onboarding",
            cancellationToken);
        if (transitionResult.IsFailure)
        {
            return transitionResult;
        }

        transitionResult = await EnsureStepStatusAsync(
            runId,
            sequence: 2,
            ProcessStepRunStatus.Completed,
            "Kickoff readiness was approved after commercial, staffing, and governance evidence aligned.",
            "seed-service/customer-onboarding",
            cancellationToken);
        if (transitionResult.IsFailure)
        {
            return transitionResult;
        }

        return await EnsureArtifactAsync(
            runId,
            artifacts,
            stepRuns[2].Id,
            ProcessArtifactKind.Decision,
            "Kickoff readiness approval memo",
            ProcessArtifactTrustStatus.Approved,
            ProcessSensitivityLevel.Internal,
            "Governance board approval note generated for the seeded onboarding scenario.",
            "Reusable for customer kickoff preparation, later delivery audits, and onboarding playbook review.",
            "Approved by seeded operations governance board on behalf of delivery leadership.",
            cancellationToken);
    }

    private async Task<Result> SeedIncidentResponseRuntimeAsync(
        ProcessSeedScenario scenario,
        Guid runId,
        CancellationToken cancellationToken)
    {
        var stepRuns = await processesService.ListStepRunsAsync(runId, cancellationToken);
        var assignments = await processesService.ListAssignmentsAsync(runId, cancellationToken);
        var artifacts = await processesService.ListArtifactsAsync(runId, cancellationToken);

        if (stepRuns.Count < 3 || scenario.Roles.Count < 3)
        {
            return Result.Success();
        }

        var triageLeadRoleId = scenario.Roles[0].Id ?? Guid.Empty;
        var resolverRoleId = scenario.Roles[1].Id ?? Guid.Empty;
        var approverRoleId = scenario.Roles[2].Id ?? Guid.Empty;

        var assignmentResult = await EnsureAssignmentAsync(
            runId,
            assignments,
            triageLeadRoleId,
            stepRuns[0].StepDefinitionId,
            "Noah Petrov",
            "person",
            "Primary incident triage lead for the managed-services rota.",
            false,
            cancellationToken);
        if (assignmentResult.IsFailure)
        {
            return assignmentResult;
        }

        assignmentResult = await EnsureAssignmentAsync(
            runId,
            assignments,
            resolverRoleId,
            stepRuns[1].StepDefinitionId,
            "CanDoItAll Recovery Agent",
            "agent",
            "Agent-assisted diagnostics are allowed for the technical investigation stage.",
            false,
            cancellationToken);
        if (assignmentResult.IsFailure)
        {
            return assignmentResult;
        }

        assignmentResult = await EnsureAssignmentAsync(
            runId,
            assignments,
            approverRoleId,
            stepRuns[2].StepDefinitionId,
            "Mila Andrejevic",
            "person",
            "Emergency escalation approver for non-standard remediation paths.",
            false,
            cancellationToken);
        if (assignmentResult.IsFailure)
        {
            return assignmentResult;
        }

        var transitionResult = await EnsureStepStatusAsync(
            runId,
            sequence: 0,
            ProcessStepRunStatus.Completed,
            "The incident was acknowledged, severity classified, and customer communications opened within the target SLA.",
            "seed-service/incident-response",
            cancellationToken);
        if (transitionResult.IsFailure)
        {
            return transitionResult;
        }

        transitionResult = await EnsureStepStatusAsync(
            runId,
            sequence: 1,
            ProcessStepRunStatus.Blocked,
            "The diagnosis is blocked until the managed database telemetry export completes and the production trace package is reviewed.",
            "seed-service/incident-response",
            cancellationToken);
        if (transitionResult.IsFailure)
        {
            return transitionResult;
        }

        return await EnsureArtifactAsync(
            runId,
            artifacts,
            stepRuns[1].Id,
            ProcessArtifactKind.Evidence,
            "Database trace and customer impact correlation pack",
            ProcessArtifactTrustStatus.ReviewRequired,
            ProcessSensitivityLevel.Confidential,
            "Telemetry snapshot exported from the managed-services evidence lane for blocked-incident review.",
            "Reusable for diagnosis completion, escalation approval, and post-incident forensics only.",
            "Needs human confirmation because the trace package contains partial customer-specific identifiers.",
            cancellationToken);
    }

    private async Task<Result> EnsureAssignmentAsync(
        Guid runId,
        IReadOnlyList<ProcessRunAssignmentViewModel> existingAssignments,
        Guid roleRequirementId,
        Guid? stepDefinitionId,
        string? displayName,
        string executorKind,
        string bindingReason,
        bool isFallback,
        CancellationToken cancellationToken)
    {
        if (roleRequirementId == Guid.Empty)
        {
            return Result.Success();
        }

        var normalizedDisplayName = string.IsNullOrWhiteSpace(displayName)
            ? "Unassigned role"
            : displayName.Trim();
        var existingAssignment = existingAssignments.FirstOrDefault(item =>
            item.RoleRequirementId == roleRequirementId &&
            item.StepDefinitionId == stepDefinitionId);
        if (existingAssignment is not null &&
            string.Equals(existingAssignment.DisplayName, normalizedDisplayName, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(existingAssignment.ExecutorKind, executorKind, StringComparison.OrdinalIgnoreCase) &&
            existingAssignment.IsFallback == isFallback)
        {
            return Result.Success();
        }

        return await processesService.ResolveAssignmentAsync(
            new ProcessAssignmentResolutionRequest
            {
                ProcessRunId = runId,
                RoleRequirementId = roleRequirementId,
                StepDefinitionId = stepDefinitionId,
                DisplayName = displayName ?? string.Empty,
                ExecutorKind = executorKind,
                BindingReason = bindingReason,
                IsFallback = isFallback
            },
            cancellationToken);
    }

    private async Task<Result> EnsureArtifactAsync(
        Guid runId,
        IReadOnlyList<ProcessArtifactViewModel> existingArtifacts,
        Guid stepRunId,
        ProcessArtifactKind artifactKind,
        string title,
        ProcessArtifactTrustStatus trustStatus,
        ProcessSensitivityLevel sensitivityLevel,
        string provenanceSummary,
        string allowedFutureUsageSummary,
        string reviewSummary,
        CancellationToken cancellationToken)
    {
        if (existingArtifacts.Any(item => string.Equals(item.Title, title, StringComparison.OrdinalIgnoreCase)))
        {
            return Result.Success();
        }

        var recordResult = await processesService.RecordArtifactAsync(
            new ProcessArtifactRecordRequest
            {
                ProcessRunId = runId,
                StepRunId = stepRunId,
                ArtifactKind = artifactKind,
                Title = title,
                TrustStatus = trustStatus,
                SensitivityLevel = sensitivityLevel,
                ProvenanceSummary = provenanceSummary,
                AllowedFutureUsageSummary = allowedFutureUsageSummary,
                ReviewSummary = reviewSummary
            },
            cancellationToken);
        return recordResult.IsFailure
            ? Result.Failure(recordResult.Errors.ToArray())
            : Result.Success();
    }

    private async Task<Result> EnsureStepStatusAsync(
        Guid runId,
        int sequence,
        ProcessStepRunStatus targetStatus,
        string reason,
        string decidedBy,
        CancellationToken cancellationToken)
    {
        var stepRun = (await processesService.ListStepRunsAsync(runId, cancellationToken))
            .FirstOrDefault(item => item.Sequence == sequence);
        if (stepRun is null || stepRun.Status == targetStatus)
        {
            return Result.Success();
        }

        if (stepRun.Status is ProcessStepRunStatus.Completed or ProcessStepRunStatus.Refused or ProcessStepRunStatus.Failed or ProcessStepRunStatus.Skipped)
        {
            return Result.Success();
        }

        if (targetStatus == ProcessStepRunStatus.Completed)
        {
            if (stepRun.Status is ProcessStepRunStatus.Ready or ProcessStepRunStatus.Blocked)
            {
                var startResult = await processesService.TransitionStepAsync(
                    new ProcessStepTransitionRequest
                    {
                        StepRunId = stepRun.Id,
                        TargetStatus = ProcessStepRunStatus.InProgress,
                        Reason = $"Seed progression for {reason}",
                        DecidedBy = decidedBy
                    },
                    cancellationToken);
                if (startResult.IsFailure)
                {
                    return startResult;
                }

                stepRun = (await processesService.ListStepRunsAsync(runId, cancellationToken))
                    .First(item => item.Sequence == sequence);
            }

            if (stepRun.Status is ProcessStepRunStatus.WaitingApproval or ProcessStepRunStatus.InProgress)
            {
                return await processesService.TransitionStepAsync(
                    new ProcessStepTransitionRequest
                    {
                        StepRunId = stepRun.Id,
                        TargetStatus = ProcessStepRunStatus.Completed,
                        Reason = reason,
                        DecidedBy = decidedBy
                    },
                    cancellationToken);
            }

            return Result.Success();
        }

        if (targetStatus == ProcessStepRunStatus.Failed)
        {
            if (stepRun.Status is ProcessStepRunStatus.Ready or ProcessStepRunStatus.WaitingApproval)
            {
                var startResult = await processesService.TransitionStepAsync(
                    new ProcessStepTransitionRequest
                    {
                        StepRunId = stepRun.Id,
                        TargetStatus = ProcessStepRunStatus.InProgress,
                        Reason = $"Seed progression for {reason}",
                        DecidedBy = decidedBy
                    },
                    cancellationToken);
                if (startResult.IsFailure)
                {
                    return startResult;
                }

                stepRun = (await processesService.ListStepRunsAsync(runId, cancellationToken))
                    .First(item => item.Sequence == sequence);
            }

            if (stepRun.Status is ProcessStepRunStatus.InProgress or ProcessStepRunStatus.Blocked)
            {
                return await processesService.TransitionStepAsync(
                    new ProcessStepTransitionRequest
                    {
                        StepRunId = stepRun.Id,
                        TargetStatus = ProcessStepRunStatus.Failed,
                        Reason = reason,
                        DecidedBy = decidedBy
                    },
                    cancellationToken);
            }

            return Result.Success();
        }

        if (stepRun.Status is ProcessStepRunStatus.Ready or ProcessStepRunStatus.WaitingApproval or ProcessStepRunStatus.InProgress or ProcessStepRunStatus.Blocked)
        {
            return await processesService.TransitionStepAsync(
                new ProcessStepTransitionRequest
                {
                    StepRunId = stepRun.Id,
                    TargetStatus = targetStatus,
                    Reason = reason,
                    DecidedBy = decidedBy
                },
                cancellationToken);
        }

        return Result.Success();
    }
}
