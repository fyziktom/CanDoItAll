using CanDoItAll.Processes.Drivers.Abstractions.Gateway;
using CanDoItAll.Processes.Drivers.Abstractions.Permissions;
using CanDoItAll.Processes.Drivers.Abstractions.Verification;

namespace CanDoItAll.Modules.Processes;

internal interface IProcessManagerReadOnlyVerificationFacade
{
    Task<ProcessManagerReadOnlyVerificationFacadeResult> VerifyAsync(
        ProcessManagerReadOnlyVerificationCommandRequest request,
        CancellationToken cancellationToken = default);

    Task<ProcessManagerReadOnlyVerificationAuditQueryResult> ListAuditAsync(
        ProcessManagerReadOnlyVerificationAuditQueryRequest request,
        CancellationToken cancellationToken = default);

    Task<ProcessManagerReadOnlyVerificationReadbackDto> VerifyForReadbackAsync(
        ProcessManagerReadOnlyVerificationReadbackRequest request,
        CancellationToken cancellationToken = default);
}

internal sealed class ProcessManagerReadOnlyVerificationCommandService : IProcessManagerReadOnlyVerificationFacade
{
    private readonly IProcessVerificationRuntimeHost host;
    private readonly IProcessVerificationAuditQueryService auditQueryService;

    public ProcessManagerReadOnlyVerificationCommandService(
        IProcessVerificationRuntimeHost host,
        IProcessVerificationAuditQueryService auditQueryService)
    {
        this.host = host ?? throw new ArgumentNullException(nameof(host));
        this.auditQueryService = auditQueryService ?? throw new ArgumentNullException(nameof(auditQueryService));
    }

    public ProcessManagerReadOnlyVerificationCommandResult Run(ProcessManagerReadOnlyVerificationCommandRequest request)
    {
        var result = VerifyAsync(request).GetAwaiter().GetResult();
        if (result.Response is not null)
        {
            return result.Response;
        }

        var denial = result.Denial ?? throw new InvalidOperationException("Manager read-only verification returned neither response nor denial.");
        throw new InvalidOperationException(denial.Message);
    }

    public async Task<ProcessManagerReadOnlyVerificationFacadeResult> VerifyAsync(
        ProcessManagerReadOnlyVerificationCommandRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var hostResult = await host.VerifyAsync(new ProcessVerificationHostRequest(
            request.Lane,
            request.Payload,
            request.RequestedBy,
            request.RequestedAt),
            cancellationToken);
        if (hostResult.Denial is not null)
        {
            return ProcessManagerReadOnlyVerificationFacadeResult.Denied(hostResult.Denial);
        }

        var hostResponse = hostResult.Response ??
            throw new InvalidOperationException("Verification host returned neither response nor denial.");
        var projection = ProcessManagerReadOnlyVerificationProjectionMapper.Project(
            new ProcessManagerReadOnlyVerificationProjectionRequest(
                hostResponse.Observation,
                request.ProjectionMode,
                hostResponse.AuditRecord.RequestedBy,
                request.RequestedAt,
                hostResponse.AuditRecord.Id));

        return ProcessManagerReadOnlyVerificationFacadeResult.Succeeded(new ProcessManagerReadOnlyVerificationCommandResult(
            request.Lane,
            hostResponse.Observation,
            projection,
            hostResponse.AuditRecord,
            hostResponse.NoMutationPerformed,
            hostResponse.AllowsProcessMutation,
            hostResponse.AllowsTransitionMutation,
            hostResponse.AllowsFinalizerMutation));
    }

    public async Task<ProcessManagerReadOnlyVerificationAuditQueryResult> ListAuditAsync(
        ProcessManagerReadOnlyVerificationAuditQueryRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var records = await auditQueryService.ListAsync(
            new ProcessVerificationAuditQuery(
                request.ProcessRunId,
                request.StepRunId,
                request.Lane,
                request.Limit),
            cancellationToken);

        return new ProcessManagerReadOnlyVerificationAuditQueryResult(
            request.RequestedBy,
            records,
            NoMutationPerformed: true,
            AllowsProcessMutation: false,
            AllowsTransitionMutation: false,
            AllowsFinalizerMutation: false);
    }

