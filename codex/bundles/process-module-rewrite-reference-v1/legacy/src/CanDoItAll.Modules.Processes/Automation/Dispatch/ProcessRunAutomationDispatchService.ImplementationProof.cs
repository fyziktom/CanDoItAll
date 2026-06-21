using CanDoItAll.Processes.Drivers.SoftwareDeliveryEvidence;

namespace CanDoItAll.Modules.Processes;

internal sealed partial class ProcessRunAutomationDispatchService
{
    private static string ResolveMissingConcreteImplementationProofSummary(
        DispatchCandidate candidate,
        ProcessAutomationExecutionRunDetail detail)
    {
        return EvaluateSoftwareDeliveryEvidence(candidate, detail).MissingConcreteImplementationProofSummary;
    }

    private static string ResolveMissingRunnableApplicationProofSummary(
        DispatchCandidate candidate,
        ProcessAutomationExecutionRunDetail detail)
    {
        return EvaluateSoftwareDeliveryEvidence(candidate, detail).MissingRunnableApplicationProofSummary;
    }

    private static CarriedImplementationProof ResolveCarriedImplementationProof(
        DispatchCandidate candidate,
        ProcessAutomationExecutionRunDetail detail,
        CarriedImplementationProof previous)
    {
        var request = CreateSoftwareDeliveryProofPolicyRequest(
            candidate,
            detail,
            previous,
            detail.Run.CreatedAtUtc);
        var result = SoftwareDeliveryEvidencePolicy.Evaluate(request);
        var carriedProof = SoftwareDeliveryEvidencePolicy.ResolveCarriedProof(
            request,
            result,
            CreateSoftwareDeliveryCarriedProofSnapshot(previous, detail.Run.Id));
        return CreateCarriedImplementationProof(carriedProof);
    }

    private static CarriedImplementationProof ResolveHistoricalCarriedImplementationProof(
        DispatchCandidate candidate,
        IEnumerable<ProcessAutomationExecutionRunDetail> historicalDetails)
    {
        ArgumentNullException.ThrowIfNull(historicalDetails);

        var historicalProofs = historicalDetails
            .Select(detail =>
            {
                var result = EvaluateSoftwareDeliveryEvidence(candidate, detail);
                return new SoftwareDeliveryHistoricalExecutionProofSnapshot(
                    IsHistoricalCarryForwardExecutionRun(detail.Run),
                    result.HasSuccessfulConcreteProductMutation);
            })
            .ToList();
        var carriedProof = SoftwareDeliveryEvidencePolicy.ResolveHistoricalCarriedProof(
            RequiresCurrentAttemptProductMutation(candidate),
            historicalProofs);
        return CreateCarriedImplementationProof(carriedProof);
    }

    private static bool IsHistoricalCarryForwardExecutionRun(ProcessAutomationExecutionRunRecord executionRun)
    {
        return string.Equals(executionRun.RequestedBy, AutomationActor, StringComparison.OrdinalIgnoreCase) &&
               executionRun.State is ProcessAutomationExecutionState.Completed or ProcessAutomationExecutionState.Failed;
    }

    private static string ResolveMissingConcreteImplementationProofSummaryWithCarryForward(
        DispatchCandidate candidate,
        ProcessAutomationExecutionRunDetail detail,
        CarriedImplementationProof carriedProof)
    {
        var result = EvaluateSoftwareDeliveryEvidence(candidate, detail, carriedProof);
        return SoftwareDeliveryEvidencePolicy.ResolveMissingConcreteImplementationProofSummaryWithCarryForward(
            result.MissingConcreteImplementationProofSummary,
            result,
            CreateSoftwareDeliveryCarriedProofSnapshot(carriedProof, detail.Run.Id));
    }

    private static string ResolveMissingRunnableApplicationProofSummaryWithCarryForward(
        DispatchCandidate candidate,
        ProcessAutomationExecutionRunDetail detail,
        CarriedImplementationProof carriedProof)
    {
        var result = EvaluateSoftwareDeliveryEvidence(candidate, detail, carriedProof);
        return SoftwareDeliveryEvidencePolicy.ResolveMissingRunnableApplicationProofSummaryWithCarryForward(
            result.MissingRunnableApplicationProofSummary,
            result,
            CreateSoftwareDeliveryCarriedProofSnapshot(carriedProof, detail.Run.Id));
    }

    private static SoftwareDeliveryProofPolicyResult EvaluateSoftwareDeliveryEvidence(
        DispatchCandidate candidate,
        ProcessAutomationExecutionRunDetail detail,
        CarriedImplementationProof carriedProof = default)
    {
        var request = CreateSoftwareDeliveryProofPolicyRequest(
            candidate,
            detail,
            carriedProof,
            detail.Run.CreatedAtUtc);
        return SoftwareDeliveryEvidencePolicy.Evaluate(request);
    }

    private static SoftwareDeliveryCarriedProofSnapshot CreateSoftwareDeliveryCarriedProofSnapshot(
        CarriedImplementationProof carriedProof,
        Guid sourceRunId)
    {
        return new SoftwareDeliveryCarriedProofSnapshot(
            carriedProof.HasConcreteImplementationProof,
            carriedProof.HasRunnableApplicationProof,
            carriedProof.HasConcreteProductMutation,
            sourceRunId.ToString("D"),
            carriedProof == CarriedImplementationProof.None
                ? string.Empty
                : "Concrete proof was carried from prior process execution facts.");
    }

    private static CarriedImplementationProof CreateCarriedImplementationProof(
        SoftwareDeliveryCarriedProofSnapshot carriedProof)
    {
        return new CarriedImplementationProof(
            carriedProof.HasCarriedConcreteImplementationProof,
            carriedProof.HasCarriedRunnableApplicationProof,
            carriedProof.HasCarriedConcreteProductMutation);
    }
}
