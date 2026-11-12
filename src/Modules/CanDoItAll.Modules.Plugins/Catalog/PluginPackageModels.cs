using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Modules.Plugins;

public interface IRuntimePluginServiceRegistrar
{
    void ConfigureServices(IServiceCollection services);
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


public sealed record PluginPackageAsset(
    string FilePath,
    string ContentType,
    DateTimeOffset LastModifiedUtc);
