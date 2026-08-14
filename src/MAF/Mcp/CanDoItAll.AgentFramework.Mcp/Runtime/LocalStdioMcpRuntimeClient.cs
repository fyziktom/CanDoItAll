using CanDoItAll.AgentFramework.Capabilities.Abstractions;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Mcp.Abstractions;

namespace CanDoItAll.AgentFramework.Mcp;

internal sealed class LocalStdioMcpRuntimeClient : IMcpRuntimeClient
{
    private const string ProtocolVersion = "2025-06-18";

    private readonly LocalStdioMcpProcessSession session;
    private readonly LocalStdioMcpJsonRpcConnection connection;
    private readonly McpOperationTimeout timeout;
    private readonly McpServerKey serverKey;
    private readonly IReadOnlySet<McpToolName> allowedTools;

    public LocalStdioMcpRuntimeClient(
        LocalStdioMcpServerDescriptor descriptor,
        string correlationId,
        IWorkspaceLongRunningProcessHost processHost,
        IWorkspacePathResolutionService pathResolver)
    {
        serverKey = descriptor.ServerKey;
        allowedTools = descriptor.AllowedTools;
        session = new LocalStdioMcpProcessSession(
            descriptor,
            correlationId,
            processHost,
            pathResolver);
        connection = new LocalStdioMcpJsonRpcConnection(
            descriptor.ServerKey,
            descriptor.MessageFraming,
            session);
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
                LocalStdioMcpResponseParser.ValidateInitializeResponse(
                    response,
                    ProtocolVersion);
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
        if (!allowedTools.Contains(toolName))
        {
            throw new McpSetupException(
                CapabilityDiagnosticCategory.AccessPolicy,
                "$.allowedTools",
                $"MCP tool '{toolName}' is outside the allowed tool set for '{serverKey}'.",
                "Add the tool to the descriptor allowlist only after reviewing its behavior and side effects.");
        }

        return timeout.RunAsync(
            async operationToken =>
            {
                var arguments = McpToolArgumentsParser.Parse(jsonArguments, serverKey);
                using var response = await connection.SendRequestAsync(
                    "tools/call",
                    new
                    {
                        name = toolName.Value,
                        arguments
                    },
                    CapabilityDiagnosticCategory.RuntimeAdapter,
                    "$.tools.call",
                    operationToken);
                var result = response.RootElement.GetProperty("result");
                if (result.ValueKind != System.Text.Json.JsonValueKind.Object)
                {
                    throw new McpSetupException(
                        CapabilityDiagnosticCategory.RuntimeAdapter,
                        "$.tools.call.result",
                        $"MCP server '{serverKey}' returned a tools/call result that is not an object.",
                        "Return a protocol-compliant MCP CallToolResult object.");
                }

                return result.GetRawText();
            },
            $"MCP tools/call request for '{toolName.Value}'",
            cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return session.StopAsync(cancellationToken);
    }
}
