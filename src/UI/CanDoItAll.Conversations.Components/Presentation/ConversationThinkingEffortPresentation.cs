using CanDoItAll.Components.BaseLib;

namespace CanDoItAll.Conversations.Components.Presentation;

public sealed record ConversationThinkingEffortOption<TValue>(string ChoiceValue, TValue Value, string Text);

public sealed record ConversationThinkingEffortPresentation<TValue>(
    IReadOnlyList<ConversationThinkingEffortOption<TValue>> Options,
    string SelectedChoiceValue,
    bool IsDisabled,
    AlertStyle SupportStyle,
    string SupportMessage);
