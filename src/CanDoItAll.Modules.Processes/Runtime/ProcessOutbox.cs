using System.Data;
using System.Data.Common;
using System.Text.Json;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Infrastructure.Search;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

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
    internal const string AutomationDispatchCommandKey = "dispatch-run-automation";
    private static readonly JsonSerializerOptions PayloadSerializerOptions = new(JsonSerializerDefaults.Web);
    private static readonly TimeSpan DefaultLeaseDuration = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan AutomationDispatchLeaseDuration = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan LeaseRenewalHeartbeatInterval = TimeSpan.FromSeconds(5);

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
        DateTimeOffset? minimumAutomationDispatchCreatedAtUtc = null,
        int? maxParallelism = null,
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
        if (dbContext.Database.IsNpgsql())
        {
            var claimedRecords = await ClaimPendingRecordsPostgreSqlAsync(
                dbContext,
                take,
                now,
                effectiveLeaseDuration,
                minimumAutomationDispatchCreatedAtUtc,
                cancellationToken);

            return await ProcessClaimedPostgreSqlBatchAsync(
                claimedRecords,
                take,
                maxParallelism,
                cancellationToken);
        }

        var query = dbContext.Set<ProcessOutboxRecord>()
            .Where(item => item.Status == ProcessOutboxRecordStatus.Pending)
            .Where(item => item.NextAttemptAtUtc == null || item.NextAttemptAtUtc <= now)
            .Where(item => item.LeaseExpiresAtUtc == null || item.LeaseExpiresAtUtc <= now);
        if (minimumAutomationDispatchCreatedAtUtc.HasValue)
        {
            var cutoff = minimumAutomationDispatchCreatedAtUtc.Value;
            query = query.Where(item =>
                item.CommandKey != AutomationDispatchCommandKey ||
                item.CreatedAtUtc >= cutoff);
        }

        var recordIds = await query
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

    private static async Task<IReadOnlyList<ClaimedProcessOutboxRecord>> ClaimPendingRecordsPostgreSqlAsync(
        AppDbContext dbContext,
        int take,
        DateTimeOffset now,
        TimeSpan leaseDuration,
        DateTimeOffset? minimumAutomationDispatchCreatedAtUtc,
        CancellationToken cancellationToken)
    {
        var connection = dbContext.Database.GetDbConnection();
        var shouldCloseConnection = connection.State != ConnectionState.Open;
        if (shouldCloseConnection)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText =
                """
                WITH due AS (
                    SELECT o."Id"
                    FROM "Processes_Outbox" AS o
                    WHERE o."Status" = @pendingStatus
                      AND (o."NextAttemptAtUtc" IS NULL OR o."NextAttemptAtUtc" <= @now)
                      AND (o."LeaseExpiresAtUtc" IS NULL OR o."LeaseExpiresAtUtc" <= @now)
                      AND (
                          @hasMinimumAutomationCreatedAtUtc = FALSE
                          OR o."CommandKey" <> @automationCommandKey
                          OR o."CreatedAtUtc" >= @minimumAutomationCreatedAtUtc
                      )
                    ORDER BY COALESCE(o."NextAttemptAtUtc", o."CreatedAtUtc"), o."CreatedAtUtc"
                    FOR UPDATE SKIP LOCKED
                    LIMIT @take
                )
                UPDATE "Processes_Outbox" AS o
                SET "LeaseToken" = concat(@tokenPrefix, replace(o."Id"::text, '-', '')),
                    "LeaseExpiresAtUtc" = CASE
                        WHEN o."CommandKey" = @automationCommandKey THEN @automationLeaseExpiresAtUtc
                        ELSE @leaseExpiresAtUtc
                    END,
                    "UpdatedAtUtc" = @now
                FROM due
                WHERE o."Id" = due."Id"
                RETURNING o."Id", o."LeaseToken", o."ProcessRunId", o."CommandKey";
                """;
            AddParameter(command, "@pendingStatus", (int)ProcessOutboxRecordStatus.Pending);
            AddParameter(command, "@now", now);
            AddParameter(command, "@take", take);
            AddParameter(command, "@tokenPrefix", $"{Guid.NewGuid():N}:");
            AddParameter(command, "@automationCommandKey", AutomationDispatchCommandKey);
            AddParameter(command, "@hasMinimumAutomationCreatedAtUtc", minimumAutomationDispatchCreatedAtUtc.HasValue);
            AddParameter(command, "@minimumAutomationCreatedAtUtc", minimumAutomationDispatchCreatedAtUtc ?? now);
            AddParameter(command, "@leaseExpiresAtUtc", now.Add(leaseDuration));
            AddParameter(command, "@automationLeaseExpiresAtUtc", now.Add(ResolveClaimLeaseDuration(AutomationDispatchCommandKey, leaseDuration)));

            var claims = new List<ClaimedProcessOutboxRecord>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                claims.Add(new ClaimedProcessOutboxRecord(
                    reader.GetGuid(0),
                    reader.GetString(1),
                    BuildOutboxPartitionKey(
                        reader.IsDBNull(2) ? null : reader.GetGuid(2),
                        reader.GetString(3))));
            }

            return claims;
        }
        finally
        {
            if (shouldCloseConnection)
            {
                await connection.CloseAsync();
            }
        }
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
        using var leaseRenewalCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var leaseRenewalTask = RenewLeaseUntilDispatchCompletesAsync(
            record.Id,
            record.LeaseToken,
            record.CommandKey,
            leaseRenewalCancellation.Token);
        try
        {
            await DispatchAsync(record, cancellationToken);
        }
        catch (Exception exception)
        {
            dispatchFailure = exception;
        }
        finally
        {
            await StopLeaseRenewalAsync(leaseRenewalCancellation, leaseRenewalTask);
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
        var updatedRows = await dbContext.Set<ProcessOutboxRecord>()
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

    private async Task RenewLeaseUntilDispatchCompletesAsync(
        Guid outboxId,
        string leaseToken,
        string commandKey,
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(LeaseRenewalHeartbeatInterval, cancellationToken);
                await RenewClaimedLeaseAsync(outboxId, leaseToken, commandKey, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogWarning(
                    exception,
                    "Could not heartbeat-renew process outbox lease for record {OutboxId}. The dispatch will continue and the next heartbeat will retry.",
                    outboxId);
            }
        }
    }

    private static async Task StopLeaseRenewalAsync(
        CancellationTokenSource leaseRenewalCancellation,
        Task leaseRenewalTask)
    {
        await leaseRenewalCancellation.CancelAsync();
        try
        {
            await leaseRenewalTask;
        }
        catch (OperationCanceledException)
        {
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

        var updatedRows = await dbContext.Set<ProcessOutboxRecord>()
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

        if (updatedRows > 0 &&
            string.Equals(commandKey, AutomationDispatchCommandKey, StringComparison.Ordinal))
        {
            logger.LogInformation(
                "Claimed process automation dispatch outbox record {OutboxId} with lease duration {LeaseDuration}.",
                outboxId,
                claimLeaseDuration);
        }

        return updatedRows == 0
            ? null
            : leaseToken;
    }

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    private async Task<int> ProcessClaimedPostgreSqlBatchAsync(
        IReadOnlyList<ClaimedProcessOutboxRecord> claimedRecords,
        int take,
        int? maxParallelism,
        CancellationToken cancellationToken)
    {
        if (claimedRecords.Count == 0)
        {
            return 0;
        }

        var boundedParallelism = ResolveBatchParallelism(maxParallelism, take);
        using var throttler = new SemaphoreSlim(boundedParallelism, boundedParallelism);
        var processedCount = 0;
        var tasks = claimedRecords
            .GroupBy(record => record.PartitionKey, StringComparer.Ordinal)
            .Select(async group =>
            {
                await throttler.WaitAsync(cancellationToken);
                try
                {
                    foreach (var record in group)
                    {
                        await ProcessClaimedAsync(record.Id, record.LeaseToken, cancellationToken);
                        Interlocked.Increment(ref processedCount);
                    }
                }
                finally
                {
                    throttler.Release();
                }
            })
            .ToArray();

        await Task.WhenAll(tasks);
        return processedCount;
    }

    private static int ResolveBatchParallelism(int? maxParallelism, int take)
    {
        var requested = maxParallelism.GetValueOrDefault(1);
        if (requested <= 0)
        {
            requested = 1;
        }

        return Math.Clamp(requested, 1, Math.Max(1, take));
    }

    private static string BuildOutboxPartitionKey(Guid? processRunId, string commandKey)
    {
        var runPartition = processRunId?.ToString("N") ?? "global";
        return $"{runPartition}:{commandKey}";
    }

    private sealed record ClaimedProcessOutboxRecord(Guid Id, string LeaseToken, string PartitionKey);

    internal static TimeSpan ResolveClaimLeaseDuration(string commandKey, TimeSpan requestedLeaseDuration)
    {
        if (string.Equals(commandKey, AutomationDispatchCommandKey, StringComparison.Ordinal) &&
            requestedLeaseDuration < AutomationDispatchLeaseDuration)
        {
            return AutomationDispatchLeaseDuration;
        }

        return requestedLeaseDuration;
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
    ProcessRunRecoveryStartupGate recoveryStartupGate,
    ProcessRuntimeSession runtimeSession,
    IOptions<ProcessRuntimeOptions> processRuntimeOptions,
    ILogger<ProcessOutboxDrainWorker> logger) : BackgroundService
{
    private static readonly TimeSpan IdleDelay = TimeSpan.FromMilliseconds(200);
    private static readonly TimeSpan FailureBackoff = TimeSpan.FromSeconds(2);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!processRuntimeOptions.Value.RecoverActiveRunsOnStartup)
        {
            recoveryStartupGate.MarkStartupRecoveryCompleted();
        }

        try
        {
            await recoveryStartupGate.WaitForStartupRecoveryAsync(stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            return;
        }

        var activeDispatches = new HashSet<Task<int>>();
        while (!stoppingToken.IsCancellationRequested)
        {
            await ObserveCompletedDispatchesAsync(activeDispatches);

            if (activeDispatches.Count == 0)
            {
                activeDispatches.Add(ProcessPendingRecordAsync(stoppingToken));
            }

            if (activeDispatches.Count == 0)
            {
                await Task.Delay(IdleDelay, stoppingToken);
                continue;
            }

            var delayTask = Task.Delay(IdleDelay, stoppingToken);
            await Task.WhenAny(activeDispatches.Cast<Task>().Append(delayTask));
        }

        await ObserveCompletedDispatchesAsync(activeDispatches);
    }

    private async Task<int> ProcessPendingRecordAsync(CancellationToken stoppingToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var outbox = scope.ServiceProvider.GetRequiredService<ProcessOutboxService>();
            var minimumAutomationDispatchCreatedAtUtc = processRuntimeOptions.Value.ResumePersistedAutomationDispatchesOnStartup
                ? (DateTimeOffset?)null
                : runtimeSession.StartedAtUtc;
            return await outbox.ProcessPendingAsync(
                take: ResolveBatchSize(),
                minimumAutomationDispatchCreatedAtUtc: minimumAutomationDispatchCreatedAtUtc,
                maxParallelism: ResolveMaxConcurrentDispatches(),
                cancellationToken: stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            return 0;
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "ProcessOutboxDrainWorker iteration failed. The worker will retry after {FailureBackoff}.",
                FailureBackoff);
            await Task.Delay(FailureBackoff, stoppingToken);
            return 0;
        }
    }

    private int ResolveMaxConcurrentDispatches()
    {
        var configured = processRuntimeOptions.Value.OutboxBatchMaxParallelism > 0
            ? processRuntimeOptions.Value.OutboxBatchMaxParallelism
            : processRuntimeOptions.Value.OutboxWorkerMaxConcurrency;
        return configured <= 0
            ? ProcessRuntimeOptions.DefaultOutboxWorkerConcurrency
            : Math.Clamp(configured, 1, ProcessRuntimeOptions.MaximumOutboxWorkerConcurrency);
    }

    private int ResolveBatchSize()
    {
        var configured = processRuntimeOptions.Value.OutboxBatchSize;
        return configured <= 0
            ? 1
            : configured;
    }

    private static async Task ObserveCompletedDispatchesAsync(HashSet<Task<int>> activeDispatches)
    {
        foreach (var task in activeDispatches.Where(task => task.IsCompleted).ToArray())
        {
            activeDispatches.Remove(task);
            if (task.IsCanceled)
            {
                continue;
            }

            if (task.IsFaulted)
            {
                _ = task.Exception;
                continue;
            }

            await task;
        }
    }
}
