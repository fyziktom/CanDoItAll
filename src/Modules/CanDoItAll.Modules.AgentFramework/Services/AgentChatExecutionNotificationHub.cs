using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.AgentFramework;

public sealed class AgentChatExecutionNotificationHub(
    ILogger<AgentChatExecutionNotificationHub> logger) : IAgentChatExecutionNotificationHub
{
    private const int MaximumSubscriptions = 64;
    private const int MaximumRememberedExecutions = 1_024;
    private readonly object gate = new();
    private readonly Dictionary<Guid, SubscriptionEntry> subscriptions = [];
    private readonly HashSet<Guid> publishedExecutionRunIds = [];
    private readonly Queue<Guid> publishedExecutionRunOrder = [];

    public IAgentChatExecutionNotificationSubscription Subscribe(
        AgentChatContextSource source,
        Func<AgentChatExecutionCompleted, Task> handler)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(handler);
        var subscriptionId = Guid.NewGuid();
        lock (gate)
        {
            if (subscriptions.Count >= MaximumSubscriptions)
            {
                throw new InvalidOperationException(
                    $"No more than {MaximumSubscriptions} agent chat execution notification subscriptions may be active in one scope.");
            }

            subscriptions.Add(subscriptionId, new SubscriptionEntry(source, handler));
        }

        return new Subscription(this, subscriptionId, source);
    }

    public async Task PublishAsync(
        AgentChatExecutionCompleted notification,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(notification);
        SubscriptionEntry[] matchingSubscriptions;
        lock (gate)
        {
            if (!publishedExecutionRunIds.Add(notification.ExecutionRunId))
            {
                return;
            }

            publishedExecutionRunOrder.Enqueue(notification.ExecutionRunId);
            while (publishedExecutionRunOrder.Count > MaximumRememberedExecutions)
            {
                publishedExecutionRunIds.Remove(publishedExecutionRunOrder.Dequeue());
            }

            matchingSubscriptions = subscriptions.Values
                .Where(item => item.Source == notification.Source)
                .ToArray();
        }

        cancellationToken.ThrowIfCancellationRequested();
        await Task.WhenAll(matchingSubscriptions.Select(subscription =>
            PublishToSubscriptionAsync(subscription, notification, cancellationToken)));
    }

    private async Task PublishToSubscriptionAsync(
        SubscriptionEntry subscription,
        AgentChatExecutionCompleted notification,
        CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            await subscription.Handler(notification);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Agent chat execution notification handler failed. SourceKind={SourceKind} SourceId={SourceId} ExecutionRunId={ExecutionRunId} FailureType={FailureType}.",
                notification.Source.Kind.Value,
                notification.Source.Id.Value,
                notification.ExecutionRunId,
                exception.GetType().Name);
        }
    }

    private void Unsubscribe(Guid subscriptionId)
    {
        lock (gate)
        {
            subscriptions.Remove(subscriptionId);
        }
    }

    private sealed record SubscriptionEntry(
        AgentChatContextSource Source,
        Func<AgentChatExecutionCompleted, Task> Handler);

    private sealed class Subscription(
        AgentChatExecutionNotificationHub owner,
        Guid subscriptionId,
        AgentChatContextSource source) : IAgentChatExecutionNotificationSubscription
    {
        private AgentChatExecutionNotificationHub? owner = owner;

        public AgentChatContextSource Source { get; } = source;

        public void Dispose()
        {
            Interlocked.Exchange(ref owner, null)?.Unsubscribe(subscriptionId);
        }
    }
}
