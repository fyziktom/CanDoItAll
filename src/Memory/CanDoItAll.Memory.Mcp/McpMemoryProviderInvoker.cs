using System.Text.Json;
using CanDoItAll.AgentFramework.Capabilities.Abstractions;
using CanDoItAll.AgentFramework.Mcp.Abstractions;
using CanDoItAll.Memory.Abstractions;

namespace CanDoItAll.Memory.Mcp;

internal sealed class McpMemoryProviderInvoker(IMcpClientFactory clientFactory)
{
    public async Task<string> CallToolAsync(
        McpMemoryProviderConfiguration configuration,
        string correlationId,
        McpToolName toolName,
        object toolRequest,
        MemoryProviderResponseSizeLimit responseSizeLimit,
        CancellationToken cancellationToken)
    {
        var client = await clientFactory.CreateAsync(
            configuration.Descriptor,
            correlationId,
            cancellationToken);
        await client.StartAsync(cancellationToken);
        try
        {
            var arguments = JsonSerializer.Serialize(toolRequest, McpMemoryProviderJson.Options);
            var responseJson = await client.CallToolAsync(toolName, arguments, cancellationToken);
            McpMemoryProviderResponseGuard.EnsureWithinLimit(responseJson, responseSizeLimit);
            return responseJson;
        }
        finally
        {
            await client.StopAsync(CancellationToken.None);
        }
    }
}
