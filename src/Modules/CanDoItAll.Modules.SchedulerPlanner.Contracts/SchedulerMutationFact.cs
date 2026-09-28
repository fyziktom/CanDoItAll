namespace CanDoItAll.Modules.SchedulerPlanner;

public enum SchedulerMutationKind { Save, Enable, Disable, Delete }
public enum SchedulerMutationStage { None, Persisted, ProjectionSynchronized }

public sealed record SchedulerMutationFact(SchedulerMutationKind Kind, Guid PlanId,
    SchedulerMutationStage Stage, SchedulerPlanSummary Plan, string InputJson);
