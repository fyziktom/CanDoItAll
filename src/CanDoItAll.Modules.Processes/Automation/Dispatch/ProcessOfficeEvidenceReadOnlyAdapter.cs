using CanDoItAll.Processes.Drivers.Abstractions.Audit;
using CanDoItAll.Processes.Drivers.Abstractions.Evidence;
using CanDoItAll.Processes.Drivers.Abstractions.Permissions;
using CanDoItAll.Processes.Drivers.Abstractions.Verification;
using CanDoItAll.Processes.Drivers.OfficeEvidence;
using CanDoItAll.Processes.Drivers.VerificationGateway;

namespace CanDoItAll.Modules.Processes;

internal sealed class ProcessOfficeEvidenceReadOnlyAdapter
{
    private readonly Func<OfficeEvidenceVerificationRequest, ProcessDriverVerificationResponse> verifyOfficeEvidence;

    public ProcessOfficeEvidenceReadOnlyAdapter()
        : this(ProcessDriverVerificationGateway.CreateDefault().VerifyOfficeEvidence)
    {
    }

    internal ProcessOfficeEvidenceReadOnlyAdapter(
        Func<OfficeEvidenceVerificationRequest, ProcessDriverVerificationResponse> verifyOfficeEvidence)
    {
        this.verifyOfficeEvidence = verifyOfficeEvidence ?? throw new ArgumentNullException(nameof(verifyOfficeEvidence));
    }

    public ProcessOfficeEvidenceReadOnlyObservation Verify(ProcessOfficeEvidenceReadOnlyPayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);

        var evidenceReferences = ProcessDriverEvidencePolicy.NormalizeEvidenceReferences(payload.EvidenceReferences);
        var requestedOperations = ProcessReadOnlyVerificationOperationPolicy.Normalize(
            payload.RequestedOperations,
            ProcessReadOnlyVerificationOperationPolicy.OfficeEvidenceDefaults);
        var verificationRequest = ProcessReadOnlyVerificationRequestFactory.Create(
            payload.PermissionMode,
            payload.Scope,
            evidenceReferences,
            requestedOperations,
            payload.CallerContext);
        var request = new OfficeEvidenceVerificationRequest(
            verificationRequest,
            payload.SuppliedContent,
            payload.Items,
            payload.RequestedAt);

        return ProcessOfficeEvidenceObservationMapper.Create(payload, verifyOfficeEvidence(request));
    }
}

internal static class ProcessOfficeEvidenceObservationMapper
{
    public static ProcessOfficeEvidenceReadOnlyObservation Create(
        ProcessOfficeEvidenceReadOnlyPayload payload,
        ProcessDriverVerificationResponse response)
    {
        return new ProcessOfficeEvidenceReadOnlyObservation(
            payload.ProcessRunId,
            payload.StepRunId,
            payload.ArtifactId,
            ProcessOfficeEvidenceSourceLane.OfficeEvidenceRead,
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

internal sealed record ProcessOfficeEvidenceReadOnlyPayload(
    Guid ProcessRunId,
    Guid StepRunId,
    Guid? ArtifactId,
    string CallerContext,
    ProcessDriverPermissionMode PermissionMode,
    ProcessDriverCapabilityScope Scope,
    IReadOnlyList<ProcessDriverEvidenceReference> EvidenceReferences,
    IReadOnlyList<ProcessDriverOperation> RequestedOperations,
    ProcessDriverSuppliedEvidenceContent SuppliedContent,
    IReadOnlyList<OfficeEvidenceItem> Items,
    DateTimeOffset RequestedAt);

internal sealed record ProcessOfficeEvidenceReadOnlyObservation(
    Guid ProcessRunId,
    Guid StepRunId,
    Guid? ArtifactId,
    ProcessOfficeEvidenceSourceLane SourceLane,
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

internal enum ProcessOfficeEvidenceSourceLane
{
    OfficeEvidenceRead = 1
}
