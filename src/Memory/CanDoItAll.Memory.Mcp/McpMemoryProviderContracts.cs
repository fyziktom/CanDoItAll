using CanDoItAll.Memory.Abstractions;

namespace CanDoItAll.Memory.Mcp;

public enum McpMemoryProviderResponseKind
{
    ContextPack = 0,
    OperationAccepted = 1,
    ProviderError = 2,
    UnsupportedCapability = 3
}

public enum McpMemoryOperationStatusResponseKind
{
    OperationAccepted = 0,
    OperationResult = 1,
    ProviderError = 2,
    UnsupportedCapability = 3
}

public static class McpMemoryCapabilityVersions
{
    public const string ToolV1 = "mcp-tool.v1";
}

public sealed record McpMemoryProviderResponse(
    McpMemoryProviderResponseKind Kind,
    MemoryContextPack? ContextPack,
    MemoryOperationAccepted? AcceptedOperation,
    string? ErrorMessage)
{
    public static McpMemoryProviderResponse FromContextPack(MemoryContextPack contextPack) =>
        new(McpMemoryProviderResponseKind.ContextPack, contextPack, AcceptedOperation: null, ErrorMessage: null);

    public static McpMemoryProviderResponse FromAccepted(MemoryOperationAccepted acceptedOperation) =>
        new(McpMemoryProviderResponseKind.OperationAccepted, ContextPack: null, acceptedOperation, ErrorMessage: null);

    public static McpMemoryProviderResponse ProviderError(string message) =>
        new(McpMemoryProviderResponseKind.ProviderError, ContextPack: null, AcceptedOperation: null, message);

    public static McpMemoryProviderResponse UnsupportedCapability(string message) =>
        new(McpMemoryProviderResponseKind.UnsupportedCapability, ContextPack: null, AcceptedOperation: null, message);
}

public sealed record McpMemoryOperationStatusToolResponse(
    McpMemoryOperationStatusResponseKind Kind,
    MemoryOperationAccepted? AcceptedOperation,
    MemoryOperationResult? OperationResult,
    string? ErrorMessage)
{
    public static McpMemoryOperationStatusToolResponse FromResult(MemoryOperationResult operationResult) =>
        new(McpMemoryOperationStatusResponseKind.OperationResult, AcceptedOperation: null, operationResult, ErrorMessage: null);

    public static McpMemoryOperationStatusToolResponse FromAccepted(MemoryOperationAccepted acceptedOperation) =>
        new(McpMemoryOperationStatusResponseKind.OperationAccepted, acceptedOperation, OperationResult: null, ErrorMessage: null);

    public static McpMemoryOperationStatusToolResponse ProviderError(string message) =>
        new(McpMemoryOperationStatusResponseKind.ProviderError, AcceptedOperation: null, OperationResult: null, message);
}
