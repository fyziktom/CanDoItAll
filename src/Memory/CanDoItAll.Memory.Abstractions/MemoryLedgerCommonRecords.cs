namespace CanDoItAll.Memory.Abstractions;

public sealed record MemoryLedgerRequester(
    string RequesterId,
    string? AgentId,
    string? AgentRole,
    string? SessionId,
    string? WorkflowId,
    string? WorkflowNodeId,
    string? ProcessId,
    string? ProcessStepId);

public static class MemoryLedgerStatusReasons
{
    public const string Created = "created";
    public const string Received = "received";
}

public static class MemoryEventOutboxPayloadKinds
{
    public const string Acknowledgement = "event.acknowledgement";
}

public sealed record MemoryLedgerRetentionPolicy
{
    public MemoryLedgerRetentionPolicy(
        DateTimeOffset expiresAtUtc,
        DateTimeOffset forgetAtUtc,
        bool deletePayloadOnForget)
    {
        if (forgetAtUtc < expiresAtUtc)
        {
            throw new ArgumentOutOfRangeException(nameof(forgetAtUtc), "Forget time cannot be earlier than expiry time.");
        }

        ExpiresAtUtc = expiresAtUtc;
        ForgetAtUtc = forgetAtUtc;
        DeletePayloadOnForget = deletePayloadOnForget;
    }

    public DateTimeOffset ExpiresAtUtc { get; }

    public DateTimeOffset ForgetAtUtc { get; }

    public bool DeletePayloadOnForget { get; }

    public static MemoryLedgerRetentionPolicy Expiring(
        DateTimeOffset expiresAtUtc,
        DateTimeOffset forgetAtUtc,
        bool deletePayloadOnForget = true) =>
        new(expiresAtUtc, forgetAtUtc, deletePayloadOnForget);
}

public sealed record MemoryIpfsSnapshotMetadata(
    string SnapshotUri,
    MemoryIpfsPinState PinState,
    DateTimeOffset? PinnedAtUtc,
    DateTimeOffset? UnpinRequestedAtUtc,
    string? UnpinReason)
{
    public MemoryIpfsSnapshotMetadata RequestUnpin(
        DateTimeOffset requestedAtUtc,
        string reason)
    {
        MemoryProtocolGuard.EnsureText(SnapshotUri, nameof(SnapshotUri));
        var normalizedReason = MemoryProtocolGuard.EnsureText(reason, nameof(reason));
        return this with
        {
            PinState = MemoryIpfsPinState.UnpinRequested,
            UnpinRequestedAtUtc = requestedAtUtc,
            UnpinReason = normalizedReason
        };
    }
}
