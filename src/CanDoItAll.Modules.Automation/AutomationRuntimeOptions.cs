namespace CanDoItAll.Modules.Automation;

public sealed class AutomationRuntimeOptions
{
    public TimeSpan MessagePollInterval { get; set; } = TimeSpan.FromMilliseconds(200);

    public TimeSpan ConnectorOutboxPollInterval { get; set; } = TimeSpan.FromMilliseconds(200);

    public TimeSpan LegacyBackgroundQueuePollInterval { get; set; } = TimeSpan.FromMilliseconds(200);

    public int MessageDispatchBatchSize { get; set; } = 20;

    public int ConnectorOutboxBatchSize { get; set; } = 20;

    public AutomationMqttOptions Mqtt { get; set; } = new();
}

public sealed class AutomationMqttOptions
{
    public bool Enabled { get; set; }

    public string ClientId { get; set; } = "CanDoItAll.Automation";

    public string Host { get; set; } = string.Empty;

    public int Port { get; set; } = 1883;

    public string TopicPrefix { get; set; } = "candoitall/runtime";
}
