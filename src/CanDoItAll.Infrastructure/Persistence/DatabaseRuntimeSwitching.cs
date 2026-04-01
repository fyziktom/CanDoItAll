using CanDoItAll.Infrastructure.ControlPlane;

namespace CanDoItAll.Infrastructure.Persistence;

public sealed record DatabaseRuntimeSnapshot(
    Guid? ActiveProfileId,
    string? ActiveFingerprint,
    long Generation,
    bool IsSwitchInProgress,
    int ActiveContextCount);

public sealed record DatabaseProfileChangedNotification(
    Guid? PreviousProfileId,
    string? PreviousFingerprint,
    Guid CurrentProfileId,
    string CurrentFingerprint,
    long Generation);

public interface IDatabaseSwitchNotificationService
{
    event EventHandler<DatabaseProfileChangedNotification>? Changed;

    void Publish(DatabaseProfileChangedNotification notification);
}

public interface IDatabaseRuntimeState
{
    DatabaseRuntimeSnapshot GetSnapshot();

    Task<DatabaseContextLease> AcquireContextLeaseAsync(CancellationToken cancellationToken = default);

    Task<DatabaseSwitchSession> BeginSwitchAsync(CancellationToken cancellationToken = default);

    void MarkCurrentProfile(ResolvedDatabaseProfile profile);
}

public sealed class DatabaseSwitchNotificationService : IDatabaseSwitchNotificationService
{
    public event EventHandler<DatabaseProfileChangedNotification>? Changed;

    public void Publish(DatabaseProfileChangedNotification notification)
    {
        Changed?.Invoke(this, notification);
    }
}

public sealed class DatabaseRuntimeState(IDatabaseSwitchNotificationService notificationService) : IDatabaseRuntimeState
{
    private readonly SemaphoreSlim _switchLock = new(1, 1);
    private readonly object _sync = new();
    private TaskCompletionSource<bool> _contextsAllowed =
        CreateCompletedSignal();
    private TaskCompletionSource<bool>? _drainSignal;
    private Guid? _activeProfileId;
    private string? _activeFingerprint;
    private long _generation;
    private int _activeContextCount;
    private bool _switchInProgress;

    public DatabaseRuntimeSnapshot GetSnapshot()
    {
        lock (_sync)
        {
            return new DatabaseRuntimeSnapshot(
                _activeProfileId,
                _activeFingerprint,
                _generation,
                _switchInProgress,
                _activeContextCount);
        }
    }

    public async Task<DatabaseContextLease> AcquireContextLeaseAsync(CancellationToken cancellationToken = default)
    {
        while (true)
        {
            Task allowedTask;
            lock (_sync)
            {
                allowedTask = _contextsAllowed.Task;
            }

            await allowedTask.WaitAsync(cancellationToken);

            lock (_sync)
            {
                if (_switchInProgress)
                {
                    continue;
                }

                _activeContextCount++;
                return new DatabaseContextLease(this);
            }
        }
    }

    public async Task<DatabaseSwitchSession> BeginSwitchAsync(CancellationToken cancellationToken = default)
    {
        await _switchLock.WaitAsync(cancellationToken);

        lock (_sync)
        {
            _switchInProgress = true;
            _contextsAllowed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            _drainSignal = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            if (_activeContextCount == 0)
            {
                _drainSignal.TrySetResult(true);
            }

            return new DatabaseSwitchSession(this, GetSnapshotUnsafe());
        }
    }

    public void MarkCurrentProfile(ResolvedDatabaseProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        lock (_sync)
        {
            _activeProfileId = profile.Profile.Id;
            _activeFingerprint = profile.Profile.Runtime.Fingerprint;
        }
    }

    internal Task WaitForDrainAsync() =>
        (_drainSignal ?? CreateCompletedSignal()).Task;

    internal DatabaseProfileChangedNotification CompleteSwitch(
        DatabaseRuntimeSnapshot previousSnapshot,
        ResolvedDatabaseProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        DatabaseProfileChangedNotification notification;
        lock (_sync)
        {
            _switchInProgress = false;
            _activeProfileId = profile.Profile.Id;
            _activeFingerprint = profile.Profile.Runtime.Fingerprint;
            _generation++;
            _drainSignal = null;
            _contextsAllowed.TrySetResult(true);

            notification = new DatabaseProfileChangedNotification(
                previousSnapshot.ActiveProfileId,
                previousSnapshot.ActiveFingerprint,
                profile.Profile.Id,
                profile.Profile.Runtime.Fingerprint,
                _generation);
        }

        _switchLock.Release();
        notificationService.Publish(notification);
        return notification;
    }

    internal void AbortSwitch()
    {
        lock (_sync)
        {
            _switchInProgress = false;
            _drainSignal = null;
            _contextsAllowed.TrySetResult(true);
        }

        _switchLock.Release();
    }

    internal void ReleaseContext()
    {
        lock (_sync)
        {
            if (_activeContextCount > 0)
            {
                _activeContextCount--;
            }

            if (_switchInProgress && _activeContextCount == 0)
            {
                _drainSignal?.TrySetResult(true);
            }
        }
    }

    private DatabaseRuntimeSnapshot GetSnapshotUnsafe()
    {
        return new DatabaseRuntimeSnapshot(
            _activeProfileId,
            _activeFingerprint,
            _generation,
            _switchInProgress,
            _activeContextCount);
    }

    private static TaskCompletionSource<bool> CreateCompletedSignal()
    {
        var signal = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        signal.TrySetResult(true);
        return signal;
    }
}

public sealed class DatabaseContextLease(DatabaseRuntimeState owner) : IDisposable
{
    private DatabaseRuntimeState? _owner = owner;

    public void Dispose()
    {
        Interlocked.Exchange(ref _owner, null)?.ReleaseContext();
    }
}

public sealed class DatabaseSwitchSession(
    DatabaseRuntimeState owner,
    DatabaseRuntimeSnapshot previousSnapshot) : IAsyncDisposable
{
    private DatabaseRuntimeState? _owner = owner;
    private bool _completed;

    public DatabaseRuntimeSnapshot PreviousSnapshot { get; } = previousSnapshot;

    public async Task WaitForDrainAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        var owner = _owner ?? throw new InvalidOperationException("The switch session has already been released.");
        await owner.WaitForDrainAsync().WaitAsync(timeout, cancellationToken);
    }

    public DatabaseProfileChangedNotification Complete(ResolvedDatabaseProfile profile)
    {
        var owner = _owner ?? throw new InvalidOperationException("The switch session has already been released.");
        _completed = true;
        _owner = null;
        return owner.CompleteSwitch(PreviousSnapshot, profile);
    }

    public ValueTask DisposeAsync()
    {
        if (!_completed)
        {
            Interlocked.Exchange(ref _owner, null)?.AbortSwitch();
            _completed = true;
        }

        return ValueTask.CompletedTask;
    }
}
