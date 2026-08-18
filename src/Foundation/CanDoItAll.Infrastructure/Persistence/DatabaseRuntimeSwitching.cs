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

public interface IDatabaseRuntimeWriteFence
{
    Task<T> ExecuteAsync<T>(
        DatabaseRuntimeSnapshot expected,
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default);
}

public sealed class DatabaseRuntimeProfileChangedException : InvalidOperationException
{
    public DatabaseRuntimeProfileChangedException()
        : base("The active database profile changed before the durable write could commit.")
    {
    }
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

public sealed class DatabaseRuntimeState(IDatabaseSwitchNotificationService notificationService) :
    IDatabaseRuntimeState,
    IDatabaseRuntimeWriteFence
{
    private readonly object updateGate = new();
    private readonly SemaphoreSlim writeFence = new(1, 1);
    private DatabaseRuntimeSnapshot snapshot = new(
        ActiveProfileId: null,
        ActiveFingerprint: null,
        Generation: 0);

    public DatabaseRuntimeSnapshot GetSnapshot()
        => Volatile.Read(ref snapshot);

    public void MarkCurrentProfile(ResolvedDatabaseProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        writeFence.Wait();
        try
        {
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
        finally
        {
            writeFence.Release();
        }
    }

    public async Task<T> ExecuteAsync<T>(
        DatabaseRuntimeSnapshot expected,
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        await writeFence.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!Matches(Volatile.Read(ref snapshot), expected))
            {
                throw new DatabaseRuntimeProfileChangedException();
            }

            return await operation(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            writeFence.Release();
        }
    }

    public void PublishRestartObserved(
        DatabaseRuntimeSnapshot previousSnapshot,
        ResolvedDatabaseProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        writeFence.Wait();
        DatabaseProfileChangedNotification notification;
        try
        {
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
        }
        finally
        {
            writeFence.Release();
        }

        notificationService.Publish(notification);
    }

    private static bool Matches(DatabaseRuntimeSnapshot current, DatabaseRuntimeSnapshot expected)
        => current.ActiveProfileId == expected.ActiveProfileId &&
           string.Equals(current.ActiveFingerprint, expected.ActiveFingerprint, StringComparison.Ordinal) &&
           current.Generation == expected.Generation;
}
