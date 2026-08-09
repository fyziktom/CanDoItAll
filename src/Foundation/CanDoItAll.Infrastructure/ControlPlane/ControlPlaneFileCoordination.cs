using CanDoItAll.Infrastructure;

namespace CanDoItAll.Infrastructure.ControlPlane;

internal enum ControlPlaneCoordinationScope
{
    DatabaseProfiles,
    FileApplicationPreferences
}

internal static class ControlPlaneFileCoordination
{
    private static readonly TimeSpan LockTimeout = TimeSpan.FromSeconds(15);

    public static IDisposable Acquire(
        DurableFileWriter durableFileWriter,
        string controlPlaneRoot,
        ControlPlaneCoordinationScope scope,
        CancellationToken cancellationToken = default)
    {
        string lockPath = scope switch
        {
            ControlPlaneCoordinationScope.DatabaseProfiles => Path.Combine(
                controlPlaneRoot,
                "database-profiles",
                ".generation.candoitall.lock"),
            ControlPlaneCoordinationScope.FileApplicationPreferences => Path.Combine(
                controlPlaneRoot,
                ".file-application-preferences.candoitall.lock"),
            _ => throw new ArgumentOutOfRangeException(nameof(scope), scope, "Unsupported control-plane lock scope.")
        };

        return durableFileWriter.AcquireCoordination(
            controlPlaneRoot,
            lockPath,
            LockTimeout,
            requirePrivateUnixMode: true,
            cancellationToken);
    }
}
