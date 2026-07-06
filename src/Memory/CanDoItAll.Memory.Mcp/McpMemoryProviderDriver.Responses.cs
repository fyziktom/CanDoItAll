using System.Text.Json;
using CanDoItAll.Memory.Abstractions;
using CanDoItAll.Memory.Application;

namespace CanDoItAll.Memory.Mcp;

public sealed partial class McpMemoryProviderDriver
{
    private static MemoryProviderDriverResult MapContextQueryResponse(
        MemoryProviderProfile provider,
        string responseJson)
    {
        var response = Deserialize<McpMemoryProviderResponse>(responseJson);
        if (response is null)
        {
            return MemoryProviderDriverResult.Failed(
                MemoryProviderDriverResultKind.ProviderError,
                "Malformed MCP memory provider response: empty body.");
        }

        return response.Kind switch
        {
            McpMemoryProviderResponseKind.ContextPack when response.ContextPack is not null =>
                MemoryProviderDriverResult.ContextPackResult(
                    response.ContextPack,
                    $"MCP memory provider '{provider.InstanceId}' returned a context pack."),
            McpMemoryProviderResponseKind.OperationAccepted when response.AcceptedOperation is not null =>
                MemoryProviderDriverResult.Accepted(
                    response.AcceptedOperation,
                    $"MCP memory provider '{provider.InstanceId}' accepted an async operation."),
            McpMemoryProviderResponseKind.UnsupportedCapability =>
                MemoryProviderDriverResult.Failed(
                    MemoryProviderDriverResultKind.UnsupportedCapability,
                    response.ErrorMessage ?? $"MCP memory provider '{provider.InstanceId}' does not support the requested capability."),
            McpMemoryProviderResponseKind.ProviderError =>
                MemoryProviderDriverResult.Failed(
                    MemoryProviderDriverResultKind.ProviderError,
                    response.ErrorMessage ?? $"MCP memory provider '{provider.InstanceId}' returned a provider error."),
            _ => MemoryProviderDriverResult.Failed(
                MemoryProviderDriverResultKind.ProviderError,
                "Malformed MCP memory provider response: response kind does not match payload.")
        };
    }

    private static McpMemoryAdapterResult MapAcceptedOrErrorResponse(
        MemoryProviderProfile provider,
        string responseJson)
    {
        var response = Deserialize<McpMemoryProviderResponse>(responseJson);
        if (response is null)
        {
            return McpMemoryAdapterResult.ProviderError("Malformed MCP memory provider response: empty body.");
        }

        return response.Kind switch
        {
            McpMemoryProviderResponseKind.OperationAccepted when response.AcceptedOperation is not null =>
                McpMemoryAdapterResult.Accepted(
                    response.AcceptedOperation,
                    $"MCP memory provider '{provider.InstanceId}' accepted an async operation."),
            McpMemoryProviderResponseKind.UnsupportedCapability =>
                McpMemoryAdapterResult.UnsupportedCapability(
                    response.ErrorMessage ?? $"MCP memory provider '{provider.InstanceId}' does not support the requested capability."),
            _ => McpMemoryAdapterResult.ProviderError(
                response.ErrorMessage ?? "Malformed MCP memory provider response: expected accepted operation.")
        };
    }

    private static McpMemoryAdapterResult MapOperationStatusResponse(
        MemoryProviderProfile provider,
        string responseJson)
    {
        var response = Deserialize<McpMemoryOperationStatusToolResponse>(responseJson);
        if (response is null)
        {
            return McpMemoryAdapterResult.ProviderError("Malformed MCP memory status response: empty body.");
        }

        return response.Kind switch
        {
            McpMemoryAdapterResultKind.OperationResult when response.OperationResult is not null =>
                McpMemoryAdapterResult.FromOperationResult(
                    response.OperationResult,
                    $"MCP memory provider '{provider.InstanceId}' returned operation status."),
            McpMemoryAdapterResultKind.OperationAccepted when response.AcceptedOperation is not null =>
                McpMemoryAdapterResult.Accepted(
                    response.AcceptedOperation,
                    $"MCP memory provider '{provider.InstanceId}' returned a still-running operation."),
            McpMemoryAdapterResultKind.UnsupportedCapability =>
                McpMemoryAdapterResult.UnsupportedCapability(
                    response.ErrorMessage ?? $"MCP memory provider '{provider.InstanceId}' does not support operation status."),
            _ => McpMemoryAdapterResult.ProviderError(
                response.ErrorMessage ?? "Malformed MCP memory status response: response kind does not match payload.")
        };
    }

    private static McpMemoryAdapterResult MapEventPollResponse(
        MemoryProviderProfile provider,
        string responseJson)
    {
        var response = Deserialize<McpMemoryProviderEventPollResponse>(responseJson);
        if (response is null)
        {
            return McpMemoryAdapterResult.ProviderError("Malformed MCP memory event response: empty body.");
        }

        return McpMemoryAdapterResult.ProviderEvents(
            response.Events,
            $"MCP memory provider '{provider.InstanceId}' returned {response.Events.Count} event(s).");
    }

    private static T? Deserialize<T>(string responseJson)
    {
        try
        {
            return JsonSerializer.Deserialize<T>(responseJson, JsonOptions);
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException($"Malformed MCP memory provider JSON response: {exception.Message}", exception);
        }
    }
}
