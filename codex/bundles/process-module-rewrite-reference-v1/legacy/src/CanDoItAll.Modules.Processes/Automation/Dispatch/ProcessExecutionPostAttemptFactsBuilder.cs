using CanDoItAll.AgentFramework.Core;

namespace CanDoItAll.Modules.Processes;

internal sealed partial class ProcessRunAutomationDispatchService
{
    private static class ProcessExecutionPostAttemptFactsBuilder
    {
        public static ProcessExecutionPostAttemptFacts Create(
            DispatchCandidate candidate,
            ProcessAutomationExecutionRunDetail detail,
            IReadOnlySet<string> successfulToolNamesAcrossAttempts,
            string responseText,
            CarriedImplementationProof carriedImplementationProof,
            int attemptNumber,
            int maxExecutionAttempts)
        {
            var missingRequiredTools = ResolveMissingRequiredToolExecutionsWithCarriedImplementationProof(
                candidate,
                detail,
                successfulToolNamesAcrossAttempts,
                carriedImplementationProof);
            var completionStatus = ResolveCompletionStatusWithCarryForward(
                candidate,
                detail,
                successfulToolNamesAcrossAttempts,
                responseText,
                carriedImplementationProof);
            var completionReason = BuildCompletionReasonWithCarryForward(
                candidate,
                detail,
                candidate.StepRun.Title,
                successfulToolNamesAcrossAttempts,
                responseText,
                carriedImplementationProof);

            return new ProcessExecutionPostAttemptFacts(
                carriedImplementationProof,
                missingRequiredTools,
                ResolveUnresolvedCriticalToolFailures(candidate, detail),
                completionStatus,
                completionReason,
                ResolveSelectedBranchOutcomeId(
                    candidate,
                    completionStatus,
                    responseText))
                .WithRecoveryAttemptSuffix(attemptNumber, maxExecutionAttempts);
        }
    }
}
