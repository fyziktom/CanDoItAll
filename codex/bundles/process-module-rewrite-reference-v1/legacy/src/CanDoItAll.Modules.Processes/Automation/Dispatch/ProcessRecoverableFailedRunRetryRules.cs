using CanDoItAll.AgentFramework.Core;

namespace CanDoItAll.Modules.Processes;

internal sealed partial class ProcessRunAutomationDispatchService
{
    private static class ProcessRecoverableFailedRunRetryRules
    {
        public static bool ShouldRetry(
            DispatchCandidate candidate,
            ProcessAutomationExecutionRunDetail detail,
            string? responseText,
            IReadOnlyList<string> missingRequiredTools,
            IReadOnlyList<ProcessAutomationToolExecutionReceipt> unresolvedCriticalToolFailures,
            int attemptNumber,
            int maxExecutionAttempts)
        {
            var run = detail.Run;
            var recoverableGovernedOutcomeGap = IsRecoverableGovernedOutcomeGap(candidate, responseText);
            var recoverableProviderFailure = TryResolveRecoverableProviderFailure(detail, responseText, out _);
            var recoverableFinalizerValidationFailure = TryResolveRecoverableFinalizerValidationFailure(candidate, detail, responseText, out _);
            var recoverableExecutionInterruption = TryResolveRecoverableExecutionInterruption(detail, responseText, out _);
            var recoverableRepeatedToolInvocation =
                MentionsRepeatedToolInvocation(responseText) ||
                MentionsRepeatedToolInvocation(run.ResultSummary);

            if (attemptNumber >= maxExecutionAttempts ||
                run.State != ProcessAutomationExecutionState.Failed ||
                run.PendingApprovals.Count > 0)
            {
                return false;
            }

            if (!RequiresConcreteImplementationProof(candidate) &&
                !RequiresConcreteBrowserProof(candidate) &&
                !recoverableProviderFailure &&
                !recoverableFinalizerValidationFailure &&
                !recoverableExecutionInterruption &&
                !recoverableRepeatedToolInvocation)
            {
                return false;
            }

            if (!recoverableFinalizerValidationFailure &&
                HasValidNonCompletedDeclaredOutcome(
                    candidate,
                    detail,
                    responseText,
                    ResolveOutputInspectionText(responseText)))
            {
                return false;
            }

            return missingRequiredTools.Count > 0 ||
                   unresolvedCriticalToolFailures.Count > 0 ||
                   recoverableGovernedOutcomeGap ||
                   recoverableProviderFailure ||
                   recoverableFinalizerValidationFailure ||
                   recoverableExecutionInterruption ||
                   recoverableRepeatedToolInvocation;
        }
    }
}
