using CanDoItAll.AgentFramework.Core;

namespace CanDoItAll.Modules.Processes;

internal sealed partial class ProcessRunAutomationDispatchService
{
    private static class ProcessIncompleteSuccessfulRunRetryRules
    {
        public static bool ShouldRetry(
            DispatchCandidate candidate,
            ProcessAutomationExecutionRunDetail detail,
            string? responseText,
            IReadOnlyList<string> missingRequiredTools,
            CarriedImplementationProof carriedImplementationProof,
            int attemptNumber,
            int maxExecutionAttempts)
        {
            var run = detail.Run;
            var inspectionText = ResolveOutputInspectionText(responseText);
            if (HasValidNonCompletedDeclaredOutcome(candidate, detail, responseText, inspectionText))
            {
                return ShouldRetryRepairableImplementationBlockedOutcome(
                           candidate,
                           detail,
                           responseText,
                           missingRequiredTools,
                           attemptNumber,
                           maxExecutionAttempts) ||
                       ShouldRetryRecoverableBrowserProofBlockedOutcome(
                           candidate,
                           detail,
                           responseText,
                           missingRequiredTools,
                           attemptNumber,
                           maxExecutionAttempts);
            }

            if (!string.IsNullOrWhiteSpace(ResolveMissingUpstreamArtifactInputSummary(candidate)) &&
                TryResolveDeclaredStepOutcome(candidate, responseText, out var declaredOutcome) &&
                declaredOutcome.Status == ProcessStepRunStatus.Blocked)
            {
                return false;
            }

            if (TryResolveDeclaredStepOutcome(candidate, responseText, out declaredOutcome, out var processOutcome) &&
                CanCompleteExplicitDispositionOutcomeWithCriticalToolFailures(
                    candidate,
                    detail,
                    declaredOutcome,
                    processOutcome,
                    responseText,
                    missingRequiredTools,
                    carriedImplementationProof))
            {
                return false;
            }

            var retryReasons = ProcessExecutionRetryReasonAggregator.ResolveIncompleteSuccessfulRunRetryReasons(
                candidate,
                detail,
                responseText,
                missingRequiredTools,
                carriedImplementationProof);

            return attemptNumber < maxExecutionAttempts
                   && run.State == ProcessAutomationExecutionState.Completed
                   && run.PendingApprovals.Count == 0
                   && run.Outcome == ProcessAutomationRunOutcome.Succeeded
                   && retryReasons.Count > 0
                   && !ProcessNoProgressRetrySignalBuilder.ShouldCompress(
                       candidate,
                       detail,
                       responseText,
                       missingRequiredTools,
                       retryReasons,
                       attemptNumber);
        }
    }
}
