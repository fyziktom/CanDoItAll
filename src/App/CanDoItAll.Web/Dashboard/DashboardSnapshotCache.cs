using CanDoItAll.Infrastructure.Persistence;
using Microsoft.Extensions.Options;

namespace CanDoItAll.Web.Dashboard;

public sealed class DashboardSnapshotCache
{
    private const int MaximumRuntimeChangeRetries = 3;

    private readonly object sync = new();
    private readonly IDatabaseRuntimeState runtimeState;
    private readonly IDashboardSnapshotLoadRunner loadRunner;
    private readonly TimeProvider timeProvider;
    private readonly TimeSpan refreshInterval;
    private readonly ILogger<DashboardSnapshotCache> logger;
    private CacheState? state;
    private InFlightRefresh? inFlight;

    public DashboardSnapshotCache(
        IDatabaseRuntimeState runtimeState,
        IDashboardSnapshotLoadRunner loadRunner,
        TimeProvider timeProvider,
        IOptions<DashboardSnapshotOptions> options,
        ILogger<DashboardSnapshotCache> logger)
    {
        ArgumentNullException.ThrowIfNull(runtimeState);
        ArgumentNullException.ThrowIfNull(loadRunner);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        if (options.Value.RefreshInterval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                options.Value.RefreshInterval,
                "Dashboard snapshot refresh interval must be positive.");
        }

