using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.Processes;

public sealed partial class ProcessDevelopmentSeedService
{
    private async Task<Result> EnsureScenarioRuntimeStateAsync(
        ProcessTemplateBaselineScenario scenario,
        Guid runId,
        CancellationToken cancellationToken)
    {
        var runtimeBindings = await processesService.GetRuntimeBindingCatalogAsync(runId, cancellationToken);
        if (runtimeBindings is null)
        {
            return Result.Failure(
                Error.Validation(
                    $"Process run '{runId:D}' was not found while seeding baseline scenario '{scenario.Key}'.",
                    "processes.seed-run-not-found"));
        }

        var stepRuns = await processesService.ListStepRunsAsync(runId, cancellationToken);
        var stepRunIdsByStepKey = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        var stepRunsByStepKey = new Dictionary<string, ProcessStepRunViewModel>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in runtimeBindings.StepDefinitionIdsByKey)
        {
            var stepRun = stepRuns.FirstOrDefault(item => item.StepDefinitionId == pair.Value);
            if (stepRun is not null)
            {
                stepRunIdsByStepKey[pair.Key] = stepRun.Id;
                stepRunsByStepKey[pair.Key] = stepRun;
            }
        }

        foreach (var assignment in scenario.Assignments)
        {
            var roleRequirementIdResult = ResolveRoleRequirementId(
                runtimeBindings.RoleRequirementIdsByKey,
                assignment.RoleKey,
                scenario.Key);
            if (roleRequirementIdResult.IsFailure)
            {
                return Result.Failure(roleRequirementIdResult.Errors.ToArray());
            }

            var stepDefinitionIdResult = ResolveOptionalStepDefinitionId(
                runtimeBindings.StepDefinitionIdsByKey,
                assignment.StepKey,
                scenario.Key,
                "assignment");
            if (stepDefinitionIdResult.IsFailure)
            {
                return Result.Failure(stepDefinitionIdResult.Errors.ToArray());
            }

            var assignmentResult = await EnsureAssignmentAsync(
                runId,
                roleRequirementIdResult.Value,
                stepDefinitionIdResult.Value,
                assignment.DisplayName,
                assignment.ExecutorKind,
                assignment.BindingReason,
                assignment.IsFallback,
                cancellationToken);
            if (assignmentResult.IsFailure)
            {
                return assignmentResult;
            }
        }

        foreach (var artifact in scenario.Artifacts)
        {
            var stepRunIdResult = ResolveOptionalStepRunId(stepRunIdsByStepKey, artifact.StepKey, scenario.Key, "artifact");
            if (stepRunIdResult.IsFailure)
            {
                return Result.Failure(stepRunIdResult.Errors.ToArray());
            }

            var artifactOutputs = string.IsNullOrWhiteSpace(artifact.StepKey)
                ? []
                : stepRunsByStepKey.TryGetValue(artifact.StepKey, out var stepRun)
                    ? stepRun.ArtifactOutputs
                    : [];
            var artifactResult = await EnsureArtifactAsync(
                runId,
                stepRunIdResult.Value,
                artifactOutputs,
                ParseEnum(artifact.ArtifactKind, ProcessArtifactKind.Evidence),
                artifact.Title,
                ParseArtifactTrustStatus(artifact.TrustStatus),
                ParseEnum(artifact.SensitivityLevel, ProcessSensitivityLevel.Internal),
                artifact.ProvenanceSummary,
                artifact.AllowedFutureUsageSummary,
                artifact.ReviewSummary,
                cancellationToken);
            if (artifactResult.IsFailure)
            {
                return artifactResult;
            }
        }

        foreach (var transition in scenario.Transitions)
        {
            var stepRunIdResult = ResolveStepRunId(stepRunIdsByStepKey, transition.StepKey, scenario.Key, "transition");
            if (stepRunIdResult.IsFailure)
            {
                return Result.Failure(stepRunIdResult.Errors.ToArray());
            }

            var selectedBranchOutcomeIdResult = ResolveOptionalBranchOutcomeId(
                runtimeBindings.BranchOutcomeIdsByCompositeKey,
                transition.StepKey,
                transition.SelectedBranchOutcomeKey,
                scenario.Key);
            if (selectedBranchOutcomeIdResult.IsFailure)
            {
                return Result.Failure(selectedBranchOutcomeIdResult.Errors.ToArray());
            }

            var transitionResult = await EnsureStepStatusAsync(
                runId,
                stepRunIdResult.Value,
                ParseEnum(transition.TargetStatus, ProcessStepRunStatus.Completed),
                selectedBranchOutcomeIdResult.Value,
                transition.Reason,
                transition.DecidedBy,
                cancellationToken);
            if (transitionResult.IsFailure)
            {
                return transitionResult;
            }
        }

