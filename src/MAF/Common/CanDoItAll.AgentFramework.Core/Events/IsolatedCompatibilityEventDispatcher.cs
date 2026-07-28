namespace CanDoItAll.AgentFramework.Core;

public sealed record IsolatedCompatibilityEventFailure<TEvent>(
    object Sender,
    TEvent Event,
    Exception Exception);

public sealed record IsolatedCompatibilityEventOverflow<TEvent>(
    object Sender,
    TEvent LastDroppedEvent,
    int MailboxCapacity,
    long DroppedEventCount);

public sealed class IsolatedCompatibilityEventDispatcher<TEvent> : IDisposable
{
    public const int DefaultMailboxCapacity = 64;

    private readonly Lock gate = new();
    private readonly List<SubscriberRegistration> registrations = [];
    private readonly int mailboxCapacity;
    private readonly Action<IsolatedCompatibilityEventFailure<TEvent>> onHandlerFailure;
    private readonly Action<IsolatedCompatibilityEventOverflow<TEvent>> onMailboxOverflow;
    private long generation;
    private bool disposed;

    public IsolatedCompatibilityEventDispatcher(
        Action<IsolatedCompatibilityEventFailure<TEvent>> onHandlerFailure,
        Action<IsolatedCompatibilityEventOverflow<TEvent>> onMailboxOverflow,
        int mailboxCapacity = DefaultMailboxCapacity)
    {
        ArgumentNullException.ThrowIfNull(onHandlerFailure);
        ArgumentNullException.ThrowIfNull(onMailboxOverflow);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(mailboxCapacity);

        this.onHandlerFailure = onHandlerFailure;
        this.onMailboxOverflow = onMailboxOverflow;
        this.mailboxCapacity = mailboxCapacity;
    }

