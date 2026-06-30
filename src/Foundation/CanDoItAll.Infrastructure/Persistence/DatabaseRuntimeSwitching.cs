using CanDoItAll.Infrastructure.ControlPlane;

namespace CanDoItAll.Infrastructure.Persistence;

public sealed record DatabaseRuntimeSnapshot(
    Guid? ActiveProfileId,
    string? ActiveFingerprint,
    long Generation);

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
    private readonly object _sync = new();
    private Guid? _activeProfileId;
    private string? _activeFingerprint;
    private long _generation;

    public DatabaseRuntimeSnapshot GetSnapshot()
    {
        lock (_sync)
        {
            return new DatabaseRuntimeSnapshot(
                _activeProfileId,
                _activeFingerprint,
                _generation);
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

    public void PublishRestartObserved(
        DatabaseRuntimeSnapshot previousSnapshot,
        ResolvedDatabaseProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        DatabaseProfileChangedNotification notification;
        lock (_sync)
        {
            _activeProfileId = profile.Profile.Id;
            _activeFingerprint = profile.Profile.Runtime.Fingerprint;
            _generation++;

            notification = new DatabaseProfileChangedNotification(
                previousSnapshot.ActiveProfileId,
                previousSnapshot.ActiveFingerprint,
                profile.Profile.Id,
                profile.Profile.Runtime.Fingerprint,
                _generation);
        }

        notificationService.Publish(notification);
    }
}
