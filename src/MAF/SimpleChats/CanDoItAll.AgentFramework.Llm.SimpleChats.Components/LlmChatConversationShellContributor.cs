using CanDoItAll.Components.BaseLib;
using CanDoItAll.Conversations.Components.Presentation;
using CanDoItAll.Conversations.Shell;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Conversations;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Definitions;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.AgentFramework.Llm.SimpleChats.Components;

public sealed class LlmChatConversationShellContributor(
    ILlmChatDefinitionUiGateway definitions,
    ILlmChatConversationUiGateway conversations,
    ILlmChatUiAuthorizationFacade authorization,
    ILlmChatDefinitionCatalogInvalidator definitionCatalogInvalidator,
    IConversationShellLauncher shell,
    DialogService dialogService,
    NotificationService notificationService,
    ILlmChatConversationEditorAccess editorAccess,
    ILogger<LlmChatConversationShellContributor> logger) : IConversationShellContributor, IAsyncDisposable
{
    public const string SourceIdentifier = "simple-chats";
    private static readonly ConversationPresentationKey NewChatActionKey = new("new-chat");
    private static readonly ConversationPresentationKey HistoryActionKey = new("history");
    private static readonly ConversationPresentationKey OpenActionKey = new("open");
    private static readonly ConversationPresentationKey ArchiveActionKey = new("archive");
    private readonly Dictionary<Guid, FloatingConversationState> activeConversations = [];
    private IReadOnlyList<LlmChatDefinitionListItem> activeDefinitions = [];
    private IReadOnlyList<LlmChatConversationListItem> conversationHistory = [];
    private LlmChatUiAuthorizationSnapshot authorizationSnapshot = new(false, false, false);
    private Task initializationTask = Task.CompletedTask;
    private string failureMessage = string.Empty;
    private bool initialized;
    private bool attached;
    private LlmChatFloatingCatalogCommands? commands;
    private long changeRevision;
    private long refreshedRevision;
    private bool refreshing;
    private int disposed;
    private readonly CancellationTokenSource retirement = new();
    private bool IsRetired => Volatile.Read(ref disposed) != 0;

    public string SourceId => SourceIdentifier;

    public ConversationParticipantKind Kind => ConversationParticipantKind.Chat;

    public event EventHandler? Changed;

    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(IsRetired, this);
        if (initialized || !initializationTask.IsCompleted)
        {
            return initializationTask;
        }

        initializationTask = InitializeCoreAsync(cancellationToken);
        return initializationTask;
    }

    public ConversationShellContributorSnapshot Snapshot()
    {
        var available = activeDefinitions
            .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .Select(MapDefinition)
            .ToArray();
        var active = activeConversations.Values
            .OrderByDescending(item => item.Conversation.UpdatedAtUtc)
            .Select(MapActiveConversation)
            .ToArray();
        var windows = activeConversations.Values
            .Select(MapWindow)
            .ToArray();
        var badges = authorizationSnapshot.CanRead
            ? new PresentationBadge[]
            {
                new($"{available.Length} definitions", PresentationTone.Info),
                new($"{conversationHistory.Count} saved conversations", PresentationTone.Default),
                new("No application context", PresentationTone.Default)
            }
            : [];
        return new(
            available,
            active,
            windows,
            badges,
            string.IsNullOrWhiteSpace(failureMessage) ? null : failureMessage,
            RefreshAsync, changeRevision != refreshedRevision);
    }

    public async Task HandleParticipantActionAsync(
        ParticipantActionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (IsRetired) {
            return;
        }
        if (!await AuthorizeReadAsync(cancellationToken)) {
            return;
        }
        if (!LlmChatDefinitionPresentationMapper.TryGetDefinitionId(request.ParticipantKey, out var definitionId))
        {
            throw new ArgumentException($"'{request.ParticipantKey.Value}' is not a Simple Chat definition key.", nameof(request));
        }

        if (request.ActionKey == NewChatActionKey)
        {
            await StartConversationAsync(definitionId, cancellationToken);
            return;
        }

        if (request.ActionKey == HistoryActionKey)
        {
            await OpenHistoryAsync(definitionId, cancellationToken);
            return;
        }

        throw new InvalidOperationException($"Unsupported Simple Chat participant action '{request.ActionKey.Value}'.");
    }

    public async Task HandleActiveActionAsync(
        ConversationActionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (IsRetired) {
            return;
        }
        if (!await AuthorizeReadAsync(cancellationToken)) {
            return;
        }
        if (!LlmChatConversationPresentationMapper.TryGetConversationId(request.ItemKey, out var conversationId) ||
            !activeConversations.TryGetValue(conversationId, out var state))
        {
            throw new ArgumentException($"'{request.ItemKey.Value}' is not an active Simple Chat key.", nameof(request));
        }
        if (request.Version != state.Conversation.ConcurrencyToken) {
            notificationService.Warning("Conversation changed", "Use the current catalog item before starting another action.");
            return;
        }

        if (request.ActionKey == OpenActionKey)
        {
            ShowConversation(state.Conversation);
            return;
        }

        if (request.ActionKey == ArchiveActionKey)
        {
            await ArchiveConversationAsync(state.Conversation, cancellationToken);
            return;
        }
        if (request.ActionKey == HistoryActionKey) {
            await OpenHistoryAsync(state.Conversation.DefinitionId, cancellationToken);
            return;
        }

        throw new InvalidOperationException($"Unsupported Simple Chat active action '{request.ActionKey.Value}'.");
    }

    public Task HandleWindowCloseAsync(
        string windowId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (IsRetired) {
            return Task.CompletedTask;
        }
        var conversationId = ResolveWindowId(windowId);
        if (activeConversations.TryGetValue(conversationId, out var state))
        {
            activeConversations[conversationId] = state with { IsVisible = false };
            shell.ClearFocusedWindow(SourceIdentifier, windowId);
            RaiseChanged();
        }

        return Task.CompletedTask;
    }

    public static string BuildWindowId(Guid conversationId)
        => $"floating-simple-chat-{conversationId:N}";

    private async Task InitializeCoreAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(IsRetired, this);
        using var readCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, retirement.Token);
        cancellationToken = readCancellation.Token;
        Attach();
        failureMessage = string.Empty;
        try
        {
            commands = new(conversations, editorAccess, MarkChangedAsync, RetireAccessAsync, logger, RaiseChanged);
            await commands.OpenAsync();
            var currentAuthorization = await authorization.GetAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            authorizationSnapshot = currentAuthorization;
            if (!authorizationSnapshot.CanRead)
            {
                failureMessage = "Read Simple Chats permission is required.";
                return;
            }

            await ReloadCatalogsAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            initialized = true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Unable to initialize the Simple Chat conversation contributor. FailureType={FailureType}.",
                exception.GetType().Name);
            failureMessage = "Simple Chats could not be initialized for the active runtime profile.";
        }
        finally
        {
            RaiseChanged();
        }
    }

    private async Task<bool> ReloadCatalogsAsync(CancellationToken cancellationToken)
    {
        await commands!.AuthorizeReadAsync(cancellationToken);
        var definitionResult = await definitions.ListPageAsync(
            new(take: LlmChatConversationWorkspaceController.MaximumDefinitionCount, status: LlmChatDefinitionStatus.Active),
            cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        await commands.AuthorizeReadAsync(cancellationToken);
        var conversationResult = await conversations.ListPageAsync(
            new(take: LlmChatConversationWorkspaceController.MaximumConversationCount),
            cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        await commands.AuthorizeReadAsync(cancellationToken);
        if (!definitionResult.IsSuccess || definitionResult.Value is null)
        {
            SetFailure(definitionResult.Failures);
            return false;
        }

        if (!conversationResult.IsSuccess || conversationResult.Value is null)
        {
            SetFailure(conversationResult.Failures);
            return false;
        }

        activeDefinitions = definitionResult.Value.Items
            .Where(item => item.Status == LlmChatDefinitionStatus.Active)
            .ToArray();
        conversationHistory = conversationResult.Value.Items.ToArray();
        foreach (var current in conversationHistory) {
            if (activeConversations.TryGetValue(current.ConversationId, out var window)) {
                activeConversations[current.ConversationId] = window with { Conversation = current };
            }
        }
        failureMessage = string.Empty;
        return true;
    }

    private async Task RefreshAsync(CancellationToken cancellationToken) {
        if (refreshing || IsRetired || !await AuthorizeReadAsync(cancellationToken)) {
            return;
        }
        refreshing = true;
        var coveringRevision = changeRevision;
        try {
            if (await ReloadCatalogsAsync(cancellationToken)) {
                refreshedRevision = coveringRevision;
            }
        } catch (UnauthorizedAccessException) {
            await RetireAccessAsync();
        } finally {
            refreshing = false;
            RaiseChanged();
        }
    }

    private async Task<bool> AuthorizeReadAsync(CancellationToken token) {
        try {
            if (commands is null) {
                return false;
            }
            await commands.AuthorizeReadAsync(token);
            return true;
        } catch (UnauthorizedAccessException) {
            await RetireAccessAsync();
            return false;
        }
    }

    private Task MarkChangedAsync() {
        changeRevision++;
        RaiseChanged();
        return Task.CompletedTask;
    }

    private Task RetireAccessAsync() {
        commands?.Stop();
        activeDefinitions = [];
        conversationHistory = [];
        activeConversations.Clear();
        authorizationSnapshot = new(false, false, false);
        failureMessage = "Original conversation access changed. Reopen with current access.";
        RaiseChanged();
        return Task.CompletedTask;
    }

    private ConversationShellParticipant MapDefinition(LlmChatDefinitionListItem definition)
    {
        var participant = LlmChatDefinitionPresentationMapper.ToParticipant(definition);
        var actions = new ParticipantActionPresentation[]
        {
            new(
                NewChatActionKey,
                $"Start a new chat with {definition.Name}",
                "add_comment",
                $"floating-simple-chat-new-{definition.DefinitionId:N}",
                !authorizationSnapshot.CanManage || commands?.IsPending == true),
            new(
                HistoryActionKey,
                $"Open conversation history for {definition.Name}",
                "history",
                $"floating-simple-chat-history-{definition.DefinitionId:N}",
                commands?.IsPending == true,
                ParticipantActionStyle.Light)
        };
        return new(
            SourceIdentifier,
            Kind,
            new(
                participant,
                actions,
                $"floating-simple-chat-definition-{definition.DefinitionId:N}",
                $"floating-simple-chat-definition-select-{definition.DefinitionId:N}"));
    }

    private ConversationShellActiveItem MapActiveConversation(FloatingConversationState state)
    {
        var conversation = state.Conversation;
        var badges = new PresentationBadge[]
        {
            new(
                conversation.Status.ToString(),
                conversation.Status == LlmChatConversationStatus.Active
                    ? PresentationTone.Success
                    : PresentationTone.Default),
            new($"Revision {conversation.DefinitionRevision}", PresentationTone.Info),
            new(
                conversation.ActiveOperationId.HasValue ? "Responding" : state.IsVisible ? "Open" : "Kept active",
                conversation.ActiveOperationId.HasValue ? PresentationTone.Info : PresentationTone.Default)
        };
        var actions = new ConversationActionPresentation[]
        {
            new(
                OpenActionKey,
                "Open",
                "open_in_new",
                $"floating-simple-chat-open-{conversation.ConversationId:N}"),
            new(
                ArchiveActionKey,
                "Archive",
                "archive",
                $"floating-simple-chat-archive-{conversation.ConversationId:N}",
                !authorizationSnapshot.CanManage || commands?.IsPending == true ||
                conversation.Status != LlmChatConversationStatus.Active ||
                conversation.ActiveOperationId.HasValue,
                ConversationActionStyle.Danger),
            new(HistoryActionKey, "History", "history", $"floating-simple-chat-active-history-{conversation.ConversationId:N}",
                commands?.IsPending == true)
        };
        return new(
            SourceIdentifier,
            Kind,
            new(
                LlmChatConversationPresentationMapper.ToKey(conversation.ConversationId),
                conversation.Title,
                badges,
                actions, conversation.ConcurrencyToken));
    }

    private static ConversationShellWindowDescriptor MapWindow(FloatingConversationState state)
    {
        var conversation = state.Conversation;
        return new(
            new(SourceIdentifier, BuildWindowId(conversation.ConversationId)),
            ConversationParticipantKind.Chat,
            "floating-simple-chat-window",
            $"Simple Chat {conversation.Title}",
            "Simple Chat",
            conversation.Title,
            $"{conversation.DefinitionName} · Revision {conversation.DefinitionRevision}",
            typeof(LlmChatFloatingConversationContent),
            new Dictionary<string, object>
            {
                [nameof(LlmChatFloatingConversationContent.ConversationId)] = conversation.ConversationId
            }, keepContentMounted: true);
    }

    private async Task StartConversationAsync(Guid definitionId, CancellationToken cancellationToken)
    {
        if (!authorizationSnapshot.CanManage)
        {
            notificationService.Warning("Simple Chat unavailable", "Manage Simple Chats permission is required to start a conversation.");
            return;
        }

        var definition = activeDefinitions.Single(item => item.DefinitionId == definitionId);
        var result = await commands!.CreateAsync(definitionId, $"{definition.Name} chat", cancellationToken);
        if (IsRetired) {
            return;
        }
        if (!result.IsSuccess || result.Value is null)
        {
            NotifyFailure("Unable to start Simple Chat", result.Failures);
            return;
        }

        ReplaceConversation(result.Value.Conversation);
        ShowConversation(result.Value.Conversation);
        if (!string.IsNullOrWhiteSpace(result.Warning)) {
            notificationService.Warning("Conversation saved", result.Warning);
        }
    }

    private async Task OpenHistoryAsync(Guid definitionId, CancellationToken cancellationToken) {
        using var operation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, retirement.Token);
        operation.Token.ThrowIfCancellationRequested();
        if (!await ReloadCatalogsAsync(operation.Token)) {
            RaiseChanged();
            return;
        }
        var definition = activeDefinitions.SingleOrDefault(item => item.DefinitionId == definitionId);
        var matchingConversations = conversationHistory
            .Where(item => item.DefinitionId == definitionId)
            .OrderByDescending(item => item.UpdatedAtUtc)
            .ToArray();
        var definitionName = definition?.Name ?? matchingConversations.FirstOrDefault()?.DefinitionName;
        if (definitionName is null) {
            notificationService.Warning("History unavailable", "Refresh the catalog before opening this definition's history.");
            return;
        }
        object? result;
        try {
            result = await dialogService.OpenAsync<LlmChatFloatingHistoryDialog>(
                "Simple Chat history",
                new Dictionary<string, object?> {
                    [nameof(LlmChatFloatingHistoryDialog.Definition)] = definition,
                    [nameof(LlmChatFloatingHistoryDialog.Conversations)] = matchingConversations,
                    [nameof(LlmChatFloatingHistoryDialog.RequiresAcknowledgement)] = commands!.IsUnconfirmed(definitionId,
                        matchingConversations.Select(item => item.ConversationId))
                },
                new DialogOptions {
                    Eyebrow = "Floating Simple Chat",
                    Subtitle = definitionName,
                    Size = ModalSize.Wide,
                    DenseChrome = true,
                    TestId = "floating-simple-chat-history-dialog",
                    AriaLabel = "Simple Chat conversation history"
                }, operation.Token);
        } catch (OperationCanceledException) when (operation.IsCancellationRequested) {
            return;
        }
        if (IsRetired || operation.IsCancellationRequested || !await AuthorizeReadAsync(operation.Token)) {
            return;
        }
        if (result is LlmChatFloatingHistoryResolution.CommandsReviewed) {
            await commands!.AcknowledgeHistoryAsync(definitionId, matchingConversations.Select(item => item.ConversationId), operation.Token);
            RaiseChanged();
            return;
        }
        if (result is not Guid conversationId) {
            return;
        }

        var conversation = matchingConversations.Single(item => item.ConversationId == conversationId);
        ShowConversation(conversation);
    }

    private async Task ArchiveConversationAsync(
        LlmChatConversationListItem conversation,
        CancellationToken cancellationToken) {
        using var operation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, retirement.Token);
        operation.Token.ThrowIfCancellationRequested();
        object? confirmed;
        try {
            confirmed = await dialogService.OpenAsync<LlmChatFloatingArchiveDialog>(
                "Archive Simple Chat",
                new Dictionary<string, object?> {
                    [nameof(LlmChatFloatingArchiveDialog.Conversation)] = conversation
                },
                new DialogOptions {
                    Eyebrow = "Floating Simple Chat",
                    Subtitle = conversation.Title,
                    Size = ModalSize.Compact,
                    DenseChrome = true,
                    TestId = "floating-simple-chat-archive-dialog",
                    AriaLabel = "Archive Simple Chat conversation"
                }, operation.Token);
        } catch (OperationCanceledException) when (operation.IsCancellationRequested) {
            return;
        }
        if (IsRetired || operation.IsCancellationRequested || confirmed is not true) {
            return;
        }

        var result = await commands!.ArchiveAsync(conversation, operation.Token);
        if (IsRetired) {
            return;
        }
        if (!result.IsSuccess || result.Value is null) {
            NotifyFailure("Unable to archive Simple Chat", result.Failures);
            return;
        }

        ReplaceConversation(result.Value.Conversation);
        activeConversations[conversation.ConversationId] = activeConversations[conversation.ConversationId] with { IsVisible = false };
        shell.ClearFocusedWindow(SourceIdentifier, BuildWindowId(conversation.ConversationId));
        if (!string.IsNullOrWhiteSpace(result.Warning)) {
            notificationService.Warning("Conversation archived", result.Warning);
        }
        RaiseChanged();
    }

    private void ShowConversation(LlmChatConversationListItem conversation)
    {
        foreach (var pair in activeConversations.ToArray())
        {
            activeConversations[pair.Key] = pair.Value with { IsVisible = false };
        }

        activeConversations[conversation.ConversationId] = new(conversation, true);
        shell.FocusWindow(SourceIdentifier, BuildWindowId(conversation.ConversationId));
        RaiseChanged();
    }

    private void ReplaceConversation(LlmChatConversationListItem conversation)
    {
        conversationHistory = conversationHistory
            .Where(item => item.ConversationId != conversation.ConversationId)
            .Append(conversation)
            .OrderByDescending(item => item.UpdatedAtUtc)
            .ToArray();
        if (activeConversations.TryGetValue(conversation.ConversationId, out var state))
        {
            activeConversations[conversation.ConversationId] = state with { Conversation = conversation };
        }
    }

    private void SetFailure(IReadOnlyList<LlmChatUiFailure> failures)
    {
        failureMessage = failures.FirstOrDefault()?.Message ?? "Simple Chats could not be loaded.";
    }

    private void Attach()
    {
        if (attached)
        {
            return;
        }

        attached = true;
        definitionCatalogInvalidator.Invalidated += HandleDefinitionCatalogInvalidated;
    }

    private void HandleDefinitionCatalogInvalidated(
        object? sender,
        LlmChatDefinitionCatalogInvalidatedEventArgs eventArgs)
    {
        if (IsRetired) {
            return;
        }
        _ = MarkChangedAsync();
    }

    private void RaiseChanged()
    {
        if (!IsRetired)
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    private void NotifyFailure(string title, IReadOnlyList<LlmChatUiFailure> failures)
    {
        var message = failures.FirstOrDefault()?.Message ?? "The Simple Chat request could not be completed.";
        notificationService.Error(title, message);
    }

    private static Guid ResolveWindowId(string windowId)
    {
        const string prefix = "floating-simple-chat-";
        return windowId.StartsWith(prefix, StringComparison.Ordinal) &&
               Guid.TryParseExact(windowId[prefix.Length..], "N", out var conversationId) &&
               conversationId != Guid.Empty
            ? conversationId
            : throw new ArgumentException($"'{windowId}' is not a Simple Chat window id.", nameof(windowId));
    }

    public ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0)
        {
            return ValueTask.CompletedTask;
        }

        retirement.Cancel();
        commands?.Stop();
        if (attached) {
            definitionCatalogInvalidator.Invalidated -= HandleDefinitionCatalogInvalidated;
        }
        _ = ReleaseRetirementAsync();

        return ValueTask.CompletedTask;
    }

    private async Task ReleaseRetirementAsync() {
        try {
            await initializationTask;
        } catch (OperationCanceledException) when (IsRetired) {
        } finally {
            if (commands is not null) {
                await commands.DisposeAsync();
            }
            retirement.Dispose();
        }
    }

    private sealed record FloatingConversationState(
        LlmChatConversationListItem Conversation,
        bool IsVisible);
}
