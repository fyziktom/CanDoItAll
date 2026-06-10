using CanDoItAll.Processes.Drivers.Abstractions.Gateway;
using CanDoItAll.Processes.Drivers.Abstractions.Permissions;

namespace CanDoItAll.Modules.Processes;

internal sealed record ProcessVerificationHostRequest
{
    public ProcessVerificationHostRequest(
        ProcessDriverVerificationGatewayLane lane,
        ProcessReadOnlyVerificationBatchPayload payload,
        string requestedBy,
        DateTimeOffset requestedAt)
    {
        ArgumentNullException.ThrowIfNull(payload);
        if (string.IsNullOrWhiteSpace(requestedBy))
        {
            throw new ArgumentException("A verification host request requires the requesting manager or process identity.", nameof(requestedBy));
        }

        Lane = lane;
        Payload = payload;
        RequestedBy = requestedBy.Trim();
        RequestedAt = requestedAt;
    }

    public ProcessDriverVerificationGatewayLane Lane { get; }

    public ProcessReadOnlyVerificationBatchPayload Payload { get; }

    public string RequestedBy { get; }

    public DateTimeOffset RequestedAt { get; }
}

internal sealed record ProcessVerificationHostResponse(
    ProcessDriverVerificationGatewayLane Lane,
    ProcessVerificationLaneRegistration Registration,
    ProcessReadOnlyVerificationBatchObservation Observation,
    ProcessVerificationAuditRecord AuditRecord,
    bool NoMutationPerformed,
    bool AllowsProcessMutation,
    bool AllowsTransitionMutation,
    bool AllowsFinalizerMutation);

internal sealed record ProcessVerificationHostResult
{
    private ProcessVerificationHostResult(
        ProcessVerificationHostResultStatus Status,
        ProcessVerificationHostResponse? response,
        ProcessVerificationHostDenial? denial)
    {
        if (Status == ProcessVerificationHostResultStatus.Succeeded && response is null)
        {
            throw new ArgumentException("A succeeded verification host result requires a response.", nameof(response));
        }

        if (Status == ProcessVerificationHostResultStatus.Denied && denial is null)
        {
            throw new ArgumentException("A denied verification host result requires a denial.", nameof(denial));
        }

        this.Status = Status;
        Response = response;
        Denial = denial;
    }

    public ProcessVerificationHostResultStatus Status { get; }

    public ProcessVerificationHostResponse? Response { get; }

    public ProcessVerificationHostDenial? Denial { get; }

    public bool IsSuccess => Status == ProcessVerificationHostResultStatus.Succeeded;

    public bool IsDenied => Status == ProcessVerificationHostResultStatus.Denied;

    public static ProcessVerificationHostResult Succeeded(ProcessVerificationHostResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);
        return new ProcessVerificationHostResult(ProcessVerificationHostResultStatus.Succeeded, response, denial: null);
    }

    public static ProcessVerificationHostResult Denied(ProcessVerificationHostDenial denial)
    {
        ArgumentNullException.ThrowIfNull(denial);
        return new ProcessVerificationHostResult(ProcessVerificationHostResultStatus.Denied, response: null, denial);
    }
}

internal enum ProcessVerificationHostResultStatus
{
    Succeeded,
    Denied
}

internal enum ProcessVerificationHostDenialCode
{
    HostDisabled,
    LaneDisabled,
    UnsupportedLane,
    MissingLaneRegistration,
    MissingLanePayload,
    PayloadLimitExceeded,
    SuppliedEvidenceContentLimitExceeded,
    NoResponsesProduced
}

internal enum ProcessVerificationHostFailureCategory
{
    OperationalPolicy,
    LaneConfiguration,
    RequestValidation,
    ResourceLimit,
    VerificationOutcome
}

internal static class ProcessVerificationHostDenialClassifier
{
    public static ProcessVerificationHostFailureCategory Classify(ProcessVerificationHostDenialCode code)
        => code switch
        {
            ProcessVerificationHostDenialCode.HostDisabled or
                ProcessVerificationHostDenialCode.LaneDisabled => ProcessVerificationHostFailureCategory.OperationalPolicy,
            ProcessVerificationHostDenialCode.MissingLaneRegistration => ProcessVerificationHostFailureCategory.LaneConfiguration,
            ProcessVerificationHostDenialCode.UnsupportedLane or
                ProcessVerificationHostDenialCode.MissingLanePayload => ProcessVerificationHostFailureCategory.RequestValidation,
            ProcessVerificationHostDenialCode.PayloadLimitExceeded or
                ProcessVerificationHostDenialCode.SuppliedEvidenceContentLimitExceeded => ProcessVerificationHostFailureCategory.ResourceLimit,
            ProcessVerificationHostDenialCode.NoResponsesProduced => ProcessVerificationHostFailureCategory.VerificationOutcome,
            _ => throw new ArgumentOutOfRangeException(nameof(code), code, "Unsupported verification host denial code.")
        };
}

internal sealed record ProcessVerificationHostDenial(
    ProcessVerificationHostFailureCategory Category,
    ProcessVerificationHostDenialCode Code,
    string Message,
    ProcessDriverVerificationGatewayLane Lane,
    Guid ProcessRunId,
    Guid StepRunId,
    string RequestedBy,
    DateTimeOffset RequestedAt,
    ProcessVerificationAuditRecord AuditRecord,
    bool NoMutationPerformed,
    bool AllowsProcessMutation,
    bool AllowsTransitionMutation,
    bool AllowsFinalizerMutation);

internal sealed record ProcessVerificationLaneRegistration(
    ProcessDriverVerificationGatewayLane Lane,
    ProcessDriverCapabilityScopeKind RequiredScopeKind,
    ProcessDriverPermissionMode RequiredPermissionMode,
    IReadOnlyList<ProcessDriverOperation> AllowedOperations);

internal sealed record ProcessVerificationAuditRecord(
    Guid Id,
    DateTimeOffset RecordedAt,
    Guid ProcessRunId,
    Guid StepRunId,
    string RequestedBy,
    ProcessDriverVerificationGatewayLane Lane,
    int ResponseCount,
    int AcceptedCount,
    int DeniedCount,
    bool NoMutationPerformed,
    bool AllowsProcessMutation,
    bool AllowsTransitionMutation,
    bool AllowsFinalizerMutation,
    string ObservationHash);
