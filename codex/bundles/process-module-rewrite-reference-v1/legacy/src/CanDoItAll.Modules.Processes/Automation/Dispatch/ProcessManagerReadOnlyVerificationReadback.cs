using CanDoItAll.Processes.Contracts;
using CanDoItAll.Processes.Drivers.Abstractions.Gateway;
using CanDoItAll.Processes.Drivers.Abstractions.Permissions;
using CanDoItAll.Processes.Drivers.Abstractions.Verification;

namespace CanDoItAll.Modules.Processes;

internal sealed record ProcessManagerReadOnlyVerificationReadbackDto(
    ProcessManagerReadOnlyVerificationFacadeStatus Status,
    string CapabilityKey,
    ProcessDriverVerificationGatewayLane Lane,
    Guid ProcessRunId,
    Guid StepRunId,
    string CallerContext,
    ProcessManagerReadOnlyVerificationProjectionMode ProjectionMode,
    ProcessManagerReadOnlyVerificationProjectionSource? ProjectionSource,
    bool ProjectionAttached,
    Guid? AuditRecordId,
    int ResponseCount,
    int DiagnosticCount,
    IReadOnlyList<ProcessManagerReadOnlyVerificationDiagnosticReadbackDto> Diagnostics,
    IReadOnlyList<ProcessManagerReadOnlyVerificationAuditRecordDto> AuditRecords,
    int EvidenceReferenceCount,
    string AuditRecordObservationHash,
    ProcessVerificationHostFailureCategory? DenialCategory,
    ProcessVerificationHostDenialCode? DenialCode,
    string DenialMessage,
    bool NoMutationPerformed,
    bool AllowsProcessMutation,
    bool AllowsTransitionMutation,
    bool AllowsFinalizerMutation,
    DateTimeOffset RequestedAt,
    DateTimeOffset ObservedAt)
{
    public ProcessRuntimeHostContractSnapshot Contract { get; init; } =
        ProcessRuntimeHostContractSnapshot.Create(ProcessRuntimeHostContractSurface.ManagerReadback);
}

internal sealed record ProcessManagerReadOnlyVerificationDiagnosticReadbackDto(
    ProcessDriverCapabilityScopeKind? Lane,
    ProcessDriverDiagnosticSeverity Severity,
    ProcessDriverDiagnosticCategory Category,
    string Message,
    int EvidenceReferenceCount,
    ProcessDriverContractVersion ContractVersion);

internal sealed record ProcessManagerReadOnlyVerificationAuditRecordDto(
    Guid Id,
    DateTimeOffset RecordedAt,
    ProcessDriverVerificationGatewayLane Lane,
    int ResponseCount,
    int AcceptedCount,
    int DeniedCount,
    bool NoMutationPerformed,
    bool AllowsProcessMutation,
    bool AllowsTransitionMutation,
    bool AllowsFinalizerMutation,
    string ObservationHash);

internal static class ProcessManagerReadOnlyVerificationReadbackMapper
{
    public static ProcessManagerReadOnlyVerificationReadbackDto Project(
        ProcessManagerReadOnlyVerificationCommandRequest request,
        ProcessManagerReadOnlyVerificationFacadeResult verificationResult,
        IReadOnlyList<ProcessVerificationAuditRecord> auditRecords)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(verificationResult);
        ArgumentNullException.ThrowIfNull(auditRecords);

        if (verificationResult.Response is { } response)
        {
            return FromResponse(request, verificationResult, response, auditRecords);
        }

        var denial = verificationResult.Denial ??
            throw new InvalidOperationException("Manager verification readback requires either a response or a denial.");
        return FromDenial(request, verificationResult, denial, auditRecords);
    }

    private static ProcessManagerReadOnlyVerificationReadbackDto FromResponse(
        ProcessManagerReadOnlyVerificationCommandRequest request,
        ProcessManagerReadOnlyVerificationFacadeResult result,
        ProcessManagerReadOnlyVerificationCommandResult response,
        IReadOnlyList<ProcessVerificationAuditRecord> auditRecords)
    {
        return new ProcessManagerReadOnlyVerificationReadbackDto(
            result.Status,
            response.CapabilityKey,
            response.Lane,
            response.Observation.ProcessRunId,
            response.Observation.StepRunId,
            response.Observation.CallerContext,
            response.Projection.Mode,
            response.Projection.Source,
            response.Projection.IsAttached,
            response.AuditRecord.Id,
            response.Observation.ResponseCount,
            response.Projection.Diagnostics.Count,
            response.Projection.Diagnostics.Select(ToDiagnosticDto).ToArray(),
            auditRecords.Select(ToAuditRecordDto).ToArray(),
            CountEvidenceReferences(response.Projection),
            response.AuditRecord.ObservationHash,
            null,
            null,
            string.Empty,
            response.NoMutationPerformed,
            response.AllowsProcessMutation,
            response.AllowsTransitionMutation,
            response.AllowsFinalizerMutation,
            request.RequestedAt,
            response.Projection.ObservedAt);
    }

    private static ProcessManagerReadOnlyVerificationReadbackDto FromDenial(
        ProcessManagerReadOnlyVerificationCommandRequest request,
        ProcessManagerReadOnlyVerificationFacadeResult result,
        ProcessVerificationHostDenial denial,
        IReadOnlyList<ProcessVerificationAuditRecord> auditRecords)
    {
        return new ProcessManagerReadOnlyVerificationReadbackDto(
            result.Status,
            denial.CapabilityKey,
            denial.Lane,
            denial.ProcessRunId,
            denial.StepRunId,
            request.Payload.CallerContext,
            request.ProjectionMode,
            null,
            false,
            denial.AuditRecord.Id,
            0,
            0,
            [],
            auditRecords.Select(ToAuditRecordDto).ToArray(),
            0,
            denial.AuditRecord.ObservationHash,
            denial.Category,
            denial.Code,
            denial.Message,
            denial.NoMutationPerformed,
            denial.AllowsProcessMutation,
            denial.AllowsTransitionMutation,
            denial.AllowsFinalizerMutation,
            request.RequestedAt,
            denial.AuditRecord.RecordedAt);
    }

    private static int CountEvidenceReferences(ProcessManagerReadOnlyVerificationProjection projection)
    {
        return projection.EvidenceEnvelope?.EvidenceReferences.Count ??
            projection.Diagnostics.Sum(diagnostic => diagnostic.EvidenceReferences.Count);
    }

    private static ProcessManagerReadOnlyVerificationDiagnosticReadbackDto ToDiagnosticDto(
        ProcessManagerReadOnlyVerificationDiagnostic diagnostic)
    {
        return new ProcessManagerReadOnlyVerificationDiagnosticReadbackDto(
            diagnostic.Lane,
            diagnostic.Severity,
            diagnostic.Category,
            diagnostic.Message,
            diagnostic.EvidenceReferences.Count,
            diagnostic.ContractVersion);
    }

    private static ProcessManagerReadOnlyVerificationAuditRecordDto ToAuditRecordDto(
        ProcessVerificationAuditRecord record)
    {
        return new ProcessManagerReadOnlyVerificationAuditRecordDto(
            record.Id,
            record.RecordedAt,
            record.Lane,
            record.ResponseCount,
            record.AcceptedCount,
            record.DeniedCount,
            record.NoMutationPerformed,
            record.AllowsProcessMutation,
            record.AllowsTransitionMutation,
            record.AllowsFinalizerMutation,
            record.ObservationHash);
    }
}
