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
            .Where(item => IsEligibleFallbackProvider(item, failedProviderId))
            .OrderBy(item => HasFallbackTag(item) ? 0 : 1)
            .ThenBy(item => item.Kind is ProviderKind.OpenAi or ProviderKind.AzureOpenAi ? 0 : 1)
            .ThenBy(item => item.Transport == ProviderTransportKind.Responses ? 0 : 1)
            .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool HasFallbackTag(ProviderProfile provider)
    {
        return provider.Tags.Any(tag => string.Equals(tag, "fallback", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsEligibleFallbackProvider(
        ProviderProfile provider,
        Guid failedProviderId)
    {
        return provider.IsEnabled &&
               provider.SupportsTools &&
               provider.Id != failedProviderId &&
               provider.Purpose == ProviderProfilePurpose.Chat &&
               provider.Kind is ProviderKind.OpenAi or ProviderKind.AzureOpenAi or ProviderKind.Ollama &&
               !IsScenarioHarnessProvider(provider);
    }

    private static bool IsScenarioHarnessProvider(ProviderProfile provider)
    {
        return provider.BaseUrl.StartsWith("scenario://", StringComparison.OrdinalIgnoreCase) ||
               provider.Name.Contains("Scenario Harness", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(provider.DefaultModel, "scenario-local", StringComparison.OrdinalIgnoreCase) ||
               provider.Tags.Any(tag => string.Equals(tag, "scenario-harness", StringComparison.OrdinalIgnoreCase));
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
        return !string.IsNullOrWhiteSpace(fallbackResolution.Model)
            ? fallbackResolution.Model
            : fallbackResolution.Provider.DefaultModel;
    }
}
