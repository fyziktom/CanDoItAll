using CanDoItAll.AgentFramework.Capabilities.Abstractions;

namespace CanDoItAll.Memory.Mcp;

public static class McpMemoryProviderConfigurationKeys
{
    public const string DescriptorKind = "host.candoitall.memory.mcp.descriptorKind";
    public const string ServerKey = "host.candoitall.memory.mcp.serverKey";
    public const string DisplayName = "host.candoitall.memory.mcp.displayName";
    public const string Description = "host.candoitall.memory.mcp.description";
    public const string RemoteEndpoint = "host.candoitall.memory.mcp.remoteEndpoint";
    public const string ImplementationKey = "host.candoitall.memory.mcp.implementationKey";
    public const string AuthHeaderName = "host.candoitall.memory.mcp.authHeaderName";
    public const string AuthHeaderEnvironmentVariable = "host.candoitall.memory.mcp.authHeaderEnvironmentVariable";
    public const string ContextQueryTool = "host.candoitall.memory.mcp.tools.contextQuery";
    public const string IngestionTool = "host.candoitall.memory.mcp.tools.ingestion";
    public const string SourceRequestTool = "host.candoitall.memory.mcp.tools.sourceRequest";
    public const string FeedbackTool = "host.candoitall.memory.mcp.tools.feedback";
    public const string EventPollTool = "host.candoitall.memory.mcp.tools.eventPoll";
    public const string OperationStatusTool = "host.candoitall.memory.mcp.tools.operationStatus";
}

public static class McpMemoryProviderDescriptorKinds
{
    public const string RemoteHttp = "remote-http";
    public const string InternalHosted = "internal-hosted";
}

public sealed record McpMemoryProviderToolMap(
    McpToolName? ContextQueryTool,
    McpToolName? OperationStatusTool)
{
    public IReadOnlySet<McpToolName> AllowedTools =>
        new[]
        {
            ContextQueryTool,
            OperationStatusTool
        }
        .OfType<McpToolName>()
        .ToHashSet();
}
