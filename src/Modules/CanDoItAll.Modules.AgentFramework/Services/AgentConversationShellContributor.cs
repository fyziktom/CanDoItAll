using CanDoItAll.AgentFramework.Components;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Conversations.Components.Presentation;
using CanDoItAll.Conversations.Shell;
using CanDoItAll.Modules.AgentFramework.Pages.Components;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.AgentFramework;

public sealed class AgentConversationShellContributor(
    IFloatingAgentChatCoordinator coordinator,
    IConversationShellCoordinator shell,
    IAgentChatContextRegistry contextRegistry,
    IAgentFrameworkWorkspaceService workspaceService,
    IAgentReferenceDataProvider referenceDataProvider,
    IAgentReferenceDataCacheInvalidator referenceDataCacheInvalidator,
    DialogService dialogService,
    NotificationService notificationService,
    ILogger<AgentConversationShellContributor> logger) : IConversationShellContributor, IAsyncDisposable
{
    public const string SourceIdentifier = "agents";
    private const string ParticipantKeyPrefix = "agent:";
    private const string ActiveKeyPrefix = "agent-chat:";
    private static readonly ConversationPresentationKey NewChatActionKey = new("new-chat");
    private static readonly ConversationPresentationKey HistoryActionKey = new("history");
    private static readonly ConversationPresentationKey OpenActionKey = new("open");
    private static readonly ConversationPresentationKey StopActionKey = new("stop");
    private readonly CancellationTokenSource lifetime = new();
    private readonly HashSet<Guid> busyAgentIds = [];
    private IReadOnlyList<AgentDefinition> agents = [];
    private AgentChatContextSnapshot? currentContext;
    private Task initializationTask = Task.CompletedTask;
    private string failureMessage = string.Empty;
    private bool initialized;
    private bool attached;
    private int disposed;

    public string SourceId => SourceIdentifier;

    public ConversationParticipantKind Kind => ConversationParticipantKind.Agent;

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
        var state = coordinator.Snapshot();
        var visibleAgents = agents
            .Where(agent => agent.Status == AgentLifecycleStatus.Active && !agent.IsTemplate)
            .Where(agent => currentContext is null || currentContext.CanRead(agent.Id))
            .OrderBy(agent => agent.Name, StringComparer.OrdinalIgnoreCase)
            .Select(MapParticipant)
            .ToArray();
        var active = state.ActiveChats
            .Select(MapActiveChat)
            .ToArray();
        ConversationShellWindowDescriptor[] windows = state.VisibleChat is { } visibleChat
            ? [MapWindow(visibleChat)]
            : [];
        return new(
            visibleAgents,
            active,
            windows,
            BuildStatusBadges(visibleAgents.Length, state.ActiveChats.Count),
            string.IsNullOrWhiteSpace(failureMessage) ? null : failureMessage);
    }

    public async Task HandleParticipantActionAsync(
        ParticipantActionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var agentId = ResolveAgentId(request.ParticipantKey);
        var agent = agents.Single(item => item.Id == agentId);
        if (!busyAgentIds.Add(agentId))
        {
            return;
        }

        RaiseChanged();
        try
        {
            if (request.ActionKey == NewChatActionKey)
            {
                var chat = await coordinator.StartNewChatAsync(agentId, cancellationToken);
                shell.FocusWindow(SourceIdentifier, BuildWindowId(chat.HandleId));
                notificationService.Success("Chat ready", $"Opened a new chat with {agent.Name}.");
                return;
            }

            if (request.ActionKey == HistoryActionKey)
            {
                await OpenHistoryAsync(agent, cancellationToken);
                return;
            }

            throw new InvalidOperationException($"Unsupported Agent participant action '{request.ActionKey.Value}'.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Unable to execute Agent conversation participant action. AgentId={AgentId} ActionKey={ActionKey} FailureType={FailureType}.",
                agentId,
                request.ActionKey.Value,
                exception.GetType().Name);
            notificationService.Error("Unable to open chat", "The Agent chat could not be opened.");
        }
        finally
        {
            busyAgentIds.Remove(agentId);
            RaiseChanged();
        }
    }

    public async Task HandleActiveActionAsync(
        ConversationActionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var handleId = ResolveHandleId(request.ItemKey);
        if (request.ActionKey == OpenActionKey)
        {
            coordinator.ShowChat(handleId);
            shell.FocusWindow(SourceIdentifier, BuildWindowId(handleId));
            return;
        }

        if (request.ActionKey == StopActionKey)
        {
            await RequestCloseAsync(handleId, cancellationToken);
            return;
        }

        throw new InvalidOperationException($"Unsupported Agent active-chat action '{request.ActionKey.Value}'.");
    }

    public async Task HandleWindowCloseAsync(
        string windowId,
        CancellationToken cancellationToken = default)
    {
        var handleId = ResolveWindowId(windowId);
        await RequestCloseAsync(handleId, cancellationToken);
    }

    public static string BuildWindowId(AgentChatHandleId handleId)
        => $"floating-agent-chat-{handleId.Value:N}";

    private async Task InitializeCoreAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed) != 0, this);
        Attach();
        failureMessage = string.Empty;
        currentContext = contextRegistry.Capture();
        try
        {
            await coordinator.InitializeAsync(cancellationToken);
            await LoadAgentsAsync(cancellationToken);
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
                "Unable to initialize the Agent conversation contributor. FailureType={FailureType}.",
                exception.GetType().Name);
            failureMessage = "Agent chats could not be initialized. Retry after checking the active runtime profile.";
        }
        finally
        {
            RaiseChanged();
        }
    }

    private async Task LoadAgentsAsync(CancellationToken cancellationToken)
    {
        var referenceData = await referenceDataProvider.GetAsync(
            new AgentReferenceDataRequest(
                AgentReferenceDataSections.Agents,
                IncludeAgentTemplates: false,
                ActiveAgentsOnly: true),
            cancellationToken);
        agents = referenceData.Agents;
        failureMessage = string.Empty;
    }

    private ConversationShellParticipant MapParticipant(AgentDefinition agent)
    {
        var isBusy = busyAgentIds.Contains(agent.Id);
        var actions = new ParticipantActionPresentation[]
        {
            new(
                NewChatActionKey,
                $"Start a new chat with {agent.Name}",
                "add_comment",
                $"floating-agent-chat-agent-list-new-chat-{agent.Id:N}",
                isBusy),
            new(
                HistoryActionKey,
                $"Open chat history for {agent.Name}",
                "history",
                $"floating-agent-chat-agent-list-history-{agent.Id:N}",
                isBusy,
                ParticipantActionStyle.Light)
        };
        var presentation = AgentParticipantPresentationMapper.MapCompactItem(
            agent,
            isSelected: false,
            isBusy,
            $"floating-agent-chat-agent-list-item-{agent.Id:N}",
            $"floating-agent-chat-agent-list-select-{agent.Id:N}",
            actions,
            ParticipantKey(agent.Id));
        return new(SourceIdentifier, Kind, presentation);
    }

    private ConversationShellActiveItem MapActiveChat(ActiveAgentChat chat)
    {
        var isFocused = shell.Snapshot().FocusedWindow == new ConversationShellWindowKey(
            SourceIdentifier,
            BuildWindowId(chat.HandleId));
        var mapped = AgentActiveChatPresentationMapper.Map(chat, ActiveKey(chat.HandleId), isFocused);
        return new(SourceIdentifier, ConversationParticipantKind.Agent, mapped);
    }

    private ConversationShellWindowDescriptor MapWindow(ActiveAgentChat chat)
        => new(
            new(SourceIdentifier, BuildWindowId(chat.HandleId)),
            ConversationParticipantKind.Agent,
            "floating-agent-chat-window",
            $"Chat with {chat.Agent.Name}",
            "Active agent chat",
            chat.Agent.Name,
            chat.Agent.RoleTitle,
            typeof(AgentFloatingConversationContent),
            new Dictionary<string, object>
            {
                [nameof(AgentFloatingConversationContent.Chat)] = chat,
                [nameof(AgentFloatingConversationContent.PreferredAgent)] = agents.FirstOrDefault(
                    agent => agent.Id == chat.Agent.AgentId)!
            });

    private IReadOnlyList<PresentationBadge> BuildStatusBadges(
        int visibleAgentCount,
        int activeChatCount)
    {
        if (currentContext is not { } context)
        {
            return
            [
                new("No module context", PresentationTone.Default),
                new($"{activeChatCount} active", PresentationTone.Info),
                new($"Kept for {coordinator.CurrentSettings.HiddenActiveChatRetentionMinutes} min", PresentationTone.Default)
            ];
        }

        return
        [
            new(context.Scope.DisplayName, PresentationTone.Info),
            new(
                context.Scope.AccessState.ToString(),
                context.Scope.AccessState == AgentChatContextAccessState.Ready
                    ? PresentationTone.Default
                    : PresentationTone.Warning),
            new($"{visibleAgentCount} available", PresentationTone.Default),
            new($"{activeChatCount} active", PresentationTone.Info),
            new($"Kept for {coordinator.CurrentSettings.HiddenActiveChatRetentionMinutes} min", PresentationTone.Default)
        ];
    }

    private async Task OpenHistoryAsync(AgentDefinition agent, CancellationToken cancellationToken)
    {
        var workspace = await workspaceService.GetChatAgentWorkspaceAsync(
            agent.Id,
            preferredSessionId: null,
            cancellationToken);
        var sessions = workspace.Sessions
            .Take(AgentThreadHistoryDialog.MaxThreadCount)
            .ToArray();
        var activeSessionId = coordinator.Snapshot().ActiveChats
            .FirstOrDefault(chat => chat.Agent.AgentId == agent.Id && chat.IsVisible)
            ?.ChatSessionId;
        var result = await dialogService.OpenAsync<AgentThreadHistoryDialog>(
            "Agent thread history",
            new Dictionary<string, object?>
            {
                [nameof(AgentThreadHistoryDialog.Agent)] = agent,
                [nameof(AgentThreadHistoryDialog.SessionSummaries)] = sessions,
                [nameof(AgentThreadHistoryDialog.SelectedSessionId)] = activeSessionId
            },
            new DialogOptions
            {
                Eyebrow = "Floating agent chat",
                Subtitle = $"{agent.Name} / latest {sessions.Length} thread(s)",
                Size = ModalSize.Wide,
                DenseChrome = true,
                TestId = "floating-agent-chat-history-dialog",
                AriaLabel = "Agent thread history"
            });
        if (result is not Guid sessionId)
        {
            return;
        }

        var chat = await coordinator.OpenChatAsync(agent.Id, sessionId, cancellationToken);
        shell.FocusWindow(SourceIdentifier, BuildWindowId(chat.HandleId));
    }

    private async Task RequestCloseAsync(
        AgentChatHandleId handleId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var chat = coordinator.Snapshot().ActiveChats
            .FirstOrDefault(item => item.HandleId == handleId);
        if (chat is null)
        {
            shell.ClearFocusedWindow(SourceIdentifier, BuildWindowId(handleId));
            return;
        }

        var result = await dialogService.OpenAsync<FloatingAgentChatCloseDialog>(
            "Close active chat",
            new Dictionary<string, object?>
            {
                [nameof(FloatingAgentChatCloseDialog.Chat)] = chat
            },
            new DialogOptions
            {
                Eyebrow = "Floating agent chat",
                Subtitle = chat.Agent.Name,
                Size = ModalSize.Compact,
                DenseChrome = true,
                TestId = "floating-agent-chat-close-confirmation",
                AriaLabel = "Close active agent chat"
            });
        if (result is not FloatingAgentChatCloseDecision decision ||
            decision == FloatingAgentChatCloseDecision.Cancel)
        {
            shell.FocusWindow(SourceIdentifier, BuildWindowId(handleId));
            return;
        }

        if (decision == FloatingAgentChatCloseDecision.Stop && chat.CanStop)
        {
            try
            {
                coordinator.Stop(handleId);
                shell.ClearFocusedWindow(SourceIdentifier, BuildWindowId(handleId));
                return;
            }
            catch (InvalidOperationException exception)
            {
                logger.LogWarning(
                    exception,
                    "Unable to stop Agent chat handle during close. HandleId={HandleId}.",
                    handleId);
                notificationService.Warning("Chat kept active", "The Agent chat could not be stopped yet.");
            }
        }

        if (decision == FloatingAgentChatCloseDecision.KeepActive)
        {
            coordinator.KeepActive(handleId);
            shell.ClearFocusedWindow(SourceIdentifier, BuildWindowId(handleId));
        }
    }

    private void Attach()
    {
        if (attached)
        {
            return;
        }

        attached = true;
        coordinator.Changed += HandleCoordinatorChanged;
        contextRegistry.Changed += HandleContextChanged;
        referenceDataCacheInvalidator.Invalidated += HandleReferenceDataInvalidated;
    }

    private void HandleCoordinatorChanged(object? sender, EventArgs eventArgs)
        => RaiseChanged();

    private void HandleContextChanged(object? sender, EventArgs eventArgs)
    {
        currentContext = contextRegistry.Capture();
        RaiseChanged();
    }

    private void HandleReferenceDataInvalidated(object? sender, EventArgs eventArgs)
    {
        _ = ReloadAgentsAsync();
    }

    private async Task ReloadAgentsAsync()
    {
        try
        {
            await LoadAgentsAsync(lifetime.Token);
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Unable to reload the Agent conversation catalog. FailureType={FailureType}.",
                exception.GetType().Name);
            agents = [];
            failureMessage = "The Agent catalog could not be loaded.";
        }
        finally
        {
            RaiseChanged();
        }
    }

    private void RaiseChanged()
    {
        if (Volatile.Read(ref disposed) == 0)
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    private static ConversationPresentationKey ParticipantKey(Guid agentId)
        => new($"{ParticipantKeyPrefix}{agentId:N}");

    private static ConversationPresentationKey ActiveKey(AgentChatHandleId handleId)
        => new($"{ActiveKeyPrefix}{handleId.Value:N}");

    private static Guid ResolveAgentId(ConversationPresentationKey key)
        => key.Value.StartsWith(ParticipantKeyPrefix, StringComparison.Ordinal) &&
           Guid.TryParseExact(key.Value[ParticipantKeyPrefix.Length..], "N", out var agentId) &&
           agentId != Guid.Empty
            ? agentId
            : throw new ArgumentException($"'{key.Value}' is not an Agent participant key.", nameof(key));

    private static AgentChatHandleId ResolveHandleId(ConversationPresentationKey key)
        => key.Value.StartsWith(ActiveKeyPrefix, StringComparison.Ordinal) &&
           Guid.TryParseExact(key.Value[ActiveKeyPrefix.Length..], "N", out var handleId) &&
           handleId != Guid.Empty
            ? new(handleId)
            : throw new ArgumentException($"'{key.Value}' is not an active Agent chat key.", nameof(key));

    private static AgentChatHandleId ResolveWindowId(string windowId)
    {
        const string prefix = "floating-agent-chat-";
        return windowId.StartsWith(prefix, StringComparison.Ordinal) &&
               Guid.TryParseExact(windowId[prefix.Length..], "N", out var handleId) &&
               handleId != Guid.Empty
            ? new(handleId)
            : throw new ArgumentException($"'{windowId}' is not an Agent chat window id.", nameof(windowId));
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0)
        {
            return;
        }

        if (attached)
        {
            coordinator.Changed -= HandleCoordinatorChanged;
            contextRegistry.Changed -= HandleContextChanged;
            referenceDataCacheInvalidator.Invalidated -= HandleReferenceDataInvalidated;
        }

        await lifetime.CancelAsync();
        lifetime.Dispose();
    }
}
