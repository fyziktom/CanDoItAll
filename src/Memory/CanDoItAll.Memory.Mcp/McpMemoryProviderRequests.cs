using CanDoItAll.Memory.Abstractions;

namespace CanDoItAll.Memory.Mcp;

public sealed record McpMemoryContextQueryToolRequest(
    string OperationId,
    string CorrelationId,
    string CausationId,
    string ProviderInstanceId,
    string CapabilityId,
    string MemoryProtocolVersion,
    string Query,
    IReadOnlyList<string> RequestedCapabilities,
    MemoryOperationEnvelope<MemoryContextQueryRequest> Envelope);

public sealed record McpMemoryOperationStatusToolRequest(
    string OperationId,
    string CorrelationId,
    string CausationId,
    string ProviderInstanceId,
    string CapabilityId,
    string MemoryProtocolVersion,
    MemoryOperationStatusRequest Request,
    MemoryOperationEnvelope<MemoryOperationStatusRequest> Envelope);
