using CanDoItAll.AgentFramework.Capabilities.Abstractions;
using CanDoItAll.AgentFramework.Mcp.Abstractions;

namespace CanDoItAll.AgentFramework.Mcp;

public sealed class LocalStdioMcpClientFactory : IMcpClientFactory
{
    public Task<IMcpRuntimeClient> CreateAsync(
        McpServerDescriptor descriptor,
        string correlationId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        cancellationToken.ThrowIfCancellationRequested();

        return descriptor switch
        {
            LocalStdioMcpServerDescriptor local =>
                Task.FromResult<IMcpRuntimeClient>(new LocalStdioMcpRuntimeClient(local)),
            RemoteHttpMcpServerDescriptor remote =>
                Task.FromResult<IMcpRuntimeClient>(new RemoteHttpMcpRuntimeClient(remote)),
            InternalHostedMcpServerDescriptor => throw new McpSetupException(
                CapabilityDiagnosticCategory.ImplementationMissing,
                "$.transport",
                $"Internal hosted MCP setup testing is not implemented for '{descriptor.ServerKey}'.",
                "Use local stdio MCP setup testing or add an internal hosted MCP client implementation."),
            _ => throw new McpSetupException(
                CapabilityDiagnosticCategory.ImplementationMissing,
                "$.transport",
                $"MCP descriptor kind '{descriptor.DescriptorKind}' is not supported by this client factory.",
                "Use a supported MCP descriptor kind before running setup tests.")
        };
    }
}
