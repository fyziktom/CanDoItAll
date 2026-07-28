using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.AgentFramework;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Unit;

public sealed class DatabaseRuntimeAgentExecutionProfileGenerationSourceTests
{
    [Fact]
    public void Generation_reflects_initial_and_monotonic_runtime_state()
    {
        var runtimeNotifications = new DatabaseSwitchNotificationService();
        var runtimeState = new DatabaseRuntimeState(runtimeNotifications);
        var sourceNotifications =
            new TrackingDatabaseSwitchNotificationService();
        var preparationCache = new TrackingPreparationCache();
        var initialProfile = CreateResolvedProfile("initial");
        runtimeState.MarkCurrentProfile(initialProfile);
        using var fixture = CreateFixture(
            runtimeState,
            sourceNotifications,
            preparationCache);

        Assert.Equal(
            new DatabaseProfileGeneration(0),
            fixture.Source.GetGeneration());

        var initialSnapshot = runtimeState.GetSnapshot();
        runtimeState.PublishRestartObserved(
            initialSnapshot,
            CreateResolvedProfile("second"));

        Assert.Equal(
            new DatabaseProfileGeneration(1),
            fixture.Source.GetGeneration());

        var secondSnapshot = runtimeState.GetSnapshot();
        runtimeState.PublishRestartObserved(
            secondSnapshot,
            CreateResolvedProfile("third"));

        Assert.Equal(
            new DatabaseProfileGeneration(2),
            fixture.Source.GetGeneration());
        Assert.Equal(0, preparationCache.InvalidateAllCount);
    }

    [Fact]
    public void Switch_notifications_invalidate_all_without_replacing_runtime_generation()
    {
        var runtimeState = new MutableDatabaseRuntimeState(generation: 7);
        var notifications =
            new TrackingDatabaseSwitchNotificationService();
        var preparationCache = new TrackingPreparationCache();
        using var fixture = CreateFixture(
            runtimeState,
            notifications,
            preparationCache);

        notifications.Publish(CreateProfileChange(generation: 8));
        notifications.Publish(CreateProfileChange(generation: 9));

        Assert.Equal(2, preparationCache.InvalidateAllCount);
        Assert.Equal(
            new DatabaseProfileGeneration(7),
            fixture.Source.GetGeneration());
        Assert.Equal(1, notifications.SubscriberCount);
    }

    [Fact]
    public void Dispose_unsubscribes_without_disposing_shared_dependencies()
    {
        var runtimeState = new MutableDatabaseRuntimeState(generation: 3);
        var notifications =
            new TrackingDatabaseSwitchNotificationService();
        var preparationCache = new TrackingPreparationCache();
        using var fixture = CreateFixture(
            runtimeState,
            notifications,
            preparationCache);
        var disposable = Assert.IsAssignableFrom<IDisposable>(fixture.Source);

        Assert.Equal(1, notifications.SubscriberCount);

        disposable.Dispose();
        disposable.Dispose();

        Assert.Equal(0, notifications.SubscriberCount);
        notifications.Publish(CreateProfileChange(generation: 4));
        Assert.Equal(0, preparationCache.InvalidateAllCount);
        Assert.Equal(0, preparationCache.DisposeCount);
    }

    private static TestFixture CreateFixture(
        IDatabaseRuntimeState runtimeState,
        IDatabaseSwitchNotificationService notifications,
        IAgentExecutionPreparationCache preparationCache)
    {
        var services = new ServiceCollection();
        services.AddSingleton(runtimeState);
        services.AddSingleton(notifications);
        services.AddSingleton(preparationCache);
        services.AddAgentFrameworkModule(
            new ConfigurationBuilder().Build());
        var provider = services.BuildServiceProvider(
            new ServiceProviderOptions
            {
                ValidateScopes = true
            });
        var scope = provider.CreateScope();
        var source = scope.ServiceProvider
            .GetRequiredService<IAgentExecutionProfileGenerationSource>();
        return new TestFixture(provider, scope, source);
    }

