using System.Text.Json;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MQTTnet;

namespace CanDoItAll.Modules.Automation;

public interface IAutomationTelemetryBridge
{
    Task PublishAsync(AutomationTelemetryEvent telemetryEvent, CancellationToken cancellationToken);
}

public sealed class AutomationTelemetryPublisher(
    IDbContextFactory<AppDbContext> dbContextFactory,
    IClock clock,
    IEnumerable<IAutomationTelemetryBridge> bridges) : IAutomationTelemetryPublisher
{
    public async Task PublishAsync(AutomationTelemetryEvent telemetryEvent, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await dbContext.Set<AutomationExecutionLogRecord>().AddAsync(new AutomationExecutionLogRecord
        {
            EventKind = telemetryEvent.EventKind,
            SourceType = telemetryEvent.SourceType,
            SourceId = telemetryEvent.SourceId,
            CorrelationId = telemetryEvent.CorrelationId,
            CausationId = telemetryEvent.CausationId,
            Message = telemetryEvent.Message,
            DetailsJson = NormalizeJson(telemetryEvent.DetailsJson),
            CreatedAtUtc = clock.GetUtcNow()
        }, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        foreach (var bridge in bridges)
        {
            await bridge.PublishAsync(telemetryEvent, cancellationToken);
        }
    }

    private static string NormalizeJson(string? json)
    {
        return string.IsNullOrWhiteSpace(json)
            ? "{}"
            : json.Trim();
    }
}

public sealed class MqttAutomationTelemetryBridge(
    IOptions<AutomationRuntimeOptions> options,
    ILogger<MqttAutomationTelemetryBridge> logger) : IAutomationTelemetryBridge
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public async Task PublishAsync(AutomationTelemetryEvent telemetryEvent, CancellationToken cancellationToken)
    {
        var mqttOptions = options.Value.Mqtt;
        if (!mqttOptions.Enabled)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(mqttOptions.Host))
        {
            logger.LogWarning(
                "Automation MQTT telemetry is enabled but no host is configured. Event {EventKind} for {SourceType}/{SourceId} was not published.",
                telemetryEvent.EventKind,
                telemetryEvent.SourceType,
                telemetryEvent.SourceId);
            return;
        }

        try
        {
            var factory = new MqttClientFactory();
            using var client = factory.CreateMqttClient();
            var clientOptions = new MqttClientOptionsBuilder()
                .WithTcpServer(mqttOptions.Host, mqttOptions.Port)
                .WithClientId(mqttOptions.ClientId)
                .Build();

            await client.ConnectAsync(clientOptions, cancellationToken);

            var payload = JsonSerializer.Serialize(telemetryEvent, SerializerOptions);
            var message = new MqttApplicationMessageBuilder()
                .WithTopic(BuildTopic(mqttOptions.TopicPrefix, telemetryEvent))
                .WithPayload(payload)
                .Build();

            await client.PublishAsync(message, cancellationToken);
            await client.DisconnectAsync(cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Failed to publish automation telemetry event {EventKind} for {SourceType}/{SourceId} to MQTT.",
                telemetryEvent.EventKind,
                telemetryEvent.SourceType,
                telemetryEvent.SourceId);
        }
    }

    private static string BuildTopic(string topicPrefix, AutomationTelemetryEvent telemetryEvent)
    {
        var normalizedPrefix = string.IsNullOrWhiteSpace(topicPrefix)
            ? "candoitall/runtime"
            : topicPrefix.Trim().TrimEnd('/');

        return $"{normalizedPrefix}/{telemetryEvent.SourceType}/{telemetryEvent.EventKind}".ToLowerInvariant();
    }
}
