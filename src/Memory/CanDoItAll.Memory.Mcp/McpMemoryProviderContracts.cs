using CanDoItAll.AgentFramework.Mcp.Abstractions;
using CanDoItAll.Memory.Abstractions;
using CanDoItAll.Memory.Application;

namespace CanDoItAll.Memory.Mcp;

public enum McpMemoryProviderResponseKind
{
    ContextPack = 0,
    OperationAccepted = 1,
    ProviderError = 2,
    UnsupportedCapability = 3
}

public enum McpMemoryAdapterResultKind
{
    OperationAccepted = 0,
    OperationResult = 1,
    ProviderEvents = 2,
    ProviderError = 3,
    UnsupportedCapability = 4
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

public sealed record McpMemoryAdapterResult(
    McpMemoryAdapterResultKind Kind,
    MemoryOperationAccepted? AcceptedOperation,
    MemoryOperationResult? OperationResult,
    IReadOnlyList<MemoryProviderEvent> Events,
    string Diagnostic)
{
    public static McpMemoryAdapterResult UnsupportedCapability(string diagnostic) =>
        new(McpMemoryAdapterResultKind.UnsupportedCapability, AcceptedOperation: null, OperationResult: null, Events: [], diagnostic);

    public static McpMemoryAdapterResult ProviderError(string diagnostic) =>
        new(McpMemoryAdapterResultKind.ProviderError, AcceptedOperation: null, OperationResult: null, Events: [], diagnostic);

    public static McpMemoryAdapterResult Accepted(MemoryOperationAccepted acceptedOperation, string diagnostic) =>
        new(McpMemoryAdapterResultKind.OperationAccepted, acceptedOperation, OperationResult: null, Events: [], diagnostic);

    public static McpMemoryAdapterResult FromOperationResult(MemoryOperationResult operationResult, string diagnostic) =>
        new(McpMemoryAdapterResultKind.OperationResult, AcceptedOperation: null, operationResult, Events: [], diagnostic);

    public static McpMemoryAdapterResult ProviderEvents(IReadOnlyList<MemoryProviderEvent> events, string diagnostic) =>
        new(McpMemoryAdapterResultKind.ProviderEvents, AcceptedOperation: null, OperationResult: null, events, diagnostic);
}

public sealed record McpMemoryOperationStatusToolResponse(
    McpMemoryAdapterResultKind Kind,
    MemoryOperationAccepted? AcceptedOperation,
    MemoryOperationResult? OperationResult,
    string? ErrorMessage)
{
    public static McpMemoryOperationStatusToolResponse FromResult(MemoryOperationResult operationResult) =>
        new(McpMemoryAdapterResultKind.OperationResult, AcceptedOperation: null, operationResult, ErrorMessage: null);

    public static McpMemoryOperationStatusToolResponse FromAccepted(MemoryOperationAccepted acceptedOperation) =>
        new(McpMemoryAdapterResultKind.OperationAccepted, acceptedOperation, OperationResult: null, ErrorMessage: null);

    public static McpMemoryOperationStatusToolResponse ProviderError(string message) =>
        new(McpMemoryAdapterResultKind.ProviderError, AcceptedOperation: null, OperationResult: null, message);
}

public sealed record McpMemoryProviderEventPollResponse(IReadOnlyList<MemoryProviderEvent> Events);

public interface IMcpMemoryProviderAdapter
{
    Task<McpMemoryAdapterResult> ExecuteIngestionAsync(
        MemoryProviderProfile provider,
        MemoryOperationRecord operation,
        MemoryIngestionRequest request,
        CancellationToken cancellationToken = default);

    Task<McpMemoryAdapterResult> GetOperationStatusAsync(
        MemoryProviderProfile provider,
        MemoryOperationStatusRequest request,
        CancellationToken cancellationToken = default);

    Task<McpMemoryAdapterResult> PollEventsAsync(
        MemoryProviderProfile provider,
        CancellationToken cancellationToken = default);
}
