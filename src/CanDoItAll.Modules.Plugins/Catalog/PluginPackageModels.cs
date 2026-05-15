using CanDoItAll.Plugins.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Modules.Plugins;

public interface IRuntimePluginServiceRegistrar
{
    void ConfigureServices(IServiceCollection services);
}

public enum PluginPackageCatalogSourceKind
{
    Catalogue,
    Installed
}

public enum PluginPackageInstallSourceKind
{
    Catalogue,
    Upload
}

public sealed class PluginPackageOptions
{
    public const string SectionName = "PluginPackages";

    public string RootPath { get; init; } = string.Empty;

    public string CatalogRootPath { get; init; } = string.Empty;

    public string InstalledRootPath { get; init; } = string.Empty;

    public string RuntimeStateRootPath { get; init; } = string.Empty;

    public long MaxPackageBytes { get; init; } = 100L * 1024L * 1024L;

    public static PluginPackageOptions FromConfiguration(
        IConfiguration? configuration,
        string? contentRootPath)
    {
        var section = configuration?.GetSection(SectionName);
        var basePath = string.IsNullOrWhiteSpace(contentRootPath)
            ? AppContext.BaseDirectory
            : contentRootPath;
        var rootPath = ResolvePath(
            basePath,
            section?["RootPath"],
            Path.Combine(basePath, "App_Data", "plugins"));

        return new PluginPackageOptions
        {
            RootPath = rootPath,
            CatalogRootPath = ResolvePath(
                rootPath,
                section?["CatalogRootPath"],
                Path.Combine(rootPath, "catalogue")),
            InstalledRootPath = ResolvePath(
                rootPath,
                section?["InstalledRootPath"],
                Path.Combine(rootPath, "installed")),
            RuntimeStateRootPath = ResolvePath(
                rootPath,
                section?["RuntimeStateRootPath"],
                Path.Combine(rootPath, "state")),
            MaxPackageBytes = ResolveMaxPackageBytes(section?["MaxPackageBytes"])
        };
    }

    private static string ResolvePath(
        string basePath,
        string? configuredPath,
        string defaultPath)
    {
        var path = string.IsNullOrWhiteSpace(configuredPath)
            ? defaultPath
            : configuredPath.Trim();

        return Path.GetFullPath(Path.IsPathRooted(path) ? path : Path.Combine(basePath, path));
    }

    private static long ResolveMaxPackageBytes(string? configuredValue)
    {
        if (long.TryParse(configuredValue, out var value) && value > 0)
        {
            return value;
        }

        return 100L * 1024L * 1024L;
    }
}

public sealed class PluginPackageManifest
{
    public PluginDescriptor Plugin { get; init; } = default!;

    public string EntryAssembly { get; init; } = string.Empty;

    public IReadOnlyList<string> Assemblies { get; init; } = [];

    public string IconPath { get; init; } = string.Empty;

    public bool RequiresRestart { get; init; } = true;
}

public sealed record PluginPackageCatalogItem(
    PluginPackageId PackageId,
    PluginId PluginId,
    string DisplayName,
    string Description,
    string Version,
    string Vendor,
    PluginPackageCatalogSourceKind CatalogSourceKind,
    PluginSourceKind PluginSourceKind,
    PluginTrustLevel TrustLevel,
    PluginCapabilityKind Capabilities,
    bool IsInstalled,
    bool RequiresRestart,
    bool HasRuntimeAssemblies,
    string IconPath,
    string SourceName);

public sealed record PluginPackageInstallRequest(
    bool Enable = true,
    string Actor = "system");

public sealed record PluginPackageInstallResult(
    PluginPackageId PackageId,
    PluginId PluginId,
    string DisplayName,
    string Version,
    PluginPackageInstallSourceKind SourceKind,
    bool RestartRequired,
    PluginRuntimeRestartStatus RestartStatus);

public sealed record PluginPackageAsset(
    string FilePath,
    string ContentType,
    DateTimeOffset LastModifiedUtc);

public sealed record PluginRuntimeRestartStatus(
    bool IsRestartRequired,
    bool IsRestartRequested,
    string Reason,
    DateTimeOffset? RequiredAtUtc,
    string RequestedBy,
    DateTimeOffset? RequestedAtUtc,
    int ProcessId);

public sealed record PluginRuntimeRestartRequest(
    string Actor = "system");
