using CanDoItAll.AgentFramework.Core;

namespace CanDoItAll.Modules.Processes;

internal sealed record ProcessExecutionPostAttemptFacts(
    ProcessRunAutomationDispatchService.CarriedImplementationProof CarriedImplementationProof,
    IReadOnlyList<string> MissingRequiredTools,
    IReadOnlyList<ProcessAutomationToolExecutionReceipt> UnresolvedCriticalToolFailures,
    ProcessStepRunStatus CompletionStatus,
    string CompletionReason,
    Guid? SelectedBranchOutcomeId)
{
    public ProcessExecutionPostAttemptFacts WithRecoveryAttemptSuffix(int attemptNumber, int maxExecutionAttempts)
    {
        if (attemptNumber <= 1)
        {
            return this;
        }

        var suffixedReason = CompletionStatus == ProcessStepRunStatus.Completed
            ? $"{CompletionReason} Recovered on attempt {attemptNumber} of {maxExecutionAttempts}."
            : $"{CompletionReason} Recovery attempt {attemptNumber} of {maxExecutionAttempts}.";

        return this with { CompletionReason = suffixedReason };
    }
}
