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
    IConversationShellLauncher shell,
    DialogService dialogService,
    NotificationService notificationService,
    ILogger<LlmChatConversationShellContributor> logger) : IConversationShellContributor
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

    public string SourceId => SourceIdentifier;

    public ConversationParticipantKind Kind => ConversationParticipantKind.Chat;

    public event EventHandler? Changed;

    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
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
            .Where(item => item.IsVisible)
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
            string.IsNullOrWhiteSpace(failureMessage) ? null : failureMessage);
    }

    public async Task HandleParticipantActionAsync(
        ParticipantActionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
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
        if (!LlmChatConversationPresentationMapper.TryGetConversationId(request.ItemKey, out var conversationId) ||
            !activeConversations.TryGetValue(conversationId, out var state))
        {
            throw new ArgumentException($"'{request.ItemKey.Value}' is not an active Simple Chat key.", nameof(request));
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

        throw new InvalidOperationException($"Unsupported Simple Chat active action '{request.ActionKey.Value}'.");
    }

    public Task HandleWindowCloseAsync(
        string windowId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var conversationId = ResolveWindowId(windowId);
        if (activeConversations.TryGetValue(conversationId, out var state))
        {
            activeConversations[conversationId] = state with { IsVisible = false };
            shell.ClearFocusedWindow(SourceIdentifier, windowId);
            Changed?.Invoke(this, EventArgs.Empty);
        }

        return Task.CompletedTask;
    }

    public static string BuildWindowId(Guid conversationId)
        => $"floating-simple-chat-{conversationId:N}";

    private async Task InitializeCoreAsync(CancellationToken cancellationToken)
    {
        failureMessage = string.Empty;
        try
        {
            authorizationSnapshot = await authorization.GetAsync(cancellationToken);
            if (!authorizationSnapshot.CanRead)
            {
                failureMessage = "Read Simple Chats permission is required.";
                return;
            }

            await ReloadCatalogsAsync(cancellationToken);
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
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    private async Task ReloadCatalogsAsync(CancellationToken cancellationToken)
    {
        var definitionResult = await definitions.ListPageAsync(
            new(take: LlmChatConversationWorkspaceController.MaximumDefinitionCount, status: LlmChatDefinitionStatus.Active),
            cancellationToken);
        var conversationResult = await conversations.ListPageAsync(
            new(take: LlmChatConversationWorkspaceController.MaximumConversationCount),
            cancellationToken);
        if (!definitionResult.IsSuccess || definitionResult.Value is null)
        {
            SetFailure(definitionResult.Failures);
            return;
        }

        if (!conversationResult.IsSuccess || conversationResult.Value is null)
        {
            SetFailure(conversationResult.Failures);
            return;
        }

        activeDefinitions = definitionResult.Value.Items
            .Where(item => item.Status == LlmChatDefinitionStatus.Active)
            .ToArray();
        conversationHistory = conversationResult.Value.Items.ToArray();
        failureMessage = string.Empty;
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
                !authorizationSnapshot.CanManage),
            new(
                HistoryActionKey,
                $"Open conversation history for {definition.Name}",
                "history",
                $"floating-simple-chat-history-{definition.DefinitionId:N}",
                false,
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
                !authorizationSnapshot.CanManage ||
                conversation.Status != LlmChatConversationStatus.Active ||
                conversation.ActiveOperationId.HasValue,
                ConversationActionStyle.Danger)
        };
        return new(
            SourceIdentifier,
            Kind,
            new(
                LlmChatConversationPresentationMapper.ToKey(conversation.ConversationId),
                conversation.Title,
                badges,
                actions));
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
            });
    }

    private async Task StartConversationAsync(Guid definitionId, CancellationToken cancellationToken)
    {
        if (!authorizationSnapshot.CanManage)
        {
            notificationService.Warning("Simple Chat unavailable", "Manage Simple Chats permission is required to start a conversation.");
            return;
        }

        var definition = activeDefinitions.Single(item => item.DefinitionId == definitionId);
        var result = await conversations.CreateAsync(definitionId, $"{definition.Name} chat", cancellationToken);
        if (!result.IsSuccess || result.Value is null)
        {
            NotifyFailure("Unable to start Simple Chat", result.Failures);
            return;
        }

        ReplaceConversation(result.Value.Conversation);
        ShowConversation(result.Value.Conversation);
    }

    private async Task OpenHistoryAsync(Guid definitionId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var definition = activeDefinitions.Single(item => item.DefinitionId == definitionId);
        var matchingConversations = conversationHistory
            .Where(item => item.DefinitionId == definitionId)
            .OrderByDescending(item => item.UpdatedAtUtc)
            .ToArray();
        var result = await dialogService.OpenAsync<LlmChatFloatingHistoryDialog>(
            "Simple Chat history",
            new Dictionary<string, object?>
            {
                [nameof(LlmChatFloatingHistoryDialog.Definition)] = definition,
                [nameof(LlmChatFloatingHistoryDialog.Conversations)] = matchingConversations
            },
            new DialogOptions
            {
                Eyebrow = "Floating Simple Chat",
                Subtitle = definition.Name,
                Size = ModalSize.Wide,
                DenseChrome = true,
                TestId = "floating-simple-chat-history-dialog",
                AriaLabel = "Simple Chat conversation history"
            });
        if (result is not Guid conversationId)
        {
            return;
        }

        var conversation = matchingConversations.Single(item => item.ConversationId == conversationId);
        ShowConversation(conversation);
    }

    private async Task ArchiveConversationAsync(
        LlmChatConversationListItem conversation,
        CancellationToken cancellationToken)
    {
        var confirmed = await dialogService.OpenAsync<LlmChatFloatingArchiveDialog>(
            "Archive Simple Chat",
            new Dictionary<string, object?>
            {
                [nameof(LlmChatFloatingArchiveDialog.Conversation)] = conversation
            },
            new DialogOptions
            {
                Eyebrow = "Floating Simple Chat",
                Subtitle = conversation.Title,
                Size = ModalSize.Compact,
                DenseChrome = true,
                TestId = "floating-simple-chat-archive-dialog",
                AriaLabel = "Archive Simple Chat conversation"
            });
        if (confirmed is not true)
        {
            return;
        }

        var result = await conversations.ArchiveAsync(
            conversation.ConversationId,
            conversation.ConcurrencyToken,
            cancellationToken);
        if (!result.IsSuccess || result.Value is null)
        {
            NotifyFailure("Unable to archive Simple Chat", result.Failures);
            return;
        }

        ReplaceConversation(result.Value.Conversation);
        activeConversations.Remove(conversation.ConversationId);
        shell.ClearFocusedWindow(SourceIdentifier, BuildWindowId(conversation.ConversationId));
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void ShowConversation(LlmChatConversationListItem conversation)
    {
        foreach (var pair in activeConversations.ToArray())
        {
            activeConversations[pair.Key] = pair.Value with { IsVisible = false };
        }

        activeConversations[conversation.ConversationId] = new(conversation, true);
        shell.FocusWindow(SourceIdentifier, BuildWindowId(conversation.ConversationId));
        Changed?.Invoke(this, EventArgs.Empty);
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
        Changed?.Invoke(this, EventArgs.Empty);
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

    private sealed record FloatingConversationState(
        LlmChatConversationListItem Conversation,
        bool IsVisible);
}
