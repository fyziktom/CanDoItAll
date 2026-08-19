namespace CanDoItAll.Conversations.Components.Presentation;

public sealed record ConversationThreadRailText(
    string Eyebrow = "Threads",
    string SearchPlaceholder = "Search threads",
    string SearchAriaLabel = "Search agent threads",
    string EmptyText = "The selected participant does not have a thread yet.",
    string NoMatchesText = "No threads match the current search.",
    string LoadingText = "Loading threads...",
    string ErrorEyebrow = "Threads unavailable");