    public async Task<ProcessManagerReadOnlyVerificationReadbackDto> VerifyForReadbackAsync(
        ProcessManagerReadOnlyVerificationReadbackRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var verificationResult = await VerifyAsync(request.VerificationRequest, cancellationToken);
        var auditReadback = await ListAuditAsync(new ProcessManagerReadOnlyVerificationAuditQueryRequest(
            request.VerificationRequest.RequestedBy,
            request.VerificationRequest.Payload.ProcessRunId,
            request.VerificationRequest.Payload.StepRunId,
            request.VerificationRequest.Lane,
            request.AuditRecordLimit),
            cancellationToken);

        return ProcessManagerReadOnlyVerificationReadbackMapper.Project(
            request.VerificationRequest,
            verificationResult,
            auditReadback.Records);
    }
}

internal sealed record ProcessManagerReadOnlyVerificationCommandRequest
{
    public ProcessManagerReadOnlyVerificationCommandRequest(
        ProcessDriverVerificationGatewayLane lane,
        ProcessReadOnlyVerificationBatchPayload payload,
        ProcessManagerReadOnlyVerificationProjectionMode projectionMode,
        string requestedBy,
        DateTimeOffset requestedAt)
    {
        ArgumentNullException.ThrowIfNull(payload);
        if (!Enum.IsDefined(projectionMode))
        {
            throw new ArgumentOutOfRangeException(nameof(projectionMode), projectionMode, "Unsupported manager verification projection mode.");
        }

        if (string.IsNullOrWhiteSpace(requestedBy))
        {
            throw new ArgumentException("Manager read-only verification requires a requesting manager identity.", nameof(requestedBy));
        }

        Lane = lane;
        Payload = payload;
        ProjectionMode = projectionMode;
        RequestedBy = requestedBy.Trim();
        RequestedAt = requestedAt;
    }

    public ProcessDriverVerificationGatewayLane Lane { get; }

    public ProcessReadOnlyVerificationBatchPayload Payload { get; }

    public ProcessManagerReadOnlyVerificationProjectionMode ProjectionMode { get; }

    public string RequestedBy { get; }

    public DateTimeOffset RequestedAt { get; }
}

internal sealed record ProcessManagerReadOnlyVerificationAuditQueryRequest
{
    public ProcessManagerReadOnlyVerificationAuditQueryRequest(
        string requestedBy,
        Guid? processRunId = null,
        Guid? stepRunId = null,
        ProcessDriverVerificationGatewayLane? lane = null,
        int limit = 100)
    {
        if (string.IsNullOrWhiteSpace(requestedBy))
        {
            throw new ArgumentException("Manager read-only audit query requires a requesting manager identity.", nameof(requestedBy));
        }

        if (limit <= 0 || limit > ProcessVerificationAuditQuery.MaximumLimit)
        {
            throw new ArgumentOutOfRangeException(
                nameof(limit),
                limit,
                $"Manager read-only audit query limit must be between 1 and {ProcessVerificationAuditQuery.MaximumLimit}.");
        }

        RequestedBy = requestedBy.Trim();
        ProcessRunId = processRunId;
        StepRunId = stepRunId;
        Lane = lane;
        Limit = limit;
    }

    public string RequestedBy { get; }

    public Guid? ProcessRunId { get; }

    public Guid? StepRunId { get; }

    public ProcessDriverVerificationGatewayLane? Lane { get; }

    public int Limit { get; }
}

internal sealed record ProcessManagerReadOnlyVerificationReadbackRequest
{
    public const int DefaultAuditRecordLimit = 25;

    public ProcessManagerReadOnlyVerificationReadbackRequest(
        ProcessManagerReadOnlyVerificationCommandRequest verificationRequest,
        int auditRecordLimit = DefaultAuditRecordLimit)
    {
        ArgumentNullException.ThrowIfNull(verificationRequest);
        if (auditRecordLimit <= 0 || auditRecordLimit > ProcessVerificationAuditQuery.MaximumLimit)
        {
            throw new ArgumentOutOfRangeException(
                nameof(auditRecordLimit),
                auditRecordLimit,
                $"Manager verification readback audit limit must be between 1 and {ProcessVerificationAuditQuery.MaximumLimit}.");
        }

        VerificationRequest = verificationRequest;
        AuditRecordLimit = auditRecordLimit;
    }

    public ProcessManagerReadOnlyVerificationCommandRequest VerificationRequest { get; }

    public int AuditRecordLimit { get; }
}

