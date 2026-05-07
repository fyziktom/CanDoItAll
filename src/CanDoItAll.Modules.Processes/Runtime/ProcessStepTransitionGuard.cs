using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.Processes;

internal static class ProcessStepTransitionGuard
{
    public static Result<ProcessStepTransitionResolution> ValidateAndResolve(
        ProcessStepTransitionRequest request,
        ProcessStepRun stepRun,
        ProcessStepDefinition currentStepDefinition,
        IReadOnlyList<ProcessStepDefinition> stepDefinitions,
        IReadOnlyDictionary<Guid, List<ProcessStepDependencyDefinition>> stepDependenciesByStepId,
        IReadOnlyList<ProcessStepBranchOutcomeDefinition> availableBranchOutcomes)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(stepRun);
        ArgumentNullException.ThrowIfNull(currentStepDefinition);
        ArgumentNullException.ThrowIfNull(stepDefinitions);
        ArgumentNullException.ThrowIfNull(stepDependenciesByStepId);
        ArgumentNullException.ThrowIfNull(availableBranchOutcomes);

        if (!IsAllowedTransition(stepRun, request))
        {
            return Result<ProcessStepTransitionResolution>.Failure(
                Error.Validation(
                    $"Cannot move step from {stepRun.Status} to {request.TargetStatus}.",
                    "processes.invalid-step-transition"));
        }

        if (request.SelectedBranchOutcomeId.HasValue && request.TargetStatus != ProcessStepRunStatus.Completed)
        {
            return Result<ProcessStepTransitionResolution>.Failure(
                Error.Validation(
                    "Branch outcomes can only be selected when completing a step.",
                    "processes.branch-outcome-invalid-transition"));
        }

        ProcessStepBranchOutcomeDefinition? selectedBranchOutcome = null;
        if (request.TargetStatus == ProcessStepRunStatus.Completed)
        {
            var hasConditionalDependents = stepDefinitions.Any(
                item => ProcessStepDependencyCollection.GetPersistedDependencies(item.Id, stepDependenciesByStepId)
                    .Any(
                        dependency => dependency.DependsOnStepId == currentStepDefinition.Id &&
                            dependency.DependsOnBranchOutcomeId.HasValue));
            if (hasConditionalDependents && !request.SelectedBranchOutcomeId.HasValue)
            {
                return Result<ProcessStepTransitionResolution>.Failure(
                    Error.Validation(
                        "Completing this step requires selecting a branch outcome.",
                        "processes.branch-outcome-required"));
            }

            if (request.SelectedBranchOutcomeId.HasValue)
            {
                selectedBranchOutcome = availableBranchOutcomes.SingleOrDefault(item => item.Id == request.SelectedBranchOutcomeId.Value);
                if (selectedBranchOutcome is null)
                {
                    return Result<ProcessStepTransitionResolution>.Failure(
                        Error.Validation(
                            "Selected branch outcome is not valid for this step.",
                            "processes.branch-outcome-invalid"));
                }
            }
        }

        return Result<ProcessStepTransitionResolution>.Success(new ProcessStepTransitionResolution(selectedBranchOutcome));
    }

    private static bool IsAllowedTransition(ProcessStepRun stepRun, ProcessStepTransitionRequest request)
    {
        if (ProcessStepRunTransitions.IsAllowed(stepRun.Status, request.TargetStatus))
        {
            return true;
        }

        return request.AllowCompletedAgentRerun &&
            stepRun.Status == ProcessStepRunStatus.Completed &&
            request.TargetStatus == ProcessStepRunStatus.InProgress &&
            stepRun.CurrentExecutorPartyId.HasValue;
    }
}

internal sealed record ProcessStepTransitionResolution(ProcessStepBranchOutcomeDefinition? SelectedBranchOutcome);
