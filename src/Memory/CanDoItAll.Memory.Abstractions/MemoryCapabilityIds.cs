namespace CanDoItAll.Memory.Abstractions;

public static class MemoryCapabilityIds
{
    public static readonly MemoryCapabilityId ContextQuerySync = MemoryCapabilityId.Parse("context.query.sync");
    public static readonly MemoryCapabilityId ContextQueryAsync = MemoryCapabilityId.Parse("context.query.async");
    public static readonly MemoryCapabilityId IngestionSnapshot = MemoryCapabilityId.Parse("ingestion.snapshot");
    public static readonly MemoryCapabilityId IngestionProviderRequestedSource = MemoryCapabilityId.Parse("ingestion.provider-requested-source");
    public static readonly MemoryCapabilityId FeedbackImmediate = MemoryCapabilityId.Parse("feedback.immediate");
    public static readonly MemoryCapabilityId FeedbackDelayed = MemoryCapabilityId.Parse("feedback.delayed");
    public static readonly MemoryCapabilityId EventsProviderPush = MemoryCapabilityId.Parse("events.provider-push");
    public static readonly MemoryCapabilityId EventsHostPoll = MemoryCapabilityId.Parse("events.host-poll");
    public static readonly MemoryCapabilityId OperationStatus = MemoryCapabilityId.Parse("operations.status");
    public static readonly MemoryCapabilityId UiRcl = MemoryCapabilityId.Parse("ui.rcl");
    public static readonly MemoryCapabilityId UiIframe = MemoryCapabilityId.Parse("ui.iframe");
}
