using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Components;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Components.OverlayLib;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.AgentFramework.Pages.Components;

public partial class FloatingAgentChatHost
{
    [Inject]
    public IFloatingAgentChatCoordinator Coordinator { get; set; } = default!;

    [Inject]
    public IAgentChatContextRegistry ContextRegistry { get; set; } = default!;

    [Inject]
    public IAgentFrameworkWorkspaceService WorkspaceService { get; set; } = default!;

    [Inject]
    public IAgentReferenceDataProvider ReferenceDataProvider { get; set; } = default!;

    [Inject]
    public IAgentReferenceDataCacheInvalidator ReferenceDataCacheInvalidator { get; set; } = default!;

    [Inject]
    public DialogService DialogService { get; set; } = default!;

    [Inject]
    public NotificationService NotificationService { get; set; } = default!;

    [Inject]
    public ILogger<FloatingAgentChatHost> Logger { get; set; } = default!;

    private readonly CancellationTokenSource disposalCts = new();
    private readonly Dictionary<AgentChatHandleId, OverlayWindowState> chatWindowStates = [];
    private readonly HashSet<Guid> busyAgentIds = [];
    private OverlayWindowState catalogWindowState = new() { IsVisible = true };
    private IReadOnlyList<AgentDefinition> agents = [];
    private IReadOnlyList<AgentDefinition> visibleAgents = [];
    private AgentChatContextSnapshot? currentContext;
    private FloatingAgentChatState state = new(false, AgentChatCatalogTab.Agents, []);
    private Guid? selectedAgentId;
    private string searchText = string.Empty;
    private string initializationError = string.Empty;
    private string loadError = string.Empty;
    private long agentCatalogGeneration;
    private bool hasLoadedAgents;
    private bool isDisposed;
    private bool isInitializing;
    private bool isLoadingAgents;
    private bool reloadAgentsRequested;

    private FloatingAgentChatState State => state;

    private AgentChatContextSnapshot? CurrentContext => currentContext;

    private IReadOnlyList<AgentDefinition> VisibleAgents => visibleAgents;

    private int SelectedCatalogTabIndex
        => State.CatalogTab == AgentChatCatalogTab.ActiveChats ? 1 : 0;

    protected override async Task OnInitializedAsync()
    {
        Coordinator.Changed += HandleCoordinatorChanged;
        ContextRegistry.Changed += HandleContextChanged;
        ReferenceDataCacheInvalidator.Invalidated += HandleReferenceDataInvalidated;
        RefreshContextAndVisibleAgents();
        await InitializeHostAsync();
    }

    private async Task InitializeHostAsync()
    {
        if (isInitializing || isDisposed)
        {
            return;
        }

        isInitializing = true;
        initializationError = string.Empty;
        var preserveCatalogVisibility = State.IsCatalogVisible;
        try
        {
            await Coordinator.InitializeAsync(disposalCts.Token);
            if (preserveCatalogVisibility && !Coordinator.Snapshot().IsCatalogVisible)
            {
                Coordinator.ShowCatalog();
            }

            SynchronizeState();
            if (State.IsCatalogVisible)
            {
                await LoadAgentsAsync();
            }
        }
        catch (OperationCanceledException) when (disposalCts.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            Logger.LogError(
                exception,
                "Unable to initialize the floating agent chat host. FailureType={FailureType}.",
                exception.GetType().Name);
            initializationError = "Floating agent chats could not be initialized. Retry to load the current settings and agent catalog.";
            state = Coordinator.Snapshot() with { IsCatalogVisible = true };
        }
        finally
        {
            isInitializing = false;
        }
    }

    private async Task LoadAgentsAsync()
    {
        if (isLoadingAgents)
        {
            reloadAgentsRequested = true;
            return;
        }

        do
        {
            reloadAgentsRequested = false;
            isLoadingAgents = true;
            loadError = string.Empty;
            var loadGeneration = Volatile.Read(ref agentCatalogGeneration);
            try
            {
                var referenceData = await ReferenceDataProvider.GetAsync(
                    new AgentReferenceDataRequest(
                        AgentReferenceDataSections.Agents,
                        IncludeAgentTemplates: false,
                        ActiveAgentsOnly: true),
                    disposalCts.Token);
                if (loadGeneration != Volatile.Read(ref agentCatalogGeneration))
                {
                    reloadAgentsRequested = true;
                    continue;
                }

                agents = referenceData.Agents;
                hasLoadedAgents = true;
                RefreshVisibleAgents();
            }
            catch (OperationCanceledException) when (disposalCts.IsCancellationRequested)
            {
            }
            catch (Exception exception)
            {
                if (loadGeneration != Volatile.Read(ref agentCatalogGeneration))
                {
                    reloadAgentsRequested = true;
                    continue;
                }

                loadError = exception.Message;
            }
            finally
            {
                isLoadingAgents = false;
            }
        }
        while (reloadAgentsRequested && !disposalCts.IsCancellationRequested);
    }

