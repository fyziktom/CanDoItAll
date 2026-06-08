using CanDoItAll.Processes.Core.Artifacts;
using CanDoItAll.Processes.Drivers.Abstractions.Audit;
using CanDoItAll.Processes.Drivers.Abstractions.Evidence;
using CanDoItAll.Processes.Drivers.Abstractions.Permissions;
using CanDoItAll.Processes.Drivers.Abstractions.Verification;
using CanDoItAll.Processes.Drivers.ArtifactEvidence;
using CanDoItAll.Processes.Drivers.BusinessAnalysis;
using CanDoItAll.Processes.Drivers.ObservationAggregation;
using CanDoItAll.Processes.Drivers.OfficeEvidence;

namespace CanDoItAll.Modules.Processes;

internal sealed class ProcessArtifactEvidenceReadOnlyAdapter
{
    private readonly Func<ArtifactEvidenceVerificationRequest, ProcessDriverVerificationResponse> verifyArtifactEvidence;

    public ProcessArtifactEvidenceReadOnlyAdapter()
        : this(request => new ArtifactEvidenceAlphaVerifier().Verify(request))
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
        var verificationRequest = CreateVerificationRequest(payload, evidenceReferences, requestedOperations);
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

    private static ProcessDriverVerificationRequest CreateVerificationRequest(
        ProcessArtifactEvidenceReadOnlyPayload payload,
        IReadOnlyList<ProcessDriverEvidenceReference> evidenceReferences,
        IReadOnlyList<ProcessDriverOperation> requestedOperations)
    {
        return new ProcessDriverVerificationRequest(
            payload.PermissionMode,
            payload.Scope,
            evidenceReferences,
            requestedOperations,
            payload.CallerContext.Trim(),
            ProcessDriverContractVersion.Current);
    }
}

internal sealed class ProcessOfficeEvidenceReadOnlyAdapter
{
    private readonly Func<OfficeEvidenceVerificationRequest, ProcessDriverVerificationResponse> verifyOfficeEvidence;

    public ProcessOfficeEvidenceReadOnlyAdapter()
        : this(request => new OfficeEvidenceAlphaVerifier().Verify(request))
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
        var verificationRequest = new ProcessDriverVerificationRequest(
            payload.PermissionMode,
            payload.Scope,
            evidenceReferences,
            requestedOperations,
            payload.CallerContext.Trim(),
            ProcessDriverContractVersion.Current);
        var request = new OfficeEvidenceVerificationRequest(
            verificationRequest,
            payload.SuppliedContent,
            payload.Items,
            payload.RequestedAt);

        return ProcessOfficeEvidenceObservationMapper.Create(payload, verifyOfficeEvidence(request));
    }
}

internal sealed class ProcessBusinessAnalysisReadOnlyAdapter
{
    private readonly Func<BusinessAnalysisVerificationRequest, ProcessDriverVerificationResponse> verifyBusinessAnalysis;

    public ProcessBusinessAnalysisReadOnlyAdapter()
        : this(request => new BusinessAnalysisAlphaVerifier().Verify(request))
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
        var verificationRequest = new ProcessDriverVerificationRequest(
            payload.PermissionMode,
            payload.Scope,
            evidenceReferences,
            requestedOperations,
            payload.CallerContext.Trim(),
            ProcessDriverContractVersion.Current);
        var request = new BusinessAnalysisVerificationRequest(
            verificationRequest,
            payload.SuppliedContent,
            payload.Items,
            payload.RequestedAt);

        return ProcessBusinessAnalysisObservationMapper.Create(payload, verifyBusinessAnalysis(request));
    }
}

internal sealed class ProcessDriverObservationAggregationReadOnlyAdapter
{
    private readonly Func<ProcessDriverObservationAggregationRequest, ProcessDriverObservationAggregate> aggregateObservations;

    public ProcessDriverObservationAggregationReadOnlyAdapter()
        : this(request => new ProcessDriverObservationAggregator().Aggregate(request))
    {
    }

    internal ProcessDriverObservationAggregationReadOnlyAdapter(
        Func<ProcessDriverObservationAggregationRequest, ProcessDriverObservationAggregate> aggregateObservations)
    {
        this.aggregateObservations = aggregateObservations ?? throw new ArgumentNullException(nameof(aggregateObservations));
    }

    public ProcessDriverObservationAggregationReadOnlyObservation Aggregate(
        ProcessDriverObservationAggregationReadOnlyPayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);

        var request = new ProcessDriverObservationAggregationRequest(
            payload.Responses,
            payload.RequestedAt,
            payload.CallerContext.Trim());

        return ProcessDriverObservationAggregationMapper.Create(payload, aggregateObservations(request));
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
            payload.RequestedAt);
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
            payload.RequestedAt);
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
            payload.RequestedAt);
    }
}

internal static class ProcessDriverObservationAggregationMapper
{
    public static ProcessDriverObservationAggregationReadOnlyObservation Create(
        ProcessDriverObservationAggregationReadOnlyPayload payload,
        ProcessDriverObservationAggregate aggregate)
    {
        return new ProcessDriverObservationAggregationReadOnlyObservation(
            payload.ProcessRunId,
            payload.StepRunId,
            aggregate.ResponseCount,
            aggregate.AcceptedCount,
            aggregate.DeniedCount,
            aggregate.DiagnosticCount,
            aggregate.ErrorCount,
            aggregate.WarningCount,
            aggregate.AggregationMutationFree,
            aggregate.AllResponsesMutationFree,
            aggregate.LaneSummaries,
            aggregate.EvidenceReferences,
            aggregate.Redaction,
            aggregate.ContractVersion,
            payload.RequestedAt,
            payload.RequestedAt);
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

internal sealed record ProcessDriverObservationAggregationReadOnlyPayload(
    Guid ProcessRunId,
    Guid StepRunId,
    string CallerContext,
    IReadOnlyList<ProcessDriverVerificationResponse> Responses,
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

internal sealed record ProcessDriverObservationAggregationReadOnlyObservation(
    Guid ProcessRunId,
    Guid StepRunId,
    int ResponseCount,
    int AcceptedCount,
    int DeniedCount,
    int DiagnosticCount,
    int ErrorCount,
    int WarningCount,
    bool AggregationMutationFree,
    bool AllResponsesMutationFree,
    IReadOnlyList<ProcessDriverObservationLaneSummary> LaneSummaries,
    IReadOnlyList<ProcessDriverEvidenceReference> EvidenceReferences,
    ProcessDriverRedactionDescriptor Redaction,
    ProcessDriverContractVersion ContractVersion,
    DateTimeOffset RequestedAt,
    DateTimeOffset ObservedAt);

internal enum ProcessArtifactEvidenceSourceLane
{
    ArtifactEvidenceConsistency = 1
}

internal enum ProcessOfficeEvidenceSourceLane
{
    OfficeEvidenceRead = 1
}

internal enum ProcessBusinessAnalysisSourceLane
{
    BusinessAnalysisRead = 1
}
