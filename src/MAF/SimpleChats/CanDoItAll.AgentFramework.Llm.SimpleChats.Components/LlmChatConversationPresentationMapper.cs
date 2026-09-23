using CanDoItAll.AgentFramework.Llm.Abstractions;
using CanDoItAll.Conversations.Components.Presentation;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Conversations;
using CanDoItAll.AgentFramework.Llm.SimpleChats.UI;
using System.Collections.Immutable;

namespace CanDoItAll.AgentFramework.Llm.SimpleChats.Components;

internal static class LlmChatConversationPresentationMapper
{
    public const string ConversationKeyPrefix = ConversationWorkspaceMapping.ConversationKeyPrefix;

    public static ConversationWorkspacePresentation ToWorkspace(LlmChatConversationWorkspaceController? controller, bool focused, string draft, int composerKey) {
        if (controller is null) {
            return new() { Focused = focused };
        }
        var selected = controller.SelectedConversation;
        var messages = ToMessages(controller.Messages).ToImmutableArray();
        var transient = ImmutableArray.CreateBuilder<ConversationMessagePresentation>(2);
        if (controller.PendingTurn is { } pending) {
            transient.Add(ToPending(pending.OperationId, pending.Message, pending.AdmittedAtUtc));
        }
        if (controller.OperationProjection is { TransientAssistantText.Length: > 0 } projection && controller.ActiveOperation is { } operation) {
            transient.Add(ToStreaming(projection.OperationId, projection.TransientAssistantText, operation.StartedAtUtc));
        }
        return new() {
            Generation = controller.SelectionGeneration, IsAuthorizing = controller.IsAuthorizing,
            CanRead = controller.Authorization.CanRead, CanManage = controller.Authorization.CanManage,
            IsLoading = controller.IsLoading, IsMutating = controller.IsMutating, Focused = focused,
            HasMoreConversations = controller.HasMoreConversations, HasMoreMessages = controller.HasMoreMessages,
            CanReloadSelected = controller.CanReloadSelected, CanCancel = controller.CanCancel,
            CanReconcile = controller.CanReconcile, CanAbandon = controller.CanAbandon,
            IsSendDisabled = !controller.Authorization.CanExecute || selected is not { Status: LlmChatConversationStatus.Active, ActiveOperationId: null }
                || string.IsNullOrWhiteSpace(draft),
            DraftPrompt = draft, ComposerKey = composerKey, ErrorMessage = controller.ErrorMessage,
            OperationStatus = controller.ActiveOperation is null ? null : controller.OperationStatusText,
            Selected = selected is null ? null : new(selected.ConversationId, selected.Title, selected.DefinitionName, ToHeader(selected)),
            Threads = controller.Conversations.Select(item => ToThread(item, selected?.ConversationId, controller.IsLoading || controller.IsMutating)).ToImmutableArray(),
            Messages = messages, TransientMessages = transient.ToImmutable(),
            EmptyState = messages.IsEmpty && transient.Count == 0 ? ToEmptyState(selected is not null) : null
        };
    }

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
            CanDoItAll.AgentFramework.UI.Chat.ChatPresentationTime.Format(conversation.UpdatedAtUtc),
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
            CanDoItAll.AgentFramework.UI.Chat.ChatPresentationTime.Format(admittedAtUtc),
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
            CanDoItAll.AgentFramework.UI.Chat.ChatPresentationTime.Format(startedAtUtc),
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
        => ConversationWorkspaceMapping.ToKey(conversationId);

    public static bool TryGetConversationId(ConversationPresentationKey key, out Guid conversationId)
    {
        return ConversationWorkspaceMapping.TryGetConversationId(key, out conversationId);
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
            CanDoItAll.AgentFramework.UI.Chat.ChatPresentationTime.Format(message.CreatedAtUtc),
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
