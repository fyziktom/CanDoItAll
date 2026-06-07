using System.Runtime.CompilerServices;

namespace CanDoItAll.Modules.Processes;

internal static class ProcessDispatchRouteModelAdapters
{
    private static readonly ConditionalWeakTable<ProcessRouteCandidate, DispatcherCandidateSource> CandidateSources = new();

    private static readonly ConditionalWeakTable<ProcessRouteDispatchClaim, DispatcherDispatchClaimSource> DispatchClaimSources = new();

    private static readonly ConditionalWeakTable<ProcessRouteExecutionOutcome, DispatcherExecutionOutcomeSource> ExecutionOutcomeSources = new();

    public static ProcessRouteCandidate FromDispatcherCandidate(
        ProcessRunAutomationDispatchService.DispatchCandidate candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        var routeCandidate = new ProcessRouteCandidate(
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
                .ToList());
        CandidateSources.Add(routeCandidate, new DispatcherCandidateSource(candidate));

        return routeCandidate;
    }

    public static ProcessRunAutomationDispatchService.DispatchCandidate ToDispatcherCandidate(
        ProcessRouteCandidate candidate)
    {
        return RequireSource(CandidateSources, candidate, nameof(candidate)).Candidate;
    }

    public static ProcessDispatchRouteSnapshot ToRouteSnapshot(
        ProcessRouteCandidate candidate,
        string trigger,
        Guid? triggerStepRunId)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        return ProcessDispatchRouteSnapshot.Create(
            candidate.Run.Id,
            candidate.StepRun.Id,
            candidate.Run.Status,
            candidate.StepRun.Status,
            candidate.StepRun.StepKind,
            candidate.TechnicalAgentId,
            candidate.RecoveryExecutionRunId,
            candidate.StepRun.StartedAtUtc,
            trigger,
            triggerStepRunId);
    }

    public static ProcessRouteDispatchClaim FromDispatcherClaim(
        ProcessRunAutomationDispatchService.ProcessStepDispatchClaim dispatchClaim)
    {
        var routeClaim = new ProcessRouteDispatchClaim(
            dispatchClaim.StepRunId,
            dispatchClaim.ClaimToken);
        DispatchClaimSources.Add(routeClaim, new DispatcherDispatchClaimSource(dispatchClaim));

        return routeClaim;
    }

    public static ProcessRunAutomationDispatchService.ProcessStepDispatchClaim ToDispatcherClaim(
        ProcessRouteDispatchClaim dispatchClaim)
    {
        return RequireSource(DispatchClaimSources, dispatchClaim, nameof(dispatchClaim)).DispatchClaim;
    }

    public static ProcessRouteExecutionOutcome FromDispatcherExecutionOutcome(
        ProcessRunAutomationDispatchService.DispatchExecutionOutcome executionOutcome)
    {
        ArgumentNullException.ThrowIfNull(executionOutcome);

        var routeOutcome = new ProcessRouteExecutionOutcome(
            new ProcessRouteExecutionRunSnapshot(executionOutcome.Detail.Run.Id),
            executionOutcome.ResponseText,
            executionOutcome.CompletionStatus,
            executionOutcome.CompletionReason,
            executionOutcome.MissingRequiredTools,
            executionOutcome.AttemptNumber,
            executionOutcome.SelectedBranchOutcomeId);
        ExecutionOutcomeSources.Add(routeOutcome, new DispatcherExecutionOutcomeSource(executionOutcome));

        return routeOutcome;
    }

    public static ProcessRunAutomationDispatchService.DispatchExecutionOutcome ToDispatcherExecutionOutcome(
        ProcessRouteExecutionOutcome executionOutcome)
    {
        return RequireSource(ExecutionOutcomeSources, executionOutcome, nameof(executionOutcome)).ExecutionOutcome;
    }

    private static TSource RequireSource<TKey, TSource>(
        ConditionalWeakTable<TKey, TSource> sourceTable,
        TKey routeModel,
        string routeModelName)
        where TKey : class
        where TSource : class
    {
        ArgumentNullException.ThrowIfNull(routeModel);

        if (sourceTable.TryGetValue(routeModel, out var source))
        {
            return source;
        }

        throw new InvalidOperationException(
            $"Route model '{routeModelName}' must be created by {nameof(ProcessDispatchRouteModelAdapters)} before it can be converted back to dispatcher payload.");
    }

    private sealed record DispatcherCandidateSource(
        ProcessRunAutomationDispatchService.DispatchCandidate Candidate);

    private sealed record DispatcherDispatchClaimSource(
        ProcessRunAutomationDispatchService.ProcessStepDispatchClaim DispatchClaim);

    private sealed record DispatcherExecutionOutcomeSource(
        ProcessRunAutomationDispatchService.DispatchExecutionOutcome ExecutionOutcome);
}
