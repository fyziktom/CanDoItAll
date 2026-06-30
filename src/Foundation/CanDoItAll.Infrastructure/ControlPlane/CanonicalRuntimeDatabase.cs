using CanDoItAll.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Infrastructure.ControlPlane;

public sealed class CanonicalRuntimeDatabase : ICanonicalRuntimeDatabase
{
    public CanonicalRuntimeDatabase(
        DatabaseProfileControlPlaneService controlPlane,
        IDatabaseRuntimeState runtimeState,
        ILogger<CanonicalRuntimeDatabase> logger)
    {
        Profile = ResolveCanonicalProfile(controlPlane);
        runtimeState.MarkCurrentProfile(Profile);
        logger.LogInformation(
            "Initialized canonical runtime database profile {ProfileId} ({DisplayName}). Provider={ProviderKind}. Fingerprint={Fingerprint}. Generation={Generation}.",
            Profile.Profile.Id,
            Profile.Profile.DisplayName,
            Profile.Profile.ProviderKind,
            Profile.Profile.Runtime.Fingerprint,
            Generation);
    }

    public ResolvedDatabaseProfile Profile { get; }

    public long Generation { get; } = 0;

    private static ResolvedDatabaseProfile ResolveCanonicalProfile(DatabaseProfileControlPlaneService controlPlane)
    {
        var profile = controlPlane.ResolveCurrentProfile();
        if (profile.Profile.ProviderKind == DatabaseProviderKind.InMemory &&
            !profile.Profile.Runtime.LockedByRuntimeOverride)
        {
            throw new InvalidOperationException("Persisted in-memory database profiles cannot be used as the canonical runtime database.");
        }

        return profile;
    }
}

public sealed class CanonicalDatabaseProfileRuntimeAccessor(
    ICanonicalRuntimeDatabase canonicalRuntimeDatabase,
    DatabaseProfileControlPlaneService controlPlane) : IDatabaseProfileRuntimeAccessor
{
    public ResolvedDatabaseProfile ResolveCurrentProfile() => canonicalRuntimeDatabase.Profile;

    public ResolvedDatabaseProfile ResolveProfile(Guid profileId) => controlPlane.ResolveProfile(profileId);
}
