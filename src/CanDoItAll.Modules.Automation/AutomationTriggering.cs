using System.Text.Json;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Quartz;
using Quartz.Impl.Matchers;

namespace CanDoItAll.Modules.Automation;

public sealed class AutomationTriggerRegistry(
    IDbContextFactory<AppDbContext> dbContextFactory,
    QuartzAutomationSchedulerBridge schedulerBridge,
    IClock clock) : IAutomationTriggerRegistry
{
    public async Task<AutomationTriggerDefinition> SaveAsync(
        AutomationTriggerDefinition definition,
        CancellationToken cancellationToken = default)
    {
        Validate(definition);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var record = await dbContext.Set<AutomationTriggerRecord>()
            .FirstOrDefaultAsync(item => item.Id == definition.Id, cancellationToken);

        if (record is null)
        {
            record = new AutomationTriggerRecord
            {
                Id = definition.Id == Guid.Empty ? Guid.NewGuid() : definition.Id
            };
            await dbContext.Set<AutomationTriggerRecord>().AddAsync(record, cancellationToken);
        }

        record.OwnerKind = definition.OwnerKind;
        record.OwnerKey = definition.OwnerKey.Trim();
        record.TriggerKey = definition.TriggerKey.Trim();
        record.IsEnabled = definition.IsEnabled;
        record.TriggerKind = definition.TriggerKind;
        record.CronExpression = definition.CronExpression.Trim();
        record.TimeZoneId = definition.TimeZoneId.Trim();
        record.StartAtUtc = definition.StartAtUtc;
        record.EndAtUtc = definition.EndAtUtc;
        record.MisfirePolicy = definition.MisfirePolicy;
        record.PayloadJson = string.IsNullOrWhiteSpace(definition.PayloadJson)
            ? "{}"
            : definition.PayloadJson.Trim();
        record.DedupeKey = definition.DedupeKey?.Trim() ?? string.Empty;
        record.LastFiredAtUtc = definition.LastFiredAtUtc;
        record.NextPlannedFireAtUtc = definition.NextPlannedFireAtUtc;
        record.UpdatedAtUtc = clock.GetUtcNow();

        await dbContext.SaveChangesAsync(cancellationToken);
        await schedulerBridge.SynchronizeAsync(cancellationToken);

        await using var verificationContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var canonicalRecord = await verificationContext.Set<AutomationTriggerRecord>()
            .FirstAsync(item => item.Id == record.Id, cancellationToken);
        return Map(canonicalRecord);
    }

    public async Task<AutomationTriggerDefinition?> GetAsync(
        Guid triggerId,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var record = await dbContext.Set<AutomationTriggerRecord>()
            .FirstOrDefaultAsync(item => item.Id == triggerId, cancellationToken);
        return record is null
            ? null
            : Map(record);
    }

    public async Task<IReadOnlyList<AutomationTriggerDefinition>> ListAsync(
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.Set<AutomationTriggerRecord>()
            .OrderBy(item => item.OwnerKind)
            .ThenBy(item => item.OwnerKey)
            .ThenBy(item => item.TriggerKey)
            .Select(item => Map(item))
            .ToListAsync(cancellationToken);
    }

    private static AutomationTriggerDefinition Map(AutomationTriggerRecord record)
    {
        return new AutomationTriggerDefinition(
            record.Id,
            record.OwnerKind,
            record.OwnerKey,
            record.TriggerKey,
            record.IsEnabled,
            record.TriggerKind,
            record.CronExpression,
            record.TimeZoneId,
            record.StartAtUtc,
            record.EndAtUtc,
            record.MisfirePolicy,
            record.PayloadJson,
            record.DedupeKey,
            record.NextPlannedFireAtUtc,
            record.LastFiredAtUtc,
            record.UpdatedAtUtc);
    }

    private static void Validate(AutomationTriggerDefinition definition)
    {
        if (string.IsNullOrWhiteSpace(definition.OwnerKey))
        {
            throw new InvalidOperationException("Automation trigger OwnerKey is required.");
        }

        if (string.IsNullOrWhiteSpace(definition.TriggerKey))
        {
            throw new InvalidOperationException("Automation trigger TriggerKey is required.");
        }

        if (string.IsNullOrWhiteSpace(definition.TimeZoneId))
        {
            throw new InvalidOperationException("Automation trigger TimeZoneId is required.");
        }

        _ = TimeZoneInfo.FindSystemTimeZoneById(definition.TimeZoneId);

        if (definition.TriggerKind == AutomationTriggerKind.Cron &&
            !CronExpression.IsValidExpression(definition.CronExpression))
        {
            throw new InvalidOperationException(
                $"Automation trigger cron expression '{definition.CronExpression}' is invalid.");
        }
    }
}

