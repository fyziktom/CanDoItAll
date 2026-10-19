using CanDoItAll.Infrastructure;

namespace CanDoItAll.Infrastructure.ControlPlane;

internal enum ControlPlaneCoordinationScope
{
    DatabaseProfiles,
    FileApplicationPreferences,
    DatasetActivation
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
        return durableFileWriter.AcquireCoordination(controlPlaneRoot, GetLockPath(controlPlaneRoot, scope),
            LockTimeout, requirePrivateUnixMode: true, cancellationToken);
    }

    internal static ValueTask<IAsyncDisposable> AcquireExistingAsync(DurableFileWriter files,
        string controlPlaneRoot, ControlPlaneCoordinationScope scope, CancellationToken cancellationToken)
        => files.AcquireExistingCoordinationAsync(controlPlaneRoot, GetLockPath(controlPlaneRoot, scope),
            TimeSpan.FromSeconds(5), requirePrivateUnixMode: true, cancellationToken);

    private static string GetLockPath(string controlPlaneRoot, ControlPlaneCoordinationScope scope)
        => scope switch {
            ControlPlaneCoordinationScope.DatabaseProfiles => Path.Combine(
                controlPlaneRoot,
                "database-profiles",
                ".generation.candoitall.lock"),
            ControlPlaneCoordinationScope.FileApplicationPreferences => Path.Combine(
                controlPlaneRoot,
                ".file-application-preferences.candoitall.lock"),
            ControlPlaneCoordinationScope.DatasetActivation => Path.Combine(
                controlPlaneRoot,
                "database-profiles",
                "dataset-activations",
                ".activation.candoitall.lock"),
            _ => throw new ArgumentOutOfRangeException(nameof(scope), scope, "Unsupported control-plane lock scope.")
        };
}