    private void SelectAgent(Guid agentId)
        => selectedAgentId = agentId;

    private bool IsAgentBusy(Guid agentId)
        => busyAgentIds.Contains(agentId);

    private async Task StartNewChatAsync(AgentDefinition agent)
    {
        selectedAgentId = agent.Id;
        if (!busyAgentIds.Add(agent.Id))
        {
            return;
        }

        try
        {
            await Coordinator.StartNewChatAsync(agent.Id, disposalCts.Token);
            NotificationService.Success("Chat ready", $"Opened a new chat with {agent.Name}.");
        }
        catch (OperationCanceledException) when (disposalCts.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            NotificationService.Error("Unable to open chat", exception.Message);
        }
        finally
        {
            busyAgentIds.Remove(agent.Id);
        }
    }

    private async Task OpenHistoryAsync(AgentDefinition agent)
    {
        selectedAgentId = agent.Id;
        if (!busyAgentIds.Add(agent.Id))
        {
            return;
        }

        IReadOnlyList<ChatSessionSummaryRecord> sessions;
        try
        {
            var workspace = await WorkspaceService.GetChatAgentWorkspaceAsync(
                agent.Id,
                preferredSessionId: null,
                disposalCts.Token);
            sessions = workspace.Sessions
                .Take(AgentThreadHistoryDialog.MaxThreadCount)
                .ToArray();
        }
        catch (OperationCanceledException) when (disposalCts.IsCancellationRequested)
        {
            return;
        }
        catch (Exception exception)
        {
            NotificationService.Error("Unable to load thread history", exception.Message);
            return;
        }
        finally
        {
            busyAgentIds.Remove(agent.Id);
        }

        var activeSessionId = State.ActiveChats
            .FirstOrDefault(chat => chat.Agent.AgentId == agent.Id && chat.IsVisible)
            ?.ChatSessionId;
        var result = await DialogService.OpenAsync<AgentThreadHistoryDialog>(
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
                Subtitle = $"{agent.Name} / latest {sessions.Count} thread(s)",
                Size = ModalSize.Wide,
                DenseChrome = true,
                TestId = "floating-agent-chat-history-dialog",
                AriaLabel = "Agent thread history"
            });

        if (result is not Guid sessionId)
        {
            return;
        }

