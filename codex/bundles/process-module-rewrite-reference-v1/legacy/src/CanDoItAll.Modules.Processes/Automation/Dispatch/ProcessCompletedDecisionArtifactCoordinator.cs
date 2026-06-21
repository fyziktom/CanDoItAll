using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.Storage;
using Microsoft.Extensions.Logging;
using System.Text;


namespace CanDoItAll.Modules.Processes;

internal sealed class ProcessCompletedDecisionArtifactCoordinator : IProcessArtifactProjectionSourceCoordinator
{
    private readonly IProcessProjectionExpectationMatcher expectationMatcher;
    private readonly IProcessProjectionDecisionArtifactRules decisionArtifactRules;
    private readonly IProcessProjectionLineageFactory lineageFactory;
    private readonly IProcessProjectionCandidateStateUpdater candidateState;

    public ProcessCompletedDecisionArtifactCoordinator(
        IProcessProjectionExpectationMatcher expectationMatcher,
        IProcessProjectionDecisionArtifactRules decisionArtifactRules,
        IProcessProjectionLineageFactory lineageFactory,
        IProcessProjectionCandidateStateUpdater candidateState)
    {
        this.expectationMatcher = expectationMatcher;
        this.decisionArtifactRules = decisionArtifactRules;
        this.lineageFactory = lineageFactory;
        this.candidateState = candidateState;
    }

    public async Task ProjectAsync(ProcessArtifactProjectionContext context)
    {
        if (context.CompletionStatus != ProcessStepRunStatus.Completed ||
            context.Candidate.ExpectedArtifacts.Count == 0)
        {
            return;
        }

        foreach (var expectedArtifact in context.Candidate.ExpectedArtifacts.Where(decisionArtifactRules.ShouldAutoRecordCompletedDecisionArtifact))
        {
            if (context.Candidate.MutableState.RecordedArtifactExpectationIds.Contains(expectedArtifact.Id) ||
                expectationMatcher.HasProjectedArtifactExpectationExternalReference(context.Candidate.MutableState.ExternalReferenceKeys, expectedArtifact.Id))
            {
                continue;
            }

            var externalReferenceKey = decisionArtifactRules.BuildCompletedDecisionArtifactExternalReferenceKey(
                context.Candidate.Step.Id,
                expectedArtifact.Id);
            if (context.Candidate.MutableState.ExternalReferenceKeys.Contains(externalReferenceKey))
            {
                continue;
            }

            var recordResult = await context.RecordOnlyCoordinator.RecordAsync(
                new ProcessArtifactProjectionRecordOnlyRequest(
                    context.Candidate.RunId,
                    context.Candidate.Step.Id,
                    expectedArtifact.Id,
                    expectedArtifact.ArtifactKind,
                    expectedArtifact.Title,
                    decisionArtifactRules.ResolveCompletedDecisionArtifactTrustStatus(expectedArtifact.TrustRequirement),
                    expectedArtifact.SensitivityLevel,
                    decisionArtifactRules.BuildCompletedDecisionArtifactProvenanceSummary(context.Candidate, context.Run),
                    string.IsNullOrWhiteSpace(expectedArtifact.AllowedFutureUsageSummary)
                        ? "Reusable for audit, release replay, and governance tuning."
                        : expectedArtifact.AllowedFutureUsageSummary,
                    decisionArtifactRules.BuildCompletedDecisionArtifactReviewSummary(
                        context.Candidate,
                        context.Run,
                        context.ResponseText,
                        expectedArtifact),
                    externalReferenceKey,
                    lineageFactory.BuildArtifactProjectionLineage(
                        ProcessArtifactProjectionSourceKind.CompletedDecision,
                        context.Run.Id,
                        sourceExternalReferenceKey: externalReferenceKey)),
                context.CancellationToken);
            if (!candidateState.TryApplyExpectedRecordOnlyOutcome(
                    context.Candidate.MutableState,
                    expectedArtifact,
                    recordResult,
                    out var errorSummary))
            {
                context.Logger.LogWarning(
                    "Completed-step decision artifact projection failed for run {RunId}, step {StepRunId}, expected artifact {ArtifactTitle}. Errors: {Errors}",
                    context.Candidate.RunId,
                    context.Candidate.Step.Id,
                    expectedArtifact.Title,
                    errorSummary);
            }
        }
    }
}
