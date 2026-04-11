using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.Processes;

public sealed partial class ProcessDevelopmentSeedService
{
    private async Task<Result> EnsureScenarioRuntimeStateAsync(
        Guid definitionId,
        ProcessTemplateBaselineScenario scenario,
        Guid runId,
        CancellationToken cancellationToken)
    {
        var editor = await processesService.GetEditorAsync(definitionId, cancellationToken: cancellationToken);
        var roleIdsByKey = editor.Roles
            .Where(item => item.Id.HasValue && !string.IsNullOrWhiteSpace(item.Key))
            .ToDictionary(item => item.Key, item => item.Id!.Value, StringComparer.OrdinalIgnoreCase);
        var stepIdsByKey = editor.Steps
            .Where(item => item.Id.HasValue && !string.IsNullOrWhiteSpace(item.Key))
            .ToDictionary(item => item.Key, item => item.Id!.Value, StringComparer.OrdinalIgnoreCase);

        var branchIdsByCompositeKey = editor.Steps
            .Where(step => !string.IsNullOrWhiteSpace(step.Key))
            .SelectMany(
                step => step.BranchOutcomes
                    .Where(outcome => outcome.Id.HasValue && !string.IsNullOrWhiteSpace(outcome.Key))
                    .Select(outcome => new
                    {
                        CompositeKey = BuildCompositeBranchKey(step.Key, outcome.Key),
                        BranchOutcomeId = outcome.Id!.Value
                    }))
            .ToDictionary(item => item.CompositeKey, item => item.BranchOutcomeId, StringComparer.OrdinalIgnoreCase);

        var stepRuns = await processesService.ListStepRunsAsync(runId, cancellationToken);
        var stepRunIdsByStepKey = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in stepIdsByKey)
        {
            var stepRun = stepRuns.FirstOrDefault(item => item.StepDefinitionId == pair.Value);
            if (stepRun is not null)
            {
                stepRunIdsByStepKey[pair.Key] = stepRun.Id;
            }
        }

        foreach (var assignment in scenario.Assignments)
        {
            var roleRequirementId = string.IsNullOrWhiteSpace(assignment.RoleKey)
                ? Guid.Empty
                : roleIdsByKey.GetValueOrDefault(assignment.RoleKey);
            var stepDefinitionId = ResolveOptionalStepDefinitionId(stepIdsByKey, assignment.StepKey);

            var assignmentResult = await EnsureAssignmentAsync(
                runId,
                roleRequirementId,
                stepDefinitionId,
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

        foreach (var transition in scenario.Transitions)
        {
            if (!stepRunIdsByStepKey.TryGetValue(transition.StepKey, out var stepRunId))
            {
                continue;
            }

            var selectedBranchOutcomeId = string.IsNullOrWhiteSpace(transition.SelectedBranchOutcomeKey)
                ? null
                : branchIdsByCompositeKey.GetValueOrDefault(BuildCompositeBranchKey(transition.StepKey, transition.SelectedBranchOutcomeKey));

            var transitionResult = await EnsureStepStatusAsync(
                runId,
                stepRunId,
                ParseEnum(transition.TargetStatus, ProcessStepRunStatus.Completed),
                selectedBranchOutcomeId,
                transition.Reason,
                transition.DecidedBy,
                cancellationToken);
            if (transitionResult.IsFailure)
            {
                return transitionResult;
            }
        }

        foreach (var artifact in scenario.Artifacts)
        {
            var stepRunId = string.IsNullOrWhiteSpace(artifact.StepKey)
                ? null
                : stepRunIdsByStepKey.GetValueOrDefault(artifact.StepKey);

            var artifactResult = await EnsureArtifactAsync(
                runId,
                stepRunId,
                ParseEnum(artifact.ArtifactKind, ProcessArtifactKind.Evidence),
                artifact.Title,
                ParseEnum(artifact.TrustStatus, ProcessArtifactTrustStatus.ReviewRequired),
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

        return Result.Success();
    }

    private static Guid? ResolveOptionalStepDefinitionId(
        IReadOnlyDictionary<string, Guid> stepIdsByKey,
        string stepKey)
    {
        if (string.IsNullOrWhiteSpace(stepKey))
        {
            return null;
        }

        return stepIdsByKey.GetValueOrDefault(stepKey);
    }

    private static string BuildCompositeBranchKey(string stepKey, string branchOutcomeKey)
    {
        return stepKey.Trim() + "|" + branchOutcomeKey.Trim();
    }
}
