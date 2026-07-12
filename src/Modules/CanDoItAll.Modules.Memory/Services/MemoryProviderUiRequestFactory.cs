using CanDoItAll.Memory.Abstractions;
using CanDoItAll.Memory.Application;

namespace CanDoItAll.Modules.Memory.Services;

public sealed class MemoryProviderUiRequestFactory(TimeProvider timeProvider)
{
    public MemoryProviderSelectionPolicy CreateSelectionPolicy(
        string? providerInstanceId,
        MemoryCapabilityId requiredCapability)
    {
        var policy = MemoryProviderSelectionPolicy.RequireCapability(requiredCapability);
        return string.IsNullOrWhiteSpace(providerInstanceId)
            ? policy
            : policy with { ExplicitProviderId = MemoryProviderInstanceId.Parse(providerInstanceId) };
    }

    public MemoryOperationCaller CreateCaller(string route) =>
        MemoryOperationCaller.UiAction(route, CreateRequester());

    public MemoryLedgerRequester CreateRequester() =>
        new(
            RequesterId: "memory-ui",
            AgentId: null,
            AgentRole: null,
            SessionId: "memory-ui-session",
            WorkflowId: null,
            WorkflowNodeId: null,
            ProcessId: null,
            ProcessStepId: null);

    public MemoryLedgerRetentionPolicy CreateRetentionPolicy()
    {
        var now = timeProvider.GetUtcNow();
        return MemoryLedgerRetentionPolicy.Expiring(now.AddDays(7), now.AddDays(30));
    }

    public static MemoryOperationId ParseOperationId(string operationId) =>
        Guid.TryParse(operationId, out var parsed)
            ? new MemoryOperationId(parsed)
            : throw new ArgumentException("Operation id must be a valid GUID.", nameof(operationId));

    public static MemoryContextPackId ParseContextPackId(string contextPackId) =>
        Guid.TryParse(contextPackId, out var parsed)
            ? new MemoryContextPackId(parsed)
            : throw new ArgumentException("Context pack id must be a valid GUID.", nameof(contextPackId));

    public static MemoryProviderEventId ParseProviderEventId(string providerEventId) =>
        Guid.TryParse(providerEventId, out var parsed)
            ? new MemoryProviderEventId(parsed)
            : throw new ArgumentException("Provider event id must be a valid GUID.", nameof(providerEventId));
}
