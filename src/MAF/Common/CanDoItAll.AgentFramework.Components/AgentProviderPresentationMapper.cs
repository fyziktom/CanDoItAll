using CanDoItAll.AgentFramework.Models;
using System.Collections.Immutable;
using CanDoItAll.Conversations.Components.Presentation;

namespace CanDoItAll.AgentFramework.Components;

public static class AgentProviderPresentationMapper
{
    public static ConversationProviderOption Map(ProviderProfile provider)
    {
        ArgumentNullException.ThrowIfNull(provider);

        return new ConversationProviderOption(
            ToPresentationKey(provider.Id),
            provider.Name,
            provider.IsEnabled,
            provider.DefaultModel,
            !provider.IsSourceManaged && provider.Kind == ProviderKind.OpenAi && provider.Purpose == ProviderProfilePurpose.Chat
                ? provider.SuggestedModels.Where(OpenAiModelSuggestions.IsMainModel).ToArray()
                : provider.SuggestedModels) {
            ModelDisplayNames = provider.SuggestedModels.ToImmutableDictionary(model => model, model => model)
                .SetItems(provider.ModelCatalog.Select(model => KeyValuePair.Create(model.Id, model.DisplayName))),
            AllowsModelOverride = !provider.IsSourceManaged
        };
    }

    public static IReadOnlyList<ConversationProviderOption> Map(
        IEnumerable<ProviderProfile> providers)
    {
        ArgumentNullException.ThrowIfNull(providers);
        return providers.Select(Map).ToList();
    }

    public static ConversationPresentationKey? ToPresentationKey(Guid? providerId)
        => providerId.HasValue ? ToPresentationKey(providerId.Value) : null;

    public static Guid? ToProviderId(ConversationPresentationKey? key)
    {
        if (key is null)
        {
            return null;
        }

        if (!Guid.TryParseExact(key.Value, "D", out var providerId))
        {
            throw new InvalidOperationException(
                $"Provider presentation key '{key.Value}' is not a canonical provider identifier.");
        }

        return providerId;
    }

    private static ConversationPresentationKey ToPresentationKey(Guid providerId)
        => new(providerId.ToString("D"));
}
