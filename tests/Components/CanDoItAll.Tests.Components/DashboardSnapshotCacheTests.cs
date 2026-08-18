using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Web.Dashboard;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CanDoItAll.Tests.Components.Shell;

public sealed class DashboardSnapshotCacheTests
{
    private static readonly DateTimeOffset InitialUtc = new(2026, 7, 22, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Reuses_success_until_expiry_and_force_bypasses_settled_entry()
    {
        var context = CreateContext();
        var loadCount = 0;
        context.Runner.LoadAsyncHandler = () =>
        {
            loadCount++;
            return Task.FromResult(CreateData(loadCount));
        };

        var first = await context.Cache.GetAsync(DashboardSnapshotRefreshMode.UseCached);
        var cached = await context.Cache.GetAsync(DashboardSnapshotRefreshMode.UseCached);

        Assert.Equal(1, loadCount);
        Assert.Same(first.Snapshot, cached.Snapshot);

        var forced = await context.Cache.GetAsync(DashboardSnapshotRefreshMode.Force);

        Assert.Equal(2, loadCount);
        Assert.NotSame(first.Snapshot, forced.Snapshot);
        Assert.Equal(2, forced.Snapshot.Data.Usage.ObservedTokens);
    }

    [Fact]
    public async Task Reloads_when_five_minute_interval_expires()
    {
        var context = CreateContext();
        var loadCount = 0;
        context.Runner.LoadAsyncHandler = () =>
        {
            loadCount++;
            return Task.FromResult(CreateData(loadCount));
        };

        await context.Cache.GetAsync(DashboardSnapshotRefreshMode.UseCached);
        context.Clock.Advance(TimeSpan.FromMinutes(5) - TimeSpan.FromTicks(1));
        await context.Cache.GetAsync(DashboardSnapshotRefreshMode.UseCached);
        Assert.Equal(1, loadCount);

        context.Clock.Advance(TimeSpan.FromTicks(1));
        await context.Cache.GetAsync(DashboardSnapshotRefreshMode.UseCached);
        Assert.Equal(2, loadCount);
    }

    [Fact]
    public async Task Concurrent_normal_and_force_calls_join_one_refresh()
    {
        var context = CreateContext();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var loadCount = 0;
        context.Runner.LoadAsyncHandler = async () =>
        {
            loadCount++;
            started.TrySetResult();
            await release.Task;
            return CreateData(loadCount);
        };

        var normalTask = context.Cache.GetAsync(DashboardSnapshotRefreshMode.UseCached);
        await started.Task;
        var forceTask = context.Cache.GetAsync(DashboardSnapshotRefreshMode.Force);

        release.TrySetResult();
        var reads = await Task.WhenAll(normalTask, forceTask);

        Assert.Equal(1, loadCount);
        Assert.Same(reads[0].Snapshot, reads[1].Snapshot);
    }

    [Fact]
    public async Task Caller_cancellation_does_not_cancel_shared_refresh()
    {
        var context = CreateContext();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var loadCount = 0;
        context.Runner.LoadAsyncHandler = async () =>
        {
            loadCount++;
            started.TrySetResult();
            await release.Task;
            return CreateData(loadCount);
        };

        using var callerCancellation = new CancellationTokenSource();
        var cancelledWait = context.Cache.GetAsync(
            DashboardSnapshotRefreshMode.UseCached,
            callerCancellation.Token);
        await started.Task;
        var joinedWait = context.Cache.GetAsync(DashboardSnapshotRefreshMode.UseCached);

        callerCancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cancelledWait);
        release.TrySetResult();
        var read = await joinedWait;

        Assert.Equal(1, loadCount);
        Assert.Equal(DashboardSnapshotState.Current, read.State);
    }

    [Fact]
    public async Task Failed_refresh_preserves_old_snapshot_and_throttles_normal_retry()
    {
        var context = CreateContext();
        var loadCount = 0;
        var shouldFail = false;
        context.Runner.LoadAsyncHandler = () =>
        {
            loadCount++;
            return shouldFail
                ? Task.FromException<DashboardSnapshotData>(new InvalidOperationException("source failed"))
                : Task.FromResult(CreateData(loadCount));
        };

        var first = await context.Cache.GetAsync(DashboardSnapshotRefreshMode.UseCached);
        context.Clock.Advance(TimeSpan.FromMinutes(5));
        shouldFail = true;

        var stale = await context.Cache.GetAsync(DashboardSnapshotRefreshMode.UseCached);
        var throttled = await context.Cache.GetAsync(DashboardSnapshotRefreshMode.UseCached);

        Assert.Equal(2, loadCount);
        Assert.Same(first.Snapshot, stale.Snapshot);
        Assert.Same(stale.Snapshot, throttled.Snapshot);
        Assert.Equal(DashboardSnapshotState.StaleAfterRefreshFailure, stale.State);
        Assert.NotNull(stale.LastRefreshFailureAtUtc);

        await context.Cache.GetAsync(DashboardSnapshotRefreshMode.Force);
        Assert.Equal(3, loadCount);
    }

