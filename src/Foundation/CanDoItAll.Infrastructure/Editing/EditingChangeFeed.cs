using CanDoItAll.Editing;

namespace CanDoItAll.Infrastructure.Editing;

public sealed class EditingChangeFeed(TimeProvider? timeProvider = null) : IEditingChangeObserver, IEditingChangePublisher {
    public const int SubscriptionCapacity = 16;
    public const int MaximumSubscriptions = 10_000;
    private readonly TimeProvider clock = timeProvider ?? TimeProvider.System;
    private readonly object sync = new();
    private readonly Dictionary<EditingTopic, HashSet<EditingChangeSubscription>> topics = [];
    private readonly Guid epoch = Guid.NewGuid();
    private long sequence;
    private int subscriptions;
    private long deliveries;

    public long DeliveredHints => Interlocked.Read(ref deliveries);

    public int ActiveSubscriptions {
        get {
            lock (sync) {
                return subscriptions;
            }
        }
    }

    public int ActiveTopics {
        get {
            lock (sync) {
                return topics.Count;
            }
        }
    }

    public IEditingChangeSubscription Subscribe(EditingTopic topic) {
        RequireTopic(topic);
        lock (sync) {
            if (subscriptions == MaximumSubscriptions) {
                throw new InvalidOperationException("The change observer has reached its subscription limit.");
            }
            if (!topics.TryGetValue(topic, out var interested)) {
                topics.Add(topic, interested = []);
            }
            var subscription = new EditingChangeSubscription(new(epoch, sequence), item => Remove(topic, item));
            interested.Add(subscription);
            subscriptions++;
            return subscription;
        }
    }

    public void Publish(EditingTopic resource, EditingChangeKind kind, Guid? originEditorId = null, Guid? operationId = null) {
        RequireTopic(resource);
        if (resource.ResourceId is null || !Enum.IsDefined(kind)) {
            throw new ArgumentException("A committed change must name a resource and a valid category.");
        }
        lock (sync) {
            var change = new EditingChange(new(epoch, ++sequence), resource, kind, originEditorId,
                operationId ?? Guid.NewGuid(), clock.GetUtcNow());
            Deliver(resource, change);
            Deliver(resource with { ResourceId = null }, change);
        }
    }

    private void Deliver(EditingTopic topic, EditingChange change) {
        if (!topics.TryGetValue(topic, out var interested)) {
            return;
        }
        foreach (var subscriber in interested) {
            subscriber.Enqueue(change);
            deliveries++;
        }
    }

    private void Remove(EditingTopic topic, EditingChangeSubscription subscription) {
        lock (sync) {
            if (topics.TryGetValue(topic, out var interested) && interested.Remove(subscription)) {
                subscriptions--;
                if (interested.Count == 0) {
                    topics.Remove(topic);
                }
            }
        }
    }

    private static void RequireTopic(EditingTopic topic) {
        if (topic.Scope.Id == Guid.Empty || !Enum.IsDefined(topic.Scope.Kind) || !Enum.IsDefined(topic.Owner) ||
                topic.ResourceId == Guid.Empty) {
            throw new ArgumentException("A change topic must have a valid scope, owner and optional record identity.");
        }
    }
}
