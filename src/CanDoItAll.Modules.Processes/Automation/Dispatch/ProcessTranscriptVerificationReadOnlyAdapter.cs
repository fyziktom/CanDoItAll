using CanDoItAll.Processes.Drivers.Abstractions.Audit;
using CanDoItAll.Processes.Drivers.Abstractions.Evidence;
using CanDoItAll.Processes.Drivers.Abstractions.Permissions;
using CanDoItAll.Processes.Drivers.Abstractions.Verification;
using CanDoItAll.Processes.Drivers.TranscriptVerification;

namespace CanDoItAll.Modules.Processes;

internal sealed class ProcessTranscriptVerificationReadOnlyAdapter
{
    private readonly Func<TranscriptVerificationAlphaRequest, ProcessDriverVerificationResponse> verifyTranscript;

    public ProcessTranscriptVerificationReadOnlyAdapter()
        : this(request => new TranscriptVerificationAlphaVerifier().Verify(request))
    {
    }

    internal ProcessTranscriptVerificationReadOnlyAdapter(
        Func<TranscriptVerificationAlphaRequest, ProcessDriverVerificationResponse> verifyTranscript)
    {
        this.verifyTranscript = verifyTranscript ?? throw new ArgumentNullException(nameof(verifyTranscript));
    }

    public ProcessTranscriptVerificationReadOnlyObservation Verify(
        ProcessTranscriptVerificationReadOnlyEvidencePayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);

        var evidenceReferences = ProcessDriverEvidencePolicy.NormalizeEvidenceReferences(payload.EvidenceReferences);
        var requestedOperations = ProcessReadOnlyVerificationOperationPolicy.Normalize(
            payload.RequestedOperations,
            ProcessReadOnlyVerificationOperationPolicy.TranscriptVerificationDefaults);
        var transcriptEvidence = ProcessDriverEvidencePolicy.CreateTranscriptEvidenceReference(
            payload.TranscriptReference,
            payload.TranscriptText);
        var primaryEvidence = evidenceReferences.FirstOrDefault() ?? transcriptEvidence;
        var preflightDenial = ProcessTranscriptVerificationPreflightPolicy.Validate(
            payload,
            evidenceReferences,
            requestedOperations,
            primaryEvidence);

        if (preflightDenial is not null)
        {
            return ProcessTranscriptVerificationObservationMapper.Create(payload, preflightDenial.Response);
        }

        var verificationRequest = new ProcessDriverVerificationRequest(
            payload.PermissionMode,
            payload.Scope,
            evidenceReferences,
            requestedOperations,
            payload.CallerContext.Trim(),
            ProcessDriverContractVersion.Current);
        var request = new TranscriptVerificationAlphaRequest(
            verificationRequest,
            payload.TranscriptReference,
            payload.TranscriptText,
            payload.RequestedAt);

        return ProcessTranscriptVerificationObservationMapper.Create(payload, verifyTranscript(request));
    }
}

internal sealed record ProcessTranscriptVerificationReadOnlyEvidencePayload(
    Guid ProcessRunId,
    Guid StepRunId,
    Guid? ArtifactId,
    string CallerContext,
    ProcessDriverPermissionMode PermissionMode,
    ProcessDriverCapabilityScope Scope,
    ProcessDriverTranscriptReference TranscriptReference,
    string TranscriptText,
    IReadOnlyList<ProcessDriverEvidenceReference> EvidenceReferences,
    IReadOnlyList<ProcessDriverOperation> RequestedOperations,
    DateTimeOffset RequestedAt);

internal sealed record ProcessTranscriptVerificationReadOnlyObservation(
    Guid ProcessRunId,
    Guid StepRunId,
    Guid? ArtifactId,
    ProcessTranscriptVerificationSourceLane SourceLane,
    bool Accepted,
    ProcessDriverDenialReason DenialReason,
    IReadOnlyList<ProcessDriverDiagnostic> Diagnostics,
    IReadOnlyList<ProcessDriverEvidenceReference> EvidenceReferences,
    ProcessDriverRedactionDescriptor Redaction,
    bool NoMutationPerformed,
    IReadOnlyList<ProcessDriverAuditFact> AuditFacts,
    ProcessDriverContractVersion ContractVersion,
    DateTimeOffset RequestedAt,
    DateTimeOffset ObservedAt);

internal enum ProcessTranscriptVerificationSourceLane
{
    DotNetRustTranscriptVerification = 1
}
