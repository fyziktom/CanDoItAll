using System.IO;
using CanDoItAll.Infrastructure.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace CanDoItAll.Infrastructure.ControlPlane;

public interface IControlPlanePathResolver
{
    string ResolveRootPath();

    string ResolveDatabaseProfilesRootPath();

    string ResolveCatalogFilePath();

    string ResolveActiveProfileStateFilePath();

    string ResolveDataProtectionKeysPath();

    string ResolveManagedSqliteRootPath();

    string ResolveManagedSqliteProfileRootPath(Guid profileId);

    string ResolveManagedSqliteDatabasePath(Guid profileId);

    string ResolveManagedSqliteWorkspaceRootPath(Guid profileId);

    string ResolveSnapshotsRootPath();

    string ResolveSnapshotPackagePath(Guid snapshotId);

    string ResolveSnapshotCacheRootPath();

    string ResolveSnapshotCacheProfileRootPath(Guid profileId);

    string ResolveSnapshotCacheDatabasePath(Guid profileId);

    string ResolveSnapshotCacheWorkspaceRootPath(Guid profileId);
}

public static class ControlPlanePathDefaults
{
    public static string ResolveControlPlaneRootPath(IHostEnvironment environment, ControlPlaneOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.RootPath))
        {
            return ResolveConfiguredPath(environment.ContentRootPath, options.RootPath);
        }

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(localAppData))
        {
            return Path.Combine(environment.ContentRootPath, ".artifacts", "control-plane");
        }

        return Path.Combine(localAppData, "CanDoItAll", "control-plane");
    }

    public static string ResolveConfiguredPath(string contentRootPath, string configuredPath)
    {
        if (Path.IsPathRooted(configuredPath))
        {
            return Path.GetFullPath(configuredPath);
        }

        return Path.GetFullPath(Path.Combine(contentRootPath, configuredPath));
    }
}

public sealed class ControlPlanePathResolver(
    IOptions<ControlPlaneOptions> options,
    IHostEnvironment hostEnvironment) : IControlPlanePathResolver
{
    private readonly ControlPlaneOptions _options = options.Value;

    public string ResolveRootPath()
    {
        var path = ControlPlanePathDefaults.ResolveControlPlaneRootPath(hostEnvironment, _options);
        Directory.CreateDirectory(path);
        return path;
    }

    public string ResolveDatabaseProfilesRootPath()
    {
        var path = Path.Combine(ResolveRootPath(), "database-profiles");
        Directory.CreateDirectory(path);
        return path;
    }

    public string ResolveCatalogFilePath() => Path.Combine(ResolveDatabaseProfilesRootPath(), "catalog.json");

    public string ResolveActiveProfileStateFilePath() => Path.Combine(ResolveDatabaseProfilesRootPath(), "active-profile.json");

    public string ResolveDataProtectionKeysPath()
    {
        var path = Path.Combine(ResolveRootPath(), "dataprotection-keys");
        Directory.CreateDirectory(path);
        return path;
    }

    public string ResolveManagedSqliteRootPath()
    {
        var path = Path.Combine(ResolveDatabaseProfilesRootPath(), "managed-sqlite");
        Directory.CreateDirectory(path);
        return path;
    }

    public string ResolveManagedSqliteProfileRootPath(Guid profileId)
    {
        var path = Path.Combine(ResolveManagedSqliteRootPath(), profileId.ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    public string ResolveManagedSqliteDatabasePath(Guid profileId)
    {
        var databaseRoot = Path.Combine(ResolveManagedSqliteProfileRootPath(profileId), "db");
        Directory.CreateDirectory(databaseRoot);
        return Path.Combine(databaseRoot, "candoitall.db");
    }

    public string ResolveManagedSqliteWorkspaceRootPath(Guid profileId)
    {
        var path = Path.Combine(ResolveManagedSqliteProfileRootPath(profileId), "workspace");
        Directory.CreateDirectory(path);
        return path;
    }

    public string ResolveSnapshotsRootPath()
    {
        var path = Path.Combine(ResolveRootPath(), "snapshots");
        Directory.CreateDirectory(path);
        return path;
    }

    public string ResolveSnapshotPackagePath(Guid snapshotId)
    {
        return Path.Combine(ResolveSnapshotsRootPath(), $"{snapshotId:N}.cda-snapshot.zip");
    }

    public string ResolveSnapshotCacheRootPath()
    {
        var path = Path.Combine(ResolveRootPath(), "snapshot-cache");
        Directory.CreateDirectory(path);
        return path;
    }

    public string ResolveSnapshotCacheProfileRootPath(Guid profileId)
    {
        var path = Path.Combine(ResolveSnapshotCacheRootPath(), profileId.ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    public string ResolveSnapshotCacheDatabasePath(Guid profileId)
    {
        var databaseRoot = Path.Combine(ResolveSnapshotCacheProfileRootPath(profileId), "db");
        Directory.CreateDirectory(databaseRoot);
        return Path.Combine(databaseRoot, "candoitall.db");
    }

    public string ResolveSnapshotCacheWorkspaceRootPath(Guid profileId)
    {
        var path = Path.Combine(ResolveSnapshotCacheProfileRootPath(profileId), "workspace");
        Directory.CreateDirectory(path);
        return path;
    }
}
