using System.Text.Json.Serialization;

namespace CanDoItAll.Memory.Abstractions;

public static class MemoryOperationEnvelope
{
    public static MemoryOperationEnvelope<TPayload> Create<TPayload>(
        MemoryProviderInstanceId providerInstanceId,
        MemoryOperationKind operationKind,
        MemoryRequesterContext requestedBy,
        MemoryWorkspaceContext workspaceContext,
        MemoryExecutionContext executionContext,
        MemoryPolicyContext policyContext,
        MemoryBudget budget,
        TPayload payload,
        MemoryExtensionData? extensionData = null) =>
        MemoryOperationEnvelope<TPayload>.Create(
            providerInstanceId,
            operationKind,
            requestedBy,
            workspaceContext,
            executionContext,
            policyContext,
            budget,
            payload,
            extensionData);
}

public sealed record MemoryOperationEnvelope<TPayload>
{
    [JsonConstructor]
    public MemoryOperationEnvelope(
        MemoryProtocolVersion memoryProtocolVersion,
        MemoryOperationId operationId,
        MemoryCorrelationId correlationId,
        MemoryCausationId causationId,
        MemoryProviderInstanceId providerInstanceId,
        MemoryOperationKind operationKind,
        MemoryRequesterContext requestedBy,
        MemoryWorkspaceContext workspaceContext,
        MemoryExecutionContext executionContext,
        MemoryPolicyContext policyContext,
        MemoryBudget budget,
        TPayload payload,
        MemoryExtensionData extensionData)
    {
        ArgumentNullException.ThrowIfNull(requestedBy);
        ArgumentNullException.ThrowIfNull(workspaceContext);
        ArgumentNullException.ThrowIfNull(executionContext);
        ArgumentNullException.ThrowIfNull(policyContext);
        ArgumentNullException.ThrowIfNull(budget);
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentNullException.ThrowIfNull(extensionData);

        MemoryProtocolVersion = memoryProtocolVersion;
        OperationId = operationId;
        CorrelationId = correlationId;
        CausationId = causationId;
        ProviderInstanceId = providerInstanceId;
        OperationKind = operationKind;
        RequestedBy = requestedBy;
        WorkspaceContext = workspaceContext;
        ExecutionContext = executionContext;
        PolicyContext = policyContext;
        Budget = budget;
        Payload = payload;
        ExtensionData = extensionData;
    }

    public MemoryProtocolVersion MemoryProtocolVersion { get; }

    public MemoryOperationId OperationId { get; }

    public MemoryCorrelationId CorrelationId { get; }

    public MemoryCausationId CausationId { get; }

    public MemoryProviderInstanceId ProviderInstanceId { get; }

    public MemoryOperationKind OperationKind { get; }

    public MemoryRequesterContext RequestedBy { get; }

    public MemoryWorkspaceContext WorkspaceContext { get; }

    public MemoryExecutionContext ExecutionContext { get; }

    public MemoryPolicyContext PolicyContext { get; }

    public MemoryBudget Budget { get; }

    public TPayload Payload { get; }

    public MemoryExtensionData ExtensionData { get; }

    public static MemoryOperationEnvelope<TPayload> Create(
        MemoryProviderInstanceId providerInstanceId,
        MemoryOperationKind operationKind,
        MemoryRequesterContext requestedBy,
        MemoryWorkspaceContext workspaceContext,
        MemoryExecutionContext executionContext,
        MemoryPolicyContext policyContext,
        MemoryBudget budget,
        TPayload payload,
        MemoryExtensionData? extensionData = null) =>
        new(
            MemoryProtocolVersion.Current,
            MemoryOperationId.New(),
            MemoryCorrelationId.New(),
            MemoryCausationId.New(),
            providerInstanceId,
            operationKind,
            requestedBy,
            workspaceContext,
            executionContext,
            policyContext,
            budget,
            payload,
            extensionData ?? MemoryExtensionData.Empty);
}
