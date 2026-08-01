using System.Collections.Concurrent;
using CanDoItAll.AgentFramework.Core;

namespace CanDoItAll.Tests.Unit;

public sealed class IsolatedCompatibilityEventDispatcherTests
{
    [Fact]
    public async Task Blocked_subscriber_does_not_delay_other_subscribers_or_canonical_completion()
    {
        var failures = new ConcurrentQueue<IsolatedCompatibilityEventFailure<ExecutionStageEvent>>();
        var overflows = new ConcurrentQueue<IsolatedCompatibilityEventOverflow<ExecutionStageEvent>>();
        using var dispatcher = new IsolatedCompatibilityEventDispatcher<ExecutionStageEvent>(
            failures.Enqueue,
            overflows.Enqueue,
            mailboxCapacity: 8);
        var sender = new object();
        var firstSubscriberEntered = CreateCompletionSource();
        var releaseFirstSubscriber = CreateCompletionSource();
        var firstSubscriberCompleted = CreateCompletionSource();
        var laterSubscriberCompleted = CreateCompletionSource();
        var laterSubscriberEvents = new ConcurrentQueue<ExecutionStage>();
        var firstSubscriberCallCount = 0;

        dispatcher.Subscribe((_, @event) =>
        {
            if (Interlocked.Increment(ref firstSubscriberCallCount) == 1)
            {
                firstSubscriberEntered.TrySetResult();
                releaseFirstSubscriber.Task.GetAwaiter().GetResult();
            }

            if (@event.Stage == ExecutionStage.Terminal)
            {
                firstSubscriberCompleted.TrySetResult();
            }
        });
        dispatcher.Subscribe((_, @event) =>
        {
            laterSubscriberEvents.Enqueue(@event.Stage);
            if (@event.Stage == ExecutionStage.Terminal)
            {
                laterSubscriberCompleted.TrySetResult();
            }
        });

        var persistenceCompleted = CreateCompletionSource();
        var runtimeCompleted = CreateCompletionSource();
        var terminalCompleted = CreateCompletionSource();
        var producer = Task.Run(() =>
        {
            dispatcher.Publish(
                sender,
                new ExecutionStageEvent(ExecutionStage.Persistence));
            persistenceCompleted.TrySetResult();

            dispatcher.Publish(
                sender,
                new ExecutionStageEvent(ExecutionStage.Runtime));
            runtimeCompleted.TrySetResult();

            dispatcher.Publish(
                sender,
                new ExecutionStageEvent(ExecutionStage.Terminal));
            terminalCompleted.TrySetResult();
        });

        await producer.WaitAsync(TimeSpan.FromSeconds(5));
        await firstSubscriberEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await laterSubscriberCompleted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.True(persistenceCompleted.Task.IsCompletedSuccessfully);
        Assert.True(runtimeCompleted.Task.IsCompletedSuccessfully);
        Assert.True(terminalCompleted.Task.IsCompletedSuccessfully);
        Assert.False(firstSubscriberCompleted.Task.IsCompleted);
        Assert.Equal(
            [
                ExecutionStage.Persistence,
                ExecutionStage.Runtime,
                ExecutionStage.Terminal
            ],
            laterSubscriberEvents);
        Assert.Empty(failures);
        Assert.Empty(overflows);

        releaseFirstSubscriber.TrySetResult();
        await firstSubscriberCompleted.Task.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Handler_failure_is_reported_without_delaying_later_subscriber()
    {
        var failureReported =
            new TaskCompletionSource<IsolatedCompatibilityEventFailure<ExecutionStageEvent>>(
                TaskCreationOptions.RunContinuationsAsynchronously);
        using var dispatcher = new IsolatedCompatibilityEventDispatcher<ExecutionStageEvent>(
            failure => failureReported.TrySetResult(failure),
            _ => throw new InvalidOperationException("Overflow was not expected."));
        var laterSubscriberCompleted = CreateCompletionSource();
        var sender = new object();
        var @event = new ExecutionStageEvent(ExecutionStage.Runtime);

        dispatcher.Subscribe((_, _) =>
            throw new InvalidOperationException("Expected subscriber failure."));
        dispatcher.Subscribe((observedSender, observedEvent) =>
        {
            Assert.Same(sender, observedSender);
            Assert.Same(@event, observedEvent);
            laterSubscriberCompleted.TrySetResult();
        });

        dispatcher.Publish(sender, @event);

        var failure = await failureReported.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await laterSubscriberCompleted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Same(sender, failure.Sender);
        Assert.Same(@event, failure.Event);
        Assert.IsType<InvalidOperationException>(failure.Exception);
    }

    [Fact]
    public async Task Duplicate_subscriptions_are_independent_and_removal_removes_last_match()
    {
        using var dispatcher = new IsolatedCompatibilityEventDispatcher<ExecutionStageEvent>(
            _ => throw new InvalidOperationException("Failure was not expected."),
            _ => throw new InvalidOperationException("Overflow was not expected."));
        var firstPublishCompleted = CreateCompletionSource();
        var secondPublishCompleted = CreateCompletionSource();
        var callCount = 0;
        EventHandler<ExecutionStageEvent> subscriber = (_, _) =>
        {
            var observedCallCount = Interlocked.Increment(ref callCount);
            if (observedCallCount == 2)
            {
                firstPublishCompleted.TrySetResult();
            }
            else if (observedCallCount == 3)
            {
                secondPublishCompleted.TrySetResult();
            }
        };

        dispatcher.Subscribe(subscriber);
        dispatcher.Subscribe(subscriber);
        dispatcher.Publish(
            this,
            new ExecutionStageEvent(ExecutionStage.Persistence));

        await firstPublishCompleted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        dispatcher.Unsubscribe(subscriber);
        dispatcher.Publish(
            this,
            new ExecutionStageEvent(ExecutionStage.Runtime));

        await secondPublishCompleted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(3, Volatile.Read(ref callCount));
    }

    [Fact]
    public async Task Full_mailbox_drops_newest_event_and_reports_coalesced_overflow()
    {
        var overflowReported =
            new TaskCompletionSource<IsolatedCompatibilityEventOverflow<ExecutionStageEvent>>(
                TaskCreationOptions.RunContinuationsAsynchronously);
        using var dispatcher = new IsolatedCompatibilityEventDispatcher<ExecutionStageEvent>(
            _ => throw new InvalidOperationException("Failure was not expected."),
            overflow => overflowReported.TrySetResult(overflow),
            mailboxCapacity: 1);
        var firstSubscriberEntered = CreateCompletionSource();
        var releaseFirstSubscriber = CreateCompletionSource();
        var acceptedEvents = new ConcurrentQueue<int>();
        var secondAcceptedEventCompleted = CreateCompletionSource();
        var sender = new object();

        dispatcher.Subscribe((_, @event) =>
        {
            acceptedEvents.Enqueue(@event.Sequence);
            if (@event.Sequence == 1)
            {
                firstSubscriberEntered.TrySetResult();
                releaseFirstSubscriber.Task.GetAwaiter().GetResult();
            }
            else if (@event.Sequence == 2)
            {
                secondAcceptedEventCompleted.TrySetResult();
            }
        });

        dispatcher.Publish(
            sender,
            new ExecutionStageEvent(ExecutionStage.Persistence, Sequence: 1));
        await firstSubscriberEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));

        dispatcher.Publish(
            sender,
            new ExecutionStageEvent(ExecutionStage.Runtime, Sequence: 2));
        var droppedEvent =
            new ExecutionStageEvent(ExecutionStage.Terminal, Sequence: 3);
        dispatcher.Publish(sender, droppedEvent);

        var overflow = await overflowReported.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Same(sender, overflow.Sender);
        Assert.Same(droppedEvent, overflow.LastDroppedEvent);
        Assert.Equal(1, overflow.MailboxCapacity);
        Assert.Equal(1, overflow.DroppedEventCount);

        releaseFirstSubscriber.TrySetResult();
        await secondAcceptedEventCompleted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal([1, 2], acceptedEvents);
    }

    private static TaskCompletionSource CreateCompletionSource()
    {
        return new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private sealed record ExecutionStageEvent(
        ExecutionStage Stage,
        int Sequence = 0);

    private enum ExecutionStage
    {
        Persistence,
        Runtime,
        Terminal
    }
}
