using System.Text.Json;
using CanDoItAll.Memory.Abstractions;
using CanDoItAll.Memory.Application;

namespace CanDoItAll.Memory.Mcp;

internal static class McpMemoryProviderResponseMapper
{
    public static MemoryProviderDriverResult MapContextQueryResponse(
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

    public static MemoryProviderOperationPollResult MapOperationStatusResponse(
        MemoryProviderProfile provider,
        string responseJson)
    {
        var response = Deserialize<McpMemoryOperationStatusToolResponse>(responseJson);
        if (response is null)
        {
            return MemoryProviderOperationPollResult.TerminalFailure(
                "Malformed MCP memory status response: empty body.");
        }

        return response.Kind switch
        {
            McpMemoryOperationStatusResponseKind.OperationResult when response.OperationResult is not null =>
                MemoryProviderOperationPollResult.FromResult(
                    response.OperationResult,
                    $"MCP memory provider '{provider.InstanceId}' returned operation status."),
            McpMemoryOperationStatusResponseKind.OperationAccepted when response.AcceptedOperation is not null =>
                MemoryProviderOperationPollResult.StillRunning(
                    $"MCP memory provider '{provider.InstanceId}' returned a still-running operation."),
            McpMemoryOperationStatusResponseKind.UnsupportedCapability =>
                MemoryProviderOperationPollResult.UnsupportedCapability(
                    response.ErrorMessage ?? $"MCP memory provider '{provider.InstanceId}' does not support operation status."),
            McpMemoryOperationStatusResponseKind.ProviderError =>
                MemoryProviderOperationPollResult.RetryableFailure(
                    response.ErrorMessage ?? $"MCP memory provider '{provider.InstanceId}' returned a provider error."),
            _ => MemoryProviderOperationPollResult.TerminalFailure(
                response.ErrorMessage ?? "Malformed MCP memory status response: response kind does not match payload.")
        };
    }

    private static T? Deserialize<T>(string responseJson)
    {
        try
        {
            return JsonSerializer.Deserialize<T>(responseJson, McpMemoryProviderJson.Options);
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException($"Malformed MCP memory provider JSON response: {exception.Message}", exception);
        }
    }
}
