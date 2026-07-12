using System.Text.Json.Serialization;

namespace CanDoItAll.Memory.Abstractions;

public sealed record MemoryRequesterContext(
    string RequesterId,
    string Reason,
    string? AgentId,
    string? AgentRole,
    string? SessionId,
    string? UserVisibleTask)
{
    public static MemoryRequesterContext Agent(
        string requesterId,
        string reason,
        string agentId,
        string agentRole,
        string sessionId,
        string? userVisibleTask = null) =>
        new(
            MemoryProtocolGuard.EnsureText(requesterId, nameof(requesterId)),
            MemoryProtocolGuard.EnsureText(reason, nameof(reason)),
            MemoryProtocolGuard.EnsureText(agentId, nameof(agentId)),
            MemoryProtocolGuard.EnsureText(agentRole, nameof(agentRole)),
            MemoryProtocolGuard.EnsureText(sessionId, nameof(sessionId)),
            userVisibleTask);

    public static MemoryRequesterContext User(
        string requesterId,
        string reason,
        string? userVisibleTask = null) =>
        new(
            MemoryProtocolGuard.EnsureText(requesterId, nameof(requesterId)),
            MemoryProtocolGuard.EnsureText(reason, nameof(reason)),
            null,
            null,
            null,
            userVisibleTask);
}

public sealed record MemoryWorkspaceContext(
    string? WorkspaceId,
    string? WorkspaceName,
    string? CustomerId,
    string? Domain,
    IReadOnlyList<string> Tags)
{
    public static readonly MemoryWorkspaceContext None = new(null, null, null, null, []);
}

public sealed record MemoryExecutionContext(
    string? ProjectId,
    string? ProjectName,
    string? ProcessId,
    string? ProcessStepId,
    string? ProcessStepName,
    string? WorkflowId,
    string? WorkflowNodeId,
    IReadOnlyList<string> ArtifactIds)
{
    public static readonly MemoryExecutionContext None = new(null, null, null, null, null, null, null, []);
}

public sealed record MemoryPolicyContext(
    MemorySensitivity Sensitivity,
    MemoryRetentionPolicy Retention,
    IReadOnlyList<MemorySourceScope> AllowedSourceScopes,
    MemoryApprovalPosture ApprovalPosture,
    MemoryRedactionLevel RedactionLevel)
{
    public static readonly MemoryPolicyContext InternalDefault = new(
        MemorySensitivity.Internal,
        MemoryRetentionPolicy.Default,
        [],
        MemoryApprovalPosture.AutoApproved,
        MemoryRedactionLevel.None);
}

public sealed record MemoryBudget
{
    public static readonly MemoryBudget Default = new(20, 250_000, 8_000, TimeSpan.FromSeconds(30));

    [JsonConstructor]
    public MemoryBudget(
        int maxContextItems,
        long maxSourceBytes,
        int maxProviderTokens,
        TimeSpan timeout)
    {
        if (maxContextItems <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxContextItems), "Context item budget must be positive.");
        }

        if (maxSourceBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxSourceBytes), "Source byte budget must be positive.");
        }

        if (maxProviderTokens <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxProviderTokens), "Provider token budget must be positive.");
        }

        if (timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout), "Timeout budget must be positive.");
        }

        MaxContextItems = maxContextItems;
        MaxSourceBytes = maxSourceBytes;
        MaxProviderTokens = maxProviderTokens;
        Timeout = timeout;
    }

    public int MaxContextItems { get; }

    public long MaxSourceBytes { get; }

    public int MaxProviderTokens { get; }

    public TimeSpan Timeout { get; }
}

public sealed record MemorySourceProvenance(
    MemorySourceSnapshotId? SourceSnapshotId,
    string? SourceModule,
    IReadOnlyList<string> SourceRecordIds,
    IReadOnlyList<string> Citations)
{
    public static readonly MemorySourceProvenance None = new(null, null, [], []);
}

public sealed record MemoryRequestContext(
    MemoryWorkspaceContext Workspace,
    MemoryExecutionContext Execution,
    MemoryPolicyContext Policy,
    MemoryBudget Budget,
    MemoryExtensionData Extensions)
{
    public static readonly MemoryRequestContext Default = new(
        MemoryWorkspaceContext.None,
        MemoryExecutionContext.None,
        MemoryPolicyContext.InternalDefault,
        MemoryBudget.Default,
        MemoryExtensionData.Empty);
}
