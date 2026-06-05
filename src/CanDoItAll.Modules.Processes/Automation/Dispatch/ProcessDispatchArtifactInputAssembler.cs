using DispatchArtifactInput = CanDoItAll.Modules.Processes.ProcessRunAutomationDispatchService.DispatchArtifactInput;
using DispatchArtifactReference = CanDoItAll.Modules.Processes.ProcessRunAutomationDispatchService.DispatchArtifactReference;

namespace CanDoItAll.Modules.Processes;

internal static class ProcessDispatchArtifactInputAssembler
{
    public static IReadOnlyList<DispatchArtifactInput> BuildResolvedArtifactInputs(
        IReadOnlyList<ProcessStepArtifactInputDefinition> configuredInputs,
        IReadOnlyDictionary<Guid, ProcessArtifactExpectation> artifactExpectationsById,
        IReadOnlyDictionary<Guid, ProcessStepDefinition> sourceStepsById,
        IReadOnlyDictionary<Guid, IReadOnlyList<ProcessStepRun>> stepRunsByDefinitionId,
        IReadOnlyList<ProcessArtifactRecord> existingArtifacts)
    {
        if (configuredInputs.Count == 0)
        {
            return [];
        }

        var resolvedInputs = new List<DispatchArtifactInput>(configuredInputs.Count);
        foreach (var configuredInput in configuredInputs)
        {
            if (!artifactExpectationsById.TryGetValue(configuredInput.ArtifactExpectationId, out var artifactExpectation))
            {
                continue;
            }

            sourceStepsById.TryGetValue(artifactExpectation.StepDefinitionId, out var sourceStepDefinition);
            stepRunsByDefinitionId.TryGetValue(artifactExpectation.StepDefinitionId, out var sourceStepRuns);
            var sourceStepRun = sourceStepRuns?
                .OrderByDescending(item => item.Sequence)
                .FirstOrDefault();
            var sourceStepRunIds = sourceStepRuns?
                .Select(item => item.Id)
                .ToHashSet()
                ?? [];
            var sourceProcessRunIds = sourceStepRuns?
                .Select(item => item.ProcessRunId)
                .ToHashSet()
                ?? [];
            var matchingArtifacts = existingArtifacts
                .Where(item =>
                    IsCurrentRunUpstreamArtifactInput(item, sourceStepRunIds, sourceProcessRunIds) &&
                    ProcessRunAutomationDispatchService.SatisfiesExpectedArtifactInput(item, artifactExpectation))
                .OrderByDescending(item => item.CreatedAtUtc)
                .Take(3)
                .Select(item => new DispatchArtifactReference(
                    item.Title,
                    item.ArtifactKind.ToString(),
                    item.ManagedStoragePath,
                    item.ReviewSummary,
                    item.ProvenanceSummary))
                .ToList();

            resolvedInputs.Add(new DispatchArtifactInput(
                sourceStepDefinition?.Title ?? "Unknown upstream step",
                artifactExpectation.Title,
                artifactExpectation.Id,
                artifactExpectation.StepDefinitionId,
                sourceStepRun?.Id,
                sourceStepRun?.ConcurrencyToken,
                sourceStepRun?.Status,
                sourceStepRun?.CurrentExecutorPartyId.HasValue == true,
                matchingArtifacts));
        }

        return resolvedInputs;
    }

    public static bool IsCurrentRunUpstreamArtifactInput(
        ProcessArtifactRecord artifact,
        IReadOnlySet<Guid> sourceStepRunIds,
        IReadOnlySet<Guid> sourceProcessRunIds)
    {
        if (!artifact.StepRunId.HasValue ||
            !sourceStepRunIds.Contains(artifact.StepRunId.Value) ||
            !sourceProcessRunIds.Contains(artifact.ProcessRunId))
        {
            return false;
        }

        return ProcessArtifactLineageValidator
            .ValidateManagedStorageBoundary(artifact, artifact.ProcessRunId)
            .IsCurrentRun;
    }

    public static IReadOnlyList<DispatchArtifactInput> PrepareArtifactInputsForPrompt(
        IReadOnlyList<DispatchArtifactInput> artifactInputs,
        Func<string, string> prepareManagedArtifactPath)
    {
        ArgumentNullException.ThrowIfNull(prepareManagedArtifactPath);

        if (artifactInputs.Count == 0)
        {
            return artifactInputs;
        }

        var preparedInputs = new List<DispatchArtifactInput>(artifactInputs.Count);
        foreach (var artifactInput in artifactInputs)
        {
            var preparedArtifacts = new List<DispatchArtifactReference>(artifactInput.Artifacts.Count);
            foreach (var artifact in artifactInput.Artifacts)
            {
                var preparedPath = prepareManagedArtifactPath(artifact.ManagedStoragePath);
                preparedArtifacts.Add(string.Equals(preparedPath, artifact.ManagedStoragePath, StringComparison.OrdinalIgnoreCase)
                    ? artifact
                    : artifact with
                    {
                        ManagedStoragePath = preparedPath
                    });
            }

            preparedInputs.Add(new DispatchArtifactInput(
                artifactInput.SourceStepTitle,
                artifactInput.ExpectedArtifactTitle,
                artifactInput.ArtifactExpectationId,
                artifactInput.SourceStepDefinitionId,
                artifactInput.SourceStepRunId,
                artifactInput.SourceStepRunConcurrencyToken,
                artifactInput.SourceStepRunStatus,
                artifactInput.SourceStepHasAgentExecutor,
                preparedArtifacts));
        }

        return preparedInputs;
    }
}
