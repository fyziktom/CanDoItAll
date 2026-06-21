using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Quartz;
using Quartz.Impl.Matchers;

namespace CanDoItAll.Modules.SchedulerPlanner;

public interface ISchedulerPlannerTriggerScheduler
{
    Task SynchronizeAsync(CancellationToken cancellationToken = default);

    Task SynchronizePlanAsync(
        Guid planId,
        CancellationToken cancellationToken = default);
}

public sealed class SchedulerPlannerTriggerScheduler(
    IDbContextFactory<AppDbContext> dbContextFactory,
    ISchedulerFactory schedulerFactory,
    IClock clock,
    ILogger<SchedulerPlannerTriggerScheduler> logger) : ISchedulerPlannerTriggerScheduler
{
    private const string JobGroup = "candoitall-scheduler-planner";
    private const string TriggerGroup = "candoitall-scheduler-planner-runtime";

    public async Task SynchronizeAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var plans = await dbContext.Set<SchedulerPlan>()
            .OrderBy(item => item.Name)
            .ToListAsync(cancellationToken);

        var scheduler = await schedulerFactory.GetScheduler(cancellationToken);
        var expectedJobKeys = plans
            .Select(item => BuildJobKey(item.Id))
            .ToHashSet();
        var existingJobKeys = await scheduler.GetJobKeys(GroupMatcher<JobKey>.GroupEquals(JobGroup), cancellationToken);
        foreach (var existingJobKey in existingJobKeys.Where(existingJobKey => !expectedJobKeys.Contains(existingJobKey)))
        {
            await scheduler.DeleteJob(existingJobKey, cancellationToken);
        }

        foreach (var plan in plans)
        {
            await SynchronizePlanAsync(scheduler, plan, cancellationToken);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task SynchronizePlanAsync(
        Guid planId,
        CancellationToken cancellationToken = default)
    {
        if (planId == Guid.Empty)
        {
            throw new ArgumentException("Scheduler plan id is required.", nameof(planId));
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var scheduler = await schedulerFactory.GetScheduler(cancellationToken);
        var plan = await dbContext.Set<SchedulerPlan>()
            .SingleOrDefaultAsync(item => item.Id == planId, cancellationToken);
        if (plan is null)
        {
            await scheduler.DeleteJob(BuildJobKey(planId), cancellationToken);
            return;
        }

        await SynchronizePlanAsync(scheduler, plan, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task SynchronizePlanAsync(
        IScheduler scheduler,
        SchedulerPlan plan,
        CancellationToken cancellationToken)
    {
        var jobKey = BuildJobKey(plan.Id);
        var triggerKey = BuildTriggerKey(plan.Id);

        if (await scheduler.CheckExists(jobKey, cancellationToken))
        {
            await scheduler.DeleteJob(jobKey, cancellationToken);
        }

        if (!plan.IsEnabled)
        {
            plan.NextPlannedFireAtUtc = null;
            plan.UpdatedAtUtc = clock.GetUtcNow();
            return;
        }

        var quartzTrigger = BuildQuartzTrigger(plan, jobKey, triggerKey);
        var job = JobBuilder.Create<SchedulerPlannerQuartzJob>()
            .WithIdentity(jobKey)
            .UsingJobData(SchedulerPlannerQuartzJob.PlanIdKey, plan.Id.ToString("N"))
            .UsingJobData(SchedulerPlannerQuartzJob.SchedulerTriggerIdKey, plan.SchedulerTriggerId.ToString("N"))
            .UsingJobData(SchedulerPlannerQuartzJob.SchedulerTriggerKeyKey, plan.SchedulerTriggerKey)
            .RequestRecovery()
            .Build();

        await scheduler.ScheduleJob(job, quartzTrigger, cancellationToken);

        plan.NextPlannedFireAtUtc = quartzTrigger.GetNextFireTimeUtc();
        plan.UpdatedAtUtc = clock.GetUtcNow();
        logger.LogInformation(
            "Projected scheduler plan {PlanId} into local Quartz runtime. Enabled={IsEnabled}, NextFireAtUtc={NextFireAtUtc}.",
            plan.Id,
            plan.IsEnabled,
            plan.NextPlannedFireAtUtc);
    }

    private static ITrigger BuildQuartzTrigger(
        SchedulerPlan plan,
        JobKey jobKey,
        TriggerKey triggerKey)
    {
        var builder = TriggerBuilder.Create()
            .WithIdentity(triggerKey)
            .ForJob(jobKey);

        builder = plan.StartAtUtc.HasValue
            ? builder.StartAt(plan.StartAtUtc.Value)
            : builder.StartNow();

        if (plan.EndAtUtc.HasValue)
        {
            builder = builder.EndAt(plan.EndAtUtc.Value);
        }

        var cronBuilder = CronScheduleBuilder.CronSchedule(plan.CronExpression)
            .InTimeZone(TimeZoneInfo.FindSystemTimeZoneById(plan.TimeZoneId));
        cronBuilder = plan.MisfirePolicy switch
        {
            SchedulerPlanMisfirePolicy.DoNothing => cronBuilder.WithMisfireHandlingInstructionDoNothing(),
            SchedulerPlanMisfirePolicy.IgnoreMisfire => cronBuilder.WithMisfireHandlingInstructionIgnoreMisfires(),
            _ => cronBuilder.WithMisfireHandlingInstructionFireAndProceed()
        };

        return builder
            .WithSchedule(cronBuilder)
            .Build();
    }

    private static JobKey BuildJobKey(Guid planId)
    {
        return new JobKey($"plan-{planId:N}", JobGroup);
    }

    private static TriggerKey BuildTriggerKey(Guid planId)
    {
        return new TriggerKey($"plan-{planId:N}", TriggerGroup);
    }
}

public sealed class SchedulerPlannerProjectionHostedService(
    IServiceScopeFactory scopeFactory,
    ILogger<SchedulerPlannerProjectionHostedService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var triggerScheduler = scope.ServiceProvider.GetRequiredService<ISchedulerPlannerTriggerScheduler>();
        await triggerScheduler.SynchronizeAsync(cancellationToken);
        logger.LogInformation("SchedulerPlanner projected saved plans into the local Quartz runtime.");
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}

[DisallowConcurrentExecution]
public sealed class SchedulerPlannerQuartzJob(
    ISchedulerPlannerRunDispatcher dispatcher,
    ILogger<SchedulerPlannerQuartzJob> logger) : IJob
{
    public const string PlanIdKey = "CanDoItAll.SchedulerPlanner.PlanId";
    public const string SchedulerTriggerIdKey = "CanDoItAll.SchedulerPlanner.TriggerId";
    public const string SchedulerTriggerKeyKey = "CanDoItAll.SchedulerPlanner.TriggerKey";

    public async Task Execute(IJobExecutionContext context)
    {
        var cancellationToken = context.CancellationToken;
        var planId = ParseRequiredGuid(context.MergedJobDataMap.GetString(PlanIdKey), PlanIdKey);
        var fireRequest = new SchedulerPlanFireRequest(
            planId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            context.FireTimeUtc,
            context.NextFireTimeUtc);

        try
        {
            await dispatcher.DispatchAsync(fireRequest, cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "SchedulerPlanner Quartz job failed for plan {PlanId} fired at {FiredAtUtc}.",
                fireRequest.PlanId,
                fireRequest.FiredAtUtc);
            throw;
        }
    }

    private static Guid ParseRequiredGuid(string? value, string key)
    {
        if (Guid.TryParseExact(value, "N", out var parsed))
        {
            return parsed;
        }

        throw new InvalidOperationException($"SchedulerPlanner Quartz job data '{key}' is missing or invalid.");
    }
}
