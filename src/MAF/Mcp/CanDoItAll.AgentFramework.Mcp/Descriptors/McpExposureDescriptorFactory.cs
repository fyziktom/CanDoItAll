using CanDoItAll.AgentFramework.Capabilities.Abstractions;
using CanDoItAll.AgentFramework.Mcp.Abstractions;

namespace CanDoItAll.AgentFramework.Mcp;

public static class McpExposureDescriptorFactory
{
    public static CapabilityExposureDescriptor CreateServer(McpServerDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        return new CapabilityExposureDescriptor(
            descriptor.Identity,
            descriptor.DisplayName,
            descriptor.Description,
            descriptor is InternalHostedMcpServerDescriptor hosted ? hosted.ImplementationKey : null,
            null,
            descriptor.ServerKey,
            null,
            descriptor.Tags,
            descriptor.OperationClassifications,
            descriptor.SideEffectProfile,
            descriptor.AvailabilityState,
            TemplatePath.Create($"Templates/Capabilities/mcps/{descriptor.DescriptorKind.ToString().ToLowerInvariant()}/{descriptor.Identity.Key}.json"));
    }

    public static CapabilityExposureDescriptor CreateTool(
        McpServerDescriptor server,
        DiscoveredMcpTool tool)
    {
        ArgumentNullException.ThrowIfNull(server);

        return new CapabilityExposureDescriptor(
            new CapabilityIdentity(CapabilityKind.McpTool, CapabilityKey.Create($"{server.ServerKey.Value}-{ToCapabilityKeySegment(tool.Name)}")),
            tool.Name.Value,
            tool.Description,
            null,
            null,
            server.ServerKey,
            tool.Name,
            server.Tags.Concat([CapabilityTag.Create("mcp-tool")]).ToHashSet(),
            server.OperationClassifications,
            server.SideEffectProfile,
            server.AvailabilityState,
            TemplatePath.Create($"Templates/Capabilities/mcps/tools/{server.ServerKey.Value}/{tool.Name.Value}.json"));
    }

    private static string ToCapabilityKeySegment(McpToolName toolName)
        => toolName.Value
            .Replace('_', '-')
            .Replace('.', '-')
            .ToLowerInvariant();
}
