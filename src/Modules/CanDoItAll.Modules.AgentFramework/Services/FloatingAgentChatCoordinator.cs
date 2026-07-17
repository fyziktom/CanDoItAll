using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.AgentFramework;

public sealed class FloatingAgentChatCoordinator : IFloatingAgentChatCoordinator, IAsyncDisposable
{
    private static readonly TimeSpan PruneInterval = TimeSpan.FromMinutes(1);
    private readonly object gate = new();
    private readonly SemaphoreSlim settingsLoadGate = new(1, 1);
    private readonly CancellationTokenSource lifetimeCts = new();
    private readonly IAgentFrameworkWorkspaceService workspaceService;
    private readonly IActiveAgentChatRegistry activeChatRegistry;
    private readonly IAgentChatPreparationPool preparationPool;
    private readonly IFloatingAgentChatSettingsService settingsService;
    private readonly TimeProvider timeProvider;
    private readonly ILogger<FloatingAgentChatCoordinator> logger;
    private readonly Dictionary<AgentChatHandleId, Guid> activeExecutionRunIds = [];
    private bool isCatalogVisible;
    private AgentChatCatalogTab catalogTab;
    private FloatingAgentChatSettings currentSettings = FloatingAgentChatSettings.Default;
    private long settingsVersion;
    private bool settingsLoaded;
    private Task? pruneLoopTask;
    private bool isDisposed;

    public FloatingAgentChatCoordinator(
        IAgentFrameworkWorkspaceService workspaceService,
        IActiveAgentChatRegistry activeChatRegistry,
        IAgentChatPreparationPool preparationPool,
        IFloatingAgentChatSettingsService settingsService,
        TimeProvider timeProvider,
        ILogger<FloatingAgentChatCoordinator> logger)
    {
        this.workspaceService = workspaceService;
        this.activeChatRegistry = activeChatRegistry;
        this.preparationPool = preparationPool;
        this.settingsService = settingsService;
        this.timeProvider = timeProvider;
        this.logger = logger;
        activeChatRegistry.Changed += HandleActiveChatsChanged;
        workspaceService.ExecutionUpdated += HandleExecutionUpdated;
    }

    public event EventHandler? Changed;

    public FloatingAgentChatSettings CurrentSettings
    {
        get
        {
            lock (gate)
            {
                return currentSettings;
            }
        }
    }

    public FloatingAgentChatState Snapshot()
    {
        bool catalogVisible;
        AgentChatCatalogTab selectedCatalogTab;
        lock (gate)
        {
            catalogVisible = isCatalogVisible;
            selectedCatalogTab = catalogTab;
        }

        return new FloatingAgentChatState(
            catalogVisible,
            selectedCatalogTab,
            activeChatRegistry.Snapshot());
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        await EnsureSettingsLoadedAsync(cancellationToken);
        PruneExpired();
        try
        {
            await preparationPool.WarmAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Unable to warm the floating agent preparation stock. FailureType={FailureType} MaximumPreparedAgents={MaximumPreparedAgents}.",
                exception.GetType().Name,
                CurrentSettings.MaximumPreparedAgents);
        }

