using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.Processes;

public sealed partial class ProcessDevelopmentSeedService
{
    private async Task<Result> EnsureAssignmentAsync(
        Guid runId,
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
            ? string.Empty
            : displayName.Trim();
        var assignments = await processesService.ListAssignmentsAsync(runId, cancellationToken);
        var existingAssignment = assignments.FirstOrDefault(item =>
            item.RoleRequirementId == roleRequirementId &&
            item.StepDefinitionId == stepDefinitionId &&
            string.Equals(item.DisplayName ?? string.Empty, normalizedDisplayName, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(item.ExecutorKind ?? string.Empty, executorKind ?? string.Empty, StringComparison.OrdinalIgnoreCase) &&
            item.IsFallback == isFallback);
        if (existingAssignment is not null)
        {
            return Result.Success();
        }

        return await processesService.ResolveAssignmentAsync(
            new ProcessAssignmentResolutionRequest
            {
                ProcessRunId = runId,
                RoleRequirementId = roleRequirementId,
                StepDefinitionId = stepDefinitionId,
                DisplayName = normalizedDisplayName,
                ExecutorKind = string.IsNullOrWhiteSpace(executorKind) ? "person" : executorKind.Trim(),
                BindingReason = bindingReason?.Trim() ?? string.Empty,
                IsFallback = isFallback
            },
            cancellationToken);
    }

    private async Task<Result> EnsureStepStatusAsync(
        Guid runId,
        Guid stepRunId,
        ProcessStepRunStatus targetStatus,
        Guid? selectedBranchOutcomeId,
        string reason,
        string decidedBy,
        CancellationToken cancellationToken)
    {
        var stepRun = (await processesService.ListStepRunsAsync(runId, cancellationToken))
            .FirstOrDefault(item => item.Id == stepRunId);
        if (stepRun is null)
        {
            return Result.Success();
        }

        if (stepRun.Status == targetStatus &&
            (!selectedBranchOutcomeId.HasValue || stepRun.SelectedBranchOutcomeId == selectedBranchOutcomeId))
        {
            return Result.Success();
        }

        if (stepRun.Status == ProcessStepRunStatus.Skipped &&
            targetStatus != ProcessStepRunStatus.Skipped)
        {
            return Result.Failure(
                Error.Validation(
                    $"Baseline seeding cannot transition skipped step '{stepRun.Title}' to {targetStatus}. Skip reason: {stepRun.DecisionSummary}",
                    "processes.seed-step-already-skipped"));
        }

        var transitionSequenceResult = BuildTransitionSequence(stepRun.Status, targetStatus);
        if (transitionSequenceResult.IsFailure)
        {
            return Result.Failure(
                transitionSequenceResult.Errors
                    .Select(error =>
                        Error.Validation(
                            $"{error.Message} Step '{stepRun.Title}' in run '{runId:D}' triggered the invalid baseline transition.",
                            error.Code))
                    .ToArray());
        }

        var transitionSequence = transitionSequenceResult.Value;
        if (transitionSequence is null)
        {
            return Result.Failure(
                Error.Validation(
                    "Baseline seeding could not resolve a transition sequence.",
                    "processes.seed-transition-sequence-missing"));
        }

        foreach (var transitionStatus in transitionSequence)
        {
            var transitionResult = await processesService.TransitionStepAsync(
                new ProcessStepTransitionRequest
                {
                    StepRunId = stepRunId,
                    TargetStatus = transitionStatus,
                    SelectedBranchOutcomeId = transitionStatus == targetStatus ? selectedBranchOutcomeId : null,
                    Reason = transitionStatus == targetStatus ? reason?.Trim() ?? string.Empty : string.Empty,
                    DecidedBy = string.IsNullOrWhiteSpace(decidedBy) ? "process-template-pack" : decidedBy.Trim()
                },
                cancellationToken);
            if (transitionResult.IsFailure)
            {
                return transitionResult;
            }
        }

        return Result.Success();
    }

    private async Task<Result> EnsureArtifactAsync(
        Guid runId,
        Guid? stepRunId,
        ProcessArtifactKind artifactKind,
        string title,
        ProcessArtifactTrustStatus trustStatus,
        ProcessSensitivityLevel sensitivityLevel,
        string provenanceSummary,
        string allowedFutureUsageSummary,
        string reviewSummary,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return Result.Success();
        }

        var artifacts = await processesService.ListArtifactsAsync(runId, cancellationToken);
        var existingArtifact = artifacts.FirstOrDefault(item =>
            string.Equals(item.Title, title.Trim(), StringComparison.OrdinalIgnoreCase) &&
            item.ArtifactKind == artifactKind);
        if (existingArtifact is not null)
        {
            return Result.Success();
        }

        var result = await processesService.RecordArtifactAsync(
            new ProcessArtifactRecordRequest
            {
                ProcessRunId = runId,
                StepRunId = stepRunId,
                ArtifactKind = artifactKind,
                Title = title.Trim(),
                TrustStatus = trustStatus,
                SensitivityLevel = sensitivityLevel,
                ProvenanceSummary = provenanceSummary?.Trim() ?? string.Empty,
                AllowedFutureUsageSummary = allowedFutureUsageSummary?.Trim() ?? string.Empty,
                ReviewSummary = reviewSummary?.Trim() ?? string.Empty
            },
            cancellationToken);
        return result.IsFailure
            ? Result.Failure(result.Errors.ToArray())
            : Result.Success();
    }

    private static Result<IReadOnlyList<ProcessStepRunStatus>> BuildTransitionSequence(
        ProcessStepRunStatus currentStatus,
        ProcessStepRunStatus targetStatus)
    {
        if (currentStatus == targetStatus)
        {
            return Result<IReadOnlyList<ProcessStepRunStatus>>.Success([]);
        }

        if (ProcessStepRunTransitions.IsAllowed(currentStatus, targetStatus))
        {
            return Result<IReadOnlyList<ProcessStepRunStatus>>.Success([targetStatus]);
        }

        if (currentStatus == ProcessStepRunStatus.Ready &&
            targetStatus is ProcessStepRunStatus.Completed or ProcessStepRunStatus.Failed)
        {
            return Result<IReadOnlyList<ProcessStepRunStatus>>.Success(
            [
                ProcessStepRunStatus.InProgress,
                targetStatus
            ]);
        }

        if (currentStatus == ProcessStepRunStatus.Blocked &&
            targetStatus is ProcessStepRunStatus.Completed or ProcessStepRunStatus.Failed)
        {
            return Result<IReadOnlyList<ProcessStepRunStatus>>.Success(
            [
                ProcessStepRunStatus.InProgress,
                targetStatus
            ]);
        }

        return Result<IReadOnlyList<ProcessStepRunStatus>>.Failure(
            Error.Validation(
                $"Baseline seeding cannot move a step from {currentStatus} to {targetStatus} without violating runtime transition rules.",
                "processes.seed-transition-path-not-found"));
    }
}
