using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Modules.AgentFramework.ProviderManagement;

using RuntimeProviderKind = CanDoItAll.AgentFramework.Models.ProviderKind;

internal static class ProviderModelCatalogPolicy {
    public static IReadOnlyList<string> Resolve(string connectorPluginKey,
        RuntimeProviderKind kind, ProviderProfilePurpose purpose,
        string defaultModel, IEnumerable<string> configuredModels) {
        return new[] { defaultModel }.Concat(configuredModels)
            .Where(model => !string.IsNullOrWhiteSpace(model))
            .Select(model => model.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
