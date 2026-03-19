using System.Collections.Concurrent;
using System.Threading.Channels;

namespace CanDoItAll.Manager;

public sealed class EventStreamHub<T>
{
    private readonly ConcurrentDictionary<Guid, Channel<T>> _subscribers = new();

    public ChannelReader<T> Subscribe(out Guid subscriptionId)
    {
        subscriptionId = Guid.NewGuid();
        var channel = Channel.CreateUnbounded<T>();
        _subscribers[subscriptionId] = channel;
        return channel.Reader;
    }

    public void Unsubscribe(Guid subscriptionId)
    {
        if (_subscribers.TryRemove(subscriptionId, out var channel))
        {
            channel.Writer.TryComplete();
        }
    }

    public async Task PublishAsync(T payload, CancellationToken cancellationToken = default)
    {
        foreach (var subscriber in _subscribers)
        {
            await subscriber.Value.Writer.WriteAsync(payload, cancellationToken);
        }
    }
}
