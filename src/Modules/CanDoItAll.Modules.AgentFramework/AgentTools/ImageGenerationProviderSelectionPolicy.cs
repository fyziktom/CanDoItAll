using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Modules.AgentFramework;

internal static class ImageGenerationProviderSelectionPolicy
{
    public static ProviderProfile? ResolveDefault(
        IReadOnlyList<ProviderProfile> providers,
        ProviderProfile? runtimeProvider)
    {
        var registryRuntimeProvider = runtimeProvider is null
            ? null
            : providers.FirstOrDefault(item => item.Id == runtimeProvider.Id);
        if (IsEnabledImageProvider(registryRuntimeProvider))
        {
            return registryRuntimeProvider;
        }

        if (IsEnabledImageProvider(runtimeProvider))
        {
            return runtimeProvider;
        }

        return providers
            .Where(IsEnabledImageProvider)
            .OrderBy(provider => provider.IsPrivateProvider)
            .ThenByDescending(IsKnownHealthy)
            .ThenBy(provider => provider.Name, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
    }

    private static bool IsEnabledImageProvider(ProviderProfile? provider)
        => provider is { IsEnabled: true, Purpose: ProviderProfilePurpose.ImageGeneration };

    private static bool IsKnownHealthy(ProviderProfile provider)
        => string.Equals(provider.HealthStatus, "Healthy", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(provider.HealthStatus, "OK", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(provider.HealthStatus, "Available", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(provider.HealthStatus, "OpenAI active", StringComparison.OrdinalIgnoreCase);
}
