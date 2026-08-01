using CanDoItAll.Memory.Abstractions;
using CanDoItAll.Memory.Application;

namespace CanDoItAll.Memory.Mcp;

internal static class McpMemoryProviderRequestFactory
{
    public static McpMemoryContextQueryToolRequest CreateContextQueryToolRequest(
        MemoryProviderProfile provider,
        MemoryOperationRecord operation,
        MemoryContextQueryRequest request)
    {
        return new McpMemoryContextQueryToolRequest(
            operation.OperationId.Value.ToString("D"),
            operation.CorrelationId.Value.ToString("D"),
            operation.CausationId.Value.ToString("D"),
            provider.InstanceId.Value,
            operation.RequestedCapability.Value,
            MemoryProtocolVersion.Current.Value,
            request.Query,
            request.RequestedCapabilities.Select(capability => capability.Value).ToArray(),
            CreateEnvelope(provider, operation, request, request.Context));
    }

    public static McpMemoryOperationStatusToolRequest CreateOperationStatusToolRequest(
        MemoryProviderProfile provider,
        MemoryOperationRecord operation)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(operation);
        if (provider.InstanceId != operation.ProviderInstanceId)
        {
            throw new InvalidOperationException(
                "The MCP status provider does not match the persisted memory operation provider.");
        }

        var request = new MemoryOperationStatusRequest(operation.OperationId);
        var context = operation.GetRequiredMemoryRequestContext();
        return new McpMemoryOperationStatusToolRequest(
            operation.OperationId.Value.ToString("D"),
            operation.CorrelationId.Value.ToString("D"),
            operation.CausationId.Value.ToString("D"),
            provider.InstanceId.Value,
            operation.RequestedCapability.Value,
            MemoryProtocolVersion.Current.Value,
            request,
            CreateEnvelope(provider, operation, request, context));
    }

    private static MemoryOperationEnvelope<TPayload> CreateEnvelope<TPayload>(
        MemoryProviderProfile provider,
        MemoryOperationRecord operation,
        TPayload payload,
        MemoryRequestContext context)
    {
        return new MemoryOperationEnvelope<TPayload>(
            MemoryProtocolVersion.Current,
            operation.OperationId,
            operation.CorrelationId,
            operation.CausationId,
            provider.InstanceId,
            operation.OperationKind,
            CreateRequesterContext(operation),
            context.Workspace,
            context.Execution,
            context.Policy,
            context.Budget,
            payload,
            context.Extensions);
    }

    private static MemoryRequesterContext CreateRequesterContext(MemoryOperationRecord operation)
    {
        var requester = operation.Requester;
        var caller = operation.Extensions.GetMemoryOperationCaller();
        return new MemoryRequesterContext(
            requester.RequesterId,
            caller is null ? "memory MCP operation" : $"{caller.Kind}: {caller.Route}",
            requester.AgentId,
            requester.AgentRole,
            requester.SessionId,
            UserVisibleTask: null);
    }
}
