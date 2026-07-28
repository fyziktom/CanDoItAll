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
        ArgumentNullException.ThrowIfNull(notification);

        var subscribers = Changed;
        if (subscribers is null)
        {
            return;
        }

        List<Exception>? failures = null;
        foreach (EventHandler<DatabaseProfileChangedNotification> subscriber in subscribers.GetInvocationList())
        {
            try
            {
                subscriber(this, notification);
            }
            catch (Exception exception)
            {
                failures ??= [];
                failures.Add(exception);
            }
        }

        if (failures is not null)
        {
            throw new AggregateException(
                "One or more database profile change subscribers failed.",
                failures);
        }
    }
}

public sealed class DatabaseRuntimeState(IDatabaseSwitchNotificationService notificationService) : IDatabaseRuntimeState
{
    private readonly object updateGate = new();
    private DatabaseRuntimeSnapshot snapshot = new(
        ActiveProfileId: null,
        ActiveFingerprint: null,
        Generation: 0);

    public DatabaseRuntimeSnapshot GetSnapshot()
        => Volatile.Read(ref snapshot);

    public void MarkCurrentProfile(ResolvedDatabaseProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        lock (updateGate)
        {
            var current = Volatile.Read(ref snapshot);
            if (current.ActiveProfileId.HasValue)
            {
                if (current.ActiveProfileId == profile.Profile.Id &&
                    string.Equals(
                        current.ActiveFingerprint,
                        profile.Profile.Runtime.Fingerprint,
                        StringComparison.Ordinal))
                {
                    return;
                }

                throw new InvalidOperationException(
                    "The current database runtime identity is already initialized. Runtime changes must use the generation-bumping restart publication path.");
            }

            Volatile.Write(
                ref snapshot,
                new DatabaseRuntimeSnapshot(
                    profile.Profile.Id,
                    profile.Profile.Runtime.Fingerprint,
                    current.Generation));
        }
    }

    public void PublishRestartObserved(
        DatabaseRuntimeSnapshot previousSnapshot,
        ResolvedDatabaseProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        DatabaseProfileChangedNotification notification;
        lock (updateGate)
        {
            var current = Volatile.Read(ref snapshot);
            var next = new DatabaseRuntimeSnapshot(
                profile.Profile.Id,
                profile.Profile.Runtime.Fingerprint,
                checked(current.Generation + 1));
            Volatile.Write(ref snapshot, next);

            notification = new DatabaseProfileChangedNotification(
                previousSnapshot.ActiveProfileId,
                previousSnapshot.ActiveFingerprint,
                next.ActiveProfileId!.Value,
                next.ActiveFingerprint!,
                next.Generation);
        }

        notificationService.Publish(notification);
    }
}
