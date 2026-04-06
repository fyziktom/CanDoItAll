using System.Text.Json;
using CanDoItAll.Infrastructure.BackgroundJobs;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Automation;

public sealed class AutomationSubscriptionRegistry(
    IEnumerable<IAutomationMessageHandler> handlers)
{
    public IReadOnlyList<IAutomationMessageHandler> List(string envelopeType)
    {
        return handlers
            .Where(handler => string.Equals(handler.EnvelopeType, envelopeType, StringComparison.Ordinal))
            .OrderBy(handler => handler.HandlerKey, StringComparer.Ordinal)
            .ToList();
    }

    public IAutomationMessageHandler? Resolve(string envelopeType, string handlerKey)
    {
        return handlers.FirstOrDefault(handler =>
            string.Equals(handler.EnvelopeType, envelopeType, StringComparison.Ordinal) &&
            string.Equals(handler.HandlerKey, handlerKey, StringComparison.Ordinal));
    }
}

public sealed class AutomationMessagePublisher(
    IDbContextFactory<AppDbContext> dbContextFactory,
    AutomationSubscriptionRegistry subscriptionRegistry,
    IClock clock,
    IAutomationTelemetryPublisher telemetryPublisher) : IAutomationMessagePublisher
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public async Task<Guid> PublishAsync<TEnvelope>(
        TEnvelope envelope,
        AutomationPublishOptions? options = null,
        CancellationToken cancellationToken = default)
        where TEnvelope : class
    {
        ArgumentNullException.ThrowIfNull(envelope);

        var resolvedOptions = options ?? new AutomationPublishOptions();
        var envelopeType = AutomationEnvelopeTypeNames.For<TEnvelope>();
        var now = clock.GetUtcNow();
        var availableAtUtc = resolvedOptions.AvailableAtUtc ?? now;

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        if (!string.IsNullOrWhiteSpace(resolvedOptions.DedupeKey))
        {
            var existing = await dbContext.Set<AutomationEnvelopeRecord>()
                .FirstOrDefaultAsync(item =>
                        item.EnvelopeType == envelopeType &&
                        item.DedupeKey == resolvedOptions.DedupeKey,
                    cancellationToken);
            if (existing is not null)
            {
                return existing.Id;
            }
        }

        var record = new AutomationEnvelopeRecord
        {
            EnvelopeType = envelopeType,
            PayloadJson = JsonSerializer.Serialize(envelope, SerializerOptions),
            DedupeKey = NormalizeOptional(resolvedOptions.DedupeKey),
            CorrelationId = resolvedOptions.CorrelationId,
            CausationId = resolvedOptions.CausationId,
            AvailableAtUtc = availableAtUtc,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            State = AutomationEnvelopeState.Pending
        };

        await dbContext.Set<AutomationEnvelopeRecord>().AddAsync(record, cancellationToken);

        var subscribers = subscriptionRegistry.List(envelopeType);
        if (subscribers.Count == 0)
        {
            record.State = AutomationEnvelopeState.Completed;
            record.CompletedAtUtc = now;
        }
        else
        {
            foreach (var subscriber in subscribers)
            {
                await dbContext.Set<AutomationEnvelopeDeliveryRecord>().AddAsync(new AutomationEnvelopeDeliveryRecord
                {
                    EnvelopeId = record.Id,
                    EnvelopeType = envelopeType,
                    HandlerKey = subscriber.HandlerKey,
                    State = AutomationDeliveryState.Pending,
                    AvailableAtUtc = availableAtUtc,
                    CreatedAtUtc = now,
                    UpdatedAtUtc = now,
                    MaxAttempts = Math.Max(1, resolvedOptions.MaxAttempts)
                }, cancellationToken);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await telemetryPublisher.PublishAsync(new AutomationTelemetryEvent(
            AutomationExecutionLogKind.Published,
            "automation-envelope",
            record.Id.ToString("N"),
            record.CorrelationId,
            record.CausationId,
            $"Published {envelopeType} for {subscribers.Count} subscriber(s).",
            JsonSerializer.Serialize(new
            {
                envelopeType,
                subscriberCount = subscribers.Count,
                availableAtUtc
            }, SerializerOptions)), cancellationToken);

        return record.Id;
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }
}

public sealed class AutomationMessageDispatcher(
    IDbContextFactory<AppDbContext> dbContextFactory,
    AutomationSubscriptionRegistry subscriptionRegistry,
    IClock clock,
    IAutomationTelemetryPublisher telemetryPublisher) : IAutomationMessageDispatcher
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public async Task<int> DispatchPendingAsync(int take, CancellationToken cancellationToken = default)
    {
        if (take <= 0)
        {
            return 0;
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var now = clock.GetUtcNow();
        var dueDeliveries = await dbContext.Set<AutomationEnvelopeDeliveryRecord>()
            .ToListAsync(cancellationToken);
        var dueDeliveryIds = dueDeliveries
            .Where(item =>
                item.AvailableAtUtc <= now &&
                (item.State == AutomationDeliveryState.Pending ||
                 item.State == AutomationDeliveryState.RetryScheduled))
            .OrderBy(item => item.AvailableAtUtc)
            .ThenBy(item => item.CreatedAtUtc)
            .Take(take)
            .Select(item => item.Id)
            .ToList();

        var processedCount = 0;
        foreach (var deliveryId in dueDeliveryIds)
        {
            if (await DispatchSingleAsync(deliveryId, cancellationToken))
            {
                processedCount++;
            }
        }

        return processedCount;
    }

    private async Task<bool> DispatchSingleAsync(Guid deliveryId, CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var delivery = await dbContext.Set<AutomationEnvelopeDeliveryRecord>()
            .FirstOrDefaultAsync(item => item.Id == deliveryId, cancellationToken);
        if (delivery is null)
        {
            return false;
        }

        var now = clock.GetUtcNow();
        if ((delivery.State != AutomationDeliveryState.Pending && delivery.State != AutomationDeliveryState.RetryScheduled) ||
            delivery.AvailableAtUtc > now)
        {
            return false;
        }

        var envelope = await dbContext.Set<AutomationEnvelopeRecord>()
            .FirstAsync(item => item.Id == delivery.EnvelopeId, cancellationToken);

        delivery.State = AutomationDeliveryState.Running;
        delivery.AttemptCount++;
        delivery.LastAttemptAtUtc = now;
        delivery.UpdatedAtUtc = now;
        delivery.LockedAtUtc = now;
        delivery.LockToken = Guid.NewGuid().ToString("N");
        envelope.AttemptCount = Math.Max(envelope.AttemptCount, delivery.AttemptCount);
        envelope.UpdatedAtUtc = now;
        await dbContext.SaveChangesAsync(cancellationToken);

        var context = new AutomationMessageContext(
            envelope.Id,
            delivery.Id,
            envelope.EnvelopeType,
            envelope.CorrelationId,
            envelope.CausationId,
            envelope.DedupeKey,
            envelope.CreatedAtUtc);

        await telemetryPublisher.PublishAsync(new AutomationTelemetryEvent(
            AutomationExecutionLogKind.DeliveryStarted,
            "automation-envelope-delivery",
            delivery.Id.ToString("N"),
            envelope.CorrelationId,
            envelope.CausationId,
            $"Starting delivery attempt {delivery.AttemptCount} for {delivery.HandlerKey}.",
            JsonSerializer.Serialize(new
            {
                envelopeId = envelope.Id,
                envelope.EnvelopeType,
                delivery.HandlerKey,
                delivery.AttemptCount
            }, SerializerOptions)), cancellationToken);

        AutomationMessageHandleResult handleResult;
        try
        {
            var handler = subscriptionRegistry.Resolve(delivery.EnvelopeType, delivery.HandlerKey);
            if (handler is null)
            {
                handleResult = AutomationMessageHandleResult.DeadLettered(
                    $"No automation message handler is registered for '{delivery.HandlerKey}'.");
            }
            else
            {
                handleResult = await handler.HandleAsync(envelope.PayloadJson, context, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            handleResult = AutomationMessageHandleResult.RetryScheduled(ex.Message);
        }

        now = clock.GetUtcNow();
        dbContext.Set<AutomationDeliveryAttemptRecord>().Add(new AutomationDeliveryAttemptRecord
        {
            EnvelopeId = envelope.Id,
            DeliveryId = delivery.Id,
            HandlerKey = delivery.HandlerKey,
            AttemptNumber = delivery.AttemptCount,
            Outcome = handleResult.Outcome,
            CorrelationId = envelope.CorrelationId,
            CausationId = envelope.CausationId,
            ErrorMessage = NormalizeError(handleResult.ErrorMessage),
            StartedAtUtc = delivery.LastAttemptAtUtc ?? now,
            CompletedAtUtc = now
        });

        switch (handleResult.Outcome)
        {
            case AutomationDeliveryAttemptOutcome.Completed:
                delivery.State = AutomationDeliveryState.Completed;
                delivery.CompletedAtUtc = now;
                delivery.LastError = string.Empty;
                delivery.LockToken = string.Empty;
                delivery.LockedAtUtc = null;
                delivery.UpdatedAtUtc = now;
                await telemetryPublisher.PublishAsync(new AutomationTelemetryEvent(
                    AutomationExecutionLogKind.DeliveryCompleted,
                    "automation-envelope-delivery",
                    delivery.Id.ToString("N"),
                    envelope.CorrelationId,
                    envelope.CausationId,
                    $"Completed delivery for {delivery.HandlerKey}.",
                    JsonSerializer.Serialize(new
                    {
                        envelopeId = envelope.Id,
                        delivery.HandlerKey,
                        delivery.AttemptCount
                    }, SerializerOptions)), cancellationToken);
                break;

            case AutomationDeliveryAttemptOutcome.RetryScheduled when delivery.AttemptCount < delivery.MaxAttempts:
                delivery.State = AutomationDeliveryState.RetryScheduled;
                delivery.LastError = NormalizeError(handleResult.ErrorMessage);
                delivery.AvailableAtUtc = now.Add(ComputeBackoff(delivery.AttemptCount));
                delivery.LockToken = string.Empty;
                delivery.LockedAtUtc = null;
                delivery.UpdatedAtUtc = now;
                await telemetryPublisher.PublishAsync(new AutomationTelemetryEvent(
                    AutomationExecutionLogKind.DeliveryRetryScheduled,
                    "automation-envelope-delivery",
                    delivery.Id.ToString("N"),
                    envelope.CorrelationId,
                    envelope.CausationId,
                    $"Scheduled retry for {delivery.HandlerKey}.",
                    JsonSerializer.Serialize(new
                    {
                        envelopeId = envelope.Id,
                        delivery.HandlerKey,
                        delivery.AttemptCount,
                        delivery.AvailableAtUtc,
                        delivery.LastError
                    }, SerializerOptions)), cancellationToken);
                break;

            default:
                delivery.State = AutomationDeliveryState.DeadLettered;
                delivery.CompletedAtUtc = now;
                delivery.LastError = NormalizeError(handleResult.ErrorMessage);
                delivery.LockToken = string.Empty;
                delivery.LockedAtUtc = null;
                delivery.UpdatedAtUtc = now;
                dbContext.Set<AutomationDeadLetterRecord>().Add(new AutomationDeadLetterRecord
                {
                    EnvelopeId = envelope.Id,
                    DeliveryId = delivery.Id,
                    EnvelopeType = envelope.EnvelopeType,
                    HandlerKey = delivery.HandlerKey,
                    PayloadJson = envelope.PayloadJson,
                    ErrorMessage = delivery.LastError,
                    AttemptCount = delivery.AttemptCount,
                    DedupeKey = envelope.DedupeKey,
                    CorrelationId = envelope.CorrelationId,
                    CausationId = envelope.CausationId,
                    CreatedAtUtc = envelope.CreatedAtUtc,
                    DeadLetteredAtUtc = now
                });
                await telemetryPublisher.PublishAsync(new AutomationTelemetryEvent(
                    AutomationExecutionLogKind.DeliveryDeadLettered,
                    "automation-envelope-delivery",
                    delivery.Id.ToString("N"),
                    envelope.CorrelationId,
                    envelope.CausationId,
                    $"Moved delivery for {delivery.HandlerKey} to dead-letter state.",
                    JsonSerializer.Serialize(new
                    {
                        envelopeId = envelope.Id,
                        delivery.HandlerKey,
                        delivery.AttemptCount,
                        delivery.LastError
                    }, SerializerOptions)), cancellationToken);
                break;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await UpdateEnvelopeAggregateStateAsync(dbContext, envelope, now, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static async Task UpdateEnvelopeAggregateStateAsync(
        AppDbContext dbContext,
        AutomationEnvelopeRecord envelope,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var deliveryStates = await dbContext.Set<AutomationEnvelopeDeliveryRecord>()
            .Where(item => item.EnvelopeId == envelope.Id)
            .Select(item => item.State)
            .ToListAsync(cancellationToken);

        if (deliveryStates.Count == 0 || deliveryStates.All(item => item == AutomationDeliveryState.Completed))
        {
            envelope.State = AutomationEnvelopeState.Completed;
            envelope.CompletedAtUtc = now;
        }
        else if (deliveryStates.All(item =>
                     item == AutomationDeliveryState.Completed ||
                     item == AutomationDeliveryState.DeadLettered))
        {
            envelope.State = AutomationEnvelopeState.DeadLettered;
            envelope.CompletedAtUtc = now;
        }
        else
        {
            envelope.State = AutomationEnvelopeState.Pending;
            envelope.CompletedAtUtc = null;
        }

        envelope.UpdatedAtUtc = now;
    }

    private static string NormalizeError(string? errorMessage)
    {
        return string.IsNullOrWhiteSpace(errorMessage)
            ? "Automation delivery failed without an explicit error message."
            : errorMessage.Trim();
    }

    private static TimeSpan ComputeBackoff(int attemptCount)
    {
        var seconds = Math.Min(1 << Math.Max(0, attemptCount - 1), 30);
        return TimeSpan.FromSeconds(seconds);
    }
}

public sealed class AutomationBackgroundJobScheduler(
    IDbContextFactory<AppDbContext> dbContextFactory,
    IAutomationMessagePublisher messagePublisher,
    IAutomationTelemetryPublisher telemetryPublisher,
    IClock clock) : IAutomationBackgroundJobScheduler
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public async Task<Guid> ScheduleAsync(
        string jobType,
        string description,
        IReadOnlyDictionary<string, string>? metadata = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(jobType);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);

        var now = clock.GetUtcNow();
        var normalizedMetadata = new Dictionary<string, string>(metadata ?? new Dictionary<string, string>(), StringComparer.OrdinalIgnoreCase);
        var record = new BackgroundJobRecord
        {
            JobType = jobType.Trim(),
            Description = description.Trim(),
            CorrelationId = Guid.NewGuid(),
            MetadataJson = JsonSerializer.Serialize(normalizedMetadata, SerializerOptions),
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await dbContext.Set<BackgroundJobRecord>().AddAsync(record, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        await messagePublisher.PublishAsync(
            new AutomationBackgroundJobRequest(
                record.Id,
                record.JobType,
                record.CorrelationId,
                record.Description,
                normalizedMetadata),
            new AutomationPublishOptions(
                DedupeKey: $"background-job:{record.Id:N}",
                CorrelationId: record.CorrelationId,
                MaxAttempts: 3),
            cancellationToken);

        await telemetryPublisher.PublishAsync(new AutomationTelemetryEvent(
            AutomationExecutionLogKind.BackgroundJobScheduled,
            "background-job",
            record.Id.ToString("N"),
            record.CorrelationId,
            null,
            $"Scheduled durable background job '{record.JobType}'.",
            JsonSerializer.Serialize(new
            {
                record.JobType,
                record.Description
            }, SerializerOptions)), cancellationToken);

        return record.Id;
    }
}

public sealed class AutomationBackgroundJobMessageHandler(
    IEnumerable<IAutomationBackgroundJobHandler> handlers,
    IBackgroundJobTracker backgroundJobTracker) : AutomationMessageHandler<AutomationBackgroundJobRequest>
{
    protected override async Task<AutomationMessageHandleResult> HandleAsync(
        AutomationBackgroundJobRequest envelope,
        AutomationMessageContext context,
        CancellationToken cancellationToken)
    {
        await backgroundJobTracker.MarkRunningAsync(envelope.JobId, cancellationToken);

        var handler = handlers.FirstOrDefault(candidate =>
            string.Equals(candidate.JobType, envelope.JobType, StringComparison.Ordinal));
        if (handler is null)
        {
            await backgroundJobTracker.MarkFailedAsync(
                envelope.JobId,
                $"No automation background job handler is registered for '{envelope.JobType}'.",
                cancellationToken);
            return AutomationMessageHandleResult.DeadLettered(
                $"No automation background job handler is registered for '{envelope.JobType}'.");
        }

        var result = await handler.HandleAsync(envelope, cancellationToken);
        switch (result.Outcome)
        {
            case AutomationDeliveryAttemptOutcome.Completed:
                await backgroundJobTracker.MarkSucceededAsync(envelope.JobId, cancellationToken);
                return result;
            case AutomationDeliveryAttemptOutcome.RetryScheduled:
                await backgroundJobTracker.MarkQueuedAsync(envelope.JobId, cancellationToken);
                return result;
            default:
                await backgroundJobTracker.MarkFailedAsync(envelope.JobId, result.ErrorMessage, cancellationToken);
                return result;
        }
    }
}

public sealed class AutomationRuntimeInspectionService(
    IDbContextFactory<AppDbContext> dbContextFactory) : IAutomationRuntimeInspectionService
{
    public async Task<IReadOnlyList<AutomationDeadLetterSnapshot>> ListDeadLettersAsync(
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var snapshots = await dbContext.Set<AutomationDeadLetterRecord>()
            .Select(item => new AutomationDeadLetterSnapshot(
                item.Id,
                item.EnvelopeId,
                item.DeliveryId,
                item.EnvelopeType,
                item.HandlerKey,
                item.ErrorMessage,
                item.AttemptCount,
                item.CorrelationId,
                item.CausationId,
                item.DeadLetteredAtUtc))
            .ToListAsync(cancellationToken);
        return snapshots
            .OrderByDescending(item => item.DeadLetteredAtUtc)
            .ToList();
    }
}
