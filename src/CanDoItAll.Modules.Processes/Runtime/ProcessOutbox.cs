using System.Text.Json;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Infrastructure.Search;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.Processes;

public enum ProcessOutboxRecordStatus
{
    Pending,
    Completed,
    DeadLettered
}

public sealed class ProcessOutboxRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid? ProjectId { get; set; }

    public Guid? ProcessDefinitionId { get; set; }

    public Guid? ProcessRunId { get; set; }

    public string CommandKey { get; set; } = string.Empty;

    public string PayloadJson { get; set; } = "{}";

    public ProcessOutboxRecordStatus Status { get; set; } = ProcessOutboxRecordStatus.Pending;

    public int AttemptCount { get; set; }

    public DateTimeOffset? LastAttemptAtUtc { get; set; }

    public DateTimeOffset? NextAttemptAtUtc { get; set; }

    public DateTimeOffset? CompletedAtUtc { get; set; }

    public string LastError { get; set; } = string.Empty;

    public string LeaseToken { get; set; } = string.Empty;

    public DateTimeOffset? LeaseExpiresAtUtc { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }
}

internal sealed class ProcessOutboxRecordConfiguration : IEntityTypeConfiguration<ProcessOutboxRecord>
{
    public void Configure(EntityTypeBuilder<ProcessOutboxRecord> builder)
    {
        builder.ToTable("Processes_Outbox");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.CommandKey).HasMaxLength(120).IsRequired();
        builder.Property(item => item.PayloadJson).HasColumnType("TEXT");
        builder.Property(item => item.LastError).HasColumnType("TEXT");
        builder.Property(item => item.LeaseToken).HasMaxLength(100).IsRequired();
        builder.HasIndex(item => new
        {
            item.Status,
            item.NextAttemptAtUtc,
            item.LeaseExpiresAtUtc
        });
        builder.HasIndex(item => new
        {
            item.ProcessDefinitionId,
            item.CreatedAtUtc
        });
        builder.HasIndex(item => new
        {
            item.ProcessRunId,
            item.CreatedAtUtc
        });
        builder.HasIndex(item => new
        {
            item.ProjectId,
            item.CreatedAtUtc
        });
    }
}

internal sealed record ProcessOutboxPayload(
    SearchDocumentInput? SearchUpsert,
    ProcessOutboxSearchDeleteRequest? SearchDelete,
    ActivityWriteRequest? Activity,
    ProcessOutboxAutomationDispatchRequest? AutomationDispatch = null);

internal sealed record ProcessOutboxSearchDeleteRequest(
    string SourceType,
    string SourceKey);

internal sealed record ProcessOutboxAutomationDispatchRequest(
    Guid ProcessRunId,
    Guid? StepRunId,
    string Trigger);

