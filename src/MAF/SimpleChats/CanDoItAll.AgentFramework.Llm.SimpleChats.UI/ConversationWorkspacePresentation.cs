using System.Collections.Immutable;
using CanDoItAll.Conversations.Components.Presentation;

namespace CanDoItAll.AgentFramework.Llm.SimpleChats.UI;

public enum ConversationWorkspaceAction {
    Select, LoadMoreConversations, LoadMoreMessages, ReloadSelected, NewConversation, Rename, Archive,
    DraftChanged, Send, CancelResponse, Reconcile, Abandon
}
public sealed record ConversationWorkspaceIntent(long Generation, ConversationWorkspaceAction Action,
    Guid? ConversationId = null, string? Text = null);
public sealed record ConversationWorkspaceSelection(Guid ConversationId, string Title, string DefinitionName,
    ConversationHeaderPresentation Header);
public sealed record ConversationWorkspacePresentation {
    public long Generation { get; init; }
    public bool IsAuthorizing { get; init; } = true;
    public bool CanRead { get; init; }
    public bool CanManage { get; init; }
    public bool IsLoading { get; init; }
    public bool IsMutating { get; init; }
    public bool Focused { get; init; }
    public bool HasMoreConversations { get; init; }
    public bool HasMoreMessages { get; init; }
    public bool CanReloadSelected { get; init; }
    public bool CanCancel { get; init; }
    public bool CanReconcile { get; init; }
    public bool CanAbandon { get; init; }
    public bool IsSendDisabled { get; init; } = true;
    public string DraftPrompt { get; init; } = "";
    public int ComposerKey { get; init; }
    public string ErrorMessage { get; init; } = "";
    public string? OperationStatus { get; init; }
    public ConversationWorkspaceSelection? Selected { get; init; }
    public ImmutableArray<ConversationThreadPresentation> Threads { get; init; } = [];
    public ImmutableArray<ConversationMessagePresentation> Messages { get; init; } = [];
    public ImmutableArray<ConversationMessagePresentation> TransientMessages { get; init; } = [];
    public ConversationEmptyStatePresentation? EmptyState { get; init; }
}

public static class ConversationWorkspaceMapping {
    public const string ConversationKeyPrefix = "llm-chat-conversation:";
    public static ConversationPresentationKey ToKey(Guid id) => new($"{ConversationKeyPrefix}{id:D}");
    public static bool TryGetConversationId(ConversationPresentationKey key, out Guid id) {
        ArgumentNullException.ThrowIfNull(key);
        id = Guid.Empty;
        return key.Value.StartsWith(ConversationKeyPrefix, StringComparison.Ordinal)
            && Guid.TryParse(key.Value[ConversationKeyPrefix.Length..], out id) && id != Guid.Empty;
    }
}
