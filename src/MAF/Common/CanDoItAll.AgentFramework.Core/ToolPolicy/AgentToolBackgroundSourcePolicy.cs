using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

public sealed record AgentToolBackgroundSourceObservation(
    AgentToolSemanticDigest OwnerFingerprint,
    bool ReadAllowed,
    bool DispatchAllowed);

public sealed record AgentToolBackgroundReservationObservation(
    AgentToolSemanticDigest OwnerFingerprint, IReadOnlySet<Guid> AcknowledgedExecutionRunIds);

public sealed record AgentToolBackgroundReservation(Guid CandidateExecutionRunId, AgentToolProfileBinding Profile,
    AgentToolSemanticDigest OwnerFingerprint, IReadOnlySet<Guid> AcknowledgedExecutionRunIds);

public interface IAgentToolBackgroundReservationPolicy : IAgentToolBackgroundSourcePolicy {
    ValueTask<AgentToolBackgroundReservationObservation> ObserveReservationAsync(ExecutionRunRecord candidate,
        AgentToolProfileBinding profile, CancellationToken cancellationToken = default);
}

public interface IAgentToolBackgroundSourcePolicy {
    string SourceKind { get; }
    ValueTask<AgentToolBackgroundSourceObservation> ObserveAsync(ExecutionRunRecord execution,
        AgentToolProfileBinding profile, CancellationToken cancellationToken = default);
}
