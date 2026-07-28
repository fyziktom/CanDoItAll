using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

public sealed record FloatingAgentChatState(
    bool IsCatalogVisible,
    AgentChatCatalogTab CatalogTab,
    IReadOnlyList<ActiveAgentChat> ActiveChats)
{
    public ActiveAgentChat? VisibleChat
        => ActiveChats.FirstOrDefault(item => item.IsVisible);
}

public interface IAgentChatLauncher
{
    void ShowCatalog(AgentChatCatalogTab tab = AgentChatCatalogTab.Agents);

    Task<ActiveAgentChat> StartNewChatAsync(
        Guid agentId,
        CancellationToken cancellationToken = default);

    Task<ActiveAgentChat> OpenChatAsync(
        Guid agentId,
        Guid chatSessionId,
        CancellationToken cancellationToken = default);
}

public interface IFloatingAgentChatCoordinator : IAgentChatLauncher
{
    event EventHandler? Changed;

    FloatingAgentChatState Snapshot();

    FloatingAgentChatSettings CurrentSettings { get; }

    Task InitializeAsync(CancellationToken cancellationToken = default);

    void HideCatalog();

    ActiveAgentChat ShowChat(AgentChatHandleId handleId);

    ActiveAgentChat KeepActive(AgentChatHandleId handleId);

    void Stop(AgentChatHandleId handleId);

    ActiveAgentChat SetRunState(
        AgentChatHandleId handleId,
        ActiveAgentChatRunState runState);

    bool TryBeginOperation(AgentChatHandleId handleId);

    void ReconcileRunStateAfterOperation(AgentChatHandleId handleId);

    ActiveAgentChat AttachSession(AgentChatHandleId handleId, Guid chatSessionId);

    int PruneExpired();

    void ApplySettings(FloatingAgentChatSettings settings);
}

public interface IFloatingAgentChatSettingsService
{
    Task<FloatingAgentChatSettings> GetSettingsAsync(
        CancellationToken cancellationToken = default);

    Task<FloatingAgentChatSettings> SaveSettingsAsync(
        FloatingAgentChatSettings settings,
        CancellationToken cancellationToken = default);
}

public interface IAgentChatExecutionOrchestrator
{
    AgentChatOperationHandle StartSendMessage(
        AgentChatSendRequest request,
        CancellationToken cancellationToken = default);

    AgentChatOperationHandle StartSendMessage(
        Guid agentId,
        Guid? chatSessionId,
        string prompt,
        IReadOnlyList<string>? attachmentPaths = null,
        CancellationToken cancellationToken = default);

    AgentChatOperationHandle StartApprovalContinuation(
        Guid agentId,
        Guid chatSessionId,
        bool approved,
        bool autoApprovePendingToolCalls = false,
        CancellationToken cancellationToken = default);

    Task<AgentChatRunResult> SendMessageAsync(
        AgentChatSendRequest request,
        CancellationToken cancellationToken = default);

    Task<AgentChatRunResult> SendMessageAsync(
        Guid agentId,
        Guid? chatSessionId,
        string prompt,
        IReadOnlyList<string>? attachmentPaths = null,
        CancellationToken cancellationToken = default);

    Task<AgentChatRunResult> RespondToPendingApprovalsAsync(
        Guid agentId,
        Guid chatSessionId,
        bool approved,
        bool autoApprovePendingToolCalls = false,
        CancellationToken cancellationToken = default);
}

public sealed record AgentChatExecutionBehavior(
    bool RuntimeToolProvidersEnabled = true,
    bool WorkspaceToolsEnabled = true,
    bool ToolCapabilitiesEnabled = true)
{
    public static AgentChatExecutionBehavior Default { get; } = new();
}

public sealed record AgentChatSendRequest(
    Guid AgentId,
    Guid? ChatSessionId,
    string Prompt)
{
    public IReadOnlyList<string>? AttachmentPaths { get; init; }

    public AgentChatExecutionBehavior Behavior { get; init; } =
        AgentChatExecutionBehavior.Default;
}

public sealed record AgentChatOperationHandle(
    AgentExecutionActivityStreamId StreamId,
    Task<AgentChatRunResult> Completion);
