using CanDoItAll.Processes.Core.Artifacts;
using CanDoItAll.Processes.Drivers.Abstractions.Audit;
using CanDoItAll.Processes.Drivers.Abstractions.Evidence;
using CanDoItAll.Processes.Drivers.Abstractions.Permissions;
using CanDoItAll.Processes.Drivers.Abstractions.Verification;
using CanDoItAll.Processes.Drivers.ArtifactEvidence;
using CanDoItAll.Processes.Drivers.VerificationGateway;

namespace CanDoItAll.Modules.Processes;

internal sealed class ProcessArtifactEvidenceReadOnlyAdapter
{
    private readonly Func<ArtifactEvidenceVerificationRequest, ProcessDriverVerificationResponse> verifyArtifactEvidence;

    public ProcessArtifactEvidenceReadOnlyAdapter()
        : this(ProcessDriverVerificationGateway.CreateDefault().VerifyArtifactEvidence)
    {
    }

    internal ProcessArtifactEvidenceReadOnlyAdapter(
        Func<ArtifactEvidenceVerificationRequest, ProcessDriverVerificationResponse> verifyArtifactEvidence)
    {
        this.verifyArtifactEvidence = verifyArtifactEvidence ?? throw new ArgumentNullException(nameof(verifyArtifactEvidence));
    }

    public ProcessArtifactEvidenceReadOnlyObservation Verify(ProcessArtifactEvidenceReadOnlyPayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);

        var evidenceReferences = ProcessDriverEvidencePolicy.NormalizeEvidenceReferences(payload.EvidenceReferences);
        var requestedOperations = ProcessReadOnlyVerificationOperationPolicy.Normalize(
            payload.RequestedOperations,
            ProcessReadOnlyVerificationOperationPolicy.ArtifactEvidenceDefaults);
        var verificationRequest = ProcessReadOnlyVerificationRequestFactory.Create(
            payload.PermissionMode,
            payload.Scope,
            evidenceReferences,
            requestedOperations,
            payload.CallerContext);
        var request = new ArtifactEvidenceVerificationRequest(
            verificationRequest,
            payload.SuppliedContent,
            payload.ProjectionLineage,
            payload.ProjectionSourceOrder,
            payload.ProviderNativeBrowserEvidence,
            payload.ValidationRequirements,
            payload.ExpectedArtifacts,
            payload.ArtifactRecords,
            payload.RequestedAt);

        return ProcessArtifactEvidenceObservationMapper.Create(payload, verifyArtifactEvidence(request));
    }
}

internal static class ProcessArtifactEvidenceObservationMapper
{
    public static ProcessArtifactEvidenceReadOnlyObservation Create(
        ProcessArtifactEvidenceReadOnlyPayload payload,
        ProcessDriverVerificationResponse response)
    {
        return new ProcessArtifactEvidenceReadOnlyObservation(
            payload.ProcessRunId,
            payload.StepRunId,
            payload.ArtifactId,
            ProcessArtifactEvidenceSourceLane.ArtifactEvidenceConsistency,
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

internal sealed record ProcessArtifactEvidenceReadOnlyPayload(
    Guid ProcessRunId,
    Guid StepRunId,
    Guid? ArtifactId,
    string CallerContext,
    ProcessDriverPermissionMode PermissionMode,
    ProcessDriverCapabilityScope Scope,
    IReadOnlyList<ProcessDriverEvidenceReference> EvidenceReferences,
    IReadOnlyList<ProcessDriverOperation> RequestedOperations,
    ProcessDriverSuppliedEvidenceContent SuppliedContent,
    IReadOnlyList<ProcessArtifactProjectionLineageDescriptor> ProjectionLineage,
    IReadOnlyList<ProcessArtifactProjectionSourceOrderDescriptor> ProjectionSourceOrder,
    IReadOnlyList<ProcessProviderNativeBrowserEvidenceDescriptor> ProviderNativeBrowserEvidence,
    IReadOnlyList<ProcessArtifactValidationRequirementDescriptor> ValidationRequirements,
    IReadOnlyList<global::CanDoItAll.Processes.Core.Artifacts.ProcessArtifactExpectationSnapshot> ExpectedArtifacts,
    IReadOnlyList<global::CanDoItAll.Processes.Core.Artifacts.ProcessArtifactRecordSnapshot> ArtifactRecords,
    DateTimeOffset RequestedAt);

internal sealed record ProcessArtifactEvidenceReadOnlyObservation(
    Guid ProcessRunId,
    Guid StepRunId,
    Guid? ArtifactId,
    ProcessArtifactEvidenceSourceLane SourceLane,
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

internal enum ProcessArtifactEvidenceSourceLane
{
    ArtifactEvidenceConsistency = 1
}
