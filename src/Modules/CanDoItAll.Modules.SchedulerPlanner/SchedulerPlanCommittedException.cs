namespace CanDoItAll.Modules.SchedulerPlanner;

public sealed class SchedulerPlanCommittedException(SchedulerMutationFact fact, Exception innerException)
    : Exception($"Scheduler {fact.Kind} persisted plan '{fact.PlanId:D}'; follow-up failed after {fact.Stage}.", innerException) {
    public SchedulerMutationFact Fact { get; } = fact;
}

public sealed class SchedulerPlanConflictException(Exception? innerException = null)
    : InvalidOperationException("The schedule changed or was removed after it was opened. Review the current schedule before saving.", innerException);
