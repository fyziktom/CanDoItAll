using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

public interface IAgentChatExecutionNotificationSubscription : IDisposable
{
    AgentChatContextSource Source { get; }
}

public interface IAgentChatExecutionNotificationHub
{
    IAgentChatExecutionNotificationSubscription Subscribe(
        AgentChatContextSource source,
        Func<AgentChatExecutionCompleted, Task> handler);

    Task PublishAsync(
        AgentChatExecutionCompleted notification,
        CancellationToken cancellationToken = default);
}
