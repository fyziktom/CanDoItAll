using CanDoItAll.AgentFramework.Llm.SimpleChats.UI;
using CanDoItAll.Conversations.Components.Presentation;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Definitions;
using System.Collections.Immutable;

namespace CanDoItAll.AgentFramework.Llm.SimpleChats.Components;

internal static class LlmChatDefinitionPresentationMapper {
    public static DefinitionCatalogCard ToCatalogCard(LlmChatDefinitionListItem definition) => new(
        definition.DefinitionId, definition.Name, definition.Summary, definition.AvatarImageUrl,
        definition.Status switch {
            LlmChatDefinitionStatus.Draft => LlmChatDefinitionStatusFilter.Draft,
            LlmChatDefinitionStatus.Active => LlmChatDefinitionStatusFilter.Active,
            LlmChatDefinitionStatus.Suspended => LlmChatDefinitionStatusFilter.Suspended,
            LlmChatDefinitionStatus.Archived => LlmChatDefinitionStatusFilter.Archived,
            _ => throw new ArgumentOutOfRangeException(nameof(definition), "Unknown definition status.")
        }, definition.Revision, definition.UpdatedAtUtc, definition.Tags.ToImmutableArray());

    public static ConversationParticipantPresentation ToParticipant(LlmChatDefinitionListItem definition, Guid? selectedDefinitionId = null)
        => ToCatalogCard(definition).ToParticipant(selectedDefinitionId);

    public static ConversationProviderOption ToProvider(LlmChatProviderOptionPresentation provider) {
        ArgumentNullException.ThrowIfNull(provider);
        var models = provider.Models
            .Select(option => option.Model)
            .Where(model => !string.IsNullOrWhiteSpace(model))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return new(
            ToProviderKey(provider.ProviderProfileId),
            provider.ProviderName,
            true,
            models.FirstOrDefault() ?? string.Empty,
            provider.Models.Where(model => model.IsSuggested).Select(model => model.Model).ToArray()) {
            ModelDisplayNames = provider.Models.ToImmutableDictionary(model => model.Model, model => model.DisplayName),
            AllowsModelOverride = !provider.IsSourceManaged
        };
    }

    public static ConversationPresentationKey ToKey(Guid definitionId)
        => DefinitionCatalogMapping.ToKey(definitionId);

    public static ConversationPresentationKey ToProviderKey(Guid providerProfileId)
        => new(providerProfileId.ToString("D"));

    public static bool TryGetDefinitionId(ConversationPresentationKey key, out Guid definitionId)
        => DefinitionCatalogMapping.TryGetDefinitionId(key, out definitionId);

    public static bool TryGetProviderId(ConversationPresentationKey? key, out Guid providerProfileId)
        => Guid.TryParse(key?.Value, out providerProfileId) && providerProfileId != Guid.Empty;

    public static LlmChatDefinitionStatus? ToStatus(LlmChatDefinitionStatusFilter filter)
        => filter switch {
            LlmChatDefinitionStatusFilter.All => null,
            LlmChatDefinitionStatusFilter.Draft => LlmChatDefinitionStatus.Draft,
            LlmChatDefinitionStatusFilter.Active => LlmChatDefinitionStatus.Active,
            LlmChatDefinitionStatusFilter.Suspended => LlmChatDefinitionStatus.Suspended,
            LlmChatDefinitionStatusFilter.Archived => LlmChatDefinitionStatus.Archived,
            _ => throw new ArgumentOutOfRangeException(nameof(filter), filter, "Unknown definition status filter.")
        };

    public static IReadOnlyList<LlmChatDefinitionStatus> GetAllowedTransitions(LlmChatDefinitionStatus status)
        => status switch {
            LlmChatDefinitionStatus.Draft => [LlmChatDefinitionStatus.Active, LlmChatDefinitionStatus.Archived],
            LlmChatDefinitionStatus.Active => [LlmChatDefinitionStatus.Suspended, LlmChatDefinitionStatus.Archived],
            LlmChatDefinitionStatus.Suspended => [LlmChatDefinitionStatus.Active, LlmChatDefinitionStatus.Archived],
            LlmChatDefinitionStatus.Archived => [],
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown definition status.")
        };

    public static string GetTransitionLabel(LlmChatDefinitionStatus status)
        => status switch {
            LlmChatDefinitionStatus.Active => "Activate",
            LlmChatDefinitionStatus.Suspended => "Suspend",
            LlmChatDefinitionStatus.Archived => "Archive",
            LlmChatDefinitionStatus.Draft => "Move to draft",
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown definition status.")
        };

}
