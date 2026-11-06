namespace CanDoItAll.AgentFramework.Core;

public sealed class AgentTeamConcurrencyException(Guid teamId) : InvalidOperationException(
    $"Team '{teamId:D}' requires an unchanged original observation. Review its current configuration before applying this intent.") {
    public Guid TeamId { get; } = teamId;
}
