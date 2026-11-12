using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

internal sealed class AgentInheritedPlanningPreparationAttempt(AgentInheritedPlanningWorkspaceRequest request,
    AgentToolProfileBinding profile, TimeProvider clock) {
    private DateTimeOffset? startedAtUtc;
    private long started;
    private bool saveAttempted;

    internal void Begin(DateTimeOffset atUtc, long timestamp) {
        startedAtUtc = atUtc;
        started = timestamp;
    }

    internal void BeginSave() => saveAttempted = true;

    internal (DateTimeOffset ClosedAtUtc, long WorkTicks)? ObserveCompletion() {
        if (startedAtUtc is not { } beginning || saveAttempted) {
            return null;
        }
        var end = clock.GetUtcNow();
        var elapsed = clock.GetElapsedTime(started);
        if (beginning == default || beginning.Offset != TimeSpan.Zero || end.Offset != TimeSpan.Zero ||
                end < beginning || elapsed < TimeSpan.Zero) {
            return null;
        }
        return (end, Math.Max(elapsed.Ticks, (end - beginning).Ticks));
    }

    internal AgentInheritedPlanningPreparationException CreateFailure(
        (DateTimeOffset ClosedAtUtc, long WorkTicks) closed, Exception primary)
        => new(new(request, profile, startedAtUtc!.Value, closed.ClosedAtUtc, closed.WorkTicks,
            primary is OperationCanceledException), primary);
}
