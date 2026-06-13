using CanDoItAll.AgentFramework.Core;

namespace CanDoItAll.Modules.Processes;

internal static class ProcessCarriedImplementationProofRules
{
    internal static ProcessRunAutomationDispatchService.CarriedImplementationProof ResolveCarriedImplementationProof(
        bool requiresConcreteImplementationProof,
        bool hasSuccessfulConcreteProductMutation,
        bool hasConcreteImplementationProofEvidence,
        bool concreteImplementationProofSummaryIsEmpty,
        bool runnableApplicationProofSummaryIsEmpty,
        bool hasRunnableApplicationProofEvidence,
        ProcessRunAutomationDispatchService.CarriedImplementationProof previous)
    {
        if (!requiresConcreteImplementationProof)
        {
            return previous;
        }

        var hasConcreteProductMutation = previous.HasConcreteProductMutation || hasSuccessfulConcreteProductMutation;
        var hasConcreteImplementationProof = hasSuccessfulConcreteProductMutation
            ? false
            : previous.HasConcreteImplementationProof;
        var hasRunnableApplicationProof = hasSuccessfulConcreteProductMutation
            ? false
            : previous.HasRunnableApplicationProof;

        if (!hasSuccessfulConcreteProductMutation &&
            previous.HasConcreteProductMutation &&
            hasConcreteImplementationProofEvidence)
        {
            hasConcreteImplementationProof = true;
        }

        if (concreteImplementationProofSummaryIsEmpty &&
            hasConcreteImplementationProofEvidence)
        {
            hasConcreteImplementationProof = true;
        }

        if (runnableApplicationProofSummaryIsEmpty &&
            hasRunnableApplicationProofEvidence)
        {
            hasRunnableApplicationProof = true;
        }

        return new ProcessRunAutomationDispatchService.CarriedImplementationProof(
            hasConcreteImplementationProof,
            hasRunnableApplicationProof,
            hasConcreteProductMutation);
    }

    internal static ProcessRunAutomationDispatchService.CarriedImplementationProof ResolveHistoricalCarriedImplementationProof(
        bool requiresCurrentAttemptProductMutation,
        IEnumerable<ProcessAutomationExecutionRunDetail> historicalDetails,
        Func<ProcessAutomationExecutionRunDetail, bool> hasSuccessfulConcreteProductMutation)
    {
        ArgumentNullException.ThrowIfNull(historicalDetails);

        if (!requiresCurrentAttemptProductMutation)
        {
            return ProcessRunAutomationDispatchService.CarriedImplementationProof.None;
        }

        return historicalDetails.Any(detail =>
            IsHistoricalCarryForwardExecutionRun(detail.Run) &&
            hasSuccessfulConcreteProductMutation(detail))
            ? new ProcessRunAutomationDispatchService.CarriedImplementationProof(false, false, true)
            : ProcessRunAutomationDispatchService.CarriedImplementationProof.None;
    }

    internal static bool IsHistoricalCarryForwardExecutionRun(ProcessAutomationExecutionRunRecord executionRun)
    {
        return string.Equals(executionRun.RequestedBy, ProcessRunAutomationDispatchService.AutomationActor, StringComparison.OrdinalIgnoreCase) &&
               executionRun.State is ProcessAutomationExecutionState.Completed or ProcessAutomationExecutionState.Failed;
    }

    internal static string ResolveMissingConcreteImplementationProofSummaryWithCarryForward(
        string summary,
        bool requiresCurrentAttemptProductMutation,
        bool hasConcreteImplementationProofEvidence,
        bool hasSuccessfulConcreteProductMutation,
        ProcessRunAutomationDispatchService.CarriedImplementationProof carriedProof)
    {
        if (requiresCurrentAttemptProductMutation &&
            carriedProof.HasConcreteProductMutation &&
            hasConcreteImplementationProofEvidence &&
            string.Equals(
                summary,
                "the current repair attempt did not mutate any concrete product file",
                StringComparison.Ordinal))
        {
            return string.Empty;
        }

        if (string.IsNullOrWhiteSpace(summary) ||
            requiresCurrentAttemptProductMutation ||
            !carriedProof.HasConcreteImplementationProof ||
            hasSuccessfulConcreteProductMutation)
        {
            return summary;
        }

        return string.Empty;
    }

    internal static string ResolveMissingRunnableApplicationProofSummaryWithCarryForward(
        string summary,
        bool hasSuccessfulConcreteProductMutation,
        ProcessRunAutomationDispatchService.CarriedImplementationProof carriedProof)
    {
        if (string.IsNullOrWhiteSpace(summary) ||
            !carriedProof.HasRunnableApplicationProof ||
            hasSuccessfulConcreteProductMutation)
        {
            return summary;
        }

        return string.Empty;
    }
}
