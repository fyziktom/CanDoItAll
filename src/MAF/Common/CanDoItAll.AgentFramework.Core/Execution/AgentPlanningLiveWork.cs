using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

public sealed class AgentPlanningLiveWork {
    private readonly AgentPlanningDecisionOwner owner;
    private long observedTicks;

    internal AgentPlanningLiveWork(AgentPlanningDecisionOwner owner, AgentToolRunLease lease,
        ExecutionRunRecord originalRun, long startedTimestamp) {
        this.owner = owner;
        Lease = lease;
        OriginalRun = originalRun;
        StartedTimestamp = startedTimestamp;
        var decision = originalRun.ToolAdmission!.PlanningDecision!;
        DecisionId = decision.DecisionId;
        Generation = decision.CurrentGeneration;
        StepId = decision.Work.Open!.StepId;
        IntervalId = decision.Work.Open.Id;
    }

    public Guid DecisionId { get; }
    public int Generation { get; }
    public Guid StepId { get; }
    public Guid IntervalId { get; }
    internal AgentToolRunLease Lease { get; }
    internal ExecutionRunRecord OriginalRun { get; }
    internal long StartedTimestamp { get; }

    public TimeSpan RequirePublication(IAgentPlanningPublicationSnapshotLease held, AgentToolApprovalBinding binding)
        => owner.RequirePublication(this, held, binding);

    public AgentInheritedContinuationPurpose RequireInheritedPublicationPurpose(IAgentPlanningPublicationSnapshotLease held,
        AgentToolApprovalBinding binding) => owner.RequireInheritedPublicationPurpose(this, held, binding);

    public Task RequireCurrentManagerPublicationAsync(IAgentPlanningPublicationSnapshotLease held,
        AgentToolApprovalBinding binding, CancellationToken cancellationToken)
        => owner.RequireCurrentManagerPublicationAsync(this, held, binding, cancellationToken);

    internal TimeSpan RequireMutation(ExecutionRunRecord run, AgentToolApprovalBinding binding)
        => owner.RequireMutation(this, run, binding);

    internal TimeSpan ObserveElapsed(TimeProvider clock, long timestamp) {
        var elapsed = clock.GetElapsedTime(StartedTimestamp, timestamp);
        while (true) {
            var previous = Volatile.Read(ref observedTicks);
            if (elapsed.Ticks < previous) {
                throw new AgentPlanningAdmissionException(DecisionId, Generation, AgentPlanningAdmissionRefusal.ClockInvalid);
            }
            if (Interlocked.CompareExchange(ref observedTicks, elapsed.Ticks, previous) == previous) {
                return elapsed;
            }
        }
    }

    internal AgentPlanningDecisionRecord Require(AgentPlanningDecisionOwner candidate, ExecutionRunRecord run) {
        var decision = run.ToolAdmission?.PlanningDecision;
        if (!ReferenceEquals(owner, candidate) || decision is null || decision.DecisionId != DecisionId ||
            decision.CurrentGeneration != Generation || decision.Work.Open is not { } open ||
            open.Id != IntervalId || open.StepId != StepId || open.DispatchLeaseId != Lease.Id) {
            throw new AgentPlanningAdmissionException(DecisionId, Generation, AgentPlanningAdmissionRefusal.OriginalStepChanged);
        }
        return decision;
    }
}

public sealed record AgentPlanningResponseAdmission(ExecutionRunRecord Run, AgentPlanningAdmissionRefusal? Refusal);
