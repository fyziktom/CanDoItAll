namespace CanDoItAll.Modules.SchedulerPlanner;

public sealed class SchedulerPlanCommittedException(SchedulerMutationFact fact, Exception innerException)
    : Exception($"Scheduler {fact.Kind} persisted plan '{fact.PlanId:D}'; follow-up failed after {fact.Stage}.", innerException) {
    public SchedulerMutationFact Fact { get; } = fact;
}