        try
        {
            await Coordinator.OpenChatAsync(agent.Id, sessionId, disposalCts.Token);
        }
        catch (OperationCanceledException) when (disposalCts.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            NotificationService.Error("Unable to open thread", exception.Message);
        }
    }

    private Task HandleSearchChangedAsync(string? value)
    {
        searchText = value?.Trim() ?? string.Empty;
        RefreshVisibleAgents();
        return Task.CompletedTask;
    }

    private void RetryContextAccess()
        => ReferenceDataCacheInvalidator.Invalidate();

    private Task HandleCatalogTabChangedAsync(int selectedIndex)
    {
        Coordinator.ShowCatalog(
            selectedIndex == 1
                ? AgentChatCatalogTab.ActiveChats
                : AgentChatCatalogTab.Agents);
        return Task.CompletedTask;
    }

    private Task HandleCatalogWindowStateChangedAsync(OverlayWindowState value)
    {
        catalogWindowState = OverlayWindowState.Normalize(value);
        if (!catalogWindowState.IsVisible)
        {
            Coordinator.HideCatalog();
        }

        return Task.CompletedTask;
    }

    private OverlayWindowState ResolveCatalogWindowState()
    {
        catalogWindowState = OverlayWindowState.Normalize(catalogWindowState);
        catalogWindowState.IsVisible = true;
        return catalogWindowState;
    }

    private OverlayWindowState ResolveChatWindowState(AgentChatHandleId handleId)
    {
        if (!chatWindowStates.TryGetValue(handleId, out var windowState))
        {
            windowState = new OverlayWindowState { IsVisible = true };
        }

        windowState = OverlayWindowState.Normalize(windowState);
        windowState.IsVisible = true;
        chatWindowStates[handleId] = windowState;
        return windowState;
    }

    private async Task HandleChatWindowStateChangedAsync(
        AgentChatHandleId handleId,
        OverlayWindowState value)
    {
        var normalized = OverlayWindowState.Normalize(value);
        var closeRequested = !normalized.IsVisible;
        normalized.IsVisible = true;
        chatWindowStates[handleId] = normalized;
        if (closeRequested)
        {
            await RequestCloseAsync(handleId);
        }
    }

    private async Task RequestCloseAsync(AgentChatHandleId handleId)
    {
        var chat = Coordinator.Snapshot().ActiveChats
            .FirstOrDefault(item => item.HandleId == handleId);
        if (chat is null)
        {
            return;
        }

        var result = await DialogService.OpenAsync<FloatingAgentChatCloseDialog>(
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

        if (result is not FloatingAgentChatCloseDecision decision
            || decision == FloatingAgentChatCloseDecision.Cancel)
        {
            return;
        }

        if (decision == FloatingAgentChatCloseDecision.Stop && chat.CanStop)
        {
            try
            {
                Coordinator.Stop(handleId);
                chatWindowStates.Remove(handleId);
                return;
            }
            catch (InvalidOperationException exception)
            {
                NotificationService.Warning("Chat kept active", exception.Message);
            }
        }

        if (decision == FloatingAgentChatCloseDecision.KeepActive)
        {
            Coordinator.KeepActive(handleId);
        }
    }

    private void ShowChat(AgentChatHandleId handleId)
        => Coordinator.ShowChat(handleId);

    private bool MatchesSearch(AgentDefinition agent)
    {
        if (string.IsNullOrWhiteSpace(searchText))
        {
            return true;
        }

        return agent.Name.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
               agent.RoleTitle.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
               agent.Tags.Any(tag => tag.Contains(searchText, StringComparison.OrdinalIgnoreCase));
    }

    private void HandleCoordinatorChanged(object? sender, EventArgs eventArgs)
    {
        if (isDisposed)
        {
            return;
        }

        _ = InvokeAsync(async () =>
        {
            if (isDisposed)
            {
                return;
            }

            SynchronizeState();
            if (State.IsCatalogVisible && !hasLoadedAgents && string.IsNullOrWhiteSpace(loadError))
            {
                await LoadAgentsAsync();
            }

            StateHasChanged();
        });
    }

    private void HandleContextChanged(object? sender, EventArgs eventArgs)
    {
        if (isDisposed)
        {
            return;
        }

        _ = InvokeAsync(() =>
        {
            RefreshContextAndVisibleAgents();
            StateHasChanged();
        });
    }

    private void HandleReferenceDataInvalidated(object? sender, EventArgs eventArgs)
    {
        if (isDisposed)
        {
            return;
        }

        Interlocked.Increment(ref agentCatalogGeneration);
        _ = InvokeAsync(async () =>
        {
            if (isDisposed)
            {
                return;
            }

            agents = [];
            hasLoadedAgents = false;
            loadError = string.Empty;
            RefreshVisibleAgents();
            if (State.IsCatalogVisible && string.IsNullOrWhiteSpace(initializationError))
            {
                await LoadAgentsAsync();
            }

            StateHasChanged();
        });
    }

    private void SynchronizeState()
    {
        state = Coordinator.Snapshot();
        RemoveOrphanedWindowStates();
    }

    private void RefreshContextAndVisibleAgents()
    {
        currentContext = ContextRegistry.Capture();
        RefreshVisibleAgents();
    }

    private void RefreshVisibleAgents()
    {
        visibleAgents = agents
            .Where(agent => agent.Status == AgentLifecycleStatus.Active && !agent.IsTemplate)
            .Where(agent => currentContext is null || currentContext.CanRead(agent.Id))
            .Where(MatchesSearch)
            .OrderBy(agent => agent.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private void RemoveOrphanedWindowStates()
    {
        var activeHandleIds = State.ActiveChats.Select(item => item.HandleId).ToHashSet();
        foreach (var handleId in chatWindowStates.Keys.Where(id => !activeHandleIds.Contains(id)).ToArray())
        {
            chatWindowStates.Remove(handleId);
        }
    }

    private static string BuildChatWindowId(AgentChatHandleId handleId)
        => $"floating-agent-chat-{handleId}";

    private static string ResolveRunStateLabel(ActiveAgentChatRunState runState)
        => runState switch
        {
            ActiveAgentChatRunState.Running => "Running",
            ActiveAgentChatRunState.AwaitingApproval => "Awaiting approval",
            _ => "Ready"
        };

    private static string ResolveRunStateTone(ActiveAgentChatRunState runState)
        => runState switch
        {
            ActiveAgentChatRunState.Running => "info",
            ActiveAgentChatRunState.AwaitingApproval => "warning",
            _ => "success"
        };

    public async ValueTask DisposeAsync()
    {
        isDisposed = true;
        Coordinator.Changed -= HandleCoordinatorChanged;
        ContextRegistry.Changed -= HandleContextChanged;
        ReferenceDataCacheInvalidator.Invalidated -= HandleReferenceDataInvalidated;
        await disposalCts.CancelAsync();
        disposalCts.Dispose();
    }
}
