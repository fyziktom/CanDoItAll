using CanDoItAll.Processes.Drivers.Abstractions.Audit;
using CanDoItAll.Processes.Drivers.Abstractions.Evidence;
using CanDoItAll.Processes.Drivers.Abstractions.Permissions;
using CanDoItAll.Processes.Drivers.Abstractions.Verification;
using CanDoItAll.Processes.Drivers.BusinessAnalysis;
using CanDoItAll.Processes.Drivers.VerificationGateway;

namespace CanDoItAll.Modules.Processes;

internal sealed class ProcessBusinessAnalysisReadOnlyAdapter
{
    private readonly Func<BusinessAnalysisVerificationRequest, ProcessDriverVerificationResponse> verifyBusinessAnalysis;

    public ProcessBusinessAnalysisReadOnlyAdapter()
        : this(ProcessDriverVerificationGateway.CreateDefault().VerifyBusinessAnalysis)
    {
    }

    internal ProcessBusinessAnalysisReadOnlyAdapter(
        Func<BusinessAnalysisVerificationRequest, ProcessDriverVerificationResponse> verifyBusinessAnalysis)
    {
        this.verifyBusinessAnalysis = verifyBusinessAnalysis ?? throw new ArgumentNullException(nameof(verifyBusinessAnalysis));
    }

    public ProcessBusinessAnalysisReadOnlyObservation Verify(ProcessBusinessAnalysisReadOnlyPayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);

        var evidenceReferences = ProcessDriverEvidencePolicy.NormalizeEvidenceReferences(payload.EvidenceReferences);
        var requestedOperations = ProcessReadOnlyVerificationOperationPolicy.Normalize(
            payload.RequestedOperations,
            ProcessReadOnlyVerificationOperationPolicy.BusinessAnalysisDefaults);
        var verificationRequest = ProcessReadOnlyVerificationRequestFactory.Create(
            payload.PermissionMode,
            payload.Scope,
            evidenceReferences,
            requestedOperations,
            payload.CallerContext);
        var request = new BusinessAnalysisVerificationRequest(
            verificationRequest,
            payload.SuppliedContent,
            payload.Items,
            payload.RequestedAt);

        return ProcessBusinessAnalysisObservationMapper.Create(payload, verifyBusinessAnalysis(request));
    }
}

internal static class ProcessBusinessAnalysisObservationMapper
{
    public static ProcessBusinessAnalysisReadOnlyObservation Create(
        ProcessBusinessAnalysisReadOnlyPayload payload,
        ProcessDriverVerificationResponse response)
    {
        return new ProcessBusinessAnalysisReadOnlyObservation(
            payload.ProcessRunId,
            payload.StepRunId,
            payload.ArtifactId,
            ProcessBusinessAnalysisSourceLane.BusinessAnalysisRead,
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

internal sealed record ProcessBusinessAnalysisReadOnlyPayload(
    Guid ProcessRunId,
    Guid StepRunId,
    Guid? ArtifactId,
    string CallerContext,
    ProcessDriverPermissionMode PermissionMode,
    ProcessDriverCapabilityScope Scope,
    IReadOnlyList<ProcessDriverEvidenceReference> EvidenceReferences,
    IReadOnlyList<ProcessDriverOperation> RequestedOperations,
    ProcessDriverSuppliedEvidenceContent SuppliedContent,
    IReadOnlyList<BusinessAnalysisEvidenceItem> Items,
    DateTimeOffset RequestedAt);

internal sealed record ProcessBusinessAnalysisReadOnlyObservation(
    Guid ProcessRunId,
    Guid StepRunId,
    Guid? ArtifactId,
    ProcessBusinessAnalysisSourceLane SourceLane,
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

internal enum ProcessBusinessAnalysisSourceLane
{
    BusinessAnalysisRead = 1
}
