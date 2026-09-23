using System.Collections.Immutable;
using System.Globalization;
using CanDoItAll.Conversations.Components.Presentation;

namespace CanDoItAll.AgentFramework.Llm.SimpleChats.UI;

public enum LlmChatDefinitionStatusFilter { All, Draft, Active, Suspended, Archived }
public enum DefinitionCatalogPhase { Authorizing, Loading, Refreshing, Ready, Failed }
public enum DefinitionCatalogFailure { Unavailable, InvalidFilters, AuthorizationUnavailable }
public sealed record LlmChatDefinitionStatusFilterOption(LlmChatDefinitionStatusFilter Value, string Label);
public sealed record DefinitionCatalogFilterLimits(int SearchLength, int TagCount, int TagLength);
public sealed record DefinitionCatalogFilters(string Search, ImmutableArray<string> Tags, LlmChatDefinitionStatusFilter Status) {
    public static DefinitionCatalogFilters Empty { get; } = new("", [], LlmChatDefinitionStatusFilter.All);
    public bool IsActive => !string.IsNullOrWhiteSpace(Search) || !Tags.IsEmpty || Status != LlmChatDefinitionStatusFilter.All;
}

public sealed record DefinitionCatalogCard(Guid DefinitionId, string Name, string Summary, string AvatarImageUrl,
    LlmChatDefinitionStatusFilter Status, int Revision, DateTimeOffset UpdatedAtUtc, ImmutableArray<string> Tags) {
    public ConversationParticipantPresentation ToParticipant(Guid? selectedDefinitionId = null) => new(
        DefinitionCatalogMapping.ToKey(DefinitionId), Name, subtitle: $"Revision {Revision}", summary: Summary,
        avatarImageUrl: AvatarImageUrl, avatarSeed: DefinitionId.ToString("D"),
        avatarFallbackText: string.Concat(Name.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Take(2).Select(word => char.ToUpperInvariant(word[0]))),
        searchText: string.Join(' ', Name, Summary, string.Join(' ', Tags)),
        badges: [new(Status.ToString(), Status switch {
            LlmChatDefinitionStatusFilter.Draft => PresentationTone.Default,
            LlmChatDefinitionStatusFilter.Active => PresentationTone.Success,
            LlmChatDefinitionStatusFilter.Suspended => PresentationTone.Warning,
            LlmChatDefinitionStatusFilter.Archived => PresentationTone.Danger,
            _ => throw new ArgumentOutOfRangeException(nameof(Status), Status, "Unknown definition status.")
        }, testId: $"llm-chat-definition-status-{DefinitionId:D}")],
        tags: Tags, metadata: [new(UpdatedAtUtc.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), "Updated")],
        isSelected: DefinitionId == selectedDefinitionId);
}

public sealed record DefinitionCatalogPresentation(bool CanRead, bool CanManage, DefinitionCatalogPhase Phase,
    ImmutableArray<DefinitionCatalogCard> Cards, DefinitionCatalogFilters Filters, bool HasMore = false,
    DefinitionCatalogFailure? Failure = null) {
    public static DefinitionCatalogPresentation Initial { get; } = new(false, false, DefinitionCatalogPhase.Authorizing, [], DefinitionCatalogFilters.Empty);
    public bool IsBusy => Phase is DefinitionCatalogPhase.Authorizing or DefinitionCatalogPhase.Loading or DefinitionCatalogPhase.Refreshing;
    public string ResultText => IsBusy ? "Loading definitions" : $"{Cards.Length} definition{(Cards.Length == 1 ? "" : "s")}";
    public string? Error => Failure switch {
        DefinitionCatalogFailure.Unavailable => "Definitions could not be loaded. Change or reset the filters to retry.",
        DefinitionCatalogFailure.InvalidFilters => "Review the definition filters and try again.",
        DefinitionCatalogFailure.AuthorizationUnavailable => "Definition access could not be checked. Reopen the catalog to retry.",
        _ => null
    };
}

public abstract record DefinitionCatalogIntent {
    public sealed record SearchChanged(string Value) : DefinitionCatalogIntent;
    public sealed record TagsChanged(ImmutableArray<string> Value) : DefinitionCatalogIntent;
    public sealed record StatusChanged(LlmChatDefinitionStatusFilter Value) : DefinitionCatalogIntent;
    public sealed record ResetFilters : DefinitionCatalogIntent;
    public sealed record LoadMore : DefinitionCatalogIntent;
    public sealed record CreateDefinition : DefinitionCatalogIntent;
    public sealed record EditDefinition(Guid DefinitionId) : DefinitionCatalogIntent;
}

public static class DefinitionCatalogMapping {
    public const string DefinitionKeyPrefix = "llm-chat-definition:";
    public static ConversationPresentationKey ToKey(Guid id) => new($"{DefinitionKeyPrefix}{id:D}");
    public static bool TryGetDefinitionId(ConversationPresentationKey key, out Guid id) {
        ArgumentNullException.ThrowIfNull(key);
        id = Guid.Empty;
        return key.Value.StartsWith(DefinitionKeyPrefix, StringComparison.Ordinal)
            && Guid.TryParse(key.Value[DefinitionKeyPrefix.Length..], out id) && id != Guid.Empty;
    }
}
