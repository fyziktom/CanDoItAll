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
