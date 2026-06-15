using CanDoItAll.Processes.Drivers.Abstractions.Verification;

namespace CanDoItAll.Modules.Processes;

internal static class ProcessRuntimeEvidenceVerificationObservationMapper
{
    public static ProcessRuntimeEvidenceVerificationReadOnlyObservation Create(
        ProcessRuntimeEvidenceVerificationReadOnlyEvidencePayload payload,
        ProcessDriverVerificationResponse response)
    {
        return new ProcessRuntimeEvidenceVerificationReadOnlyObservation(
            payload.ProcessRunId,
            payload.StepRunId,
            payload.ArtifactId,
            ProcessRuntimeEvidenceVerificationSourceLane.RuntimeEvidenceConsistency,
            response.Accepted,
            response.DenialReason,
            response.Diagnostics,
            response.EvidenceReferences,
            response.Redaction,
            response.NoMutationPerformed,
            response.AuditFacts,
            response.ContractVersion,
            payload.RequestedAt,
            ProcessReadOnlyObservationClock.ObservedAt(payload.RequestedAt));
    }
}
