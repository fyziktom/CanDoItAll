using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.Storage;
using Microsoft.Extensions.Logging;
using System.Text;

using DispatchArtifactExpectation = CanDoItAll.Modules.Processes.ProcessRunAutomationDispatchService.DispatchArtifactExpectation;
using ProcessMockArtifactProjection = CanDoItAll.Modules.Processes.ProcessRunAutomationDispatchService.ProcessMockArtifactProjection;
using SessionFileContent = CanDoItAll.Modules.Processes.ProcessRunAutomationDispatchService.SessionFileContent;

namespace CanDoItAll.Modules.Processes;

internal sealed class ProcessCompletedDecisionArtifactCoordinator : IProcessArtifactProjectionSourceCoordinator
{
    private readonly IProcessArtifactProjectionHost host;

    public ProcessCompletedDecisionArtifactCoordinator(IProcessArtifactProjectionHost host)
    {
        this.host = host;
    }

    public async Task ProjectAsync(ProcessArtifactProjectionContext context)
    {
        if (context.CompletionStatus != ProcessStepRunStatus.Completed ||
            context.Candidate.ExpectedArtifacts.Count == 0)
        {
            return;
        }

        foreach (var expectedArtifact in context.Candidate.ExpectedArtifacts.Where(host.ShouldAutoRecordCompletedDecisionArtifact))
        {
            if (context.Candidate.RecordedArtifactExpectationIds.Contains(expectedArtifact.Id) ||
                host.HasProjectedArtifactExpectationExternalReference(context.Candidate.ExternalReferenceKeys, expectedArtifact.Id))
            {
                continue;
            }

            var externalReferenceKey = host.BuildCompletedDecisionArtifactExternalReferenceKey(
                context.Candidate.StepRun.Id,
                expectedArtifact.Id);
            if (context.Candidate.ExternalReferenceKeys.Contains(externalReferenceKey))
            {
                continue;
            }

            var recordResult = await context.RecordOnlyCoordinator.RecordAsync(
                new ProcessArtifactProjectionRecordOnlyRequest(
                    context.Candidate.Run.Id,
                    context.Candidate.StepRun.Id,
                    expectedArtifact.Id,
                    expectedArtifact.ArtifactKind,
                    expectedArtifact.Title,
                    host.ResolveCompletedDecisionArtifactTrustStatus(expectedArtifact.TrustRequirement),
                    expectedArtifact.SensitivityLevel,
                    host.BuildCompletedDecisionArtifactProvenanceSummary(context.Candidate, context.Detail),
                    string.IsNullOrWhiteSpace(expectedArtifact.AllowedFutureUsageSummary)
                        ? "Reusable for audit, release replay, and governance tuning."
                        : expectedArtifact.AllowedFutureUsageSummary,
                    host.BuildCompletedDecisionArtifactReviewSummary(
                        context.Candidate,
                        context.Detail,
                        context.ResponseText,
                        expectedArtifact),
                    externalReferenceKey,
                    host.BuildArtifactProjectionLineage(
                        ProcessArtifactProjectionSourceKind.CompletedDecision,
                        context.Detail.Run.Id,
                        sourceExternalReferenceKey: externalReferenceKey)),
                context.CancellationToken);
            if (!ProcessArtifactProjectionCandidateState.TryApplyExpectedRecordOnlyOutcome(
                    context.Candidate,
                    expectedArtifact,
                    recordResult,
                    out var errorSummary))
            {
                context.Logger.LogWarning(
                    "Completed-step decision artifact projection failed for run {RunId}, step {StepRunId}, expected artifact {ArtifactTitle}. Errors: {Errors}",
                    context.Candidate.Run.Id,
                    context.Candidate.StepRun.Id,
                    expectedArtifact.Title,
                    errorSummary);
            }
        }
    }
}
