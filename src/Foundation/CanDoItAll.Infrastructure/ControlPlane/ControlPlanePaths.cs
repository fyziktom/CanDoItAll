using System.IO;
using CanDoItAll.Infrastructure;
using CanDoItAll.Infrastructure.Configuration;
using CanDoItAll.SharedKernel;
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

    string ResolveStateRootPath();

    string ResolveLogsRootPath();

    string ResolveRuntimeTemporaryRootPath();
}

public static class ControlPlanePathDefaults
{
    public static string ResolveControlPlaneRootPath(IHostEnvironment environment, ControlPlaneOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.RootPath))
        {
            return ResolveConfiguredPath(environment.ContentRootPath, options.RootPath);
        }

        return ApplicationPurposeRootPolicy.ResolveCurrent().ControlPlaneRoot;
    }

    public static string ResolveDataProtectionKeysPath(IHostEnvironment environment, ControlPlaneOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.DataProtectionKeysPath))
        {
            return ResolveConfiguredPath(environment.ContentRootPath, options.DataProtectionKeysPath);
        }

        if (!string.IsNullOrWhiteSpace(options.RootPath))
        {
            return Path.Combine(ResolveControlPlaneRootPath(environment, options), "dataprotection-keys");
        }

        return ApplicationPurposeRootPolicy.ResolveCurrent().DataProtectionKeysRoot;
    }

    public static string ResolveConfiguredPath(string contentRootPath, string configuredPath)
    {
        var expandedPath = ExpandConfiguredPath(
            configuredPath,
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            Environment.GetEnvironmentVariable,
            PortablePathTemplateCompatibility.LegacyWindowsEnvironmentTokens);
        PhysicalPathSyntaxPolicy.EnsureNativeOrRelative(expandedPath, "configured control-plane path");
        if (Path.IsPathRooted(expandedPath))
        {
            return Path.GetFullPath(expandedPath);
        }

        return Path.GetFullPath(Path.Combine(contentRootPath, expandedPath));
    }

    internal static string ExpandConfiguredPath(
        string configuredPath,
        string? homeDirectory,
        Func<string, string?> variableResolver,
        PortablePathTemplateCompatibility compatibility)
    {
        return PortablePathTemplate.Expand(
            configuredPath,
            homeDirectory,
            variableResolver,
            compatibility);
    }
}

public sealed class ControlPlanePathResolver(
    IOptions<ControlPlaneOptions> options,
    IHostEnvironment hostEnvironment,
    DurableFileWriter durableFileWriter) :
    IControlPlanePathResolver,
    IApplicationPurposeRootConfigurationSource
{
    private readonly ControlPlaneOptions _options = options.Value;

    public string ResolveRootPath()
    {
        var path = ControlPlanePathDefaults.ResolveControlPlaneRootPath(hostEnvironment, _options);
        durableFileWriter.EnsureDirectory(path, path, requirePrivateUnixMode: true);
        return path;
    }

    public string ResolveDatabaseProfilesRootPath()
    {
        var path = Path.Combine(ResolveRootPath(), "database-profiles");
        durableFileWriter.EnsureDirectory(ResolveRootPath(), path, requirePrivateUnixMode: true);
        return path;
    }

    public string ResolveCatalogFilePath() => Path.Combine(ResolveDatabaseProfilesRootPath(), "catalog.json");

    public string ResolveActiveProfileStateFilePath() => Path.Combine(ResolveDatabaseProfilesRootPath(), "active-profile.json");

    public string ResolveFileApplicationPreferencesFilePath()
        => Path.Combine(ResolveRootPath(), "file-application-preferences.json");

    public string ResolveDataProtectionKeysPath()
    {
        string path = ControlPlanePathDefaults.ResolveDataProtectionKeysPath(hostEnvironment, _options);
        durableFileWriter.EnsureDirectory(path, path, requirePrivateUnixMode: true);
        return path;
    }

    public string ResolveStateRootPath()
        => EnsurePurposeRoot(_options.StateRootPath, static roots => roots.StateRoot);

    public string ResolveLogsRootPath()
        => EnsurePurposeRoot(_options.LogsRootPath, static roots => roots.LogsRoot);

    public string ResolveRuntimeTemporaryRootPath()
        => EnsurePurposeRoot(_options.RuntimeTemporaryRootPath, static roots => roots.RuntimeTemporaryRoot);

    public ApplicationPurposeRootConfigurationSource GetConfigurationSource(
        ApplicationPurposeRootKind purpose)
        => purpose switch
        {
            ApplicationPurposeRootKind.ControlPlane => IsConfigured(_options.RootPath)
                ? ApplicationPurposeRootConfigurationSource.ExplicitConfiguration
                : ApplicationPurposeRootConfigurationSource.PlatformDefault,
            ApplicationPurposeRootKind.DatabaseProfiles =>
                ApplicationPurposeRootConfigurationSource.DerivedFromControlPlaneRoot,
            ApplicationPurposeRootKind.DataProtectionKeys => IsConfigured(_options.DataProtectionKeysPath)
                ? ApplicationPurposeRootConfigurationSource.ExplicitConfiguration
                : IsConfigured(_options.RootPath)
                    ? ApplicationPurposeRootConfigurationSource.DerivedFromControlPlaneRoot
                    : ApplicationPurposeRootConfigurationSource.PlatformDefault,
            ApplicationPurposeRootKind.State => ResolveConfiguredSource(_options.StateRootPath),
            ApplicationPurposeRootKind.Logs => ResolveConfiguredSource(_options.LogsRootPath),
            ApplicationPurposeRootKind.RuntimeTemporary => ResolveConfiguredSource(_options.RuntimeTemporaryRootPath),
            _ => throw new ArgumentOutOfRangeException(nameof(purpose), purpose, "The purpose root is not owned by the control-plane resolver.")
        };

    private string EnsurePurposeRoot(
        string? configuredPath,
        Func<ApplicationPurposeRoots, string> defaultSelector)
    {
        string path = ResolvePurposeRoot(configuredPath, defaultSelector);
        durableFileWriter.EnsureDirectory(path, path, requirePrivateUnixMode: true);
        return path;
    }

    private string ResolvePurposeRoot(
        string? configuredPath,
        Func<ApplicationPurposeRoots, string> defaultSelector)
    {
        return string.IsNullOrWhiteSpace(configuredPath)
            ? defaultSelector(ApplicationPurposeRootPolicy.ResolveCurrent())
            : ControlPlanePathDefaults.ResolveConfiguredPath(hostEnvironment.ContentRootPath, configuredPath);
    }

    private static ApplicationPurposeRootConfigurationSource ResolveConfiguredSource(string? configuredPath)
        => IsConfigured(configuredPath)
            ? ApplicationPurposeRootConfigurationSource.ExplicitConfiguration
            : ApplicationPurposeRootConfigurationSource.PlatformDefault;

    private static bool IsConfigured(string? path) => !string.IsNullOrWhiteSpace(path);

}
