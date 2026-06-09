using CanDoItAll.Processes.Drivers.Abstractions.Verification;

namespace CanDoItAll.Modules.Processes;

internal static class ProcessTranscriptVerificationObservationMapper
{
    public static ProcessTranscriptVerificationReadOnlyObservation Create(
        ProcessTranscriptVerificationReadOnlyEvidencePayload payload,
        ProcessDriverVerificationResponse response)
    {
        return new ProcessTranscriptVerificationReadOnlyObservation(
            payload.ProcessRunId,
            payload.StepRunId,
            payload.ArtifactId,
            ProcessTranscriptVerificationSourceLane.DotNetRustTranscriptVerification,
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
