using CanDoItAll.Memory.Abstractions;

namespace CanDoItAll.Memory.Mcp;

public sealed partial class McpMemoryProviderDriver
{
    private static McpMemoryContextQueryToolRequest CreateContextQueryToolRequest(
        MemoryProviderProfile provider,
        MemoryOperationRecord operation,
        MemoryContextQueryRequest request)
    {
        var envelope = CreateEnvelope(provider, operation, request);
        return new McpMemoryContextQueryToolRequest(
            operation.OperationId.Value.ToString("D"),
            operation.CorrelationId.Value.ToString("D"),
            operation.CausationId.Value.ToString("D"),
            provider.InstanceId.Value,
            operation.RequestedCapability.Value,
            MemoryProtocolVersion.Current.Value,
            request.Query,
            request.RequestedCapabilities.Select(capability => capability.Value).ToArray(),
            envelope);
    }

    private static McpMemoryIngestionToolRequest CreateIngestionToolRequest(
        MemoryProviderProfile provider,
        MemoryOperationRecord operation,
        MemoryIngestionRequest request)
    {
        var envelope = CreateEnvelope(provider, operation, request);
        return new McpMemoryIngestionToolRequest(
            operation.OperationId.Value.ToString("D"),
            operation.CorrelationId.Value.ToString("D"),
            provider.InstanceId.Value,
            operation.RequestedCapability.Value,
            MemoryProtocolVersion.Current.Value,
            request.SourceSnapshotId.Value,
            envelope);
    }

    private static MemoryOperationEnvelope<TPayload> CreateEnvelope<TPayload>(
        MemoryProviderProfile provider,
        MemoryOperationRecord operation,
        TPayload payload)
    {
        return new MemoryOperationEnvelope<TPayload>(
            MemoryProtocolVersion.Current,
            operation.OperationId,
            operation.CorrelationId,
            operation.CausationId,
            provider.InstanceId,
            operation.OperationKind,
            CreateRequesterContext(operation.Requester),
            MemoryWorkspaceContext.None,
            CreateExecutionContext(operation.Requester),
            MemoryPolicyContext.InternalDefault,
            MemoryBudget.Default,
            payload,
            MemoryExtensionData.Empty);
    }

    private static MemoryRequesterContext CreateRequesterContext(MemoryLedgerRequester requester)
    {
        return new MemoryRequesterContext(
            requester.RequesterId,
            "memory MCP operation",
            requester.AgentId,
            requester.AgentRole,
            requester.SessionId,
            UserVisibleTask: null);
    }

    private static MemoryExecutionContext CreateExecutionContext(MemoryLedgerRequester requester)
    {
        return new MemoryExecutionContext(
            ProjectId: null,
            ProjectName: null,
            requester.ProcessId,
            requester.ProcessStepId,
            ProcessStepName: null,
            requester.WorkflowId,
            requester.WorkflowNodeId,
            ArtifactIds: []);
    }
}
