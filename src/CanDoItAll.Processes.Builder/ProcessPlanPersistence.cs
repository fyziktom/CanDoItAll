using CanDoItAll.Processes.Abstractions;

namespace CanDoItAll.Processes.Builder;

public interface IProcessInstancePlanStore
{
    ValueTask<PersistedProcessInstancePlan> PersistAsync(
        ProcessInstancePlan plan,
        CancellationToken cancellationToken = default);

    ValueTask<ProcessInstancePlan?> LoadAsync(
        ProcessInstancePlanId planId,
        CancellationToken cancellationToken = default);
}

public sealed record PersistedProcessInstancePlan(
    ProcessInstancePlanId PlanId,
    string PlanHash);
