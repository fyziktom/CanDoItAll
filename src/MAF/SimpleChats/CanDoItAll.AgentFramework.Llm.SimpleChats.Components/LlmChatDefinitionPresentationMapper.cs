using CanDoItAll.Conversations.Components.Presentation;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Definitions;
using System.Collections.Immutable;

namespace CanDoItAll.AgentFramework.Llm.SimpleChats.Components;

public enum LlmChatDefinitionStatusFilter
{
    All,
    Draft,
    Active,
    Suspended,
    Archived
}

public sealed record LlmChatDefinitionStatusFilterOption(
    LlmChatDefinitionStatusFilter Value,
    string Label);

internal static class LlmChatDefinitionPresentationMapper
{
    public const string DefinitionKeyPrefix = "llm-chat-definition:";

    public static ConversationParticipantPresentation ToParticipant(
        LlmChatDefinitionListItem definition,
        Guid? selectedDefinitionId = null)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return new(
            ToKey(definition.DefinitionId),
            definition.Name,
            subtitle: $"Revision {definition.Revision}",
            summary: definition.Summary,
            avatarImageUrl: definition.AvatarImageUrl,
            avatarSeed: definition.DefinitionId.ToString("D"),
            avatarFallbackText: BuildFallback(definition.Name),
            searchText: string.Join(' ', definition.Name, definition.Summary, string.Join(' ', definition.Tags)),
            badges: [new(
                definition.Status.ToString(),
                ToTone(definition.Status),
                testId: $"llm-chat-definition-status-{definition.DefinitionId:D}")],
            tags: definition.Tags,
            metadata:
            [
                new(definition.UpdatedAtUtc.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture), "Updated")
            ],
            isSelected: definition.DefinitionId == selectedDefinitionId);
    }

    public static ConversationProviderOption ToProvider(LlmChatProviderOptionPresentation provider)
    {
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
        => new($"{DefinitionKeyPrefix}{definitionId:D}");

    public static ConversationPresentationKey ToProviderKey(Guid providerProfileId)
        => new(providerProfileId.ToString("D"));

    public static bool TryGetDefinitionId(ConversationPresentationKey key, out Guid definitionId)
    {
        ArgumentNullException.ThrowIfNull(key);
        definitionId = Guid.Empty;
        return key.Value.StartsWith(DefinitionKeyPrefix, StringComparison.Ordinal) &&
               Guid.TryParse(key.Value[DefinitionKeyPrefix.Length..], out definitionId) &&
               definitionId != Guid.Empty;
    }

    public static bool TryGetProviderId(ConversationPresentationKey? key, out Guid providerProfileId)
        => Guid.TryParse(key?.Value, out providerProfileId) && providerProfileId != Guid.Empty;

    public static LlmChatDefinitionStatus? ToStatus(LlmChatDefinitionStatusFilter filter)
        => filter switch
        {
            LlmChatDefinitionStatusFilter.All => null,
            LlmChatDefinitionStatusFilter.Draft => LlmChatDefinitionStatus.Draft,
            LlmChatDefinitionStatusFilter.Active => LlmChatDefinitionStatus.Active,
            LlmChatDefinitionStatusFilter.Suspended => LlmChatDefinitionStatus.Suspended,
            LlmChatDefinitionStatusFilter.Archived => LlmChatDefinitionStatus.Archived,
            _ => throw new ArgumentOutOfRangeException(nameof(filter), filter, "Unknown definition status filter.")
        };

    public static IReadOnlyList<LlmChatDefinitionStatus> GetAllowedTransitions(LlmChatDefinitionStatus status)
        => status switch
        {
            LlmChatDefinitionStatus.Draft => [LlmChatDefinitionStatus.Active, LlmChatDefinitionStatus.Archived],
            LlmChatDefinitionStatus.Active => [LlmChatDefinitionStatus.Suspended, LlmChatDefinitionStatus.Archived],
            LlmChatDefinitionStatus.Suspended => [LlmChatDefinitionStatus.Active, LlmChatDefinitionStatus.Archived],
            LlmChatDefinitionStatus.Archived => [],
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown definition status.")
        };

    public static string GetTransitionLabel(LlmChatDefinitionStatus status)
        => status switch
        {
            LlmChatDefinitionStatus.Active => "Activate",
            LlmChatDefinitionStatus.Suspended => "Suspend",
            LlmChatDefinitionStatus.Archived => "Archive",
            LlmChatDefinitionStatus.Draft => "Move to draft",
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown definition status.")
        };

    private static PresentationTone ToTone(LlmChatDefinitionStatus status)
        => status switch
        {
            LlmChatDefinitionStatus.Draft => PresentationTone.Default,
            LlmChatDefinitionStatus.Active => PresentationTone.Success,
            LlmChatDefinitionStatus.Suspended => PresentationTone.Warning,
            LlmChatDefinitionStatus.Archived => PresentationTone.Danger,
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown definition status.")
        };

    private static string BuildFallback(string name)
    {
        var words = name.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return string.Concat(words.Take(2).Select(word => char.ToUpperInvariant(word[0])));
    }
}
