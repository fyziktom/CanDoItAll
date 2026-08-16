namespace CanDoItAll.Conversations.Components.Presentation;

public sealed record ConversationProviderOption(
    ConversationPresentationKey Key,
    string Name,
    bool IsEnabled,
    string DefaultModel,
    IReadOnlyList<string> SuggestedModels,
    PresentationBadge? Badge = null,
    string? Description = null);
