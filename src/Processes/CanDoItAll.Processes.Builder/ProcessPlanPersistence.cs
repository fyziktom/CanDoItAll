using CanDoItAll.Processes.Abstractions;

namespace CanDoItAll.Processes.Builder;

public interface IProcessInstancePlanStore
{
    public const int MaximumBatchPlanCount = 2_049;

    ValueTask<PersistedProcessInstancePlan> PersistAsync(
        ProcessInstancePlan plan,
        CancellationToken cancellationToken = default);

    ValueTask<ProcessInstancePlan?> LoadAsync(
        ProcessInstancePlanId planId,
        CancellationToken cancellationToken = default);

    async ValueTask<IReadOnlyList<ProcessInstancePlan>> LoadManyAsync(
        IReadOnlyList<ProcessInstancePlanId> planIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(planIds);
        if (planIds.Count > MaximumBatchPlanCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(planIds),
                planIds.Count,
                $"Instance-plan batch cannot exceed {MaximumBatchPlanCount} plans.");
        }

        var result = new List<ProcessInstancePlan>(planIds.Count);
        foreach (var planId in planIds.Distinct().OrderBy(planId => planId.Value))
        {
            var plan = await LoadAsync(planId, cancellationToken).ConfigureAwait(false);
            if (plan is not null)
            {
                result.Add(plan);
            }
        }

        return result;
    }
}

public sealed record PersistedProcessInstancePlan(
    ProcessInstancePlanId PlanId,
    string PlanHash);
