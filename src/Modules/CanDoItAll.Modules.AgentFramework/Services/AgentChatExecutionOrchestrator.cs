using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Modules.AgentFramework;

public sealed class AgentChatExecutionOrchestrator(
    IAgentFrameworkWorkspaceService workspaceService,
    IAgentChatContextRegistry contextRegistry,
    IAgentChatExecutionNotificationHub notificationHub) : IAgentChatExecutionOrchestrator
{
    public async Task<AgentChatRunResult> SendMessageAsync(
        Guid agentId,
        Guid? chatSessionId,
        string prompt,
        IReadOnlyList<string>? attachmentPaths = null,
        CancellationToken cancellationToken = default)
    {
        var context = await contextRegistry.CaptureAsync(cancellationToken);
        var invocation = AgentChatContextInvocationFactory.Create(
            context,
            agentId,
            chatSessionId,
            prompt);
        var result = await workspaceService.SendMessageAsync(
            agentId,
            chatSessionId,
            invocation.Prompt,
            cancellationToken,
            attachmentPaths,
            invocation.Options);
        await PublishCompletionAsync(result);
        return result;
    }

    public async Task<AgentChatRunResult> RespondToPendingApprovalsAsync(
        Guid agentId,
        Guid chatSessionId,
        bool approved,
        bool autoApprovePendingToolCalls = false,
        CancellationToken cancellationToken = default)
    {
        var result = await workspaceService.RespondToPendingApprovalsAsync(
            agentId,
            chatSessionId,
            approved,
            autoApprovePendingToolCalls,
            cancellationToken);
        await PublishCompletionAsync(result);
        return result;
    }

    private Task PublishCompletionAsync(AgentChatRunResult result)
    {
        return result.ContextCompletionNotification is { } notification
            ? notificationHub.PublishAsync(notification)
            : Task.CompletedTask;
    }
}