    [Fact]
    public async Task Initial_failure_is_throttled_but_force_can_retry()
    {
        var context = CreateContext();
        var loadCount = 0;
        context.Runner.LoadAsyncHandler = () =>
        {
            loadCount++;
            return Task.FromException<DashboardSnapshotData>(new InvalidOperationException("source failed"));
        };

        var firstError = await Assert.ThrowsAsync<DashboardSnapshotUnavailableException>(() =>
            context.Cache.GetAsync(DashboardSnapshotRefreshMode.UseCached));
        await Assert.ThrowsAsync<DashboardSnapshotUnavailableException>(() =>
            context.Cache.GetAsync(DashboardSnapshotRefreshMode.UseCached));

        Assert.Equal(1, loadCount);
        Assert.Equal(InitialUtc.AddMinutes(5), firstError.NextAutomaticRefreshAtUtc);

        await Assert.ThrowsAsync<DashboardSnapshotUnavailableException>(() =>
            context.Cache.GetAsync(DashboardSnapshotRefreshMode.Force));
        Assert.Equal(2, loadCount);
    }

    [Fact]
    public async Task Runtime_change_during_load_discards_result_and_reloads_for_new_key()
    {
        var context = CreateContext();
        var firstStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var firstRelease = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var loadCount = 0;
        context.Runner.LoadAsyncHandler = async () =>
        {
            loadCount++;
            if (loadCount == 1)
            {
                firstStarted.TrySetResult();
                await firstRelease.Task;
            }

            return CreateData(loadCount);
        };

        var readTask = context.Cache.GetAsync(DashboardSnapshotRefreshMode.UseCached);
        await firstStarted.Task;
        context.Runtime.Snapshot = new DatabaseRuntimeSnapshot(
            Guid.NewGuid(),
            "second-fingerprint",
            Generation: 2);
        firstRelease.TrySetResult();

        var read = await readTask;

        Assert.Equal(2, loadCount);
        Assert.Equal(2, read.Snapshot.Data.Usage.ObservedTokens);
    }

    [Fact]
    public async Task Cancelled_initial_refresh_is_not_retained_or_throttled()
    {
        var context = CreateContext();
        var loadCount = 0;
        using var loaderCancellation = new CancellationTokenSource();
        loaderCancellation.Cancel();
        context.Runner.LoadAsyncHandler = () =>
        {
            loadCount++;
            return loadCount == 1
                ? Task.FromCanceled<DashboardSnapshotData>(loaderCancellation.Token)
                : Task.FromResult(CreateData(loadCount));
        };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            context.Cache.GetAsync(DashboardSnapshotRefreshMode.UseCached));
        var retry = await context.Cache.GetAsync(DashboardSnapshotRefreshMode.UseCached);

