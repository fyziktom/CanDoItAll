using CanDoItAll.Processes.Drivers.Abstractions.Gateway;

namespace CanDoItAll.Modules.Processes;

internal enum ProcessReadOnlyVerificationJobSourceKind
{
    Scheduler = 1,
    Workflow = 2
}

internal sealed record ProcessReadOnlyVerificationJob
{
    public ProcessReadOnlyVerificationJob(
        Guid id,
        ProcessReadOnlyVerificationJobSourceKind sourceKind,
        string sourceReference,
        string correlationId,
        ProcessDriverVerificationGatewayLane lane,
        ProcessReadOnlyVerificationBatchPayload payload,
        ProcessManagerReadOnlyVerificationProjectionMode projectionMode,
        string requestedBy,
        DateTimeOffset requestedAt,
        int auditRecordLimit = ProcessManagerReadOnlyVerificationReadbackRequest.DefaultAuditRecordLimit)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Read-only verification job id is required.", nameof(id));
        }

        if (!Enum.IsDefined(sourceKind))
        {
            throw new ArgumentOutOfRangeException(nameof(sourceKind), sourceKind, "Unsupported read-only verification job source kind.");
        }

        if (string.IsNullOrWhiteSpace(sourceReference))
        {
            throw new ArgumentException("Read-only verification job source reference is required.", nameof(sourceReference));
        }

        if (string.IsNullOrWhiteSpace(correlationId))
        {
            throw new ArgumentException("Read-only verification job correlation id is required.", nameof(correlationId));
        }

        ArgumentNullException.ThrowIfNull(payload);
        if (!Enum.IsDefined(projectionMode))
        {
            throw new ArgumentOutOfRangeException(nameof(projectionMode), projectionMode, "Unsupported manager verification projection mode.");
        }

        if (string.IsNullOrWhiteSpace(requestedBy))
        {
            throw new ArgumentException("Read-only verification job requires a requester identity.", nameof(requestedBy));
        }

        if (auditRecordLimit <= 0 || auditRecordLimit > ProcessVerificationAuditQuery.MaximumLimit)
        {
            throw new ArgumentOutOfRangeException(
                nameof(auditRecordLimit),
                auditRecordLimit,
                $"Read-only verification job audit limit must be between 1 and {ProcessVerificationAuditQuery.MaximumLimit}.");
        }

        Id = id;
        SourceKind = sourceKind;
        SourceReference = sourceReference.Trim();
        CorrelationId = correlationId.Trim();
        Lane = lane;
        Payload = payload;
        ProjectionMode = projectionMode;
        RequestedBy = requestedBy.Trim();
        RequestedAt = requestedAt;
        AuditRecordLimit = auditRecordLimit;
    }

    public Guid Id { get; }

    public ProcessReadOnlyVerificationJobSourceKind SourceKind { get; }

    public string SourceReference { get; }

    public string CorrelationId { get; }

    public ProcessDriverVerificationGatewayLane Lane { get; }

    public ProcessReadOnlyVerificationBatchPayload Payload { get; }

    public ProcessManagerReadOnlyVerificationProjectionMode ProjectionMode { get; }

    public string RequestedBy { get; }

    public DateTimeOffset RequestedAt { get; }

    public int AuditRecordLimit { get; }

    public bool NoMutationPerformed => true;

    public bool AllowsProcessMutation => false;

    public bool AllowsTransitionMutation => false;

    public bool AllowsFinalizerMutation => false;

    public ProcessManagerReadOnlyVerificationReadbackRequest ToManagerReadbackRequest()
    {
        return new ProcessManagerReadOnlyVerificationReadbackRequest(
            new ProcessManagerReadOnlyVerificationCommandRequest(
                Lane,
                Payload,
                ProjectionMode,
                RequestedBy,
                RequestedAt),
            AuditRecordLimit);
    }
}
