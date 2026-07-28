using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Workspace.ApiAccess;
using Microsoft.Extensions.Options;

namespace CanDoItAll.Web.Api.Streaming;

public sealed class ProfileBoundedReplayEventStream<T> : IDisposable
{
    private readonly object sync = new();
    private readonly IDatabaseRuntimeState runtimeState;
    private readonly IDatabaseSwitchNotificationService switchNotifications;
    private readonly ApiServerSentEventsOptions options;
    private readonly ILogger<ProfileBoundedReplayEventStream<T>> logger;
    private ProfileStreamState? current;
    private long latestSequence;
    private bool disposed;

    public ProfileBoundedReplayEventStream(
        IDatabaseRuntimeState runtimeState,
        IDatabaseSwitchNotificationService switchNotifications,
        IOptions<ApiAccessOptions> options,
        ILogger<ProfileBoundedReplayEventStream<T>> logger)
    {
        this.runtimeState = runtimeState;
        this.switchNotifications = switchNotifications;
        this.options = options.Value.ServerSentEvents;
        this.logger = logger;
        switchNotifications.Changed += HandleDatabaseProfileChanged;
    }

    public ProfileReplayStreamLease<T> OpenCurrent()
    {
        ProfileStreamState state;
        ProfileStreamState? retired;
        lock (sync)
        {
            state = GetCurrentState(out retired);
        }

        Cancel(retired);
        return new ProfileReplayStreamLease<T>(
            state.ProfileId,
            state.Generation,
            state.Stream,
            state.Lifetime.Token);
    }

    public long Publish(T value)
    {
        ArgumentNullException.ThrowIfNull(value);
        ProfileStreamState state;
        ProfileStreamState? retired;
        long sequence;
        lock (sync)
        {
            state = GetCurrentState(out retired);
            sequence = state.Stream.Publish(value);
            latestSequence = sequence;
        }

        Cancel(retired);
        return sequence;
    }

    public void Dispose()
    {
        ProfileStreamState? retired;
        lock (sync)
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            switchNotifications.Changed -= HandleDatabaseProfileChanged;
            retired = current;
            current = null;
        }

        Cancel(retired);
    }

    private ProfileStreamState GetCurrentState(out ProfileStreamState? retired)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        var snapshot = runtimeState.GetSnapshot();
        if (!snapshot.ActiveProfileId.HasValue)
        {
            throw new InvalidOperationException(
                "The database runtime profile must be initialized before API events can be published or streamed.");
        }

        if (current is not null &&
            current.ProfileId == snapshot.ActiveProfileId.Value &&
            current.Generation == snapshot.Generation)
        {
            retired = null;
            return current;
        }

        retired = current;
        current = CreateState(
            snapshot.ActiveProfileId.Value,
            snapshot.Generation);
        return current;
    }

    private void HandleDatabaseProfileChanged(
        object? sender,
        DatabaseProfileChangedNotification notification)
    {
        ProfileStreamState? retired;
        lock (sync)
        {
            if (disposed)
            {
                return;
            }

            var snapshot = runtimeState.GetSnapshot();
            if (snapshot.ActiveProfileId != notification.CurrentProfileId ||
                snapshot.Generation != notification.Generation)
            {
                return;
            }

            if (current is not null &&
                current.ProfileId == notification.CurrentProfileId &&
                current.Generation == notification.Generation)
            {
                return;
            }

            retired = current;
            current = CreateState(
                notification.CurrentProfileId,
                notification.Generation);
        }

        Cancel(retired);
    }

    private ProfileStreamState CreateState(Guid profileId, long generation)
    {
        return new ProfileStreamState(
            profileId,
            generation,
            new BoundedReplayEventStream<T>(
                options.ReplayCapacity,
                options.MaxBatchSize,
                options.HeartbeatInterval,
                latestSequence));
    }

    private void Cancel(ProfileStreamState? state)
    {
        if (state is null ||
            Interlocked.Exchange(ref state.Cancelled, 1) != 0)
        {
            return;
        }

        try
        {
            state.Lifetime.Cancel();
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Cancelling an API event stream for database profile {DatabaseProfileId} generation {DatabaseProfileGeneration} failed.",
                state.ProfileId,
                state.Generation);
        }
    }

    private sealed class ProfileStreamState(
        Guid profileId,
        long generation,
        BoundedReplayEventStream<T> stream)
    {
        public Guid ProfileId { get; } = profileId;

        public long Generation { get; } = generation;

        public BoundedReplayEventStream<T> Stream { get; } = stream;

        public CancellationTokenSource Lifetime { get; } = new();

        public int Cancelled;
    }
}

public readonly record struct ProfileReplayStreamLease<T>(
    Guid ProfileId,
    long Generation,
    IBoundedReplayEventReader<T> Reader,
    CancellationToken ProfileLifetime);
