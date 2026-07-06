using CanDoItAll.Memory.Abstractions;

namespace CanDoItAll.Memory.Mcp;

public static class McpMemoryProviderManifestFactory
{
    public static MemoryProviderManifest CreateManifest(
        MemoryProviderKind providerKind,
        McpMemoryProviderToolMap toolMap,
        MemoryProviderLimits limits)
    {
        ArgumentNullException.ThrowIfNull(toolMap);
        ArgumentNullException.ThrowIfNull(limits);

        var capabilities = new List<MemoryCapabilityDescriptor>();
        AddIfTool(capabilities, toolMap.ContextQueryTool, MemoryCapabilityIds.ContextQuerySync);
        if (toolMap.ContextQueryTool is not null && toolMap.OperationStatusTool is not null)
        {
            capabilities.Add(CreateCapability(MemoryCapabilityIds.ContextQueryAsync));
        }

        AddIfTool(capabilities, toolMap.IngestionTool, MemoryCapabilityIds.IngestionSnapshot);
        AddIfTool(capabilities, toolMap.SourceRequestTool, MemoryCapabilityIds.IngestionProviderRequestedSource);
        AddIfTool(capabilities, toolMap.FeedbackTool, MemoryCapabilityIds.FeedbackImmediate);
        AddIfTool(capabilities, toolMap.FeedbackTool, MemoryCapabilityIds.FeedbackDelayed);
        AddIfTool(capabilities, toolMap.EventPollTool, MemoryCapabilityIds.EventsHostPoll);
        AddIfTool(capabilities, toolMap.OperationStatusTool, MemoryCapabilityIds.OperationStatus);

        return new MemoryProviderManifest(
            providerKind,
            MemoryProtocolVersion.Current,
            capabilities,
            new MemoryProviderInteractionSupport(
                SupportsSynchronousQueries: toolMap.ContextQueryTool is not null,
                SupportsAsynchronousOperations: toolMap.OperationStatusTool is not null,
                SupportsSourceRequests: toolMap.SourceRequestTool is not null,
                SupportsFeedback: toolMap.FeedbackTool is not null,
                SupportsProviderEvents: toolMap.EventPollTool is not null),
            UiSurfaces: [],
            limits,
            MemoryExtensionData.Empty);
    }

    private static void AddIfTool(
        List<MemoryCapabilityDescriptor> capabilities,
        object? tool,
        MemoryCapabilityId capability)
    {
        if (tool is not null)
        {
            capabilities.Add(CreateCapability(capability));
        }
    }

    private static MemoryCapabilityDescriptor CreateCapability(MemoryCapabilityId capability) =>
        new(capability, McpMemoryCapabilityVersions.ToolV1, Supported: true);
}