    private static DatabaseProfileChangedNotification CreateProfileChange(
        long generation)
    {
        return new DatabaseProfileChangedNotification(
            Guid.NewGuid(),
            "previous",
            Guid.NewGuid(),
            "current",
            generation);
    }

    private static ResolvedDatabaseProfile CreateResolvedProfile(string name)
    {
        return new ResolvedDatabaseProfile(
            new DatabaseProfileRecord
            {
                Id = Guid.NewGuid(),
                DisplayName = name,
                ProviderKind = DatabaseProviderKind.InMemory,
                SourceKind = DatabaseProfileSourceKind.InMemory,
                InMemory = new InMemoryDatabaseProfileConnection
                {
                    DatabaseName = name
                },
                Storage = new DatabaseProfileStorageDescriptor
                {
                    Mode = DatabaseProfileStorageMode.Ephemeral
                },
                Runtime = new DatabaseProfileRuntimeMetadata
                {
                    Fingerprint = $"fingerprint-{name}"
                }
            },
            DatabaseProfileResolutionSource.ExplicitOverride,
            $"in-memory:{name}");
    }

    private sealed class TestFixture(
        ServiceProvider provider,
        IServiceScope scope,
        IAgentExecutionProfileGenerationSource source) : IDisposable
    {
        public IAgentExecutionProfileGenerationSource Source { get; } = source;

        public void Dispose()
        {
            scope.Dispose();
            provider.Dispose();
        }
    }

    private sealed class MutableDatabaseRuntimeState(long generation) :
        IDatabaseRuntimeState
    {
        public DatabaseRuntimeSnapshot GetSnapshot()
        {
            return new DatabaseRuntimeSnapshot(
                ActiveProfileId: null,
                ActiveFingerprint: null,
                generation);
        }

        public void MarkCurrentProfile(ResolvedDatabaseProfile profile)
        {
            ArgumentNullException.ThrowIfNull(profile);
        }
    }

    private sealed class TrackingDatabaseSwitchNotificationService :
        IDatabaseSwitchNotificationService
    {
        private readonly Lock gate = new();
        private EventHandler<DatabaseProfileChangedNotification>? handlers;

        public event EventHandler<DatabaseProfileChangedNotification>? Changed
        {
            add
            {
                lock (gate)
                {
                    handlers += value;
                }
            }
            remove
            {
                lock (gate)
                {
                    handlers -= value;
                }
            }
        }

        public int SubscriberCount
        {
            get
            {
                lock (gate)
                {
                    return handlers?.GetInvocationList().Length ?? 0;
                }
            }
        }

        public void Publish(DatabaseProfileChangedNotification notification)
        {
            EventHandler<DatabaseProfileChangedNotification>? subscribers;
            lock (gate)
            {
                subscribers = handlers;
            }

            subscribers?.Invoke(this, notification);
        }
    }

    private sealed class TrackingPreparationCache :
        IAgentExecutionPreparationCache
    {
        private int invalidateAllCount;
        private int disposeCount;

        public int InvalidateAllCount =>
            Volatile.Read(ref invalidateAllCount);

        public int DisposeCount => Volatile.Read(ref disposeCount);

        public Task<AgentExecutionPreparationAcquireResult> AcquireAsync(
            AgentExecutionPreparationRequest request,
            Func<
                CancellationToken,
                Task<AgentExecutionPreparationBlueprint>> factory,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public void Invalidate(AgentExecutionPreparationKey key)
        {
            throw new NotSupportedException();
        }

        public void InvalidateAll()
        {
            Interlocked.Increment(ref invalidateAllCount);
        }

        public AgentExecutionPreparationCacheSnapshot Snapshot()
        {
            throw new NotSupportedException();
        }

        public void Dispose()
        {
            Interlocked.Increment(ref disposeCount);
        }
    }
}
