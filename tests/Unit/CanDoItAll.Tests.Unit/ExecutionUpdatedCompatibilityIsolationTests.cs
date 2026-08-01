using System.Collections.Concurrent;
using System.Reflection;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.SharedKernel.Streaming;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Tests.Unit;

public sealed class ExecutionUpdatedCompatibilityIsolationTests
{
    [Fact]
    public async Task Current_profile_relay_invokes_every_subscriber_and_logs_non_sensitive_event_identity()
    {
        var workspace = CreateWorkspaceProxy();
        var factory = new SwitchingWorkspaceFactory(workspace.Service);
        var switchNotifications = new TrackingDatabaseSwitchNotificationService();
        var loggerProvider = new CapturingLoggerProvider();
        using var serviceProvider = new ServiceCollection()
            .AddLogging(builder => builder.AddProvider(loggerProvider))
            .BuildServiceProvider();
        var instance = CreateCurrentProfileService(factory, switchNotifications, serviceProvider);
        var service = Assert.IsAssignableFrom<IAgentFrameworkWorkspaceService>(instance);
        var executionRunId = Guid.NewGuid();
        var agentId = Guid.NewGuid();
        var chatSessionId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        const string sensitiveMessage = "sensitive-provider-payload";
        var entry = new ExecutionLogEntry(
            eventId,
            agentId,
            chatSessionId,
            DateTimeOffset.UtcNow,
            ExecutionState.Running,
            "Provider invocation",
            sensitiveMessage)
        {
            ExecutionRunId = executionRunId
        };
        var laterSubscriberObserved = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);

        service.ExecutionUpdated += (_, _) => throw new InvalidOperationException("Expected subscriber failure.");
        service.ExecutionUpdated += (_, observedEntry) =>
        {
            Assert.Same(entry, observedEntry);
            laterSubscriberObserved.TrySetResult();
        };

        workspace.Proxy.Raise(entry);

        await laterSubscriberObserved.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var warning = await loggerProvider.WarningLogged.Task.WaitAsync(
            TimeSpan.FromSeconds(5));
        Assert.IsType<InvalidOperationException>(warning.Exception);
        Assert.Contains(executionRunId.ToString(), warning.Message, StringComparison.Ordinal);
        Assert.Contains(agentId.ToString(), warning.Message, StringComparison.Ordinal);
        Assert.Contains(chatSessionId.ToString(), warning.Message, StringComparison.Ordinal);
        Assert.Contains(eventId.ToString(), warning.Message, StringComparison.Ordinal);
        Assert.Contains("Provider invocation", warning.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(ExecutionState.Running), warning.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(sensitiveMessage, warning.Message, StringComparison.Ordinal);

        Assert.IsAssignableFrom<IDisposable>(instance).Dispose();
    }