        this.runtimeState = runtimeState;
        this.loadRunner = loadRunner;
        this.timeProvider = timeProvider;
        refreshInterval = options.Value.RefreshInterval;
        this.logger = logger;
    }

    public async Task<DashboardSnapshotRead> GetAsync(
        DashboardSnapshotRefreshMode refreshMode,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        for (var attempt = 0; attempt < MaximumRuntimeChangeRetries; attempt++)
        {
            var cacheKey = ResolveCurrentKey();
            var lookup = ResolveLookup(cacheKey, refreshMode);
            if (lookup.CachedRead is not null)
            {
                return lookup.CachedRead;
            }

            if (lookup.UnavailableUntilUtc.HasValue)
            {
                throw new DashboardSnapshotUnavailableException(lookup.UnavailableUntilUtc.Value);
            }

            if (lookup.RefreshStarter is not null)
            {
                _ = CompleteRefreshAsync(lookup.RefreshStarter);
            }

            var outcome = await lookup.RefreshTask!
                .WaitAsync(cancellationToken)
                .ConfigureAwait(false);

            if (outcome.RuntimeChanged)
            {
                continue;
            }

            if (outcome.Read is not null)
            {
                return outcome.Read;
            }

            throw new DashboardSnapshotUnavailableException(outcome.NextAutomaticRefreshAtUtc);
        }

        throw new InvalidOperationException(
            "Dashboard snapshot could not stabilize because the active database runtime changed repeatedly.");
    }

    private CacheLookup ResolveLookup(
        DashboardCacheKey cacheKey,
        DashboardSnapshotRefreshMode refreshMode)
    {
        lock (sync)
        {
            if (inFlight is not null)
            {
                return CacheLookup.Join(inFlight.Completion.Task);
            }

            if (state is not null && state.Key != cacheKey)
            {
                state = null;
            }

            var nowUtc = timeProvider.GetUtcNow();
            if (refreshMode == DashboardSnapshotRefreshMode.UseCached &&
                state is not null &&
                nowUtc < state.NextAutomaticRefreshAtUtc)
            {
                return state.Snapshot is null
                    ? CacheLookup.Unavailable(state.NextAutomaticRefreshAtUtc)
                    : CacheLookup.FromRead(CreateRead(state));
            }

            var completion = new TaskCompletionSource<RefreshOutcome>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            var starter = new RefreshStarter(cacheKey, refreshMode, completion);
            inFlight = new InFlightRefresh(cacheKey, completion);

            return CacheLookup.Start(completion.Task, starter);
        }
    }

    private async Task CompleteRefreshAsync(RefreshStarter starter)
    {
        try
        {
            var data = await loadRunner.LoadAsync().ConfigureAwait(false);
            var capturedAtUtc = timeProvider.GetUtcNow();
            var hasCurrentKey = TryResolveCurrentKey(out var currentKey);
            RefreshOutcome outcome;

            lock (sync)
            {
                if (!hasCurrentKey || currentKey != starter.Key)
                {
                    outcome = RefreshOutcome.RuntimeChangedResult;
                }
                else
                {
                    var snapshot = new DashboardSnapshot(data, capturedAtUtc);
                    state = new CacheState(
                        starter.Key,
                        snapshot,
                        capturedAtUtc + refreshInterval,
                        LastRefreshFailureAtUtc: null);
                    outcome = RefreshOutcome.Success(CreateRead(state));
                }

                ClearInFlight(starter.Completion);
            }

            starter.Completion.TrySetResult(outcome);
        }
        catch (OperationCanceledException exception)
        {
            lock (sync)
            {
                ClearInFlight(starter.Completion);
            }

            if (exception.CancellationToken.CanBeCanceled)
            {
                starter.Completion.TrySetCanceled(exception.CancellationToken);
            }
            else
            {
                starter.Completion.TrySetCanceled();
            }
        }
        catch (Exception exception)
        {
            var failedAtUtc = timeProvider.GetUtcNow();
            var hasCurrentKey = TryResolveCurrentKey(out var currentKey);
            DateTimeOffset? nextAutomaticRefreshAtUtc = null;
            RefreshOutcome outcome;

            lock (sync)
            {
                if (!hasCurrentKey || currentKey != starter.Key)
                {
                    outcome = RefreshOutcome.RuntimeChangedResult;
                }
                else
                {
                    var previousSnapshot = state?.Key == starter.Key
                        ? state.Snapshot
                        : null;
                    state = new CacheState(
                        starter.Key,
                        previousSnapshot,
                        failedAtUtc + refreshInterval,
                        failedAtUtc);
                    outcome = previousSnapshot is null
                        ? RefreshOutcome.Unavailable(state.NextAutomaticRefreshAtUtc)
                        : RefreshOutcome.Success(CreateRead(state));
                    nextAutomaticRefreshAtUtc = state.NextAutomaticRefreshAtUtc;
                }

                ClearInFlight(starter.Completion);
            }

            starter.Completion.TrySetResult(outcome);

            if (nextAutomaticRefreshAtUtc.HasValue)
            {
                var source = exception is DashboardSnapshotSourceException sourceException
                    ? sourceException.SnapshotSource
                    : DashboardSnapshotSource.Coordinator;
                logger.LogError(
                    exception,
                    "Dashboard snapshot refresh failed for source {SnapshotSource}, database profile {ProfileId}, generation {Generation}, and mode {RefreshMode}. Automatic retry is eligible at {NextAutomaticRefreshAtUtc}.",
                    source,
                    starter.Key.ProfileId,
                    starter.Key.Generation,
                    starter.RefreshMode,
                    nextAutomaticRefreshAtUtc.Value);
            }
        }
    }

    private void ClearInFlight(TaskCompletionSource<RefreshOutcome> completion)
    {
        if (ReferenceEquals(inFlight?.Completion, completion))
        {
            inFlight = null;
        }
    }

    private DashboardCacheKey ResolveCurrentKey()
    {
        if (!TryResolveCurrentKey(out var cacheKey))
        {
            throw new InvalidOperationException(
                "Dashboard snapshot cannot load before the active database runtime is initialized.");
        }

        return cacheKey;
    }

    private bool TryResolveCurrentKey(out DashboardCacheKey cacheKey)
    {
        var runtime = runtimeState.GetSnapshot();
        if (!runtime.ActiveProfileId.HasValue || string.IsNullOrWhiteSpace(runtime.ActiveFingerprint))
        {
            cacheKey = default!;
            return false;
        }

        cacheKey = new DashboardCacheKey(
            runtime.ActiveProfileId.Value,
            runtime.ActiveFingerprint,
            runtime.Generation);
        return true;
    }

    private static DashboardSnapshotRead CreateRead(CacheState cacheState)
    {
        var snapshot = cacheState.Snapshot ?? throw new InvalidOperationException(
            "A dashboard cache read requires a successful snapshot.");
        var snapshotState = cacheState.LastRefreshFailureAtUtc.HasValue
            ? DashboardSnapshotState.StaleAfterRefreshFailure
            : DashboardSnapshotState.Current;

        return new DashboardSnapshotRead(
            snapshot,
            cacheState.NextAutomaticRefreshAtUtc,
            snapshotState,
            cacheState.LastRefreshFailureAtUtc);
    }

    private sealed record DashboardCacheKey(
        Guid ProfileId,
        string Fingerprint,
        long Generation);

    private sealed record CacheState(
        DashboardCacheKey Key,
        DashboardSnapshot? Snapshot,
        DateTimeOffset NextAutomaticRefreshAtUtc,
        DateTimeOffset? LastRefreshFailureAtUtc);

    private sealed record InFlightRefresh(
        DashboardCacheKey Key,
        TaskCompletionSource<RefreshOutcome> Completion);

    private sealed record RefreshStarter(
        DashboardCacheKey Key,
        DashboardSnapshotRefreshMode RefreshMode,
        TaskCompletionSource<RefreshOutcome> Completion);

    private sealed record RefreshOutcome(
        DashboardSnapshotRead? Read,
        DateTimeOffset NextAutomaticRefreshAtUtc,
        bool RuntimeChanged)
    {
        public static RefreshOutcome RuntimeChangedResult { get; } = new(
            Read: null,
            NextAutomaticRefreshAtUtc: default,
            RuntimeChanged: true);

        public static RefreshOutcome Success(DashboardSnapshotRead read)
            => new(read, read.NextAutomaticRefreshAtUtc, RuntimeChanged: false);

        public static RefreshOutcome Unavailable(DateTimeOffset nextAutomaticRefreshAtUtc)
            => new(Read: null, nextAutomaticRefreshAtUtc, RuntimeChanged: false);
    }

    private sealed record CacheLookup(
        DashboardSnapshotRead? CachedRead,
        DateTimeOffset? UnavailableUntilUtc,
        Task<RefreshOutcome>? RefreshTask,
        RefreshStarter? RefreshStarter)
    {
        public static CacheLookup FromRead(DashboardSnapshotRead read)
            => new(read, UnavailableUntilUtc: null, RefreshTask: null, RefreshStarter: null);

        public static CacheLookup Unavailable(DateTimeOffset unavailableUntilUtc)
            => new(CachedRead: null, unavailableUntilUtc, RefreshTask: null, RefreshStarter: null);

        public static CacheLookup Join(Task<RefreshOutcome> refreshTask)
            => new(CachedRead: null, UnavailableUntilUtc: null, refreshTask, RefreshStarter: null);

        public static CacheLookup Start(
            Task<RefreshOutcome> refreshTask,
            RefreshStarter refreshStarter)
            => new(CachedRead: null, UnavailableUntilUtc: null, refreshTask, refreshStarter);
    }
}

public sealed class DashboardSnapshotUnavailableException : Exception
{
    public DashboardSnapshotUnavailableException(DateTimeOffset nextAutomaticRefreshAtUtc)
        : base("Dashboard snapshot data is currently unavailable.")
    {
        NextAutomaticRefreshAtUtc = nextAutomaticRefreshAtUtc;
    }

    public DateTimeOffset NextAutomaticRefreshAtUtc { get; }
}