public sealed class ProcessOutboxService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    IClock clock,
    IActivityStream activityStream,
    ISearchIndexService searchIndexService,
    IProcessRunAutomationDispatchService automationDispatchService,
    ILogger<ProcessOutboxService> logger)
{
    private const int MaxAttempts = 3;
    private const int DefaultBatchSize = 20;
    private const string AutomationDispatchCommandKey = "dispatch-run-automation";
    private static readonly JsonSerializerOptions PayloadSerializerOptions = new(JsonSerializerDefaults.Web);
    private static readonly TimeSpan DefaultLeaseDuration = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan AutomationDispatchLeaseDuration = TimeSpan.FromMinutes(30);

    public Task<Guid> EnqueueDefinitionSaveAsync(
        AppDbContext dbContext,
        ProcessDefinition definition,
        ProcessDefinitionVersion workingVersion,
        bool isNew,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(workingVersion);

        var route = BuildDefinitionRoute(definition.Id, definition.ProjectId);
        return EnqueueAsync(
            dbContext,
            definition.ProjectId,
            definition.Id,
            null,
            "save-definition",
             new ProcessOutboxPayload(
                 new SearchDocumentInput(
                     "process-definition",
                     definition.Id.ToString(),
                     "Processes",
                    definition.Name,
                    definition.Summary,
                    $"{definition.ValueStatement}\nCustomer: {definition.CustomerName}\nOwner: {definition.OwnerName}\nVersion: {workingVersion.VersionNumber}",
                    route,
                     definition.ProjectId),
                 null,
                 new ActivityWriteRequest(
                     "processes",
                    isNew ? "create-definition" : "update-definition",
                    isNew ? "Created process definition" : "Updated process definition",
                    definition.Name,
                    definition.ProjectId,
                    "process-definition",
                     definition.Id,
                     route,
                     "process-management"),
                 null),
             cancellationToken);
    }

    public Task<Guid> EnqueueDefinitionPublishAsync(
        AppDbContext dbContext,
        ProcessDefinition definition,
        ProcessDefinitionVersion publishedVersion,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(publishedVersion);

        return EnqueueAsync(
            dbContext,
            definition.ProjectId,
            definition.Id,
            null,
            "publish-definition",
             new ProcessOutboxPayload(
                 null,
                 null,
                 new ActivityWriteRequest(
                    "processes",
                    "publish-definition",
                    "Published process definition",
                    $"{definition.Name} v{publishedVersion.VersionNumber} is now immutable for runtime use.",
                     definition.ProjectId,
                     "process-definition",
                     definition.Id,
                     BuildDefinitionRoute(definition.Id, definition.ProjectId),
                     "process-management"),
                 null),
             cancellationToken);
    }

    public Task<Guid> EnqueueDefinitionDeleteAsync(
        AppDbContext dbContext,
        Guid definitionId,
        Guid? projectId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        return EnqueueAsync(
            dbContext,
            projectId,
            definitionId,
            null,
            "delete-definition",
            new ProcessOutboxPayload(
                null,
                new ProcessOutboxSearchDeleteRequest("process-definition", definitionId.ToString()),
                null,
                null),
            cancellationToken);
    }

    public Task<Guid> EnqueueRunStartAsync(
        AppDbContext dbContext,
        ProcessRun run,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(run);

        return EnqueueAsync(
            dbContext,
            run.ProjectId,
            run.ProcessDefinitionId,
            run.Id,
            "start-run",
            new ProcessOutboxPayload(
                null,
                null,
                new ActivityWriteRequest(
                    "processes",
                    "start-run",
                    "Started process run",
                    run.Name,
                    run.ProjectId,
                    "process-run",
                    run.Id,
                    BuildRunRoute(run),
                    "process-management"),
                null),
            cancellationToken);
    }

    public Task<Guid> EnqueueAutomationDispatchAsync(
        AppDbContext dbContext,
        Guid? projectId,
        Guid definitionId,
        Guid runId,
        Guid? stepRunId,
        string trigger,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        return EnqueueAsync(
            dbContext,
            projectId,
            definitionId,
            runId,
            AutomationDispatchCommandKey,
            new ProcessOutboxPayload(
                null,
                null,
                null,
                new ProcessOutboxAutomationDispatchRequest(
                    runId,
                    stepRunId,
                    string.IsNullOrWhiteSpace(trigger)
                        ? "process-runtime"
                        : trigger.Trim())),
            cancellationToken);
    }

    public async Task<ProcessOutboxRecordStatus?> ProcessAsync(
        Guid outboxId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await ProcessDirectAsync(outboxId, DefaultLeaseDuration, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Process outbox record {OutboxId} could not be dispatched immediately. Durable retry remains available.",
                outboxId);
            return await LoadStatusAsync(outboxId, cancellationToken);
        }
    }

    public async Task<int> ProcessPendingAsync(
        int take = DefaultBatchSize,
        TimeSpan? leaseDuration = null,
        CancellationToken cancellationToken = default)
    {
        if (take <= 0)
        {
            return 0;
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var now = clock.GetUtcNow();
        var effectiveLeaseDuration = leaseDuration is null || leaseDuration.Value <= TimeSpan.Zero
            ? DefaultLeaseDuration
            : leaseDuration.Value;
        var recordIds = dbContext.Database.IsSqlite()
            ? await ListPendingRecordIdsForSqliteAsync(dbContext, now, take, cancellationToken)
            : await dbContext.Set<ProcessOutboxRecord>()
                .Where(item => item.Status == ProcessOutboxRecordStatus.Pending)
                .Where(item => item.NextAttemptAtUtc == null || item.NextAttemptAtUtc <= now)
                .Where(item => item.LeaseExpiresAtUtc == null || item.LeaseExpiresAtUtc <= now)
                .OrderBy(item => item.NextAttemptAtUtc ?? item.CreatedAtUtc)
                .ThenBy(item => item.CreatedAtUtc)
                .Take(take)
                .Select(item => item.Id)
                .ToListAsync(cancellationToken);

        var processedCount = 0;
        foreach (var recordId in recordIds)
        {
            var leaseToken = await TryClaimRecordAsync(recordId, effectiveLeaseDuration, cancellationToken);
            if (leaseToken is null)
            {
                continue;
            }

            await ProcessClaimedAsync(recordId, leaseToken, cancellationToken);
            processedCount++;
        }

        return processedCount;
    }

    private async Task<Guid> EnqueueAsync(
        AppDbContext dbContext,
        Guid? projectId,
        Guid? definitionId,
        Guid? runId,
        string commandKey,
        ProcessOutboxPayload payload,
        CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var record = new ProcessOutboxRecord
        {
            ProjectId = projectId,
            ProcessDefinitionId = definitionId,
            ProcessRunId = runId,
            CommandKey = commandKey.Trim(),
            PayloadJson = JsonSerializer.Serialize(payload, PayloadSerializerOptions),
            Status = ProcessOutboxRecordStatus.Pending,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        await dbContext.Set<ProcessOutboxRecord>().AddAsync(record, cancellationToken);
        return record.Id;
    }

    private async Task<ProcessOutboxRecordStatus?> LoadStatusAsync(Guid outboxId, CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.Set<ProcessOutboxRecord>()
            .Where(item => item.Id == outboxId)
            .Select(item => (ProcessOutboxRecordStatus?)item.Status)
            .SingleOrDefaultAsync(cancellationToken);
    }

    private async Task<ProcessOutboxRecordStatus?> ProcessDirectAsync(
        Guid outboxId,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken)
    {
        var leaseToken = await TryClaimRecordAsync(outboxId, leaseDuration, cancellationToken);
        if (leaseToken is null)
        {
            return await LoadStatusAsync(outboxId, cancellationToken);
        }

        return await ProcessClaimedAsync(outboxId, leaseToken, cancellationToken);
    }

    private async Task<ProcessOutboxRecordStatus?> ProcessClaimedAsync(
        Guid outboxId,
        string leaseToken,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var now = clock.GetUtcNow();
        var record = await dbContext.Set<ProcessOutboxRecord>()
            .FirstOrDefaultAsync(item => item.Id == outboxId, cancellationToken);
        if (record is null)
        {
            return null;
        }

        if (!string.Equals(record.LeaseToken, leaseToken, StringComparison.Ordinal) ||
            record.LeaseExpiresAtUtc is null ||
            record.LeaseExpiresAtUtc <= now)
        {
            return null;
        }

        if (record.Status is ProcessOutboxRecordStatus.Completed or ProcessOutboxRecordStatus.DeadLettered)
        {
            ReleaseLease(record);
            await dbContext.SaveChangesAsync(cancellationToken);
            return record.Status;
        }

        if (record.NextAttemptAtUtc.HasValue && record.NextAttemptAtUtc.Value > now)
        {
            ReleaseLease(record);
            await dbContext.SaveChangesAsync(cancellationToken);
            return record.Status;
        }

        record.AttemptCount++;
        record.LastAttemptAtUtc = now;
        record.UpdatedAtUtc = now;
        await dbContext.SaveChangesAsync(cancellationToken);

        Exception? dispatchFailure = null;
        try
        {
            await DispatchAsync(record, cancellationToken);
        }
        catch (Exception exception)
        {
            dispatchFailure = exception;
        }

        now = clock.GetUtcNow();
        if (dispatchFailure is null)
        {
            record.Status = ProcessOutboxRecordStatus.Completed;
            record.CompletedAtUtc = now;
            record.NextAttemptAtUtc = null;
            record.LastError = string.Empty;
            ReleaseLease(record);
            record.UpdatedAtUtc = now;
        }
        else if (record.AttemptCount >= MaxAttempts)
        {
            logger.LogError(
                dispatchFailure,
                "Process outbox record {OutboxId} exhausted all retry attempts and moved to dead-letter.",
                record.Id);
            record.Status = ProcessOutboxRecordStatus.DeadLettered;
            record.CompletedAtUtc = null;
            record.NextAttemptAtUtc = null;
            record.LastError = NormalizeError(dispatchFailure.Message, "Process side-effect dispatch exhausted all retry attempts.");
            ReleaseLease(record);
            record.UpdatedAtUtc = now;
        }
        else
        {
            logger.LogWarning(
                dispatchFailure,
                "Process outbox record {OutboxId} failed attempt {AttemptCount} and will be retried.",
                record.Id,
                record.AttemptCount);
            record.Status = ProcessOutboxRecordStatus.Pending;
            record.CompletedAtUtc = null;
            record.NextAttemptAtUtc = now.Add(ComputeBackoff(record.AttemptCount));
            record.LastError = NormalizeError(dispatchFailure.Message, "Process side-effect dispatch failed and will be retried.");
            ReleaseLease(record);
            record.UpdatedAtUtc = now;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return record.Status;
    }

    private async Task DispatchAsync(ProcessOutboxRecord record, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Deserialize<ProcessOutboxPayload>(record.PayloadJson, PayloadSerializerOptions);
        if (payload is null)
        {
            throw new InvalidOperationException($"Process outbox record '{record.Id:D}' does not contain a valid payload.");
        }

        if (payload.SearchUpsert is not null)
        {
            await searchIndexService.UpsertAsync(payload.SearchUpsert, cancellationToken);
        }

        if (payload.SearchDelete is not null)
        {
            await searchIndexService.DeleteAsync(payload.SearchDelete.SourceType, payload.SearchDelete.SourceKey, cancellationToken);
        }

        if (payload.Activity is not null)
        {
            await activityStream.RecordAsync(
                payload.Activity with
                {
                    IdempotencyKey = string.IsNullOrWhiteSpace(payload.Activity.IdempotencyKey)
                        ? $"process-outbox:{record.Id:N}:activity"
                        : payload.Activity.IdempotencyKey.Trim()
                },
                cancellationToken);
        }

        if (payload.AutomationDispatch is not null)
        {
            var leaseToken = record.LeaseToken;
            await automationDispatchService.DispatchAsync(
                payload.AutomationDispatch.ProcessRunId,
                payload.AutomationDispatch.StepRunId,
                payload.AutomationDispatch.Trigger,
                token => RenewClaimedLeaseAsync(record.Id, leaseToken, record.CommandKey, token),
                cancellationToken);
        }
    }

    private async Task RenewClaimedLeaseAsync(
        Guid outboxId,
        string leaseToken,
        string commandKey,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(leaseToken))
        {
            return;
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var now = clock.GetUtcNow();
        var leaseExpiresAtUtc = now.Add(ResolveClaimLeaseDuration(commandKey, DefaultLeaseDuration));
        var updatedRows = dbContext.Database.IsSqlite()
            ? await RenewClaimedLeaseForSqliteAsync(
                dbContext,
                outboxId,
                leaseToken,
                now,
                leaseExpiresAtUtc,
                cancellationToken)
            : await dbContext.Set<ProcessOutboxRecord>()
                .Where(item => item.Id == outboxId)
                .Where(item => item.Status == ProcessOutboxRecordStatus.Pending)
                .Where(item => item.LeaseToken == leaseToken)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(item => item.LeaseExpiresAtUtc, leaseExpiresAtUtc)
                        .SetProperty(item => item.UpdatedAtUtc, now),
                    cancellationToken);

        if (updatedRows == 0)
        {
            logger.LogWarning(
                "Could not renew process outbox lease for record {OutboxId}; another worker may have claimed or completed it.",
                outboxId);
        }
    }

    private async Task<string?> TryClaimRecordAsync(
        Guid outboxId,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var now = clock.GetUtcNow();
        var leaseToken = Guid.NewGuid().ToString("N");
        var commandKey = await dbContext.Set<ProcessOutboxRecord>()
            .Where(item => item.Id == outboxId)
            .Select(item => item.CommandKey)
            .SingleOrDefaultAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(commandKey))
        {
            return null;
        }

        var claimLeaseDuration = ResolveClaimLeaseDuration(commandKey, leaseDuration);
        var leaseExpiresAtUtc = now.Add(claimLeaseDuration);
        if (string.Equals(commandKey, AutomationDispatchCommandKey, StringComparison.Ordinal))
        {
            logger.LogInformation(
                "Claiming process automation dispatch outbox record {OutboxId} with lease duration {LeaseDuration}.",
                outboxId,
                claimLeaseDuration);
        }

        var updatedRows = dbContext.Database.IsSqlite()
            ? await TryClaimRecordForSqliteAsync(
                dbContext,
                outboxId,
                now,
                leaseExpiresAtUtc,
                leaseToken,
                cancellationToken)
            : await dbContext.Set<ProcessOutboxRecord>()
                .Where(item => item.Id == outboxId)
                .Where(item => item.Status == ProcessOutboxRecordStatus.Pending)
                .Where(item => item.NextAttemptAtUtc == null || item.NextAttemptAtUtc <= now)
                .Where(item => item.LeaseExpiresAtUtc == null || item.LeaseExpiresAtUtc <= now)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(item => item.LeaseToken, leaseToken)
                        .SetProperty(item => item.LeaseExpiresAtUtc, leaseExpiresAtUtc)
                        .SetProperty(item => item.UpdatedAtUtc, now),
                    cancellationToken);

        return updatedRows == 0
            ? null
            : leaseToken;
    }

    internal static TimeSpan ResolveClaimLeaseDuration(string commandKey, TimeSpan requestedLeaseDuration)
    {
        if (string.Equals(commandKey, AutomationDispatchCommandKey, StringComparison.Ordinal) &&
            requestedLeaseDuration < AutomationDispatchLeaseDuration)
        {
            return AutomationDispatchLeaseDuration;
        }

        return requestedLeaseDuration;
    }

    private static Task<List<Guid>> ListPendingRecordIdsForSqliteAsync(
        AppDbContext dbContext,
        DateTimeOffset now,
        int take,
        CancellationToken cancellationToken)
    {
        return dbContext.Database
            .SqlQuery<Guid>($"""
                             SELECT "Id" AS "Value"
                             FROM "Processes_Outbox"
                             WHERE "Status" = {(int)ProcessOutboxRecordStatus.Pending}
                               AND ("NextAttemptAtUtc" IS NULL OR "NextAttemptAtUtc" <= {now})
                               AND ("LeaseExpiresAtUtc" IS NULL OR "LeaseExpiresAtUtc" <= {now})
                             ORDER BY COALESCE("NextAttemptAtUtc", "CreatedAtUtc"), "CreatedAtUtc"
                             LIMIT {take}
                             """)
            .ToListAsync(cancellationToken);
    }

    private static Task<int> TryClaimRecordForSqliteAsync(
        AppDbContext dbContext,
        Guid outboxId,
        DateTimeOffset now,
        DateTimeOffset leaseExpiresAtUtc,
        string leaseToken,
        CancellationToken cancellationToken)
    {
        return dbContext.Database.ExecuteSqlInterpolatedAsync($"""
                                                               UPDATE "Processes_Outbox"
                                                               SET "LeaseToken" = {leaseToken},
                                                                   "LeaseExpiresAtUtc" = {leaseExpiresAtUtc},
                                                                   "UpdatedAtUtc" = {now}
                                                               WHERE "Id" = {outboxId}
                                                                 AND "Status" = {(int)ProcessOutboxRecordStatus.Pending}
                                                                 AND ("NextAttemptAtUtc" IS NULL OR "NextAttemptAtUtc" <= {now})
                                                                 AND ("LeaseExpiresAtUtc" IS NULL OR "LeaseExpiresAtUtc" <= {now})
                                                               """,
            cancellationToken);
    }

    private static Task<int> RenewClaimedLeaseForSqliteAsync(
        AppDbContext dbContext,
        Guid outboxId,
        string leaseToken,
        DateTimeOffset now,
        DateTimeOffset leaseExpiresAtUtc,
        CancellationToken cancellationToken)
    {
        return dbContext.Database.ExecuteSqlInterpolatedAsync($"""
                                                               UPDATE "Processes_Outbox"
                                                               SET "LeaseExpiresAtUtc" = {leaseExpiresAtUtc},
                                                                   "UpdatedAtUtc" = {now}
                                                               WHERE "Id" = {outboxId}
                                                                 AND "Status" = {(int)ProcessOutboxRecordStatus.Pending}
                                                                 AND "LeaseToken" = {leaseToken}
                                                               """,
            cancellationToken);
    }

    private static void ReleaseLease(ProcessOutboxRecord record)
    {
        record.LeaseToken = string.Empty;
        record.LeaseExpiresAtUtc = null;
    }

    private static TimeSpan ComputeBackoff(int attemptCount)
    {
        var minutes = Math.Min(1 << Math.Max(0, attemptCount - 1), 15);
        return TimeSpan.FromMinutes(minutes);
    }

    private static string NormalizeError(string value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value)
            ? fallback
            : value.Trim();
    }

    private static string BuildDefinitionRoute(Guid definitionId, Guid? projectId)
    {
        return BuildProcessWorkspaceRoute(projectId, "processId", definitionId);
    }

    private static string BuildRunRoute(ProcessRun run)
    {
        return BuildProcessWorkspaceRoute(run.ProjectId, "runId", run.Id);
    }

    private static string BuildProcessWorkspaceRoute(Guid? projectId, string queryKey, Guid entityId)
    {
        return projectId.HasValue
            ? $"/projects/{projectId.Value:D}/processes?{queryKey}={entityId:D}"
            : $"/processes?{queryKey}={entityId:D}";
    }
}

public sealed class ProcessOutboxDrainWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<ProcessOutboxDrainWorker> logger) : BackgroundService
{
    private static readonly TimeSpan IdleDelay = TimeSpan.FromMilliseconds(200);
    private static readonly TimeSpan FailureBackoff = TimeSpan.FromSeconds(2);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var outbox = scope.ServiceProvider.GetRequiredService<ProcessOutboxService>();
                var processedCount = await outbox.ProcessPendingAsync(cancellationToken: stoppingToken);
                if (processedCount == 0)
                {
                    await Task.Delay(IdleDelay, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception) when (SqliteWriteCoordination.IsBusy(exception))
            {
                logger.LogWarning(
                    exception,
                    "ProcessOutboxDrainWorker hit transient SQLite contention. The worker will retry after {FailureBackoff}.",
                    FailureBackoff);
                await Task.Delay(FailureBackoff, stoppingToken);
            }
            catch (Exception exception)
            {
                logger.LogError(
                    exception,
                    "ProcessOutboxDrainWorker iteration failed. The worker will retry after {FailureBackoff}.",
                    FailureBackoff);
                await Task.Delay(FailureBackoff, stoppingToken);
            }
        }
    }
}
