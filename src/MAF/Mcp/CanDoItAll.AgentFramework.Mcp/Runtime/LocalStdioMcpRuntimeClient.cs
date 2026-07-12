using CanDoItAll.AgentFramework.Capabilities.Abstractions;
using CanDoItAll.AgentFramework.Mcp.Abstractions;

namespace CanDoItAll.AgentFramework.Mcp;

internal sealed class LocalStdioMcpRuntimeClient : IMcpRuntimeClient
{
    private const string ProtocolVersion = "2025-06-18";

    private readonly LocalStdioMcpProcessSession session;
    private readonly LocalStdioMcpJsonRpcConnection connection;
    private readonly McpOperationTimeout timeout;

    public LocalStdioMcpRuntimeClient(LocalStdioMcpServerDescriptor descriptor)
    {
        session = new LocalStdioMcpProcessSession(descriptor);
        connection = new LocalStdioMcpJsonRpcConnection(descriptor, session);
        timeout = new McpOperationTimeout(descriptor.Timeout);
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        return timeout.RunAsync(
            async operationToken =>
            {
                await session.StartAsync(operationToken);
                using var response = await connection.SendRequestAsync(
                    "initialize",
                    new
                    {
                        protocolVersion = ProtocolVersion,
                        capabilities = new { },
                        clientInfo = new
                        {
                            name = "CanDoItAll",
                            version = typeof(LocalStdioMcpClientFactory).Assembly.GetName().Version?.ToString() ?? "0.0.0"
                        }
                    },
                    CapabilityDiagnosticCategory.McpHandshake,
                    "$.initialize",
                    operationToken);
                await connection.SendNotificationAsync(
                    "notifications/initialized",
                    parameters: null,
                    CapabilityDiagnosticCategory.McpHandshake,
                    "$.initialized",
                    operationToken);
            },
            "MCP initialize handshake",
            cancellationToken);
    }

    public Task<IReadOnlyList<DiscoveredMcpTool>> ListToolsAsync(
        CancellationToken cancellationToken)
    {
        return timeout.RunAsync(
            async operationToken =>
            {
                using var response = await connection.SendRequestAsync(
                    "tools/list",
                    new { },
                    CapabilityDiagnosticCategory.McpListTools,
                    "$.tools",
                    operationToken);
                return LocalStdioMcpResponseParser.ParseListToolsResponse(response);
            },
            "MCP tools/list request",
            cancellationToken);
    }

    public Task<string> CallToolAsync(
        McpToolName toolName,
        string jsonArguments,
        CancellationToken cancellationToken)
    {
        return timeout.RunAsync(
            async operationToken =>
            {
                var arguments = LocalStdioMcpResponseParser.ParseToolArguments(jsonArguments);
                using var response = await connection.SendRequestAsync(
                    "tools/call",
                    new
                    {
                        name = toolName.Value,
                        arguments
                    },
                    CapabilityDiagnosticCategory.McpListTools,
                    "$.tools.call",
                    operationToken);
                return response.RootElement.TryGetProperty("result", out var result)
                    ? result.GetRawText()
                    : response.RootElement.GetRawText();
            },
            $"MCP tools/call request for '{toolName.Value}'",
            cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return session.StopAsync(cancellationToken);
    }
}
