using System.Security.Cryptography;
using System.Text;

namespace CanDoItAll.Modules.Processes;

internal sealed record ProcessMissingUpstreamArtifactMaterializationFacts(
    IReadOnlyList<ProcessRouteArtifactInput> MissingInputs,
    ProcessRouteArtifactInput? MaterializationTarget)
{
    public bool HasMissingInputs => MissingInputs.Count > 0;
}

internal static class ProcessMissingUpstreamArtifactMaterializationFactsResolver
{
    public static ProcessMissingUpstreamArtifactMaterializationFacts Create(
        ProcessRunAutomationDispatchService.DispatchCandidate candidate)
    {
        var missingInputs = ResolveMissingInputs(candidate);

        return new ProcessMissingUpstreamArtifactMaterializationFacts(
            missingInputs,
            missingInputs.FirstOrDefault(IsRunnableTarget));
    }

    public static ProcessMissingUpstreamArtifactMaterializationFacts Create(
        ProcessDispatchPreExecutionRouteFacts routeFacts)
    {
        var missingInputs = ResolveMissingInputs(routeFacts);

        return new ProcessMissingUpstreamArtifactMaterializationFacts(
            missingInputs,
            missingInputs.FirstOrDefault(IsRunnableTarget));
    }

    public static IReadOnlyList<ProcessRouteArtifactInput> ResolveMissingInputs(
        ProcessRunAutomationDispatchService.DispatchCandidate candidate)
    {
        return candidate.ArtifactInputs
            .Where(input => input.Artifacts.Count == 0)
            .Select(ToRouteArtifactInput)
            .ToList();
    }

    public static IReadOnlyList<ProcessRouteArtifactInput> ResolveMissingInputs(
        ProcessDispatchPreExecutionRouteFacts routeFacts)
    {
        return routeFacts.ArtifactInputs
            .Where(input => input.Artifacts.Count == 0)
            .ToList();
    }

    private static ProcessRouteArtifactInput ToRouteArtifactInput(
        ProcessRunAutomationDispatchService.DispatchArtifactInput input)
    {
        return new ProcessRouteArtifactInput(
            input.SourceStepTitle,
            input.ExpectedArtifactTitle,
            input.ArtifactExpectationId,
            input.SourceStepDefinitionId,
            input.SourceStepRunId,
            input.SourceStepRunConcurrencyToken,
            input.SourceStepRunStatus,
            input.SourceStepHasAgentExecutor,
            input.Artifacts
                .Select(artifact => new ProcessRouteArtifactReference(
                    artifact.Title,
                    artifact.ArtifactKind,
                    artifact.ManagedStoragePath,
                    artifact.ReviewSummary,
                    artifact.ProvenanceSummary))
                .ToList());
    }

    public static bool IsRunnableTarget(ProcessRouteArtifactInput input)
    {
        return input.SourceStepRunId.HasValue &&
               input.SourceStepRunConcurrencyToken.HasValue &&
               input.SourceStepHasAgentExecutor &&
               input.SourceStepRunStatus is ProcessStepRunStatus.Completed or ProcessStepRunStatus.Blocked or ProcessStepRunStatus.Failed;
    }
}

internal static class ProcessMissingUpstreamArtifactMaterializationBlocker
{
    public static ProcessStepTransitionRequest BuildBlockTransitionRequest(
        Guid stepRunId,
        Guid concurrencyToken,
        string blockReason,
        string automationActor)
    {
        return new ProcessStepTransitionRequest
        {
            StepRunId = stepRunId,
            StepRunConcurrencyToken = concurrencyToken,
            TargetStatus = ProcessStepRunStatus.Blocked,
            Reason = blockReason,
            BlockCause = ProcessStepBlockCause.UpstreamInput,
            DecidedBy = automationActor,
            SuppressAutomationDispatch = true
        };
    }

    public static string BuildBlockReason(
        ProcessDispatchPreExecutionRouteFacts routeFacts,
        ProcessMissingUpstreamArtifactMaterializationFacts facts)
    {
        var missingSummary = string.Join(
            "; ",
            facts.MissingInputs
                .Take(3)
                .Select(input => $"upstream step '{input.SourceStepTitle}' must provide required artifact '{input.ExpectedArtifactTitle}'"));
        var targetSummary = facts.MaterializationTarget is null
            ? "No eligible agent-owned upstream step is available for automatic materialization."
            : $"Automation requested upstream artifact materialization from '{facts.MaterializationTarget.SourceStepTitle}' before retrying this step.";
        return $"Cannot dispatch '{routeFacts.StepRun.Title}' because required upstream artifacts are missing: {missingSummary}. {targetSummary}";
    }
}

internal static class ProcessMissingUpstreamArtifactMaterializationFingerprint
{
    public static string Create(
        ProcessDispatchPreExecutionRouteFacts routeFacts,
        ProcessMissingUpstreamArtifactMaterializationFacts facts)
    {
        var normalizedInputs = facts.MissingInputs
            .OrderBy(input => input.SourceStepDefinitionId)
            .ThenBy(input => input.ArtifactExpectationId)
            .Select(input => string.Join(
                ":",
                input.SourceStepDefinitionId.ToString("D"),
                input.ArtifactExpectationId.ToString("D"),
                input.SourceStepRunId?.ToString("D") ?? string.Empty,
                input.SourceStepRunStatus?.ToString() ?? string.Empty));
        var normalized = string.Join(
            "|",
            "missing-upstream-artifact-materialization",
            routeFacts.Run.Id.ToString("D"),
            routeFacts.StepRun.Id.ToString("D"),
            facts.MaterializationTarget?.SourceStepRunId?.ToString("D") ?? string.Empty,
            string.Join(",", normalizedInputs));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalized))).ToLowerInvariant();
    }
}

internal static class ProcessMissingUpstreamArtifactRerunRequestBuilder
{
    public static ProcessAgentStepRerunRequest BuildRequest(
        ProcessDispatchPreExecutionRouteFacts routeFacts,
        ProcessMissingUpstreamArtifactMaterializationFacts facts)
    {
        var materializationTarget = facts.MaterializationTarget
            ?? throw new ArgumentException("A materialization target is required to build a rerun request.", nameof(facts));

        return new ProcessAgentStepRerunRequest
        {
            StepRunId = materializationTarget.SourceStepRunId!.Value,
            StepRunConcurrencyToken = materializationTarget.SourceStepRunConcurrencyToken,
            OperatorReason = BuildDirective(routeFacts, facts)
        };
    }

    public static string BuildDirective(
        ProcessDispatchPreExecutionRouteFacts routeFacts,
        ProcessMissingUpstreamArtifactMaterializationFacts facts)
    {
        var materializationTarget = facts.MaterializationTarget
            ?? throw new ArgumentException("A materialization target is required to build a rerun directive.", nameof(facts));
        var targetMissingInputs = facts.MissingInputs
            .Where(input => input.SourceStepRunId == materializationTarget.SourceStepRunId)
            .ToList();
        var artifactTitles = targetMissingInputs.Count == 0
            ? materializationTarget.ExpectedArtifactTitle
            : string.Join(", ", targetMissingInputs.Select(input => input.ExpectedArtifactTitle).Distinct(StringComparer.OrdinalIgnoreCase));
        return $"Automatic upstream artifact materialization requested. Downstream step '{routeFacts.StepRun.Title}' cannot proceed because required upstream artifact(s) are missing: {artifactTitles}. Use this step's existing records, artifacts, decisions, and prior execution context to create or repair only the missing required artifact(s). Do not redo unrelated work. When the artifact(s) are recorded, the downstream step will retry from its configured artifact inputs.";
    }
}