        Assert.Equal(2, loadCount);
        Assert.Equal(DashboardSnapshotState.Current, retry.State);
        Assert.Null(retry.LastRefreshFailureAtUtc);
    }

    [Fact]
    public async Task Cancelled_forced_refresh_preserves_current_cache_metadata()
    {
        var context = CreateContext();
        var loadCount = 0;
        using var loaderCancellation = new CancellationTokenSource();
        loaderCancellation.Cancel();
        context.Runner.LoadAsyncHandler = () =>
        {
            loadCount++;
            return loadCount == 2
                ? Task.FromCanceled<DashboardSnapshotData>(loaderCancellation.Token)
                : Task.FromResult(CreateData(loadCount));
        };

        var initial = await context.Cache.GetAsync(DashboardSnapshotRefreshMode.UseCached);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            context.Cache.GetAsync(DashboardSnapshotRefreshMode.Force));
        var cached = await context.Cache.GetAsync(DashboardSnapshotRefreshMode.UseCached);

        Assert.Equal(2, loadCount);
        Assert.Same(initial.Snapshot, cached.Snapshot);
        Assert.Equal(initial.NextAutomaticRefreshAtUtc, cached.NextAutomaticRefreshAtUtc);
        Assert.Equal(DashboardSnapshotState.Current, cached.State);
        Assert.Null(cached.LastRefreshFailureAtUtc);
    }

    [Theory]
    [InlineData(DashboardSnapshotRefreshMode.UseCached)]
    [InlineData(DashboardSnapshotRefreshMode.Force)]
    public async Task Failure_log_contains_typed_source_cache_identity_and_refresh_mode(
        DashboardSnapshotRefreshMode refreshMode)
    {
        var logger = new RecordingLogger<DashboardSnapshotCache>();
        var context = CreateContext(logger);
        var profileId = Guid.NewGuid();
        const string fingerprint = "fingerprint-must-not-be-logged";
        context.Runtime.Snapshot = new DatabaseRuntimeSnapshot(profileId, fingerprint, Generation: 42);
        context.Runner.LoadAsyncHandler = () => Task.FromException<DashboardSnapshotData>(
            new DashboardSnapshotSourceException(
                DashboardSnapshotSource.Projects,
                new InvalidOperationException("source failed")));

        await Assert.ThrowsAsync<DashboardSnapshotUnavailableException>(() =>
            context.Cache.GetAsync(refreshMode));
        await logger.Logged.Task.WaitAsync(TimeSpan.FromSeconds(5));

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Error, entry.Level);
        Assert.Equal(
            DashboardSnapshotSource.Projects,
            Assert.IsType<DashboardSnapshotSource>(entry.Properties["SnapshotSource"]));
        Assert.Equal(profileId, Assert.IsType<Guid>(entry.Properties["ProfileId"]));
        Assert.Equal(42L, Assert.IsType<long>(entry.Properties["Generation"]));
        Assert.Equal(
            refreshMode,
            Assert.IsType<DashboardSnapshotRefreshMode>(entry.Properties["RefreshMode"]));
        Assert.DoesNotContain(fingerprint, entry.Message, StringComparison.Ordinal);
    }

    private static DashboardCacheTestContext CreateContext(
        ILogger<DashboardSnapshotCache>? logger = null)
    {
        var runtime = new MutableDatabaseRuntimeState
        {
            Snapshot = new DatabaseRuntimeSnapshot(
                Guid.NewGuid(),
                "first-fingerprint",
                Generation: 1)
        };
        var runner = new TestDashboardSnapshotLoadRunner();
        var clock = new ManualTimeProvider(InitialUtc);
        var cache = new DashboardSnapshotCache(
            runtime,
            runner,
            clock,
            Options.Create(new DashboardSnapshotOptions()),
            logger ?? NullLogger<DashboardSnapshotCache>.Instance);

        return new DashboardCacheTestContext(cache, runner, runtime, clock);
    }

    private static DashboardSnapshotData CreateData(long observedTokens)
    {
        return new DashboardSnapshotData(
            Projects: [],
            WorkflowMode: DashboardActivityMode.RecentFallback,
            Workflows: [],
            ProcessMode: DashboardActivityMode.RecentFallback,
            Processes: [],
            Usage: new DashboardUsageTotals(
                observedTokens,
                KnownCostUsd: 0m,
                UnknownUsageObservationCount: 0,
                UpdatedAtUtc: InitialUtc));
    }

    private sealed record DashboardCacheTestContext(
        DashboardSnapshotCache Cache,
        TestDashboardSnapshotLoadRunner Runner,
        MutableDatabaseRuntimeState Runtime,
        ManualTimeProvider Clock);

    private sealed class MutableDatabaseRuntimeState : IDatabaseRuntimeState
    {
        public required DatabaseRuntimeSnapshot Snapshot { get; set; }

        public DatabaseRuntimeSnapshot GetSnapshot()
        {
            return Snapshot;
        }

        public void MarkCurrentProfile(ResolvedDatabaseProfile profile)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class TestDashboardSnapshotLoadRunner : IDashboardSnapshotLoadRunner
    {
        public Func<Task<DashboardSnapshotData>> LoadAsyncHandler { get; set; } = () =>
            throw new InvalidOperationException("A dashboard load handler was not configured.");

        public Task<DashboardSnapshotData> LoadAsync()
        {
            return LoadAsyncHandler();
        }
    }

    private sealed class ManualTimeProvider(DateTimeOffset initialUtc) : TimeProvider
    {
        private DateTimeOffset utcNow = initialUtc;

        public override DateTimeOffset GetUtcNow()
        {
            return utcNow;
        }

        public void Advance(TimeSpan duration)
        {
            utcNow += duration;
        }
    }

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public List<LogEntry> Entries { get; } = [];

        public TaskCompletionSource Logged { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            IReadOnlyDictionary<string, object?> properties = state is IEnumerable<KeyValuePair<string, object?>> structuredState
                ? structuredState.ToDictionary(pair => pair.Key, pair => pair.Value)
                : new Dictionary<string, object?>();
            Entries.Add(new LogEntry(logLevel, formatter(state, exception), properties));
            Logged.TrySetResult();
        }
    }

    private sealed record LogEntry(
        LogLevel Level,
        string Message,
        IReadOnlyDictionary<string, object?> Properties);
}