    [Fact]
    public async Task Current_profile_relay_switches_subscription_on_notification_without_a_followup_workspace_call()
    {
        var firstWorkspace = CreateWorkspaceProxy();
        var secondWorkspace = CreateWorkspaceProxy();
        var factory = new SwitchingWorkspaceFactory(firstWorkspace.Service);
        var switchNotifications = new TrackingDatabaseSwitchNotificationService();
        using var serviceProvider = new ServiceCollection()
            .AddLogging()
            .BuildServiceProvider();
        var instance = CreateCurrentProfileService(factory, switchNotifications, serviceProvider);
        var service = Assert.IsAssignableFrom<IAgentFrameworkWorkspaceService>(instance);
        var disposable = Assert.IsAssignableFrom<IDisposable>(instance);
        var observedEvents = 0;
        var switchedWorkspaceEventObserved = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        EventHandler<ExecutionLogEntry> firstSubscriber = (_, _) =>
            ObserveSwitchedWorkspaceEvent();
        EventHandler<ExecutionLogEntry> secondSubscriber = (_, _) =>
            ObserveSwitchedWorkspaceEvent();

        service.ExecutionUpdated += firstSubscriber;
        service.ExecutionUpdated += secondSubscriber;

        Assert.Equal(1, firstWorkspace.Proxy.AddCount);
        Assert.Equal(0, firstWorkspace.Proxy.RemoveCount);
        Assert.Equal(1, switchNotifications.SubscriberCount);

        factory.Current = secondWorkspace.Service;
        switchNotifications.Publish(new DatabaseProfileChangedNotification(
            Guid.NewGuid(),
            "previous",
            Guid.NewGuid(),
            "current",
            2));

        Assert.Equal(1, firstWorkspace.Proxy.RemoveCount);
        Assert.Equal(1, secondWorkspace.Proxy.AddCount);
        firstWorkspace.Proxy.Raise(CreateEntry());
        secondWorkspace.Proxy.Raise(CreateEntry());
        await switchedWorkspaceEventObserved.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(2, Volatile.Read(ref observedEvents));

        service.ExecutionUpdated -= firstSubscriber;

        Assert.Equal(0, secondWorkspace.Proxy.RemoveCount);

        service.ExecutionUpdated -= secondSubscriber;

        Assert.Equal(1, secondWorkspace.Proxy.RemoveCount);
        secondWorkspace.Proxy.Raise(CreateEntry());
        Assert.Equal(2, Volatile.Read(ref observedEvents));

        service.ExecutionUpdated += firstSubscriber;
        Assert.Equal(2, secondWorkspace.Proxy.AddCount);

        disposable.Dispose();
        disposable.Dispose();

        Assert.Equal(2, secondWorkspace.Proxy.RemoveCount);
        Assert.Equal(0, switchNotifications.SubscriberCount);
        secondWorkspace.Proxy.Raise(CreateEntry());
        Assert.Equal(2, Volatile.Read(ref observedEvents));
        Assert.Throws<ObjectDisposedException>(() => service.ExecutionUpdated += firstSubscriber);

        void ObserveSwitchedWorkspaceEvent()
        {
            if (Interlocked.Increment(ref observedEvents) == 2)
            {
                switchedWorkspaceEventObserved.TrySetResult();
            }
        }
    }

    [Fact]
    public async Task Profile_change_cannot_be_overwritten_by_a_stale_concurrent_workspace_resolution()
    {
        var firstWorkspace = CreateWorkspaceProxy();
        var secondWorkspace = CreateWorkspaceProxy();
        var factory = new SwitchingWorkspaceFactory(firstWorkspace.Service);
        var switchNotifications = new TrackingDatabaseSwitchNotificationService();
        using var serviceProvider = new ServiceCollection()
            .AddLogging()
            .BuildServiceProvider();
        var instance = CreateCurrentProfileService(factory, switchNotifications, serviceProvider);
        var service = Assert.IsAssignableFrom<IAgentFrameworkWorkspaceService>(instance);
        var observedEvents = 0;
        var currentWorkspaceEventObserved = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        EventHandler<ExecutionLogEntry> firstSubscriber = (_, _) =>
            ObserveCurrentWorkspaceEvent();
        EventHandler<ExecutionLogEntry> secondSubscriber = (_, _) =>
            ObserveCurrentWorkspaceEvent();
        service.ExecutionUpdated += firstSubscriber;
        var resolutionBlock = factory.BlockNextResolution();
        var addTask = Task.Run(() =>
        {
            service.ExecutionUpdated += secondSubscriber;
        });

        await resolutionBlock.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        factory.Current = secondWorkspace.Service;
        var switchTask = Task.Run(() => switchNotifications.Publish(new DatabaseProfileChangedNotification(
            Guid.NewGuid(),
            "previous",
            Guid.NewGuid(),
            "current",
            2)));

        await switchNotifications.PublishStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        resolutionBlock.Release.TrySetResult(true);
        await Task.WhenAll(addTask, switchTask).WaitAsync(TimeSpan.FromSeconds(5));

        firstWorkspace.Proxy.Raise(CreateEntry());
        Assert.Equal(0, Volatile.Read(ref observedEvents));

        secondWorkspace.Proxy.Raise(CreateEntry());
        await currentWorkspaceEventObserved.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(2, Volatile.Read(ref observedEvents));
        Assert.Equal(1, firstWorkspace.Proxy.RemoveCount);
        Assert.Equal(1, secondWorkspace.Proxy.AddCount);

        Assert.IsAssignableFrom<IDisposable>(instance).Dispose();

        void ObserveCurrentWorkspaceEvent()
        {
            if (Interlocked.Increment(ref observedEvents) == 2)
            {
                currentWorkspaceEventObserved.TrySetResult();
            }
        }
    }

