using System.Text.Json;
using CanDoItAll.AgentFramework.Capabilities.Abstractions;
using CanDoItAll.AgentFramework.Mcp.Abstractions;
using CanDoItAll.Memory.Abstractions;
using CanDoItAll.Memory.Application;

namespace CanDoItAll.Memory.Mcp;

public sealed partial class McpMemoryProviderDriver(
    IMcpClientFactory clientFactory,
    McpMemoryProviderOptions options) : IMemoryProviderDriver,
    IMcpMemoryProviderAdapter,
    IMemoryProviderOperationStatusDriver,
    IMemoryProviderEventPollDriver
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public MemoryProviderDriverKind DriverKind => MemoryProviderDriverKind.Mcp;

    public async Task<MemoryProviderDriverResult> ExecuteContextQueryAsync(
        MemoryProviderProfile provider,
        MemoryOperationRecord operation,
        MemoryContextQueryRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(request);

        var configuration = McpMemoryProviderConfiguration.FromProfile(provider, options);
        if (configuration.ToolMap.ContextQueryTool is not { } toolName)
        {
            return MemoryProviderDriverResult.Failed(
                MemoryProviderDriverResultKind.UnsupportedCapability,
                $"MCP memory provider '{provider.InstanceId}' has no context query tool configured.");
        }

        var toolRequest = CreateContextQueryToolRequest(provider, operation, request);
        var responseJson = await CallToolAsync(
            configuration,
            operation.CorrelationId.Value.ToString("D"),
            toolName,
            toolRequest,
            cancellationToken);
        return MapContextQueryResponse(provider, responseJson);
    }

    public async Task<McpMemoryAdapterResult> ExecuteIngestionAsync(
        MemoryProviderProfile provider,
        MemoryOperationRecord operation,
        MemoryIngestionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(request);

        var configuration = McpMemoryProviderConfiguration.FromProfile(provider, options);
        if (configuration.ToolMap.IngestionTool is not { } toolName)
        {
            return McpMemoryAdapterResult.UnsupportedCapability(
                $"MCP memory provider '{provider.InstanceId}' has no '{MemoryCapabilityIds.IngestionSnapshot}' tool configured.");
        }

        var toolRequest = CreateIngestionToolRequest(provider, operation, request);
        var responseJson = await CallToolAsync(
            configuration,
            operation.CorrelationId.Value.ToString("D"),
            toolName,
            toolRequest,
            cancellationToken);
        return MapAcceptedOrErrorResponse(provider, responseJson);
    }

    public async Task<McpMemoryAdapterResult> GetOperationStatusAsync(
        MemoryProviderProfile provider,
        MemoryOperationStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(request);

        var configuration = McpMemoryProviderConfiguration.FromProfile(provider, options);
        if (configuration.ToolMap.OperationStatusTool is not { } toolName)
        {
            return McpMemoryAdapterResult.UnsupportedCapability(
                $"MCP memory provider '{provider.InstanceId}' has no operation status tool configured.");
        }

        var toolRequest = new McpMemoryOperationStatusToolRequest(
            request.OperationId.Value.ToString("D"),
            MemoryProtocolVersion.Current.Value,
            request);
        var responseJson = await CallToolAsync(
            configuration,
            request.OperationId.Value.ToString("D"),
            toolName,
            toolRequest,
            cancellationToken);
        return MapOperationStatusResponse(provider, responseJson);
    }

    public async Task<McpMemoryAdapterResult> PollEventsAsync(
        MemoryProviderProfile provider,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(provider);

        var configuration = McpMemoryProviderConfiguration.FromProfile(provider, options);
        if (configuration.ToolMap.EventPollTool is not { } toolName)
        {
            return McpMemoryAdapterResult.UnsupportedCapability(
                $"MCP memory provider '{provider.InstanceId}' has no provider event polling tool configured.");
        }

        var toolRequest = new McpMemoryProviderEventPollRequest(
            provider.InstanceId.Value,
            MemoryProtocolVersion.Current.Value);
        var responseJson = await CallToolAsync(
            configuration,
            MemoryCorrelationId.New().Value.ToString("D"),
            toolName,
            toolRequest,
            cancellationToken);
        return MapEventPollResponse(provider, responseJson);
    }

    async Task<MemoryProviderOperationPollResult> IMemoryProviderOperationStatusDriver.PollOperationAsync(
        MemoryProviderProfile provider,
        MemoryOperationRecord operation,
        CancellationToken cancellationToken)
    {
        var result = await GetOperationStatusAsync(
            provider,
            new MemoryOperationStatusRequest(operation.OperationId),
            cancellationToken);
        return result.Kind switch
        {
            McpMemoryAdapterResultKind.OperationResult when result.OperationResult is not null =>
                MemoryProviderOperationPollResult.FromResult(result.OperationResult, result.Diagnostic),
            McpMemoryAdapterResultKind.OperationAccepted =>
                MemoryProviderOperationPollResult.StillRunning(result.Diagnostic),
            McpMemoryAdapterResultKind.ProviderError =>
                MemoryProviderOperationPollResult.RetryableFailure(result.Diagnostic),
            McpMemoryAdapterResultKind.UnsupportedCapability =>
                MemoryProviderOperationPollResult.UnsupportedCapability(result.Diagnostic),
            _ => MemoryProviderOperationPollResult.TerminalFailure("Malformed MCP memory operation status result.")
        };
    }

    async Task<MemoryProviderEventPollResult> IMemoryProviderEventPollDriver.PollEventsAsync(
        MemoryProviderProfile provider,
        CancellationToken cancellationToken)
    {
        var result = await PollEventsAsync(provider, cancellationToken);
        return result.Kind switch
        {
            McpMemoryAdapterResultKind.ProviderEvents =>
                MemoryProviderEventPollResult.FromEvents(result.Events, result.Diagnostic),
            McpMemoryAdapterResultKind.ProviderError =>
                MemoryProviderEventPollResult.RetryableFailure(result.Diagnostic),
            McpMemoryAdapterResultKind.UnsupportedCapability =>
                MemoryProviderEventPollResult.UnsupportedCapability(result.Diagnostic),
            _ => MemoryProviderEventPollResult.TerminalFailure("Malformed MCP memory provider event poll result.")
        };
    }

    private async Task<string> CallToolAsync(
        McpMemoryProviderConfiguration configuration,
        string correlationId,
        McpToolName toolName,
        object toolRequest,
        CancellationToken cancellationToken)
    {
        var client = await clientFactory.CreateAsync(configuration.Descriptor, correlationId, cancellationToken);
        await client.StartAsync(cancellationToken);
        try
        {
            var arguments = JsonSerializer.Serialize(toolRequest, JsonOptions);
            return await client.CallToolAsync(toolName, arguments, cancellationToken);
        }
        finally
        {
            await client.StopAsync(CancellationToken.None);
        }
    }
}
