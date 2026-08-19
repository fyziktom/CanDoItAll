namespace CanDoItAll.Conversations.Components.Presentation;

public sealed record ConversationProviderOption(
    ConversationPresentationKey Key,
    string Name,
    bool IsEnabled,
    string DefaultModel,
    IReadOnlyList<string> SuggestedModels,
    PresentationBadge? Badge = null,
    string? Description = null)
{
    private IReadOnlyList<string> suggestedModels =
        PresentationCollection.Snapshot(SuggestedModels, nameof(SuggestedModels));

    public IReadOnlyList<string> SuggestedModels
    {
        get => suggestedModels;
        init => suggestedModels = PresentationCollection.Snapshot(value, nameof(SuggestedModels));
    }
}
