using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Modules.Processes;

internal static class ProcessFreshImplementationArtifactSatisfactionRules
{
    public static bool RequiresFreshCurrentAttemptImplementationArtifact(
        bool requiresConcreteImplementationProof,
        ProcessRunAutomationDispatchService.DispatchArtifactExpectation expectedArtifact)
    {
        return requiresConcreteImplementationProof &&
               expectedArtifact.IsRequired &&
               expectedArtifact.ArtifactKind is not ProcessArtifactKind.Decision and not ProcessArtifactKind.DecisionRecord;
    }

    public static bool HasFreshCurrentAttemptImplementationArtifact(
        ProcessAutomationToolExecutionReceipt? latestConcreteMutation,
        ProcessAutomationToolExecutionReceipt? latestConcreteRead,
        ProcessAutomationToolExecutionReceipt? latestValidation,
        IReadOnlyList<ProcessAutomationToolExecutionReceipt> artifactReceipts,
        Func<ProcessAutomationToolExecutionReceipt, ProcessAutomationToolExecutionReceipt, bool> isReceiptAfter)
    {
        if (latestConcreteMutation is null)
        {
            if (latestConcreteRead is null)
            {
                return false;
            }

            return artifactReceipts.Any(receipt =>
                !isReceiptAfter(latestConcreteRead, receipt) &&
                (latestValidation is null || !isReceiptAfter(latestValidation, receipt)));
        }

        return artifactReceipts.Any(receipt =>
            !isReceiptAfter(latestConcreteMutation, receipt) &&
            (latestValidation is null || !isReceiptAfter(latestValidation, receipt)));
    }
}

