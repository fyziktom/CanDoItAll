using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

public sealed record AgentToolBackgroundSourceObservation(
    AgentToolSemanticDigest OwnerFingerprint,
    bool ReadAllowed,
    bool DispatchAllowed);

public sealed record AgentToolBackgroundReservationObservation(
    AgentToolSemanticDigest OwnerFingerprint, IReadOnlySet<Guid> AcknowledgedExecutionRunIds);

public sealed record AgentToolBackgroundReservation(Guid CandidateExecutionRunId, AgentToolProfileBinding Profile,
    AgentToolSemanticDigest OwnerFingerprint, IReadOnlySet<Guid> AcknowledgedExecutionRunIds,
    Func<CancellationToken, Task<IAsyncDisposable>> AcquirePublication);

public interface IAgentToolBackgroundReservationPolicy : IAgentToolBackgroundSourcePolicy {
    ValueTask<AgentToolBackgroundReservationObservation> ObserveReservationAsync(ExecutionRunRecord candidate,
        AgentToolProfileBinding profile, CancellationToken cancellationToken = default);

    Task<IAsyncDisposable> AcquirePublicationAsync(ExecutionRunRecord candidate, AgentToolProfileBinding profile,
        AgentToolSemanticDigest ownerFingerprint, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("This background owner does not fence initial execution publication.");
}

public interface IAgentToolBackgroundSourcePolicy {
    string SourceKind { get; }
    ValueTask<AgentToolBackgroundSourceObservation> ObserveAsync(ExecutionRunRecord execution,
        AgentToolProfileBinding profile, CancellationToken cancellationToken = default);

    ValueTask<AgentToolBackgroundSourceObservation> ObserveInitialInheritedAsync(ExecutionRunRecord candidate,
        AgentToolProfileBinding profile, AgentToolProtocolEnvelope originalParent, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("This background source does not support initial inherited planning admission.");
}
