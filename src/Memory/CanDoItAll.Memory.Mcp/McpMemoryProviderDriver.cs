using CanDoItAll.AgentFramework.Capabilities.Abstractions;
using CanDoItAll.AgentFramework.Mcp.Abstractions;
using CanDoItAll.Memory.Abstractions;
using CanDoItAll.Memory.Application;

namespace CanDoItAll.Memory.Mcp;

public sealed class McpMemoryProviderDriver(
    IMcpClientFactory clientFactory,
    McpMemoryProviderOptions options) : IMemoryProviderDriver,
    IMemoryProviderOperationStatusDriver
{
    private readonly McpMemoryProviderInvoker invoker = new(clientFactory);

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

        var toolRequest = McpMemoryProviderRequestFactory.CreateContextQueryToolRequest(provider, operation, request);
        var responseSizeLimit = options.ResponseSizeLimit.ConstrainToJsonEnvelope(request.Context.Budget);
        try
        {
            var responseJson = await invoker.CallToolAsync(
                configuration,
                operation.CorrelationId.Value.ToString("D"),
                toolName,
                toolRequest,
                responseSizeLimit,
                cancellationToken);
            return McpMemoryProviderResponseMapper.MapContextQueryResponse(provider, responseJson);
        }
        catch (McpMemoryProviderResponseTooLargeException exception)
        {
            return MemoryProviderDriverResult.Failed(
                MemoryProviderDriverResultKind.ProviderError,
                $"MCP memory provider '{provider.InstanceId}' response exceeded the configured limit of {exception.SizeLimit.MaximumBytes} bytes.");
        }
    }

    private async Task<MemoryProviderOperationPollResult> PollOperationAsync(
        MemoryProviderProfile provider,
        MemoryOperationRecord operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(operation);

        var configuration = McpMemoryProviderConfiguration.FromProfile(provider, options);
        if (configuration.ToolMap.OperationStatusTool is not { } toolName)
        {
            return MemoryProviderOperationPollResult.UnsupportedCapability(
                $"MCP memory provider '{provider.InstanceId}' has no operation status tool configured.");
        }

        McpMemoryOperationStatusToolRequest toolRequest;
        try
        {
            toolRequest = McpMemoryProviderRequestFactory.CreateOperationStatusToolRequest(
                provider,
                operation);
        }
        catch (InvalidOperationException)
        {
            return MemoryProviderOperationPollResult.TerminalFailure(
                "MCP memory operation status requires valid persisted request context.");
        }

        var responseSizeLimit = options.ResponseSizeLimit.ConstrainToJsonEnvelope(
            toolRequest.Envelope.Budget);
        try
        {
            var responseJson = await invoker.CallToolAsync(
                configuration,
                operation.CorrelationId.Value.ToString("D"),
                toolName,
                toolRequest,
                responseSizeLimit,
                cancellationToken);
            return McpMemoryProviderResponseMapper.MapOperationStatusResponse(provider, responseJson);
        }
        catch (McpMemoryProviderResponseTooLargeException exception)
        {
            return MemoryProviderOperationPollResult.TerminalFailure(
                $"MCP memory provider '{provider.InstanceId}' response exceeded the configured limit of {exception.SizeLimit.MaximumBytes} bytes.");
        }
    }

    async Task<MemoryProviderOperationPollResult> IMemoryProviderOperationStatusDriver.PollOperationAsync(
        MemoryProviderProfile provider,
        MemoryOperationRecord operation,
        CancellationToken cancellationToken)
    {
        return await PollOperationAsync(provider, operation, cancellationToken);
    }
}
