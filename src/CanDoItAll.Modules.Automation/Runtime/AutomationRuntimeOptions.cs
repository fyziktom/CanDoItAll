using System.ComponentModel.DataAnnotations;

namespace CanDoItAll.Modules.Automation;

public sealed class AutomationRuntimeOptions
{
    public const string SectionName = "Automation:Runtime";
    public const int MinimumBatchSize = 1;
    public const int MaximumBatchSize = 500;
    public const int MinimumMaxParallelism = 1;
    public const int MaximumMaxParallelism = 16;

    public TimeSpan MessagePollInterval { get; set; } = TimeSpan.FromMilliseconds(200);

    public TimeSpan ConnectorOutboxPollInterval { get; set; } = TimeSpan.FromMilliseconds(200);

    public TimeSpan LegacyBackgroundQueuePollInterval { get; set; } = TimeSpan.FromMilliseconds(200);

    [Range(MinimumBatchSize, MaximumBatchSize)]
    public int MessageDispatchBatchSize { get; set; } = 20;

    [Range(MinimumMaxParallelism, MaximumMaxParallelism)]
    public int MessageDispatchMaxParallelism { get; set; } = 4;

    [Range(MinimumBatchSize, MaximumBatchSize)]
    public int ConnectorOutboxBatchSize { get; set; } = 20;

    [Range(MinimumMaxParallelism, MaximumMaxParallelism)]
    public int ConnectorOutboxMaxParallelism { get; set; } = 4;

    public TimeSpan DeliveryLeaseDuration { get; set; } = TimeSpan.FromMinutes(2);

    public TimeSpan ConnectorCommandLeaseDuration { get; set; } = TimeSpan.FromMinutes(2);

    public TimeSpan WorkerFailureBackoff { get; set; } = TimeSpan.FromSeconds(2);

    public AutomationMqttOptions Mqtt { get; set; } = new();
}

public sealed class AutomationMqttOptions
{
    public bool Enabled { get; set; }

    public string ClientId { get; set; } = "CanDoItAll.Automation";

    public string Host { get; set; } = string.Empty;

    [Range(1, 65535)]
    public int Port { get; set; } = 1883;

    public string TopicPrefix { get; set; } = "candoitall/runtime";
}
