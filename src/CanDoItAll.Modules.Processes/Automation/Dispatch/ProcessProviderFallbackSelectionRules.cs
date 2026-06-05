using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.ControlPlane;

namespace CanDoItAll.Modules.Processes;

internal static class ProcessProviderFallbackSelectionRules
{
    public static IReadOnlyList<ProviderProfile> OrderFallbackProviders(
        IEnumerable<ProviderProfile> providers,
        Guid failedProviderId)
    {
        return providers
            .Where(item =>
                item.IsEnabled &&
                item.SupportsTools &&
                item.Id != failedProviderId &&
                item.Kind is ProviderKind.OpenAi or ProviderKind.AzureOpenAi)
            .OrderBy(item => item.Kind is ProviderKind.OpenAi or ProviderKind.AzureOpenAi ? 0 : 1)
            .ThenBy(item => item.Transport == ProviderTransportKind.Responses ? 0 : 1)
            .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static string ResolveFallbackProviderModel(
        ProviderProfile provider,
        ProviderHealthResult healthResult)
    {
        var suggestedModels = healthResult.SuggestedModels
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (!string.IsNullOrWhiteSpace(provider.DefaultModel) &&
            (suggestedModels.Count == 0 || suggestedModels.Contains(provider.DefaultModel, StringComparer.OrdinalIgnoreCase)))
        {
            return provider.DefaultModel;
        }

        return suggestedModels.FirstOrDefault()
               ?? provider.DefaultModel;
    }

    public static string NormalizeFallbackEditorModel(
        ProcessRunAutomationDispatchService.ProviderFallbackResolution fallbackResolution)
    {
        if (!string.IsNullOrWhiteSpace(fallbackResolution.Provider.DefaultModel) &&
            string.Equals(
                fallbackResolution.Model,
                fallbackResolution.Provider.DefaultModel,
                StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }

        return fallbackResolution.Model;
    }
}