    [Fact]
    public async Task Blocked_current_profile_subscriber_does_not_delay_relay_or_terminal_delivery()
    {
        var workspace = CreateWorkspaceProxy();
        var factory = new SwitchingWorkspaceFactory(workspace.Service);
        var switchNotifications = new TrackingDatabaseSwitchNotificationService();
        using var serviceProvider = new ServiceCollection()
            .AddLogging()
            .BuildServiceProvider();
        var instance = CreateCurrentProfileService(
            factory,
            switchNotifications,
            serviceProvider);
        var service = Assert.IsAssignableFrom<IAgentFrameworkWorkspaceService>(instance);
        var firstSubscriberEntered = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirstSubscriber = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var firstSubscriberTerminalObserved = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var laterSubscriberTerminalObserved = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var canonicalRuntimeCompleted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var canonicalTerminalCompleted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var firstSubscriberCallCount = 0;
        var executionRunId = Guid.NewGuid();
        var runtimeEntry = CreateEntry() with
        {
            ExecutionRunId = executionRunId
        };
        var terminalEntry = CreateEntry() with
        {
            ExecutionRunId = executionRunId,
            State = ExecutionState.Completed,
            Phase = "Terminal"
        };

        service.ExecutionUpdated += (_, entry) =>
        {
            if (Interlocked.Increment(ref firstSubscriberCallCount) == 1)
            {
                firstSubscriberEntered.TrySetResult();
                releaseFirstSubscriber.Task.GetAwaiter().GetResult();
            }

            if (entry.State == ExecutionState.Completed)
            {
                firstSubscriberTerminalObserved.TrySetResult();
            }
        };
        service.ExecutionUpdated += (_, entry) =>
        {
            if (entry.State == ExecutionState.Completed)
            {
                laterSubscriberTerminalObserved.TrySetResult();
            }
        };

        var relay = Task.Run(() =>
        {
            workspace.Proxy.Raise(runtimeEntry);
            canonicalRuntimeCompleted.TrySetResult();
            workspace.Proxy.Raise(terminalEntry);
            canonicalTerminalCompleted.TrySetResult();
        });

        await relay.WaitAsync(TimeSpan.FromSeconds(5));
        await firstSubscriberEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await laterSubscriberTerminalObserved.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(canonicalRuntimeCompleted.Task.IsCompletedSuccessfully);
        Assert.True(canonicalTerminalCompleted.Task.IsCompletedSuccessfully);
        Assert.False(firstSubscriberTerminalObserved.Task.IsCompleted);

        releaseFirstSubscriber.TrySetResult();
        await firstSubscriberTerminalObserved.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.IsAssignableFrom<IDisposable>(instance).Dispose();
    }

    [Fact]
    public async Task Profile_switch_discards_old_workspace_events_queued_behind_blocked_subscriber()
    {
        var firstWorkspace = CreateWorkspaceProxy();
        var secondWorkspace = CreateWorkspaceProxy();
        var factory = new SwitchingWorkspaceFactory(firstWorkspace.Service);
        var switchNotifications = new TrackingDatabaseSwitchNotificationService();
        using var serviceProvider = new ServiceCollection()
            .AddLogging()
            .BuildServiceProvider();
        var instance = CreateCurrentProfileService(
            factory,
            switchNotifications,
            serviceProvider);
        var service = Assert.IsAssignableFrom<IAgentFrameworkWorkspaceService>(
            instance);
        var subscriberEntered = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseSubscriber = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var staleEventObserved = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var currentEventObserved = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var blockingEntry = CreateEntry();
        var staleEntry = CreateEntry();
        var currentEntry = CreateEntry();

        service.ExecutionUpdated += (_, entry) =>
        {
            if (entry.Id == blockingEntry.Id)
            {
                subscriberEntered.TrySetResult();
                releaseSubscriber.Task.GetAwaiter().GetResult();
            }

            if (entry.Id == staleEntry.Id)
            {
                staleEventObserved.TrySetResult();
            }

            if (entry.Id == currentEntry.Id)
            {
                currentEventObserved.TrySetResult();
            }
        };

        try
        {
            firstWorkspace.Proxy.Raise(blockingEntry);
            await subscriberEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            firstWorkspace.Proxy.Raise(staleEntry);

            factory.Current = secondWorkspace.Service;
            switchNotifications.Publish(new DatabaseProfileChangedNotification(
                Guid.NewGuid(),
                "previous",
                Guid.NewGuid(),
                "current",
                2));
            secondWorkspace.Proxy.Raise(currentEntry);

            releaseSubscriber.TrySetResult();
            await currentEventObserved.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(staleEventObserved.Task.IsCompleted);
        }
        finally
        {
            releaseSubscriber.TrySetResult();
            Assert.IsAssignableFrom<IDisposable>(instance).Dispose();
        }
    }

