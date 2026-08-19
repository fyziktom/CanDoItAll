using CanDoItAll.Infrastructure.Configuration;
using CanDoItAll.Infrastructure.ControlPlane;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;

namespace CanDoItAll.Tests.Unit.Infrastructure;

public sealed class PostgreSqlStartupReadinessPolicyTests
{
    [Fact]
    public async Task RetriesCannotConnectNowUntilTheOperationSucceeds()
    {
        var timeProvider = new ManualTimeProvider();
        var delays = new List<TimeSpan>();
        var policy = CreatePolicy(
            timeProvider,
            TimeSpan.FromSeconds(10),
            TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(4),
            (delay, _) =>
            {
                delays.Add(delay);
                timeProvider.Advance(delay);
                return Task.CompletedTask;
            });
        var attempts = 0;

        await policy.ExecuteAsync(
            Guid.NewGuid(),
            _ =>
            {
                attempts++;
                if (attempts <= 2)
                {
                    throw CreatePostgresException(PostgresErrorCodes.CannotConnectNow);
                }

                return Task.CompletedTask;
            });

        Assert.Equal(3, attempts);
        Assert.Equal([TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2)], delays);
    }

    [Fact]
    public async Task ExhaustionRethrowsTheLastStartupExceptionWithoutAnotherAttempt()
    {
        var timeProvider = new ManualTimeProvider();
        var delays = new List<TimeSpan>();
        var policy = CreatePolicy(
            timeProvider,
            TimeSpan.FromSeconds(5),
            TimeSpan.FromSeconds(2),
            TimeSpan.FromSeconds(2),
            (delay, _) =>
            {
                delays.Add(delay);
                timeProvider.Advance(delay);
                return Task.CompletedTask;
            });
        var attempts = 0;
        PostgresException? lastException = null;

        var exception = await Assert.ThrowsAsync<PostgresException>(
            () => policy.ExecuteAsync(
                Guid.NewGuid(),
                _ =>
                {
                    attempts++;
                    lastException = CreatePostgresException(PostgresErrorCodes.CannotConnectNow);
                    throw lastException;
                }));

        Assert.Equal(3, attempts);
        Assert.Equal(
            [TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(1)],
            delays);
        Assert.Same(lastException, exception);
    }

    [Fact]
    public async Task PermanentPostgreSqlFailureIsNotRetried()
    {
        var timeProvider = new ManualTimeProvider();
        var delayCount = 0;
        var policy = CreatePolicy(
            timeProvider,
            TimeSpan.FromSeconds(10),
            TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(4),
            (_, _) =>
            {
                delayCount++;
                return Task.CompletedTask;
            });
        var expected = CreatePostgresException(PostgresErrorCodes.InvalidCatalogName);
        var attempts = 0;

        var exception = await Assert.ThrowsAsync<PostgresException>(
            () => policy.ExecuteAsync(
                Guid.NewGuid(),
                _ =>
                {
                    attempts++;
                    throw expected;
                }));

        Assert.Same(expected, exception);
        Assert.Equal(1, attempts);
        Assert.Equal(0, delayCount);
    }

    [Fact]
    public async Task ExponentialBackoffStopsGrowingAtTheConfiguredMaximum()
    {
        var timeProvider = new ManualTimeProvider();
        var delays = new List<TimeSpan>();
        var policy = CreatePolicy(
            timeProvider,
            TimeSpan.FromSeconds(30),
            TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(2),
            (delay, _) =>
            {
                delays.Add(delay);
                timeProvider.Advance(delay);
                return Task.CompletedTask;
            });
        var attempts = 0;

        await policy.ExecuteAsync(
            Guid.NewGuid(),
            _ =>
            {
                attempts++;
                if (attempts <= 4)
                {
                    throw CreatePostgresException(PostgresErrorCodes.CannotConnectNow);
                }

                return Task.CompletedTask;
            });

        Assert.Equal(5, attempts);
        Assert.Equal(
            [
                TimeSpan.FromSeconds(1),
                TimeSpan.FromSeconds(2),
                TimeSpan.FromSeconds(2),
                TimeSpan.FromSeconds(2)
            ],
            delays);
    }

    [Fact]
    public async Task CancellationDuringBackoffStopsBeforeTheNextAttempt()
    {
        using var cancellation = new CancellationTokenSource();
        var timeProvider = new ManualTimeProvider();
        var policy = CreatePolicy(
            timeProvider,
            TimeSpan.FromSeconds(10),
            TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(4),
            (_, cancellationToken) =>
            {
                cancellation.Cancel();
                return Task.FromCanceled(cancellationToken);
            });
        var attempts = 0;

        var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => policy.ExecuteAsync(
                Guid.NewGuid(),
                _ =>
                {
                    attempts++;
                    throw CreatePostgresException(PostgresErrorCodes.CannotConnectNow);
                },
                cancellation.Token));

        Assert.Equal(cancellation.Token, exception.CancellationToken);
        Assert.Equal(1, attempts);
    }

    [Fact]
    public async Task BlockingConnectionAttemptIsCancelledAtTheReadinessDeadline()
    {
        var timeProvider = new ManualTimeProvider();
        var operationStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var operationObservedCancellation = false;
        var policy = CreatePolicy(
            timeProvider,
            TimeSpan.FromSeconds(5),
            TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(4),
            (_, _) => Task.CompletedTask);

        var execution = policy.ExecuteAsync(
            Guid.NewGuid(),
            async cancellationToken =>
            {
                operationStarted.SetResult();
                try
                {
                    await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    operationObservedCancellation = true;
                    throw;
                }
            });

        await operationStarted.Task;
        timeProvider.Advance(TimeSpan.FromSeconds(5));

        await Assert.ThrowsAsync<TimeoutException>(() => execution);
        Assert.True(operationObservedCancellation);
    }

    [Fact]
    public async Task LateDisposableResultIsReleasedBeforeTimeoutIsReported()
    {
        var timeProvider = new ManualTimeProvider();
        var operationStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var allowLateResult = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var result = new TrackedAsyncDisposable();
        var policy = CreatePolicy(
            timeProvider,
            TimeSpan.FromSeconds(5),
            TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(4),
            (_, _) => Task.CompletedTask);

        var execution = policy.ExecuteAsync(
            Guid.NewGuid(),
            async _ =>
            {
                operationStarted.SetResult();
                await allowLateResult.Task;
                return result;
            });

        await operationStarted.Task;
        timeProvider.Advance(TimeSpan.FromSeconds(5));
        allowLateResult.SetResult();

        await Assert.ThrowsAsync<TimeoutException>(() => execution);
        Assert.True(result.IsDisposed);
    }

    [Fact]
    public async Task PreCancelledOperationIsNeverAttempted()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var attempts = 0;
        var policy = CreatePolicy(
            new ManualTimeProvider(),
            TimeSpan.FromSeconds(10),
            TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(4),
            (_, _) => Task.CompletedTask);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => policy.ExecuteAsync(
                Guid.NewGuid(),
                _ =>
                {
                    attempts++;
                    return Task.CompletedTask;
                },
                cancellation.Token));

        Assert.Equal(0, attempts);
    }

    private static PostgreSqlStartupReadinessPolicy CreatePolicy(
        ManualTimeProvider timeProvider,
        TimeSpan timeout,
        TimeSpan initialRetryDelay,
        TimeSpan maximumRetryDelay,
        Func<TimeSpan, CancellationToken, Task> delayAsync)
    {
        return new PostgreSqlStartupReadinessPolicy(
            new PostgreSqlStartupReadinessOptions
            {
                Timeout = timeout,
                InitialRetryDelay = initialRetryDelay,
                MaximumRetryDelay = maximumRetryDelay
            },
            timeProvider,
            NullLogger<PostgreSqlStartupReadinessPolicy>.Instance,
            delayAsync);
    }

    private static PostgresException CreatePostgresException(string sqlState)
    {
        return new PostgresException(
            "database system is starting up",
            "FATAL",
            "FATAL",
            sqlState);
    }

    private sealed class ManualTimeProvider : TimeProvider
    {
        private readonly List<ManualTimer> timers = [];
        private long timestamp;

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override long GetTimestamp()
        {
            return timestamp;
        }

        public override ITimer CreateTimer(
            TimerCallback callback,
            object? state,
            TimeSpan dueTime,
            TimeSpan period)
        {
            var timer = new ManualTimer(this, callback, state, dueTime, period);
            timers.Add(timer);
            return timer;
        }

        public void Advance(TimeSpan duration)
        {
            timestamp += duration.Ticks;
            foreach (var timer in timers.ToArray())
            {
                timer.FireIfDue(timestamp);
            }

            timers.RemoveAll(timer => timer.IsDisposed);
        }

        private sealed class ManualTimer : ITimer
        {
            private readonly ManualTimeProvider owner;
            private readonly TimerCallback callback;
            private readonly object? state;
            private long dueTimestamp;
            private TimeSpan period;

            public ManualTimer(
                ManualTimeProvider owner,
                TimerCallback callback,
                object? state,
                TimeSpan dueTime,
                TimeSpan period)
            {
                this.owner = owner;
                this.callback = callback;
                this.state = state;
                Change(dueTime, period);
            }

            public bool IsDisposed { get; private set; }

            public bool Change(TimeSpan dueTime, TimeSpan period)
            {
                if (IsDisposed)
                {
                    return false;
                }

                this.period = period;
                dueTimestamp = dueTime == Timeout.InfiniteTimeSpan
                    ? long.MaxValue
                    : owner.timestamp + dueTime.Ticks;
                return true;
            }

            public void Dispose()
            {
                IsDisposed = true;
            }

            public ValueTask DisposeAsync()
            {
                Dispose();
                return ValueTask.CompletedTask;
            }

            public void FireIfDue(long currentTimestamp)
            {
                if (IsDisposed || currentTimestamp < dueTimestamp)
                {
                    return;
                }

                if (period == Timeout.InfiniteTimeSpan)
                {
                    dueTimestamp = long.MaxValue;
                }
                else
                {
                    dueTimestamp = currentTimestamp + period.Ticks;
                }

                callback(state);
            }
        }
    }

    private sealed class TrackedAsyncDisposable : IAsyncDisposable
    {
        public bool IsDisposed { get; private set; }

        public ValueTask DisposeAsync()
        {
            IsDisposed = true;
            return ValueTask.CompletedTask;
        }
    }
}
