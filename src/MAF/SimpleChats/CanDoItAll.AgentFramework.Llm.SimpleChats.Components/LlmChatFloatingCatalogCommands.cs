using Microsoft.Extensions.Logging;

namespace CanDoItAll.AgentFramework.Llm.SimpleChats.Components;

internal sealed class LlmChatFloatingCatalogCommands : IAsyncDisposable {
    private readonly ILlmChatConversationUiGateway conversations;
    private readonly LlmChatConversationAccessSession access;
    private readonly Func<Task> retired;
    private readonly ILogger logger;
    private readonly Action stateChanged;
    private readonly HashSet<Guid> unconfirmedCreates = [];
    private readonly HashSet<Guid> unconfirmedArchives = [];
    private bool pending;

    public LlmChatFloatingCatalogCommands(ILlmChatConversationUiGateway conversations, ILlmChatConversationEditorAccess editorAccess,
        Func<Task> changed, Func<Task> retired, ILogger logger, Action stateChanged) {
        this.conversations = conversations;
        this.retired = retired;
        this.logger = logger;
        this.stateChanged = stateChanged;
        access = new(editorAccess, changed, retired);
    }

    public Task OpenAsync() => access.OpenCatalogAsync();
    public bool IsPending => pending;
    public ValueTask AuthorizeReadAsync(CancellationToken token) => access.AuthorizeReadAsync(token);
    public bool IsUnconfirmed(Guid definitionId, IEnumerable<Guid> conversationIds)
        => unconfirmedCreates.Contains(definitionId) || conversationIds.Any(unconfirmedArchives.Contains);

    public async Task AcknowledgeHistoryAsync(Guid definitionId, IEnumerable<Guid> displayedConversationIds, CancellationToken token) {
        await AuthorizeReadAsync(token);
        if (pending) {
            throw new InvalidOperationException("A pending catalog command cannot be acknowledged.");
        }
        unconfirmedCreates.Remove(definitionId);
        unconfirmedArchives.ExceptWith(displayedConversationIds);
    }

    public Task<LlmChatUiResult<LlmChatConversationView>> CreateAsync(Guid definitionId, string title, CancellationToken token)
        => WriteAsync(null, definitionId, unconfirmedCreates,
            () => conversations.CreateAsync(definitionId, title, token), token);

    public Task<LlmChatUiResult<LlmChatConversationView>> ArchiveAsync(LlmChatConversationListItem original, CancellationToken token)
        => WriteAsync(original.ConversationId, original.ConversationId, unconfirmedArchives,
            () => conversations.ArchiveAsync(original.ConversationId, original.ConcurrencyToken, token), token);

    private async Task<LlmChatUiResult<LlmChatConversationView>> WriteAsync(Guid? target, Guid commandTarget,
        HashSet<Guid> unconfirmed, Func<Task<LlmChatUiResult<LlmChatConversationView>>> write, CancellationToken token) {
        if (pending) {
            return Refused(LlmChatUiFailureCodes.CommandPending, "Another catalog command is pending.");
        }
        if (unconfirmed.Contains(commandTarget)) {
            return Unconfirmed();
        }
        pending = true;
        stateChanged();
        var dispatched = false;
        LlmChatEditorOpening? opening = null;
        try {
            await AuthorizeReadAsync(token);
            opening = await access.OpenAsync(target, LlmChatConversationAccessKind.Metadata, () => Task.CompletedTask);
            await opening.AuthorizeAsync(true, token);
            token.ThrowIfCancellationRequested();
            using var admission = opening.EnterWrite(access.AuthorizeReadAsync);
            dispatched = true;
            var result = await write();
            if (result.Value is { } accepted && (target is { } id
                ? accepted.Conversation.ConversationId != id : accepted.Conversation.DefinitionId != commandTarget)) {
                throw new InvalidOperationException("The accepted catalog command belongs to a different target.");
            }
            return result;
        } catch (UnauthorizedAccessException) {
            await retired();
            return Refused(LlmChatUiFailureCodes.Forbidden, "Original conversation access changed. Reopen with current access.");
        } catch (Exception exception) {
            logger.LogWarning("Floating conversation command failed. Target={Target} Dispatched={Dispatched} FailureType={FailureType}.",
                commandTarget, dispatched, exception.GetType().Name);
            if (dispatched) {
                unconfirmed.Add(commandTarget);
                return Unconfirmed();
            }
            return Refused(LlmChatUiFailureCodes.RequestFailed, "The catalog command could not be admitted.");
        } finally {
            access.Retire(opening);
            pending = false;
            stateChanged();
        }
    }

    private static LlmChatUiResult<LlmChatConversationView> Unconfirmed() => Refused(LlmChatUiFailureCodes.OutcomeUnconfirmed,
        "The earlier command outcome is unconfirmed. Refresh and inspect its history, then explicitly acknowledge that review before a new command.");
    private static LlmChatUiResult<LlmChatConversationView> Refused(string code, string message)
        => LlmChatUiResult<LlmChatConversationView>.Failure(new LlmChatUiFailure(code, message));
    public void Stop() => access.Stop();
    public ValueTask DisposeAsync() => access.DisposeAsync();
}
