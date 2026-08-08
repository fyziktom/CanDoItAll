using System.Reflection;
using Bunit;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Modules.AgentFramework.Pages.Components;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components;

public sealed class FloatingAgentChatHostLifecycleTests
{
    [Fact]
    public async Task Pending_initialization_does_not_block_events_and_is_cancelled_on_disposal()
    {
        var coordinator = new PendingCoordinator();
        var contextRegistry = new RecordingContextRegistry();
        var cacheInvalidator = new RecordingCacheInvalidator();
        using var context = CreateContext(coordinator, contextRegistry, cacheInvalidator);

        var cut = context.Render<FloatingAgentChatHost>();

        await coordinator.InitializationStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.NotNull(cut.Find("[data-testid='floating-agent-chat-host']"));
        Assert.Equal(1, coordinator.InitializeCallCount);
        Assert.False(coordinator.InitializationSettled.Task.IsCompleted);

        coordinator.PublishCatalogVisibility(isVisible: true);

        cut.WaitForElement("[data-testid='floating-agent-catalog-window']");
        Assert.Equal(1, coordinator.InitializeCallCount);
        Assert.False(coordinator.InitializationSettled.Task.IsCompleted);

        await cut.Instance.DisposeAsync();
        cut.Dispose();

        await coordinator.InitializationCancelled.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await coordinator.InitializationSettled.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal(0, coordinator.ChangedSubscriberCount);
        Assert.Equal(0, contextRegistry.ChangedSubscriberCount);
        Assert.Equal(0, cacheInvalidator.InvalidatedSubscriberCount);
    }

    private static BunitContext CreateContext(
        PendingCoordinator coordinator,
        RecordingContextRegistry contextRegistry,
        RecordingCacheInvalidator cacheInvalidator)
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddLogging();
        context.Services.AddCanDoItAllBaseLib();
        context.Services.AddSingleton<IFloatingAgentChatCoordinator>(coordinator);
        context.Services.AddSingleton<IAgentChatContextRegistry>(contextRegistry);
        context.Services.AddSingleton<IAgentConversationContextService>(
            new AgentConversationContextService(TimeProvider.System));
        context.Services.AddSingleton<IAgentReferenceDataCacheInvalidator>(cacheInvalidator);
        context.Services.AddSingleton<IAgentReferenceDataProvider, UnexpectedReferenceDataProvider>();
        context.Services.AddSingleton(
            DispatchProxy.Create<IAgentFrameworkWorkspaceService, UnusedWorkspaceServiceProxy>());
        return context;
    }

    private sealed class PendingCoordinator : IFloatingAgentChatCoordinator
    {
        private readonly object gate = new();
        private FloatingAgentChatState state = new(false, AgentChatCatalogTab.Agents, []);
        private EventHandler? changed;
        private int initializeCallCount;

        public TaskCompletionSource InitializationStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource InitializationCancelled { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource InitializationSettled { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int InitializeCallCount => Volatile.Read(ref initializeCallCount);

        public int ChangedSubscriberCount { get; private set; }

        public FloatingAgentChatSettings CurrentSettings => FloatingAgentChatSettings.Default;

        public event EventHandler? Changed
        {
            add
            {
                changed += value;
                ChangedSubscriberCount++;
            }
            remove
            {
                changed -= value;
                ChangedSubscriberCount--;
            }
        }

        public FloatingAgentChatState Snapshot()
        {
            lock (gate)
            {
                return state;
            }
        }

        public async Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref initializeCallCount);
            InitializationStarted.TrySetResult();
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                InitializationCancelled.TrySetResult();
                throw;
            }
            finally
            {
                InitializationSettled.TrySetResult();
            }
        }

        public void PublishCatalogVisibility(bool isVisible)
        {
            lock (gate)
            {
                state = state with { IsCatalogVisible = isVisible };
            }

            changed?.Invoke(this, EventArgs.Empty);
        }

        public void ShowCatalog(AgentChatCatalogTab tab = AgentChatCatalogTab.Agents)
        {
            lock (gate)
            {
                state = state with { IsCatalogVisible = true, CatalogTab = tab };
            }

            changed?.Invoke(this, EventArgs.Empty);
        }

        public void HideCatalog()
            => PublishCatalogVisibility(isVisible: false);

        public Task<ActiveAgentChat> StartNewChatAsync(
            Guid agentId,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<ActiveAgentChat> OpenChatAsync(
            Guid agentId,
            Guid chatSessionId,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public ActiveAgentChat ShowChat(AgentChatHandleId handleId)
            => throw new NotSupportedException();

        public ActiveAgentChat KeepActive(AgentChatHandleId handleId)
            => throw new NotSupportedException();

        public void Stop(AgentChatHandleId handleId)
            => throw new NotSupportedException();

        public ActiveAgentChat SetRunState(
            AgentChatHandleId handleId,
            ActiveAgentChatRunState runState)
            => throw new NotSupportedException();

        public bool TryBeginOperation(AgentChatHandleId handleId)
            => throw new NotSupportedException();

        public void ReconcileRunStateAfterOperation(AgentChatHandleId handleId)
            => throw new NotSupportedException();

        public ActiveAgentChat AttachSession(AgentChatHandleId handleId, Guid chatSessionId)
            => throw new NotSupportedException();

        public int PruneExpired()
            => 0;

        public void ApplySettings(FloatingAgentChatSettings settings)
        {
        }
    }

    private sealed class RecordingContextRegistry : IAgentChatContextRegistry
    {
        private EventHandler? changed;

        public int ChangedSubscriberCount { get; private set; }

        public event EventHandler? Changed
        {
            add
            {
                changed += value;
                ChangedSubscriberCount++;
            }
            remove
            {
                changed -= value;
                ChangedSubscriberCount--;
            }
        }

        public IAgentChatWorkspacePositionLease RegisterWorkspacePosition(
            AgentChatWorkspacePosition position,
            AgentChatNavigationIdentity navigationIdentity)
            => throw new NotSupportedException();

        public IAgentChatContextScopeLease ActivateScope(AgentChatContextScope scope)
            => throw new NotSupportedException();

        public IAgentChatContextFragmentLease RegisterFragment(
            AgentChatContextScopeId scopeId,
            AgentChatContextFragment fragment)
            => throw new NotSupportedException();

        public AgentChatContextSnapshot? Capture()
            => null;

        public ValueTask<AgentChatContextSnapshot?> CaptureAsync(
            CancellationToken cancellationToken = default)
            => ValueTask.FromResult<AgentChatContextSnapshot?>(null);
    }

    private sealed class RecordingCacheInvalidator : IAgentReferenceDataCacheInvalidator
    {
        private EventHandler? invalidated;

        public int InvalidatedSubscriberCount { get; private set; }

        public event EventHandler? Invalidated
        {
            add
            {
                invalidated += value;
                InvalidatedSubscriberCount++;
            }
            remove
            {
                invalidated -= value;
                InvalidatedSubscriberCount--;
            }
        }

        public void Invalidate()
            => invalidated?.Invoke(this, EventArgs.Empty);
    }

    private sealed class UnexpectedReferenceDataProvider : IAgentReferenceDataProvider
    {
        public Task<AgentReferenceDataSnapshot> GetAsync(
            AgentReferenceDataRequest request,
            CancellationToken cancellationToken = default)
            => throw new InvalidOperationException(
                "Agent reference data must not load while host initialization is pending.");
    }

    private class UnusedWorkspaceServiceProxy : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
            => throw new InvalidOperationException(
                $"Workspace service member '{targetMethod?.Name}' was not expected in this component test.");
    }
}
