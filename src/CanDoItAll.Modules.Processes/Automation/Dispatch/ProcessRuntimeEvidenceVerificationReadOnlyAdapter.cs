using CanDoItAll.Processes.Core.Artifacts;
using CanDoItAll.Processes.Core.Diagnostics;
using CanDoItAll.Processes.Core.Execution;
using CanDoItAll.Processes.Core.Finalization;
using CanDoItAll.Processes.Drivers.Abstractions.Audit;
using CanDoItAll.Processes.Drivers.Abstractions.Evidence;
using CanDoItAll.Processes.Drivers.Abstractions.Permissions;
using CanDoItAll.Processes.Drivers.Abstractions.Verification;
using CanDoItAll.Processes.Drivers.RuntimeEvidence;
using CanDoItAll.Processes.Drivers.VerificationGateway;

namespace CanDoItAll.Modules.Processes;

internal sealed class ProcessRuntimeEvidenceVerificationReadOnlyAdapter
{
    private readonly Func<RuntimeEvidenceConsistencyVerificationRequest, ProcessDriverVerificationResponse> verifyRuntimeEvidence;

    public ProcessRuntimeEvidenceVerificationReadOnlyAdapter()
        : this(ProcessDriverVerificationGateway.CreateDefault().VerifyRuntimeEvidence)
    {
    }

    internal ProcessRuntimeEvidenceVerificationReadOnlyAdapter(
        Func<RuntimeEvidenceConsistencyVerificationRequest, ProcessDriverVerificationResponse> verifyRuntimeEvidence)
    {
        this.verifyRuntimeEvidence = verifyRuntimeEvidence ?? throw new ArgumentNullException(nameof(verifyRuntimeEvidence));
    }

    public ProcessRuntimeEvidenceVerificationReadOnlyObservation Verify(
        ProcessRuntimeEvidenceVerificationReadOnlyEvidencePayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);

        var evidenceReferences = ProcessDriverEvidencePolicy.NormalizeEvidenceReferences(payload.EvidenceReferences);
        var requestedOperations = ProcessReadOnlyVerificationOperationPolicy.Normalize(
            payload.RequestedOperations,
            ProcessReadOnlyVerificationOperationPolicy.RuntimeEvidenceDefaults);
        var suppliedContent = CreateSuppliedContent(payload, evidenceReferences);
        var verificationRequest = ProcessReadOnlyVerificationRequestFactory.Create(
            payload.PermissionMode,
            payload.Scope,
            evidenceReferences,
            requestedOperations,
            payload.CallerContext);
        var request = new RuntimeEvidenceConsistencyVerificationRequest(
            verificationRequest,
            suppliedContent,
            payload.ExecutionEvidence,
            payload.FinalizerEvidence,
            payload.RetryDiagnostic,
            payload.NoProgressDiagnostic,
            payload.ProviderRepairDiagnostic,
            payload.ProjectionSourceOrder,
            payload.RequestedAt);

        return ProcessRuntimeEvidenceVerificationObservationMapper.Create(
            payload,
            verifyRuntimeEvidence(request));
    }

    private static ProcessDriverSuppliedEvidenceContent CreateSuppliedContent(
        ProcessRuntimeEvidenceVerificationReadOnlyEvidencePayload payload,
        IReadOnlyList<ProcessDriverEvidenceReference> evidenceReferences)
    {
        var material = CreateDescriptorPayloadMaterial(payload);
        var evidenceReference = evidenceReferences.FirstOrDefault() ?? new ProcessDriverEvidenceReference(
            ProcessDriverEvidenceReferenceKind.CoreDescriptor,
            "bundle://runtime-evidence/supplied-descriptor-payload",
            ProcessDriverEvidencePolicy.ComputeSha256(material),
            ProcessDriverCoreDescriptorFamily.ExecutionEvidence);

        return ProcessDriverSuppliedEvidenceContentRules.CreateCoreDescriptorPayload(
            evidenceReference,
            material);
    }

    private static string CreateDescriptorPayloadMaterial(
        ProcessRuntimeEvidenceVerificationReadOnlyEvidencePayload payload)
    {
        return string.Join(
            "|",
            payload.ExecutionEvidence?.Run.ExecutionRunId,
            payload.FinalizerEvidence?.Intent.ProcessRunId,
            payload.RetryDiagnostic?.AttemptNumber,
            payload.NoProgressDiagnostic?.Fingerprint,
            payload.ProviderRepairDiagnostic?.FailureSummary,
            payload.ProjectionSourceOrder.Count);
    }
}

internal sealed record ProcessRuntimeEvidenceVerificationReadOnlyEvidencePayload(
    Guid ProcessRunId,
    Guid StepRunId,
    Guid? ArtifactId,
    string CallerContext,
    ProcessDriverPermissionMode PermissionMode,
    ProcessDriverCapabilityScope Scope,
    IReadOnlyList<ProcessDriverEvidenceReference> EvidenceReferences,
    IReadOnlyList<ProcessDriverOperation> RequestedOperations,
    ProcessExecutionEvidenceDescriptor? ExecutionEvidence,
    ProcessFinalizerEvidenceDescriptor? FinalizerEvidence,
    ProcessRetryDiagnosticDescriptor? RetryDiagnostic,
    ProcessNoProgressRetryDiagnosticDescriptor? NoProgressDiagnostic,
    ProcessProviderRepairDiagnosticDescriptor? ProviderRepairDiagnostic,
    IReadOnlyList<ProcessArtifactProjectionSourceOrderDescriptor> ProjectionSourceOrder,
    DateTimeOffset RequestedAt);

internal sealed record ProcessRuntimeEvidenceVerificationReadOnlyObservation(
    Guid ProcessRunId,
    Guid StepRunId,
    Guid? ArtifactId,
    ProcessRuntimeEvidenceVerificationSourceLane SourceLane,
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

internal enum ProcessRuntimeEvidenceVerificationSourceLane
{
    RuntimeEvidenceConsistency = 1
}