    public void Subscribe(EventHandler<TEvent> subscriber)
    {
        ArgumentNullException.ThrowIfNull(subscriber);

        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            registrations.Add(new SubscriberRegistration(
                subscriber,
                mailboxCapacity,
                generation,
                NotifyHandlerFailure,
                NotifyMailboxOverflow));
        }
    }

    public void Unsubscribe(EventHandler<TEvent> subscriber)
    {
        ArgumentNullException.ThrowIfNull(subscriber);

        SubscriberRegistration? registration = null;
        lock (gate)
        {
            if (disposed)
            {
                return;
            }

            for (var index = registrations.Count - 1; index >= 0; index--)
            {
                if (!Equals(registrations[index].Subscriber, subscriber))
                {
                    continue;
                }

                registration = registrations[index];
                registrations.RemoveAt(index);
                break;
            }
        }

        registration?.Stop(discardPendingEvents: false);
    }

    public void Publish(object sender, TEvent @event)
    {
        ArgumentNullException.ThrowIfNull(sender);
        ArgumentNullException.ThrowIfNull(@event);

        SubscriberRegistration[] subscribers;
        long publicationGeneration;
        lock (gate)
        {
            if (disposed || registrations.Count == 0)
            {
                return;
            }

            subscribers = [.. registrations];
            publicationGeneration = generation;
        }

        foreach (var subscriber in subscribers)
        {
            subscriber.TryPublish(
                sender,
                @event,
                publicationGeneration);
        }
    }

    public void DiscardPendingEvents()
    {
        lock (gate)
        {
            if (disposed)
            {
                return;
            }

            generation = checked(generation + 1);
            foreach (var registration in registrations)
            {
                registration.DiscardPendingEvents(generation);
            }
        }
    }

    public void Dispose()
    {
        SubscriberRegistration[] subscribers;
        lock (gate)
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            subscribers = [.. registrations];
            registrations.Clear();
        }

        foreach (var subscriber in subscribers)
        {
            subscriber.Stop(discardPendingEvents: true);
        }
    }

    private void NotifyHandlerFailure(
        IsolatedCompatibilityEventFailure<TEvent> failure)
    {
        try
        {
            onHandlerFailure(failure);
        }
        catch (Exception exception)
        {
            System.Diagnostics.Trace.TraceError(
                "Agent compatibility event handler-failure reporting failed: {0}",
                exception);
        }
    }

    private void NotifyMailboxOverflow(
        IsolatedCompatibilityEventOverflow<TEvent> overflow)
    {
        try
        {
            onMailboxOverflow(overflow);
        }
        catch (Exception exception)
        {
            System.Diagnostics.Trace.TraceError(
                "Agent compatibility event overflow reporting failed: {0}",
                exception);
        }
    }

    private sealed class SubscriberRegistration(
        EventHandler<TEvent> subscriber,
        int mailboxCapacity,
        long initialGeneration,
        Action<IsolatedCompatibilityEventFailure<TEvent>> onHandlerFailure,
        Action<IsolatedCompatibilityEventOverflow<TEvent>> onMailboxOverflow)
    {
        private readonly Lock gate = new();
        private readonly Queue<EventEnvelope> mailbox = new(mailboxCapacity);
        private EventEnvelope? lastOverflow;
        private long droppedEventCount;
        private bool accepting = true;
        private bool drainScheduled;
        private bool overflowNotificationScheduled;
        private bool discardPendingEvents;
        private long generation = initialGeneration;

        public EventHandler<TEvent> Subscriber { get; } = subscriber;

        public void TryPublish(
            object sender,
            TEvent @event,
            long publicationGeneration)
        {
            var scheduleDrain = false;
            var scheduleOverflowNotification = false;
            lock (gate)
            {
                if (!accepting ||
                    publicationGeneration != generation)
                {
                    return;
                }

                var envelope = new EventEnvelope(sender, @event);
                if (mailbox.Count >= mailboxCapacity)
                {
                    lastOverflow = envelope;
                    droppedEventCount++;
                    if (!overflowNotificationScheduled)
                    {
                        overflowNotificationScheduled = true;
                        scheduleOverflowNotification = true;
                    }
                }
                else
                {
                    mailbox.Enqueue(envelope);
                    if (!drainScheduled)
                    {
                        drainScheduled = true;
                        scheduleDrain = true;
                    }
                }
            }

            if (scheduleDrain)
            {
                QueueWorker(DrainMailbox);
            }

            if (scheduleOverflowNotification)
            {
                QueueWorker(ReportMailboxOverflows);
            }
        }

        public void DiscardPendingEvents(long nextGeneration)
        {
            lock (gate)
            {
                generation = nextGeneration;
                mailbox.Clear();
                lastOverflow = null;
                droppedEventCount = 0;
            }
        }

        public void Stop(bool discardPendingEvents)
        {
            lock (gate)
            {
                if (!accepting)
                {
                    return;
                }

                accepting = false;
                this.discardPendingEvents = discardPendingEvents;
                if (discardPendingEvents)
                {
                    mailbox.Clear();
                }
            }
        }

        private void DrainMailbox()
        {
            while (true)
            {
                EventEnvelope envelope;
                lock (gate)
                {
                    if (discardPendingEvents || mailbox.Count == 0)
                    {
                        drainScheduled = false;
                        return;
                    }

                    envelope = mailbox.Dequeue();
                }

                try
                {
                    Subscriber(envelope.Sender, envelope.Event);
                }
                catch (Exception exception)
                {
                    onHandlerFailure(new IsolatedCompatibilityEventFailure<TEvent>(
                        envelope.Sender,
                        envelope.Event,
                        exception));
                }
            }
        }

        private void ReportMailboxOverflows()
        {
            while (true)
            {
                EventEnvelope envelope;
                long overflowCount;
                lock (gate)
                {
                    if (droppedEventCount == 0 || lastOverflow is null)
                    {
                        overflowNotificationScheduled = false;
                        return;
                    }

                    envelope = lastOverflow;
                    overflowCount = droppedEventCount;
                    lastOverflow = null;
                    droppedEventCount = 0;
                }

                onMailboxOverflow(new IsolatedCompatibilityEventOverflow<TEvent>(
                    envelope.Sender,
                    envelope.Event,
                    mailboxCapacity,
                    overflowCount));
            }
        }

        private static void QueueWorker(Action work)
        {
            ThreadPool.UnsafeQueueUserWorkItem(
                static callback => callback(),
                work,
                preferLocal: false);
        }

        private sealed record EventEnvelope(
            object Sender,
            TEvent Event);
    }
}