    private static object CreateCurrentProfileService(
        ICanDoItAllAgentWorkspaceFactory workspaceFactory,
        IDatabaseSwitchNotificationService databaseSwitchNotificationService,
        IServiceProvider serviceProvider)
    {
        var implementationType = typeof(AgentFrameworkModuleServiceCollectionExtensions).Assembly.GetType(
            "CanDoItAll.Modules.AgentFramework.CurrentProfileAgentFrameworkWorkspaceService",
            throwOnError: true)!;
        var loggerType = typeof(ILogger<>).MakeGenericType(implementationType);
        var logger = serviceProvider.GetRequiredService(loggerType);
        var technicalAgentBridge = DispatchProxy.Create<IAiTechnicalAgentBridge, UnusedTechnicalAgentBridgeProxy>();
        var constructor = Assert.Single(implementationType.GetConstructors());
        var timeProvider = TimeProvider.System;
        var coordinator = new AgentExecutionActivityCoordinator(
            new PartitionedSequencedStream<
                AgentExecutionActivityStreamId,
                AgentExecutionActivity>(
                PartitionedSequencedStreamPolicy.Default,
                timeProvider),
            timeProvider);

        return constructor.Invoke(
            [
                workspaceFactory,
                technicalAgentBridge,
                new NoOpReferenceDataCacheInvalidator(),
                new StubDatabaseProfileRuntimeAccessor(),
                databaseSwitchNotificationService,
                coordinator,
                new FixedAgentExecutionProfileGenerationSource(
                    new DatabaseProfileGeneration(0)),
                logger
            ]);
    }

    private static (IAgentFrameworkWorkspaceService Service, WorkspaceServiceProxy Proxy) CreateWorkspaceProxy()
    {
        var service = DispatchProxy.Create<IAgentFrameworkWorkspaceService, WorkspaceServiceProxy>();
        return (service, (WorkspaceServiceProxy)(object)service);
    }

    private static ExecutionLogEntry CreateEntry()
    {
        return new ExecutionLogEntry(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            ExecutionState.Running,
            "Test",
            "Test")
        {
            ExecutionRunId = Guid.NewGuid()
        };
    }

    private sealed class SwitchingWorkspaceFactory(IAgentFrameworkWorkspaceService current)
        : ICanDoItAllAgentWorkspaceFactory
    {
        private readonly WorkspaceScopeDescriptor scope = WorkspaceScopeDescriptor.Organization("test");
        private ResolutionBlock? nextResolutionBlock;

        public IAgentFrameworkWorkspaceService Current { get; set; } = current;

        public IAgentFrameworkWorkspaceService GetOrganizationWorkspaceService() => Current;

        public IAgentFrameworkWorkspaceService GetWorkspaceService(WorkspaceScopeDescriptor requestedScope)
        {
            var resolvedService = Current;
            var resolutionBlock = Interlocked.Exchange(ref nextResolutionBlock, null);
            if (resolutionBlock is not null)
            {
                resolutionBlock.Entered.TrySetResult(true);
                resolutionBlock.Release.Task.GetAwaiter().GetResult();
            }

            return resolvedService;
        }

        public WorkspaceScopeDescriptor GetOrganizationScope() => scope;

        public string GetWorkspaceRoot() => string.Empty;

        public ResolutionBlock BlockNextResolution()
        {
            var resolutionBlock = new ResolutionBlock();
            Assert.Null(Interlocked.CompareExchange(
                ref nextResolutionBlock,
                resolutionBlock,
                null));
            return resolutionBlock;
        }
    }

