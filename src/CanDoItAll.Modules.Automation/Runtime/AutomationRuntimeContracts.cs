using System.Text.Json;

namespace CanDoItAll.Modules.Automation;

public sealed record AutomationPublishOptions(
    string? DedupeKey = null,
    Guid? CorrelationId = null,
    Guid? CausationId = null,
    DateTimeOffset? AvailableAtUtc = null,
    int MaxAttempts = 3);

public sealed record AutomationMessageContext(
    Guid EnvelopeId,
    Guid DeliveryId,
    string EnvelopeType,
    Guid? CorrelationId,
    Guid? CausationId,
    string? DedupeKey,
    DateTimeOffset PublishedAtUtc);

public sealed record AutomationMessageHandleResult(
    AutomationDeliveryAttemptOutcome Outcome,
    string ErrorMessage)
{
    public static AutomationMessageHandleResult Completed()
    {
        return new AutomationMessageHandleResult(AutomationDeliveryAttemptOutcome.Completed, string.Empty);
    }

    public static AutomationMessageHandleResult RetryScheduled(string errorMessage)
    {
        return new AutomationMessageHandleResult(AutomationDeliveryAttemptOutcome.RetryScheduled, errorMessage);
    }

    public static AutomationMessageHandleResult DeadLettered(string errorMessage)
    {
        return new AutomationMessageHandleResult(AutomationDeliveryAttemptOutcome.DeadLettered, errorMessage);
    }
}

public interface IAutomationMessagePublisher
{
    Task<Guid> PublishAsync<TEnvelope>(
        TEnvelope envelope,
        AutomationPublishOptions? options = null,
        CancellationToken cancellationToken = default)
        where TEnvelope : class;
}

public interface IAutomationMessageDispatcher
{
    Task<int> DispatchPendingAsync(int take, CancellationToken cancellationToken = default);
}

public interface IAutomationMessageHandler
{
    string EnvelopeType { get; }

    string HandlerKey { get; }

    Type PayloadType { get; }

    Task<AutomationMessageHandleResult> HandleAsync(
        string payloadJson,
        AutomationMessageContext context,
        CancellationToken cancellationToken);
}

public interface IAutomationMessageHandler<TEnvelope> : IAutomationMessageHandler
    where TEnvelope : class
{
}

public abstract class AutomationMessageHandler<TEnvelope> : IAutomationMessageHandler<TEnvelope>
    where TEnvelope : class
{
    protected static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public string EnvelopeType => AutomationEnvelopeTypeNames.For<TEnvelope>();

    public virtual string HandlerKey => GetType().FullName ?? GetType().Name;

    public Type PayloadType => typeof(TEnvelope);

    public async Task<AutomationMessageHandleResult> HandleAsync(
        string payloadJson,
        AutomationMessageContext context,
        CancellationToken cancellationToken)
    {
        var envelope = JsonSerializer.Deserialize<TEnvelope>(payloadJson, SerializerOptions);
        if (envelope is null)
        {
            throw new InvalidOperationException(
                $"Envelope '{EnvelopeType}' could not be deserialized into '{typeof(TEnvelope).FullName}'.");
        }

        return await HandleAsync(envelope, context, cancellationToken);
    }

    protected abstract Task<AutomationMessageHandleResult> HandleAsync(
        TEnvelope envelope,
        AutomationMessageContext context,
        CancellationToken cancellationToken);
}

public static class AutomationEnvelopeTypeNames
{
    public static string For<TEnvelope>()
    {
        return typeof(TEnvelope).FullName ?? typeof(TEnvelope).Name;
    }
}

public sealed record AutomationTriggerDefinition(
    Guid Id,
    AutomationTriggerOwnerKind OwnerKind,
    string OwnerKey,
    string TriggerKey,
    bool IsEnabled,
    AutomationTriggerKind TriggerKind,
    string CronExpression,
    string TimeZoneId,
    DateTimeOffset? StartAtUtc,
    DateTimeOffset? EndAtUtc,
    AutomationTriggerMisfirePolicy MisfirePolicy,
    string PayloadJson,
    string DedupeKey,
    DateTimeOffset? NextPlannedFireAtUtc,
    DateTimeOffset? LastFiredAtUtc,
    DateTimeOffset UpdatedAtUtc);

public interface IAutomationTriggerRegistry
{
    Task<AutomationTriggerDefinition> SaveAsync(
        AutomationTriggerDefinition definition,
        CancellationToken cancellationToken = default);