        EnsureMaintenanceLoopRunning();
    }

    public void ShowCatalog(AgentChatCatalogTab tab = AgentChatCatalogTab.Agents)
    {
        ThrowIfDisposed();
        lock (gate)
        {
            isCatalogVisible = true;
            catalogTab = tab;
        }

        RaiseChanged();
    }

    public void HideCatalog()
    {
        ThrowIfDisposed();
        lock (gate)
        {
            isCatalogVisible = false;
        }

        RaiseChanged();
    }

    public Task<ActiveAgentChat> StartNewChatAsync(
        Guid agentId,
        CancellationToken cancellationToken = default)
        => OpenChatCoreAsync(agentId, chatSessionId: null, createSession: true, cancellationToken);

    public Task<ActiveAgentChat> OpenChatAsync(
        Guid agentId,
        Guid chatSessionId,
        CancellationToken cancellationToken = default)
    {
        if (chatSessionId == Guid.Empty)
        {
            throw new ArgumentException("A chat session id is required.", nameof(chatSessionId));
        }

        return OpenChatCoreAsync(agentId, chatSessionId, createSession: false, cancellationToken);
    }

    public ActiveAgentChat ShowChat(AgentChatHandleId handleId)
    {
        ThrowIfDisposed();
        return activeChatRegistry.Show(handleId);
    }

    public ActiveAgentChat KeepActive(AgentChatHandleId handleId)
    {
        ThrowIfDisposed();
        return activeChatRegistry.KeepActive(handleId);
    }

    public void Stop(AgentChatHandleId handleId)
    {
        ThrowIfDisposed();
        activeChatRegistry.Stop(handleId);
        lock (gate)
        {
            activeExecutionRunIds.Remove(handleId);
        }
    }

    public ActiveAgentChat SetRunState(
        AgentChatHandleId handleId,
        ActiveAgentChatRunState runState)
    {
        ThrowIfDisposed();
        return activeChatRegistry.SetRunState(handleId, runState);
    }

    public ActiveAgentChat AttachSession(AgentChatHandleId handleId, Guid chatSessionId)
    {
        ThrowIfDisposed();
        return activeChatRegistry.AttachSession(handleId, chatSessionId);
    }

    public int PruneExpired()
    {
        ThrowIfDisposed();
        var removedCount = activeChatRegistry.PruneExpired(CurrentSettings);
        RemoveOrphanedExecutionRunIds();
        return removedCount;
    }

    public void ApplySettings(FloatingAgentChatSettings settings)
    {
        ThrowIfDisposed();
        settings = FloatingAgentChatSettingsValidator.Normalize(settings);
        lock (gate)
        {
            currentSettings = settings;
            settingsLoaded = true;
            settingsVersion++;
        }

        preparationPool.Configure(settings);

        PruneExpired();
        EnsureMaintenanceLoopRunning();
        RaiseChanged();
    }

    public async ValueTask DisposeAsync()
    {
        if (isDisposed)
        {
            return;
        }

        isDisposed = true;
        activeChatRegistry.Changed -= HandleActiveChatsChanged;
        workspaceService.ExecutionUpdated -= HandleExecutionUpdated;
        await lifetimeCts.CancelAsync();
        Task? loopTask;
        lock (gate)
        {
            loopTask = pruneLoopTask;
        }

        if (loopTask is not null)
        {
            await loopTask;
        }

        lifetimeCts.Dispose();
    }

    private async Task<ActiveAgentChat> OpenChatCoreAsync(
        Guid agentId,
        Guid? chatSessionId,
        bool createSession,
        CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        if (agentId == Guid.Empty)
        {
            throw new ArgumentException("An agent id is required.", nameof(agentId));
        }

        await EnsureSettingsLoadedAsync(cancellationToken);
        var agent = await preparationPool.AcquireAsync(agentId, cancellationToken);
        if (agent is null || agent.Status != AgentLifecycleStatus.Active || agent.IsTemplate)
        {
            throw new InvalidOperationException(
                $"Active agent '{agentId:N}' is not available for chat.");
        }

        FloatingAgentChatSettings settings;
        lock (gate)
        {
            settings = currentSettings;
        }

        var identity = new AgentChatIdentity(
            agent.Id,
            agent.Name,
            agent.RoleTitle,
            agent.AvatarImageUrl);
        if (!createSession)
        {
            await workspaceService.GetOrCreateChatSessionAsync(
                agentId,
                chatSessionId,
                cancellationToken);
            return activeChatRegistry.Open(identity, chatSessionId, settings);
        }

        var previouslyVisibleChat = activeChatRegistry.Snapshot()
            .FirstOrDefault(item => item.IsVisible);
        var reservedChat = activeChatRegistry.Open(
            identity,
            chatSessionId: null,
            settings);
        try
        {
            var session = await workspaceService.GetOrCreateChatSessionAsync(
                agentId,
                cancellationToken: cancellationToken);
            return activeChatRegistry.AttachSession(reservedChat.HandleId, session.Id);
        }
        catch
        {
            activeChatRegistry.Stop(
                reservedChat.HandleId,
                previouslyVisibleChat?.HandleId);

            throw;
        }
    }

    private async Task<FloatingAgentChatSettings> EnsureSettingsLoadedAsync(
        CancellationToken cancellationToken)
    {
        lock (gate)
        {
            if (settingsLoaded)
            {
                return currentSettings;
            }
        }

        await settingsLoadGate.WaitAsync(cancellationToken);
        try
        {
            long versionBeforeLoad;
            lock (gate)
            {
                if (settingsLoaded)
                {
                    return currentSettings;
                }

                versionBeforeLoad = settingsVersion;
            }

            var loadedSettings = FloatingAgentChatSettingsValidator.Normalize(
                await settingsService.GetSettingsAsync(cancellationToken));
            lock (gate)
            {
                if (!settingsLoaded && settingsVersion == versionBeforeLoad)
                {
                    currentSettings = loadedSettings;
                    settingsLoaded = true;
                }

                loadedSettings = currentSettings;
            }

            preparationPool.Configure(loadedSettings);
            return loadedSettings;
        }
        finally
        {
            settingsLoadGate.Release();
        }
    }

    private void HandleActiveChatsChanged(object? sender, EventArgs eventArgs)
    {
        EnsureMaintenanceLoopRunning();
        RaiseChanged();
    }

    private void HandleExecutionUpdated(object? sender, ExecutionLogEntry entry)
    {
        if (!entry.ChatSessionId.HasValue)
        {
            return;
        }

        var chat = activeChatRegistry.Snapshot().FirstOrDefault(item =>
            item.Agent.AgentId == entry.AgentId &&
            item.ChatSessionId == entry.ChatSessionId);
        if (chat is null)
        {
            return;
        }

        var runState = entry.State switch
        {
            ExecutionState.Preparing or
            ExecutionState.Running or
            ExecutionState.Persisting => ActiveAgentChatRunState.Running,
            ExecutionState.WaitingOnTool => ActiveAgentChatRunState.AwaitingApproval,
            _ => ActiveAgentChatRunState.Idle
        };
        lock (gate)
        {
            if (runState != ActiveAgentChatRunState.Idle && entry.ExecutionRunId != Guid.Empty)
            {
                activeExecutionRunIds[chat.HandleId] = entry.ExecutionRunId;
            }
            else if (entry.ExecutionRunId != Guid.Empty &&
                     activeExecutionRunIds.TryGetValue(chat.HandleId, out var activeRunId) &&
                     activeRunId != entry.ExecutionRunId)
            {
                return;
            }
            else if (runState == ActiveAgentChatRunState.Idle)
            {
                activeExecutionRunIds.Remove(chat.HandleId);
            }
        }

        try
        {
            activeChatRegistry.SetRunState(chat.HandleId, runState);
        }
        catch (InvalidOperationException exception)
        {
            logger.LogDebug(
                exception,
                "Skipped an execution update for an agent chat handle that is no longer active. HandleId={HandleId} ExecutionRunId={ExecutionRunId} State={ExecutionState}.",
                chat.HandleId,
                entry.ExecutionRunId,
                entry.State);
        }
    }

    private async Task RunPruneLoopAsync(CancellationToken cancellationToken)
    {
        await Task.Yield();
        using var timer = new PeriodicTimer(PruneInterval, timeProvider);
        try
        {
            while (HasMaintenanceWork() &&
                   await timer.WaitForNextTickAsync(cancellationToken))
            {
                activeChatRegistry.PruneExpired(CurrentSettings);
                preparationPool.PruneExpired();
                RemoveOrphanedExecutionRunIds();
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        finally
        {
            lock (gate)
            {
                pruneLoopTask = null;
            }

            if (!isDisposed && HasMaintenanceWork())
            {
                EnsureMaintenanceLoopRunning();
            }
        }
    }

    private void EnsureMaintenanceLoopRunning()
    {
        if (isDisposed || !HasMaintenanceWork())
        {
            return;
        }

        lock (gate)
        {
            if (pruneLoopTask is null or { IsCompleted: true })
            {
                pruneLoopTask = RunPruneLoopAsync(lifetimeCts.Token);
            }
        }
    }

    private bool HasMaintenanceWork()
    {
        return activeChatRegistry.Snapshot().Any(chat => !chat.IsVisible) ||
               preparationPool.HasPreparedEntries;
    }

    private void RemoveOrphanedExecutionRunIds()
    {
        var activeHandleIds = activeChatRegistry.Snapshot()
            .Select(item => item.HandleId)
            .ToHashSet();
        lock (gate)
        {
            foreach (var handleId in activeExecutionRunIds.Keys
                         .Where(id => !activeHandleIds.Contains(id))
                         .ToArray())
            {
                activeExecutionRunIds.Remove(handleId);
            }
        }
    }

    private void RaiseChanged()
        => Changed?.Invoke(this, EventArgs.Empty);

    private void ThrowIfDisposed()
        => ObjectDisposedException.ThrowIf(isDisposed, this);
}