public sealed class QuartzAutomationSchedulerBridge(
    IDbContextFactory<AppDbContext> dbContextFactory,
    ISchedulerFactory schedulerFactory,
    IClock clock,
    IAutomationTelemetryPublisher telemetryPublisher)
{
    private const string JobGroup = "candoitall-automation-triggers";
    private const string TriggerGroup = "candoitall-automation-runtime";

    public async Task SynchronizeAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var triggerRecords = await dbContext.Set<AutomationTriggerRecord>()
            .OrderBy(item => item.OwnerKind)
            .ThenBy(item => item.OwnerKey)
            .ThenBy(item => item.TriggerKey)
            .ToListAsync(cancellationToken);

        var scheduler = await schedulerFactory.GetScheduler(cancellationToken);
        var expectedJobKeys = triggerRecords
            .Select(record => BuildJobKey(record.Id))
            .ToHashSet();

        var existingJobKeys = await scheduler.GetJobKeys(GroupMatcher<JobKey>.GroupEquals(JobGroup), cancellationToken);
        foreach (var existingJobKey in existingJobKeys.Where(existingJobKey => !expectedJobKeys.Contains(existingJobKey)))
        {
            await scheduler.DeleteJob(existingJobKey, cancellationToken);
        }

        foreach (var triggerRecord in triggerRecords)
        {
            await SynchronizeTriggerAsync(scheduler, dbContext, triggerRecord, cancellationToken);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    internal static JobKey BuildJobKey(Guid triggerId)
    {
        return new JobKey($"trigger-{triggerId:N}", JobGroup);
    }

    internal static TriggerKey BuildTriggerKey(Guid triggerId)
    {
        return new TriggerKey($"trigger-{triggerId:N}", TriggerGroup);
    }

    private async Task SynchronizeTriggerAsync(
        IScheduler scheduler,
        AppDbContext dbContext,
        AutomationTriggerRecord triggerRecord,
        CancellationToken cancellationToken)
    {
        var jobKey = BuildJobKey(triggerRecord.Id);
        var triggerKey = BuildTriggerKey(triggerRecord.Id);

        if (await scheduler.CheckExists(jobKey, cancellationToken))
        {
            await scheduler.DeleteJob(jobKey, cancellationToken);
        }

        if (!triggerRecord.IsEnabled)
        {
            triggerRecord.NextPlannedFireAtUtc = null;
            triggerRecord.UpdatedAtUtc = clock.GetUtcNow();
            return;
        }

        if (IsConsumedOnceLikeTrigger(triggerRecord))
        {
            triggerRecord.IsEnabled = false;
            triggerRecord.NextPlannedFireAtUtc = null;
            triggerRecord.UpdatedAtUtc = clock.GetUtcNow();
            return;
        }

        var quartzTrigger = BuildQuartzTrigger(triggerRecord, jobKey, triggerKey);
        var job = JobBuilder.Create<AutomationTriggerQuartzJob>()
            .WithIdentity(jobKey)
            .UsingJobData(AutomationTriggerQuartzJob.TriggerIdKey, triggerRecord.Id.ToString("N"))
            .UsingJobData(AutomationTriggerQuartzJob.TriggerKeyKey, triggerRecord.TriggerKey)
            .UsingJobData(AutomationTriggerQuartzJob.OwnerKeyKey, triggerRecord.OwnerKey)
            .UsingJobData(AutomationTriggerQuartzJob.OwnerKindKey, (int)triggerRecord.OwnerKind)
            .UsingJobData(AutomationTriggerQuartzJob.PayloadJsonKey, triggerRecord.PayloadJson)
            .UsingJobData(AutomationTriggerQuartzJob.DedupeKeyKey, triggerRecord.DedupeKey)
            .Build();

        await scheduler.ScheduleJob(job, quartzTrigger, cancellationToken);

        triggerRecord.NextPlannedFireAtUtc = quartzTrigger.GetNextFireTimeUtc();
        triggerRecord.UpdatedAtUtc = clock.GetUtcNow();
        await telemetryPublisher.PublishAsync(new AutomationTelemetryEvent(
            AutomationExecutionLogKind.TriggerProjected,
            "automation-trigger",
            triggerRecord.Id.ToString("N"),
            null,
            null,
            $"Projected automation trigger '{triggerRecord.TriggerKey}' into Quartz runtime scheduling.",
            JsonSerializer.Serialize(new
            {
                triggerRecord.OwnerKind,
                triggerRecord.OwnerKey,
                triggerRecord.TriggerKey,
                triggerRecord.TriggerKind,
                triggerRecord.TimeZoneId,
                triggerRecord.NextPlannedFireAtUtc
            })), cancellationToken);
    }

    private static ITrigger BuildQuartzTrigger(
        AutomationTriggerRecord record,
        JobKey jobKey,
        TriggerKey triggerKey)
    {
        var builder = TriggerBuilder.Create()
            .WithIdentity(triggerKey)
            .ForJob(jobKey);

        if (record.StartAtUtc.HasValue)
        {
            builder = builder.StartAt(record.StartAtUtc.Value);
        }
        else
        {
            builder = builder.StartNow();
        }

        if (record.EndAtUtc.HasValue)
        {
            builder = builder.EndAt(record.EndAtUtc.Value);
        }

        switch (record.TriggerKind)
        {
            case AutomationTriggerKind.Cron:
                var cronBuilder = CronScheduleBuilder.CronSchedule(record.CronExpression)
                    .InTimeZone(TimeZoneInfo.FindSystemTimeZoneById(record.TimeZoneId));
                cronBuilder = record.MisfirePolicy switch
                {
                    AutomationTriggerMisfirePolicy.DoNothing => cronBuilder.WithMisfireHandlingInstructionDoNothing(),
                    AutomationTriggerMisfirePolicy.IgnoreMisfire => cronBuilder.WithMisfireHandlingInstructionIgnoreMisfires(),
                    _ => cronBuilder.WithMisfireHandlingInstructionFireAndProceed()
                };
                builder = builder.WithSchedule(cronBuilder);
                break;

            case AutomationTriggerKind.Once:
            case AutomationTriggerKind.Relative:
            case AutomationTriggerKind.DueDateProjection:
                builder = builder.WithSimpleSchedule(scheduleBuilder => scheduleBuilder.WithRepeatCount(0));
                break;

            default:
                throw new InvalidOperationException(
                    $"Automation trigger kind '{record.TriggerKind}' is not supported.");
        }

        return builder.Build();
    }

    private static bool IsConsumedOnceLikeTrigger(AutomationTriggerRecord triggerRecord)
    {
        return triggerRecord.LastFiredAtUtc.HasValue &&
               triggerRecord.TriggerKind is AutomationTriggerKind.Once or AutomationTriggerKind.Relative or AutomationTriggerKind.DueDateProjection;
    }
}

[DisallowConcurrentExecution]
public sealed class AutomationTriggerQuartzJob(
    IAutomationMessagePublisher messagePublisher,
    IDbContextFactory<AppDbContext> dbContextFactory,
    IAutomationTelemetryPublisher telemetryPublisher,
    IClock clock) : IJob
{
    public const string TriggerIdKey = "CanDoItAll.TriggerId";
    public const string TriggerKeyKey = "CanDoItAll.TriggerKey";
    public const string OwnerKeyKey = "CanDoItAll.OwnerKey";
    public const string OwnerKindKey = "CanDoItAll.OwnerKind";
    public const string PayloadJsonKey = "CanDoItAll.PayloadJson";
    public const string DedupeKeyKey = "CanDoItAll.DedupeKey";

    public async Task Execute(IJobExecutionContext context)
    {
        var cancellationToken = context.CancellationToken;
        var triggerId = Guid.ParseExact(context.MergedJobDataMap.GetString(TriggerIdKey)!, "N");
        var triggerKey = context.MergedJobDataMap.GetString(TriggerKeyKey) ?? string.Empty;
        var ownerKey = context.MergedJobDataMap.GetString(OwnerKeyKey) ?? string.Empty;
        var ownerKind = (AutomationTriggerOwnerKind)context.MergedJobDataMap.GetInt(OwnerKindKey);
        var payloadJson = context.MergedJobDataMap.GetString(PayloadJsonKey) ?? "{}";
        var dedupeKey = context.MergedJobDataMap.GetString(DedupeKeyKey) ?? string.Empty;
        var firedAtUtc = context.FireTimeUtc;
        var correlationId = Guid.NewGuid();

        await messagePublisher.PublishAsync(
            new AutomationTriggerFireRequest(
                triggerId,
                triggerKey,
                ownerKey,
                ownerKind,
                payloadJson,
                firedAtUtc),
            new AutomationPublishOptions(
                DedupeKey: BuildFireDedupeKey(triggerId, dedupeKey, firedAtUtc),
                CorrelationId: correlationId,
                AvailableAtUtc: firedAtUtc),
            cancellationToken);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var record = await dbContext.Set<AutomationTriggerRecord>()
            .FirstOrDefaultAsync(item => item.Id == triggerId, cancellationToken);
        if (record is not null)
        {
            record.LastFiredAtUtc = firedAtUtc;
            if (IsOnceLike(record.TriggerKind))
            {
                record.IsEnabled = false;
                record.NextPlannedFireAtUtc = null;
            }
            else
            {
                record.NextPlannedFireAtUtc = context.NextFireTimeUtc;
            }

            record.UpdatedAtUtc = clock.GetUtcNow();
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        await telemetryPublisher.PublishAsync(new AutomationTelemetryEvent(
            AutomationExecutionLogKind.TriggerFired,
            "automation-trigger",
            triggerId.ToString("N"),
            correlationId,
            null,
            $"Automation trigger '{triggerKey}' fired and published durable runtime work.",
            JsonSerializer.Serialize(new
            {
                triggerKey,
                ownerKey,
                ownerKind,
                firedAtUtc
            })), cancellationToken);
    }

    private static string BuildFireDedupeKey(Guid triggerId, string configuredDedupeKey, DateTimeOffset firedAtUtc)
    {
        var baseKey = string.IsNullOrWhiteSpace(configuredDedupeKey)
            ? triggerId.ToString("N")
            : configuredDedupeKey.Trim();

        return $"{baseKey}:{firedAtUtc.UtcTicks}";
    }

    private static bool IsOnceLike(AutomationTriggerKind triggerKind)
    {
        return triggerKind is AutomationTriggerKind.Once or AutomationTriggerKind.Relative or AutomationTriggerKind.DueDateProjection;
    }
}
