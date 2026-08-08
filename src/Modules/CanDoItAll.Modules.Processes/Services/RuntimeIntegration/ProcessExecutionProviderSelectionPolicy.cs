using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Core.Execution;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Modules.Processes;

/// <summary>
/// Governed process-step provider override, moved verbatim from the deleted
/// <c>AgentFrameworkWorkspaceExecutionService.ShouldOverrideProviderForGovernedProcessStep</c> /
/// <c>OrderGovernedProcessProviderOverrideCandidates</c> pair (SB13). Generic Core no longer knows the
/// <c>process-step</c> source kind or the <see cref="ProcessStepOutcomeResult"/> CLR type by name; both live here.
/// </summary>
public sealed class ProcessExecutionProviderSelectionPolicy(IProviderProfileService providerProfileService)
    : IAgentExecutionProviderSelectionPolicy
{
    private readonly IProviderProfileService providerProfileService =
        providerProfileService ?? throw new ArgumentNullException(nameof(providerProfileService));

    public bool ShouldOverrideConfiguredProvider(AgentExecutionProviderSelectionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!request.IsGovernedProcessStep || request.StructuredOutputType != typeof(ProcessStepOutcomeResult))
        {
            return false;
        }

        var featureMatrix = providerProfileService.ResolveFeatureMatrix(request.ConfiguredProvider);
        return !featureMatrix.SupportsStructuredOutput;
    }

    public IReadOnlyList<ProviderProfile> SelectOverrideCandidates(
        AgentExecutionProviderSelectionRequest request,
        IReadOnlyList<ProviderProfile> availableProviders)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(availableProviders);

        var configuredProvider = request.ConfiguredProvider;
        return availableProviders
            .Where(provider => provider.IsEnabled)
            .Where(provider => provider.Purpose == ProviderProfilePurpose.Chat)
            .Where(provider => !IsScenarioHarnessProvider(provider))
            .Select(provider => new
            {
                Provider = provider,
                FeatureMatrix = providerProfileService.ResolveFeatureMatrix(provider)
            })
            .Where(item => item.FeatureMatrix.SupportsStructuredOutput)
            .OrderByDescending(item => SameProviderFamily(item.Provider, configuredProvider))
            .ThenByDescending(item => IsPreferredGovernedProcessProvider(item.Provider))
            .ThenByDescending(item => item.Provider.Kind is ProviderKind.OpenAi or ProviderKind.AzureOpenAi)
            .ThenByDescending(item => string.Equals(item.Provider.Name, ManagedSeedProviderFallbacks.OpenAiChatCompletionsProviderName, StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(item => string.Equals(item.Provider.Name, configuredProvider.Name, StringComparison.OrdinalIgnoreCase))
            .ThenBy(item => item.Provider.Name, StringComparer.OrdinalIgnoreCase)
            .Select(item => item.Provider)
            .ToArray();
    }

    private static bool IsPreferredGovernedProcessProvider(ProviderProfile provider)
        => provider.Kind is ProviderKind.OpenAi or ProviderKind.AzureOpenAi &&
           provider.Transport == ProviderTransportKind.Responses;

    private static bool SameProviderFamily(ProviderProfile candidate, ProviderProfile configuredProvider)
    {
        return candidate.Kind == configuredProvider.Kind &&
               string.Equals(
                   NormalizeProviderBaseUrl(candidate.BaseUrl),
                   NormalizeProviderBaseUrl(configuredProvider.BaseUrl),
                   StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeProviderBaseUrl(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().TrimEnd('/');
    }

    private static bool IsScenarioHarnessProvider(ProviderProfile provider)
    {
        return provider.Tags.Any(tag => tag.Contains("scenario", StringComparison.OrdinalIgnoreCase)) ||
               provider.Name.Contains("Scenario Harness", StringComparison.OrdinalIgnoreCase);
    }
}
