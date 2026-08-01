using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Builder;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Processes.Persistence;

public sealed class EfProcessInstancePlanStore(ProcessPersistenceDbContext dbContext) : IProcessInstancePlanStore
{
    public async ValueTask<PersistedProcessInstancePlan> PersistAsync(
        ProcessInstancePlan plan,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plan);

        var existing = await dbContext.InstancePlans
            .FindAsync(new object[] { plan.Header.PlanId.Value }, cancellationToken)
            .ConfigureAwait(false);
        if (existing is not null)
        {
            ProcessInstancePlanPersistenceMapper.EnsureSameIdentityAndHash(existing, plan);
            return new PersistedProcessInstancePlan(plan.Header.PlanId, plan.PlanHash);
        }

        dbContext.InstancePlans.Add(ProcessInstancePlanPersistenceMapper.ToEntity(plan));

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return new PersistedProcessInstancePlan(plan.Header.PlanId, plan.PlanHash);
    }

    public async ValueTask<ProcessInstancePlan?> LoadAsync(
        ProcessInstancePlanId planId,
        CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.InstancePlans
            .AsNoTracking()
            .SingleOrDefaultAsync(plan => plan.PlanId == planId.Value, cancellationToken)
            .ConfigureAwait(false);
        if (entity is null)
        {
            return null;
        }

        return ProcessInstancePlanPersistenceMapper.ToPlan(entity);
    }

    public async ValueTask<IReadOnlyList<ProcessInstancePlan>> LoadManyAsync(
        IReadOnlyList<ProcessInstancePlanId> planIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(planIds);
        if (planIds.Count > IProcessInstancePlanStore.MaximumBatchPlanCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(planIds),
                planIds.Count,
                $"Instance-plan batch cannot exceed {IProcessInstancePlanStore.MaximumBatchPlanCount} plans.");
        }

        var values = planIds
            .Select(planId => planId.Value)
            .Distinct()
            .ToArray();
        if (values.Length == 0)
        {
            return [];
        }

        var entities = await dbContext.InstancePlans
            .AsNoTracking()
            .Where(plan => values.Contains(plan.PlanId))
            .OrderBy(plan => plan.PlanId)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        return entities
            .Select(ProcessInstancePlanPersistenceMapper.ToPlan)
            .ToArray();
    }
}
