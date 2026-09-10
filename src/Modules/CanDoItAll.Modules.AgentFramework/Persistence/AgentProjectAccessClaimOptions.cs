namespace CanDoItAll.Modules.AgentFramework;

public sealed record AgentProjectAccessClaimOptions(TimeSpan LeaseDuration, TimeSpan HeartbeatInterval, TimeSpan FailurePersistenceTimeout) {
    public static AgentProjectAccessClaimOptions Default { get; } = new(TimeSpan.FromMinutes(5), TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(5));

    public AgentProjectAccessClaimOptions Validate() {
        if (LeaseDuration <= TimeSpan.Zero || HeartbeatInterval <= TimeSpan.Zero || FailurePersistenceTimeout <= TimeSpan.Zero ||
            HeartbeatInterval >= LeaseDuration) {
            throw new InvalidOperationException("Agent project-access claims require positive durations and a heartbeat interval shorter than the lease.");
        }
        return this;
    }
}

internal sealed record AgentProjectAccessRevocationClaim(Guid ProjectId, Guid RecoveryId, int Generation);