    private sealed class ResolutionBlock
    {
        public TaskCompletionSource<bool> Entered { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource<bool> Release { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    public class WorkspaceServiceProxy : DispatchProxy
    {
        private EventHandler<ExecutionLogEntry>? executionUpdated;

        public int AddCount { get; private set; }

        public int RemoveCount { get; private set; }

        public void Raise(ExecutionLogEntry entry)
        {
            executionUpdated?.Invoke(this, entry);
        }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            ArgumentNullException.ThrowIfNull(targetMethod);

            switch (targetMethod.Name)
            {
                case "add_ExecutionUpdated":
                    executionUpdated += Assert.IsType<EventHandler<ExecutionLogEntry>>(Assert.Single(args!));
                    AddCount++;
                    return null;
                case "remove_ExecutionUpdated":
                    executionUpdated -= Assert.IsType<EventHandler<ExecutionLogEntry>>(Assert.Single(args!));
                    RemoveCount++;
                    return null;
                case nameof(IAgentFrameworkWorkspaceService.ListAgentsAsync):
                    return Task.FromResult<IReadOnlyList<AgentDefinition>>([]);
                default:
                    throw new NotSupportedException($"Unexpected workspace call '{targetMethod.Name}'.");
            }
        }
    }

    private class UnusedTechnicalAgentBridgeProxy : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            throw new NotSupportedException($"Unexpected technical-agent call '{targetMethod?.Name}'.");
        }
    }

    private sealed class NoOpReferenceDataCacheInvalidator : IAgentReferenceDataCacheInvalidator
    {
        public event EventHandler? Invalidated
        {
            add
            {
            }
            remove
            {
            }
        }

        public void Invalidate()
        {
        }
    }

    private sealed class StubDatabaseProfileRuntimeAccessor : IDatabaseProfileRuntimeAccessor
    {
        private readonly ResolvedDatabaseProfile profile = new(
            new DatabaseProfileRecord
            {
                Id = Guid.NewGuid(),
                DisplayName = "Test",
                ProviderKind = DatabaseProviderKind.InMemory,
                SourceKind = DatabaseProfileSourceKind.InMemory
            },
            DatabaseProfileResolutionSource.ExplicitOverride,
            "not-used");

        public ResolvedDatabaseProfile ResolveCurrentProfile() => profile;

        public ResolvedDatabaseProfile ResolveProfile(Guid profileId) => profile;
    }

    private sealed class TrackingDatabaseSwitchNotificationService : IDatabaseSwitchNotificationService
    {
        private EventHandler<DatabaseProfileChangedNotification>? changed;

        public TaskCompletionSource<bool> PublishStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public event EventHandler<DatabaseProfileChangedNotification>? Changed
        {
            add => changed += value;
            remove => changed -= value;
        }

        public int SubscriberCount => changed?.GetInvocationList().Length ?? 0;

        public void Publish(DatabaseProfileChangedNotification notification)
        {
            PublishStarted.TrySetResult(true);
            changed?.Invoke(this, notification);
        }
    }

    private sealed record LogEntry(LogLevel Level, string Message, Exception? Exception);

    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        public ConcurrentQueue<LogEntry> Entries { get; } = new();

        public TaskCompletionSource<LogEntry> WarningLogged { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public ILogger CreateLogger(string categoryName) =>
            new CapturingLogger(
                Entries,
                WarningLogged);

        public void Dispose()
        {
        }

        private sealed class CapturingLogger(
            ConcurrentQueue<LogEntry> entries,
            TaskCompletionSource<LogEntry> warningLogged) : ILogger
        {
            public IDisposable BeginScope<TState>(TState state) where TState : notnull => EmptyScope.Instance;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                var entry = new LogEntry(
                    logLevel,
                    formatter(state, exception),
                    exception);
                entries.Enqueue(entry);
                if (logLevel == LogLevel.Warning)
                {
                    warningLogged.TrySetResult(entry);
                }
            }
        }

        private sealed class EmptyScope : IDisposable
        {
            public static EmptyScope Instance { get; } = new();

            public void Dispose()
            {
            }
        }
    }
}
