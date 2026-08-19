using CanDoItAll.AgentFramework.Llm.Abstractions;
using CanDoItAll.Conversations.Components.Presentation;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Conversations;

namespace CanDoItAll.AgentFramework.Llm.SimpleChats.Components;

internal static class LlmChatConversationPresentationMapper
{
    public const string ConversationKeyPrefix = "llm-chat-conversation:";

    public static ConversationThreadPresentation ToThread(
        LlmChatConversationListItem conversation,
        Guid? selectedConversationId,
        bool isBusy)
    {
        ArgumentNullException.ThrowIfNull(conversation);
        return new(
            ToKey(conversation.ConversationId),
            conversation.Title,
            conversation.UpdatedAtUtc,
            conversation.UpdatedAtUtc.ToLocalTime().ToString("g", System.Globalization.CultureInfo.CurrentCulture),
            $"{conversation.DefinitionName} · Revision {conversation.DefinitionRevision}",
            ResolvePreview(conversation),
            searchText: string.Join(' ', conversation.Title, conversation.DefinitionName, conversation.Status),
            tooltipText: $"{conversation.DefinitionName}, pinned revision {conversation.DefinitionRevision}",
            selectLabel: $"Open Simple Chat {conversation.Title}",
            badges:
            [
                new(
                    conversation.Status.ToString(),
                    conversation.Status == LlmChatConversationStatus.Archived
                        ? PresentationTone.Default
                        : PresentationTone.Success)
            ],
            isSelected: conversation.ConversationId == selectedConversationId,
            isBusy: isBusy,
            isDisabled: false);
    }

    public static ConversationHeaderPresentation ToHeader(LlmChatConversationListItem conversation)
    {
        ArgumentNullException.ThrowIfNull(conversation);
        return new(
            new(
                conversation.DefinitionName,
                null,
                BuildFallback(conversation.DefinitionName),
                conversation.DefinitionId.ToString("D")),
            [
                new(
                    conversation.Status.ToString(),
                    conversation.Status == LlmChatConversationStatus.Archived
                        ? PresentationTone.Default
                        : PresentationTone.Success,
                    testId: "llm-chat-conversation-status"),
                new(
                    $"Revision {conversation.DefinitionRevision}",
                    PresentationTone.Info,
                    accessibleDescription: "Pinned definition revision",
                    testId: "llm-chat-conversation-revision")
            ]);
    }

    public static IReadOnlyList<ConversationMessagePresentation> ToMessages(
        IReadOnlyList<LlmChatMessageListItem> messages)
    {
        ArgumentNullException.ThrowIfNull(messages);
        return messages
            .Select(TryToMessage)
            .Where(static message => message is not null)
            .Cast<ConversationMessagePresentation>()
            .ToArray();
    }

    public static ConversationMessagePresentation ToPending(
        Guid operationId,
        string message,
        DateTimeOffset admittedAtUtc)
        => new(
            new($"llm-chat-pending:{operationId:D}"),
            ConversationMessageRole.User,
            "You",
            PresentationTone.Accent,
            message,
            admittedAtUtc.ToLocalTime().ToString("t", System.Globalization.CultureInfo.CurrentCulture),
            state: ConversationMessageState.Pending);

    public static ConversationMessagePresentation ToStreaming(
        Guid operationId,
        string content,
        DateTimeOffset startedAtUtc)
        => new(
            new($"llm-chat-streaming:{operationId:D}"),
            ConversationMessageRole.Assistant,
            "Assistant",
            PresentationTone.Info,
            content,
            startedAtUtc.ToLocalTime().ToString("t", System.Globalization.CultureInfo.CurrentCulture),
            state: ConversationMessageState.Streaming);

    public static ConversationEmptyStatePresentation ToEmptyState(bool hasSelection)
        => hasSelection
            ? new(
                "Conversation",
                "No messages yet",
                "Send the first message to begin this Simple Chat.")
            : new(
                "Simple Chats",
                "Select a conversation",
                "Choose a thread or start a new chat from an active definition.");

    public static ConversationPresentationKey ToKey(Guid conversationId)
        => new($"{ConversationKeyPrefix}{conversationId:D}");

    public static bool TryGetConversationId(ConversationPresentationKey key, out Guid conversationId)
    {
        ArgumentNullException.ThrowIfNull(key);
        conversationId = Guid.Empty;
        return key.Value.StartsWith(ConversationKeyPrefix, StringComparison.Ordinal) &&
               Guid.TryParse(key.Value[ConversationKeyPrefix.Length..], out conversationId) &&
               conversationId != Guid.Empty;
    }

    private static ConversationMessagePresentation? TryToMessage(LlmChatMessageListItem message)
    {
        var role = message.Role switch
        {
            LlmMessageRole.User => ConversationMessageRole.User,
            LlmMessageRole.Assistant => ConversationMessageRole.Assistant,
            _ => (ConversationMessageRole?)null
        };
        if (role is null)
        {
            return null;
        }

        var isUser = role == ConversationMessageRole.User;
        return new(
            new($"llm-chat-message:{message.EntryId:D}"),
            role.Value,
            isUser ? "You" : "Assistant",
            isUser ? PresentationTone.Accent : PresentationTone.Info,
            message.Text,
            message.CreatedAtUtc.ToLocalTime().ToString("g", System.Globalization.CultureInfo.CurrentCulture),
            copyValue: message.Text,
            copyAriaLabel: isUser ? "Copy your message" : "Copy assistant response");
    }

    private static string ResolvePreview(LlmChatConversationListItem conversation)
        => conversation.ActiveOperationId.HasValue
            ? "Response in progress"
            : conversation.Status == LlmChatConversationStatus.Archived
                ? "Archived conversation"
                : "Ready for a message";

    private static string BuildFallback(string name)
    {
        var initials = name
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Take(2)
            .Select(static word => char.ToUpperInvariant(word[0]))
            .ToArray();
        return initials.Length == 0 ? "AI" : new string(initials);
    }
}
