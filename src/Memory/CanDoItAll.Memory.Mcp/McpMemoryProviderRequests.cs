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

public sealed record McpMemoryIngestionToolRequest(
    string OperationId,
    string CorrelationId,
    string ProviderInstanceId,
    string CapabilityId,
    string MemoryProtocolVersion,
    string SourceSnapshotId,
    MemoryOperationEnvelope<MemoryIngestionRequest> Envelope);

public sealed record McpMemoryOperationStatusToolRequest(
    string OperationId,
    string MemoryProtocolVersion,
    MemoryOperationStatusRequest Request);

public sealed record McpMemoryProviderEventPollRequest(
    string ProviderInstanceId,
    string MemoryProtocolVersion);
