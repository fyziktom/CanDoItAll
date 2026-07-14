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

    string ResolveFileApplicationPreferencesFilePath();

    string ResolveDataProtectionKeysPath();
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
        var expandedPath = Environment.ExpandEnvironmentVariables(configuredPath);
        if (Path.IsPathRooted(expandedPath))
        {
            return Path.GetFullPath(expandedPath);
        }

        return Path.GetFullPath(Path.Combine(contentRootPath, expandedPath));
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

    public string ResolveFileApplicationPreferencesFilePath()
        => Path.Combine(ResolveRootPath(), "file-application-preferences.json");

    public string ResolveDataProtectionKeysPath()
    {
        var path = Path.Combine(ResolveRootPath(), "dataprotection-keys");
        Directory.CreateDirectory(path);
        return path;
    }

}