        return Result.Success();
    }

    private static Result<Guid> ResolveRoleRequirementId(
        IReadOnlyDictionary<string, Guid> roleIdsByKey,
        string? roleKey,
        string scenarioKey)
    {
        if (string.IsNullOrWhiteSpace(roleKey))
        {
            return Result<Guid>.Failure(
                Error.Validation(
                    $"Baseline scenario '{scenarioKey}' contains an assignment without a role key.",
                    "processes.seed-role-key-required"));
        }

        if (roleIdsByKey.TryGetValue(roleKey, out var roleRequirementId))
        {
            return Result<Guid>.Success(roleRequirementId);
        }

        return Result<Guid>.Failure(
            Error.Validation(
                $"Baseline scenario '{scenarioKey}' references unknown role key '{roleKey}'.",
                "processes.seed-role-key-not-found"));
    }

    private static Result<Guid?> ResolveOptionalStepDefinitionId(
        IReadOnlyDictionary<string, Guid> stepIdsByKey,
        string? stepKey,
        string scenarioKey,
        string operationName)
    {
        if (string.IsNullOrWhiteSpace(stepKey))
        {
            return Result<Guid?>.Success(null);
        }

        if (stepIdsByKey.TryGetValue(stepKey, out var stepDefinitionId))
        {
            return Result<Guid?>.Success(stepDefinitionId);
        }

        return Result<Guid?>.Failure(
            Error.Validation(
                $"Baseline scenario '{scenarioKey}' references unknown step key '{stepKey}' for {operationName}.",
                "processes.seed-step-key-not-found"));
    }

    private static Result<Guid> ResolveStepRunId(
        IReadOnlyDictionary<string, Guid> stepRunIdsByStepKey,
        string? stepKey,
        string scenarioKey,
        string operationName)
    {
        if (string.IsNullOrWhiteSpace(stepKey))
        {
            return Result<Guid>.Failure(
                Error.Validation(
                    $"Baseline scenario '{scenarioKey}' requires a step key for {operationName}.",
                    "processes.seed-step-key-required"));
        }

        if (stepRunIdsByStepKey.TryGetValue(stepKey, out var stepRunId))
        {
            return Result<Guid>.Success(stepRunId);
        }

        return Result<Guid>.Failure(
            Error.Validation(
                $"Baseline scenario '{scenarioKey}' could not find runtime step '{stepKey}' for {operationName}.",
                "processes.seed-step-run-not-found"));
    }

    private static Result<Guid?> ResolveOptionalStepRunId(
        IReadOnlyDictionary<string, Guid> stepRunIdsByStepKey,
        string? stepKey,
        string scenarioKey,
        string operationName)
    {
        if (string.IsNullOrWhiteSpace(stepKey))
        {
            return Result<Guid?>.Success(null);
        }

        if (stepRunIdsByStepKey.TryGetValue(stepKey, out var stepRunId))
        {
            return Result<Guid?>.Success(stepRunId);
        }

        return Result<Guid?>.Failure(
            Error.Validation(
                $"Baseline scenario '{scenarioKey}' could not find runtime step '{stepKey}' for {operationName}.",
                "processes.seed-step-run-not-found"));
    }

    private static Result<Guid?> ResolveOptionalBranchOutcomeId(
        IReadOnlyDictionary<string, Guid> branchOutcomeIdsByCompositeKey,
        string? stepKey,
        string? branchOutcomeKey,
        string scenarioKey)
    {
        if (string.IsNullOrWhiteSpace(branchOutcomeKey))
        {
            return Result<Guid?>.Success(null);
        }

        if (string.IsNullOrWhiteSpace(stepKey))
        {
            return Result<Guid?>.Failure(
                Error.Validation(
                    $"Baseline scenario '{scenarioKey}' references branch outcome '{branchOutcomeKey}' without a step key.",
                    "processes.seed-branch-step-key-required"));
        }

        var compositeKey = BuildCompositeBranchKey(stepKey, branchOutcomeKey);
        if (branchOutcomeIdsByCompositeKey.TryGetValue(compositeKey, out var branchOutcomeId))
        {
            return Result<Guid?>.Success(branchOutcomeId);
        }

        return Result<Guid?>.Failure(
            Error.Validation(
                $"Baseline scenario '{scenarioKey}' references unknown branch outcome '{branchOutcomeKey}' for step '{stepKey}'.",
                "processes.seed-branch-key-not-found"));
    }

    private static string BuildCompositeBranchKey(string stepKey, string branchOutcomeKey)
    {
        return stepKey.Trim() + "|" + branchOutcomeKey.Trim();
    }
}
