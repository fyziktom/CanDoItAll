namespace CanDoItAll.Modules.Processes;

internal static class ProcessDispatchRouteModelAdapters
{
    public static ProcessRouteCandidate FromDispatcherCandidate(
        ProcessRunAutomationDispatchService.DispatchCandidate candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        return new ProcessRouteCandidate(
            new ProcessRouteRunSnapshot(
                candidate.Run.Id,
                candidate.Run.Status,
                candidate.Run.OperatingMode,
                candidate.Run.ProcessDefinitionVersionId),
            new ProcessRouteStepSnapshot(
                candidate.StepRun.Id,
                candidate.StepRun.StepDefinitionId,
                candidate.StepRun.Title,
                candidate.StepRun.Status,
                candidate.StepRun.StepKind,
                candidate.StepRun.ConcurrencyToken,
                candidate.StepRun.StartedAtUtc),
            candidate.TechnicalAgentId,
            candidate.RecoveryExecutionRunId,
            candidate.ArtifactInputs
                .Select(input => new ProcessRouteArtifactInput(
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
                        .ToList()))
                .ToList(),
            new DispatcherCandidateSource(candidate));
    }

    public static ProcessRunAutomationDispatchService.DispatchCandidate ToDispatcherCandidate(
        ProcessRouteCandidate candidate)
    {
        return RequireSource<DispatcherCandidateSource>(candidate.Source).Candidate;
    }

    public static ProcessRouteDispatchClaim FromDispatcherClaim(
        ProcessRunAutomationDispatchService.ProcessStepDispatchClaim dispatchClaim)
    {
        return new ProcessRouteDispatchClaim(
            dispatchClaim.StepRunId,
            dispatchClaim.ClaimToken,
            new DispatcherDispatchClaimSource(dispatchClaim));
    }

    public static ProcessRunAutomationDispatchService.ProcessStepDispatchClaim ToDispatcherClaim(
        ProcessRouteDispatchClaim dispatchClaim)
    {
        return RequireSource<DispatcherDispatchClaimSource>(dispatchClaim.Source).DispatchClaim;
    }

    public static ProcessRouteExecutionOutcome FromDispatcherExecutionOutcome(
        ProcessRunAutomationDispatchService.DispatchExecutionOutcome executionOutcome)
    {
        ArgumentNullException.ThrowIfNull(executionOutcome);

        return new ProcessRouteExecutionOutcome(
            new ProcessRouteExecutionRunSnapshot(executionOutcome.Detail.Run.Id),
            executionOutcome.ResponseText,
            executionOutcome.CompletionStatus,
            executionOutcome.CompletionReason,
            executionOutcome.MissingRequiredTools,
            executionOutcome.AttemptNumber,
            executionOutcome.SelectedBranchOutcomeId,
            new DispatcherExecutionOutcomeSource(executionOutcome));
    }

    public static ProcessRunAutomationDispatchService.DispatchExecutionOutcome ToDispatcherExecutionOutcome(
        ProcessRouteExecutionOutcome executionOutcome)
    {
        return RequireSource<DispatcherExecutionOutcomeSource>(executionOutcome.Source).ExecutionOutcome;
    }

    private static TSource RequireSource<TSource>(object source)
        where TSource : class
    {
        return source as TSource ??
            throw new InvalidOperationException($"Route model source must be {typeof(TSource).Name}.");
    }

    private sealed record DispatcherCandidateSource(
        ProcessRunAutomationDispatchService.DispatchCandidate Candidate) : IProcessRouteCandidateSource;

    private sealed record DispatcherDispatchClaimSource(
        ProcessRunAutomationDispatchService.ProcessStepDispatchClaim DispatchClaim) : IProcessRouteDispatchClaimSource;

    private sealed record DispatcherExecutionOutcomeSource(
        ProcessRunAutomationDispatchService.DispatchExecutionOutcome ExecutionOutcome) : IProcessRouteExecutionOutcomeSource;
}