    Task<AutomationTriggerDefinition?> GetAsync(Guid triggerId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AutomationTriggerDefinition>> ListAsync(CancellationToken cancellationToken = default);
}

public sealed record AutomationTriggerFireRequest(
    Guid TriggerId,
    string TriggerKey,
    string OwnerKey,
    AutomationTriggerOwnerKind OwnerKind,
    string PayloadJson,
    DateTimeOffset FiredAtUtc);

public sealed record AutomationBackgroundJobRequest(
    Guid JobId,
    string JobType,
    Guid CorrelationId,
    string Description,
    IReadOnlyDictionary<string, string> Metadata);

public interface IAutomationBackgroundJobScheduler
{
    Task<Guid> ScheduleAsync(
        string jobType,
        string description,
        IReadOnlyDictionary<string, string>? metadata = null,
        Guid? correlationId = null,
        CancellationToken cancellationToken = default);
}

public interface IAutomationBackgroundJobHandler
{
    string JobType { get; }

    Task<AutomationMessageHandleResult> HandleAsync(
        AutomationBackgroundJobRequest request,
        CancellationToken cancellationToken);
}

public sealed record PluginIngressEnvelopeRequest(
    string SourceKind,
    string SourceKey,
    string ExternalMessageId,
    string CursorValue,
    string PayloadJson,
    Guid? CorrelationId = null);

public sealed record PluginIngressEnvelopeSnapshot(
    Guid Id,
    string SourceKind,
    string SourceKey,
    string ExternalMessageId,
    string CursorValue,
    string DedupeKey,
    PluginIngressState State,
    string PayloadJson,
    string MaterializerKey,
    string MaterializationSummary,
    string LastError,
    Guid? CorrelationId,
    DateTimeOffset ReceivedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    DateTimeOffset? MaterializedAtUtc);

public sealed record PluginIngressAcceptResult(
    Guid EnvelopeId,
    bool IsDuplicate,
    PluginIngressState State);

public sealed record PluginIngressMaterializationResult(
    bool IsSuccess,
    string Summary,
    string ErrorMessage)
{
    public static PluginIngressMaterializationResult Success(string summary)
    {
        return new PluginIngressMaterializationResult(true, summary, string.Empty);
    }

    public static PluginIngressMaterializationResult Failure(string errorMessage)
    {
        return new PluginIngressMaterializationResult(false, string.Empty, errorMessage);
    }
}

public interface IPluginIngressInbox
{
    Task<PluginIngressAcceptResult> AcceptAsync(
        PluginIngressEnvelopeRequest request,
        CancellationToken cancellationToken = default);

    Task<string?> GetCursorAsync(
        string sourceKind,
        string sourceKey,
        CancellationToken cancellationToken = default);

    Task SaveCursorAsync(
        string sourceKind,
        string sourceKey,
        string cursorValue,
        CancellationToken cancellationToken = default);

    Task<PluginIngressEnvelopeSnapshot?> GetAsync(
        Guid envelopeId,
        CancellationToken cancellationToken = default);

    Task<PluginIngressEnvelopeSnapshot> MaterializeAsync(
        Guid envelopeId,
        string materializerKey,
        CancellationToken cancellationToken = default);
}

public interface IPluginIngressMaterializer
{
    string MaterializerKey { get; }

    Task<PluginIngressMaterializationResult> MaterializeAsync(
        PluginIngressEnvelopeSnapshot envelope,
        CancellationToken cancellationToken);
}

public sealed record AutomationTelemetryEvent(
    AutomationExecutionLogKind EventKind,
    string SourceType,
    string SourceId,
    Guid? CorrelationId,
    Guid? CausationId,
    string Message,
    string DetailsJson);

public interface IAutomationTelemetryPublisher
{
    Task PublishAsync(AutomationTelemetryEvent telemetryEvent, CancellationToken cancellationToken = default);
}

public sealed record AutomationDeadLetterSnapshot(
    Guid Id,
    Guid EnvelopeId,
    Guid DeliveryId,
    string EnvelopeType,
    string HandlerKey,
    string ErrorMessage,
    int AttemptCount,
    Guid? CorrelationId,
    Guid? CausationId,
    DateTimeOffset DeadLetteredAtUtc);

public interface IAutomationRuntimeInspectionService
{
    Task<IReadOnlyList<AutomationDeadLetterSnapshot>> ListDeadLettersAsync(
        CancellationToken cancellationToken = default);
}
