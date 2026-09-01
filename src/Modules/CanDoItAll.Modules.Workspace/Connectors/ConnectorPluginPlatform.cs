using CanDoItAll.SharedKernel.Configuration;

namespace CanDoItAll.Modules.Workspace;

public sealed class ConnectorPluginRegistry(IEnumerable<IConnectorManifestSource> sources)
{
    private readonly IReadOnlyDictionary<string, ConnectorPluginManifest> manifestsByKey = sources
        .SelectMany(source => source.ListManifests())
        .GroupBy(manifest => manifest.PluginKey, StringComparer.OrdinalIgnoreCase)
        .ToDictionary(group => group.Key, group => group.Last(), StringComparer.OrdinalIgnoreCase);

    public ConnectorPluginManifest Resolve(string pluginKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginKey);

        if (manifestsByKey.TryGetValue(pluginKey.Trim(), out var manifest))
        {
            return manifest;
        }

        throw new InvalidOperationException($"Connector plugin '{pluginKey}' is not registered.");
    }

    public bool TryResolve(string? pluginKey, out ConnectorPluginManifest manifest)
    {
        manifest = default!;

        if (string.IsNullOrWhiteSpace(pluginKey))
        {
            return false;
        }

        return manifestsByKey.TryGetValue(pluginKey.Trim(), out manifest!);
    }

    public IReadOnlyList<ConnectorPluginManifest> List(ConnectorManifestCapability? requiredCapability = null)
    {
        return manifestsByKey.Values
            .Where(manifest => !requiredCapability.HasValue || manifest.Capabilities.HasFlag(requiredCapability.Value))
            .OrderBy(manifest => manifest.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