internal sealed record ProcessManagerReadOnlyVerificationFacadeResult
{
    private ProcessManagerReadOnlyVerificationFacadeResult(
        ProcessManagerReadOnlyVerificationFacadeStatus status,
        ProcessManagerReadOnlyVerificationCommandResult? response,
        ProcessVerificationHostDenial? denial)
    {
        if (status == ProcessManagerReadOnlyVerificationFacadeStatus.Succeeded && response is null)
        {
            throw new ArgumentException("A succeeded manager read-only verification result requires a response.", nameof(response));
        }

        if (status == ProcessManagerReadOnlyVerificationFacadeStatus.Denied && denial is null)
        {
            throw new ArgumentException("A denied manager read-only verification result requires a denial.", nameof(denial));
        }

        Status = status;
        Response = response;
        Denial = denial;
    }

    public ProcessManagerReadOnlyVerificationFacadeStatus Status { get; }

    public ProcessManagerReadOnlyVerificationCommandResult? Response { get; }

    public ProcessVerificationHostDenial? Denial { get; }

    public bool IsSuccess => Status == ProcessManagerReadOnlyVerificationFacadeStatus.Succeeded;

    public bool IsDenied => Status == ProcessManagerReadOnlyVerificationFacadeStatus.Denied;

    public bool NoMutationPerformed => Response?.NoMutationPerformed ?? Denial?.NoMutationPerformed ?? true;

    public bool AllowsProcessMutation => Response?.AllowsProcessMutation ?? Denial?.AllowsProcessMutation ?? false;

    public bool AllowsTransitionMutation => Response?.AllowsTransitionMutation ?? Denial?.AllowsTransitionMutation ?? false;

    public bool AllowsFinalizerMutation => Response?.AllowsFinalizerMutation ?? Denial?.AllowsFinalizerMutation ?? false;

    public static ProcessManagerReadOnlyVerificationFacadeResult Succeeded(ProcessManagerReadOnlyVerificationCommandResult response)
    {
        ArgumentNullException.ThrowIfNull(response);
        return new ProcessManagerReadOnlyVerificationFacadeResult(ProcessManagerReadOnlyVerificationFacadeStatus.Succeeded, response, denial: null);
    }

    public static ProcessManagerReadOnlyVerificationFacadeResult Denied(ProcessVerificationHostDenial denial)
    {
        ArgumentNullException.ThrowIfNull(denial);
        return new ProcessManagerReadOnlyVerificationFacadeResult(ProcessManagerReadOnlyVerificationFacadeStatus.Denied, response: null, denial);
    }
}

internal enum ProcessManagerReadOnlyVerificationFacadeStatus
{
    Succeeded,
    Denied
}

internal sealed record ProcessManagerReadOnlyVerificationCommandResult(
    ProcessDriverVerificationGatewayLane Lane,
    ProcessReadOnlyVerificationBatchObservation Observation,
    ProcessManagerReadOnlyVerificationProjection Projection,
    ProcessVerificationAuditRecord AuditRecord,
    bool NoMutationPerformed,
    bool AllowsProcessMutation,
    bool AllowsTransitionMutation,
    bool AllowsFinalizerMutation);

internal sealed record ProcessManagerReadOnlyVerificationAuditQueryResult(
    string RequestedBy,
    IReadOnlyList<ProcessVerificationAuditRecord> Records,
    bool NoMutationPerformed,
    bool AllowsProcessMutation,
    bool AllowsTransitionMutation,
    bool AllowsFinalizerMutation);

internal sealed record ProcessManagerReadOnlyVerificationReadbackDto(
    ProcessManagerReadOnlyVerificationFacadeStatus Status,
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
    ProcessVerificationHostFailureCategory? DenialCategory,
    ProcessVerificationHostDenialCode? DenialCode,
    string DenialMessage,
    bool NoMutationPerformed,
    bool AllowsProcessMutation,
    bool AllowsTransitionMutation,
    bool AllowsFinalizerMutation,
    DateTimeOffset RequestedAt,
    DateTimeOffset ObservedAt);

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
            DenialCategory: null,
            DenialCode: null,
            DenialMessage: string.Empty,
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
            denial.Lane,
            denial.ProcessRunId,
            denial.StepRunId,
            request.Payload.CallerContext,
            request.ProjectionMode,
            ProjectionSource: null,
            ProjectionAttached: false,
            denial.AuditRecord.Id,
            ResponseCount: 0,
            DiagnosticCount: 0,
            Diagnostics: [],
            auditRecords.Select(ToAuditRecordDto).ToArray(),
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
