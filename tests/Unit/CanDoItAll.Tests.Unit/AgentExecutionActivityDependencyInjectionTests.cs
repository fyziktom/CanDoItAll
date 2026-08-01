using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.SharedKernel.Streaming;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Unit;

public sealed class AgentExecutionActivityDependencyInjectionTests
{
    [Fact]
    public async Task ModuleRegistration_UsesSingletonStreamAndCoordinatorWithScopedReaders()
    {
        await using var fixture = CreateFixture();
        await using var firstScope = fixture.Provider.CreateAsyncScope();
        await using var secondScope = fixture.Provider.CreateAsyncScope();

        var firstStream = firstScope.ServiceProvider.GetRequiredService<
            PartitionedSequencedStream<
                AgentExecutionActivityStreamId,
                AgentExecutionActivity>>();
        var secondStream = secondScope.ServiceProvider.GetRequiredService<
            PartitionedSequencedStream<
                AgentExecutionActivityStreamId,
                AgentExecutionActivity>>();
        var firstCoordinator = firstScope.ServiceProvider
            .GetRequiredService<AgentExecutionActivityCoordinator>();
        var secondCoordinator = secondScope.ServiceProvider
            .GetRequiredService<AgentExecutionActivityCoordinator>();
        var firstCoordinatorContract = firstScope.ServiceProvider
            .GetRequiredService<IAgentExecutionActivityCoordinator>();
        var secondCoordinatorContract = secondScope.ServiceProvider
            .GetRequiredService<IAgentExecutionActivityCoordinator>();
        var firstReader = firstScope.ServiceProvider
            .GetRequiredService<IAgentExecutionActivityReader>();
        var repeatedFirstReader = firstScope.ServiceProvider
            .GetRequiredService<IAgentExecutionActivityReader>();
        var secondReader = secondScope.ServiceProvider
            .GetRequiredService<IAgentExecutionActivityReader>();

        Assert.Same(firstStream, secondStream);
        Assert.Same(firstCoordinator, secondCoordinator);
        Assert.Same(firstCoordinator, firstCoordinatorContract);
        Assert.Same(firstCoordinatorContract, secondCoordinatorContract);
        Assert.Same(firstReader, repeatedFirstReader);
        Assert.NotSame(firstReader, secondReader);
        Assert.NotSame(firstCoordinatorContract, firstReader);
    }

    [Fact]
    public async Task ScopedReader_AuthorizesCurrentProfileAndOrganizationScope()
    {
        await using var fixture = CreateFixture();
        await using var scope = fixture.Provider.CreateAsyncScope();
        var coordinator = scope.ServiceProvider
            .GetRequiredService<IAgentExecutionActivityCoordinator>();
        var authorizedReader = scope.ServiceProvider
            .GetRequiredService<IAgentExecutionActivityReader>();
        var streamId = fixture.CreateAuthorizedStreamId();
        using var operation = Assert.IsType<AgentExecutionActivityAdmitted>(
            coordinator.AdmitOperation(
                streamId,
                Guid.NewGuid(),
                null,
                "Accepted."))
            .Operation;

        await using var reader = authorizedReader.OpenReader(
            streamId,
            StreamSequence.Beginning);
        var events = Assert.IsType<
            SequencedStreamEvents<AgentExecutionActivity>>(
            await reader.ReadAsync());

        var accepted = Assert.Single(events.Items);
        Assert.Equal(StreamSequence.First, accepted.Sequence);
        Assert.Equal(
            AgentExecutionActivityPhase.Accepted,
            accepted.Event.Phase);
    }

    [Fact]
    public async Task ScopedReader_RejectsWrongProfileAndWrongWorkspaceWithTypedReasons()
    {
        await using var fixture = CreateFixture();
        await using var scope = fixture.Provider.CreateAsyncScope();
        var reader = scope.ServiceProvider
            .GetRequiredService<IAgentExecutionActivityReader>();
        var wrongProfileStreamId = new AgentExecutionActivityStreamId(
            Guid.NewGuid(),
            fixture.OrganizationScope,
            new DatabaseProfileGeneration(0),
            AgentExecutionOperationId.New());
        var wrongScopeStreamId = new AgentExecutionActivityStreamId(
            fixture.ProfileId,
            WorkspaceScopeDescriptor.Project("project-42"),
            new DatabaseProfileGeneration(0),
            AgentExecutionOperationId.New());

        var wrongProfile = Assert.Throws<AgentExecutionActivityAccessException>(
            () => reader.OpenReader(
                wrongProfileStreamId,
                StreamSequence.Beginning));
        var wrongScope = Assert.Throws<AgentExecutionActivityAccessException>(
            () => reader.OpenReader(
                wrongScopeStreamId,
                StreamSequence.Beginning));

        Assert.Equal(
            AgentExecutionActivityAccessRejectionReason.DatabaseProfileMismatch,
            wrongProfile.Reason);
        Assert.Equal(
            AgentExecutionActivityAccessRejectionReason.WorkspaceScopeMismatch,
            wrongScope.Reason);
        Assert.Equal(0, fixture.Notifications.SubscriberCount);
    }

    [Fact]
    public async Task SameProfileGenerationChange_CancelsOldReaderAndRejectsOldStream()
    {
        await using var fixture = CreateFixture();
        await using var scope = fixture.Provider.CreateAsyncScope();
        var coordinator = scope.ServiceProvider
            .GetRequiredService<IAgentExecutionActivityCoordinator>();
        var authorizedReader = scope.ServiceProvider
            .GetRequiredService<IAgentExecutionActivityReader>();
        var streamId = fixture.CreateAuthorizedStreamId();
        using var operation = Assert.IsType<AgentExecutionActivityAdmitted>(
            coordinator.AdmitOperation(
                streamId,
                Guid.NewGuid(),
                null,
                "Accepted."))
            .Operation;
        var reader = authorizedReader.OpenReader(
            streamId,
            StreamSequence.Beginning);

        Assert.IsType<SequencedStreamEvents<AgentExecutionActivity>>(
            await reader.ReadAsync());
        var pendingRead = reader.ReadAsync().AsTask();
        fixture.GenerationSource.SetGeneration(1);
        fixture.Notifications.Publish(
            CreateProfileChange(
                fixture.ProfileId,
                fixture.ProfileId,
                generation: 1));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => pendingRead.WaitAsync(TimeSpan.FromSeconds(5)));
        var exception = Assert.Throws<AgentExecutionActivityAccessException>(
            () => authorizedReader.OpenReader(
                streamId,
                StreamSequence.Beginning));

        Assert.Equal(
            AgentExecutionActivityAccessRejectionReason
                .DatabaseProfileGenerationMismatch,
            exception.Reason);
        await reader.DisposeAsync();
        Assert.Equal(0, fixture.Notifications.SubscriberCount);
    }

    [Fact]
    public async Task ProfileSwitchNotification_CancelsPendingRead()
    {
        await using var fixture = CreateFixture();
        await using var scope = fixture.Provider.CreateAsyncScope();
        var coordinator = scope.ServiceProvider
            .GetRequiredService<IAgentExecutionActivityCoordinator>();
        var authorizedReader = scope.ServiceProvider
            .GetRequiredService<IAgentExecutionActivityReader>();
        var streamId = fixture.CreateAuthorizedStreamId();
        using var operation = Assert.IsType<AgentExecutionActivityAdmitted>(
            coordinator.AdmitOperation(
                streamId,
                Guid.NewGuid(),
                null,
                "Accepted."))
            .Operation;
        var reader = authorizedReader.OpenReader(
            streamId,
            StreamSequence.Beginning);

        Assert.IsType<SequencedStreamEvents<AgentExecutionActivity>>(
            await reader.ReadAsync());
        var pendingRead = reader.ReadAsync().AsTask();
        Assert.False(pendingRead.IsCompleted);
        Assert.Equal(1, fixture.Notifications.SubscriberCount);

        fixture.Notifications.Publish(
            CreateProfileChange(
                fixture.ProfileId,
                fixture.ProfileId,
                generation: 0));
        Assert.False(pendingRead.IsCompleted);

        var nextProfileId = Guid.NewGuid();
        fixture.RuntimeAccessor.SwitchTo(nextProfileId);
        fixture.Notifications.Publish(
            CreateProfileChange(
                fixture.ProfileId,
                nextProfileId,
                generation: 1));

        Assert.Equal(0, fixture.Notifications.SubscriberCount);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => pendingRead.WaitAsync(TimeSpan.FromSeconds(5)));
        await reader.DisposeAsync();
        Assert.Equal(0, fixture.Notifications.SubscriberCount);
    }

    [Fact]
    public async Task DisposedReader_DetachesBeforeLaterProfileNotifications()
    {
        await using var fixture = CreateFixture();
        await using var scope = fixture.Provider.CreateAsyncScope();
        var coordinator = scope.ServiceProvider
            .GetRequiredService<IAgentExecutionActivityCoordinator>();
        var authorizedReader = scope.ServiceProvider
            .GetRequiredService<IAgentExecutionActivityReader>();
        var streamId = fixture.CreateAuthorizedStreamId();
        using var operation = Assert.IsType<AgentExecutionActivityAdmitted>(
            coordinator.AdmitOperation(
                streamId,
                Guid.NewGuid(),
                null,
                "Accepted."))
            .Operation;
        var reader = authorizedReader.OpenReader(
            streamId,
            StreamSequence.Beginning);

        Assert.Equal(1, fixture.Notifications.SubscriberCount);
        await reader.DisposeAsync();
        Assert.Equal(0, fixture.Notifications.SubscriberCount);

        var nextProfileId = Guid.NewGuid();
        fixture.RuntimeAccessor.SwitchTo(nextProfileId);
        var exception = Record.Exception(
            () => fixture.Notifications.Publish(
                CreateProfileChange(
                    fixture.ProfileId,
                    nextProfileId,
                    generation: 2)));

        Assert.Null(exception);
        Assert.Equal(0, fixture.Notifications.SubscriberCount);
        operation.Report(
            AgentExecutionActivityPhase.CapturingContext,
            "Publisher remains independent.");
    }

    private static TestFixture CreateFixture()
    {
        var profileId = Guid.NewGuid();
        var runtimeAccessor = new SwitchingDatabaseProfileRuntimeAccessor(
            profileId);
        var notifications = new TrackingDatabaseSwitchNotificationService();
        var generationSource = new MutableProfileGenerationSource();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IDatabaseProfileRuntimeAccessor>(
            runtimeAccessor);
        services.AddSingleton<IDatabaseSwitchNotificationService>(
            notifications);
        services.AddSingleton<IAgentExecutionProfileGenerationSource>(
            generationSource);
        services.AddAgentFrameworkModule(
            new ConfigurationBuilder().Build());
        var provider = services.BuildServiceProvider(
            new ServiceProviderOptions
            {
                ValidateScopes = true
            });
        return new TestFixture(
            provider,
            runtimeAccessor,
            notifications,
            generationSource,
            profileId);
    }

    private static DatabaseProfileChangedNotification CreateProfileChange(
        Guid previousProfileId,
        Guid currentProfileId,
        long generation)
    {
        return new DatabaseProfileChangedNotification(
            previousProfileId,
            CreateFingerprint(previousProfileId),
            currentProfileId,
            CreateFingerprint(currentProfileId),
            generation);
    }

    private static ResolvedDatabaseProfile CreateResolvedProfile(
        Guid profileId)
    {
        var record = new DatabaseProfileRecord
        {
            Id = profileId,
            DisplayName = $"Test profile {profileId:N}",
            ProviderKind = DatabaseProviderKind.InMemory,
            SourceKind = DatabaseProfileSourceKind.InMemory,
            InMemory = new InMemoryDatabaseProfileConnection
            {
                DatabaseName = $"test-{profileId:N}"
            },
            Storage = new DatabaseProfileStorageDescriptor
            {
                Mode = DatabaseProfileStorageMode.Ephemeral
            },
            Runtime = new DatabaseProfileRuntimeMetadata
            {
                Fingerprint = CreateFingerprint(profileId)
            }
        };
        return new ResolvedDatabaseProfile(
            record,
            DatabaseProfileResolutionSource.ExplicitOverride,
            $"in-memory:{profileId:N}");
    }

    private static string CreateFingerprint(Guid profileId)
    {
        return $"test-{profileId:N}";
    }

    private sealed class TestFixture(
        ServiceProvider provider,
        SwitchingDatabaseProfileRuntimeAccessor runtimeAccessor,
        TrackingDatabaseSwitchNotificationService notifications,
        MutableProfileGenerationSource generationSource,
        Guid profileId) : IAsyncDisposable
    {
        public ServiceProvider Provider { get; } = provider;

        public SwitchingDatabaseProfileRuntimeAccessor RuntimeAccessor { get; } =
            runtimeAccessor;

        public TrackingDatabaseSwitchNotificationService Notifications { get; } =
            notifications;

        public MutableProfileGenerationSource GenerationSource { get; } =
            generationSource;

        public Guid ProfileId { get; } = profileId;

        public WorkspaceScopeDescriptor OrganizationScope { get; } =
            WorkspaceScopeDescriptor.Organization(profileId.ToString("N"));

        public AgentExecutionActivityStreamId CreateAuthorizedStreamId()
        {
            return new AgentExecutionActivityStreamId(
                ProfileId,
                OrganizationScope,
                new DatabaseProfileGeneration(0),
                AgentExecutionOperationId.New());
        }

        public ValueTask DisposeAsync()
        {
            return Provider.DisposeAsync();
        }
    }

    private sealed class MutableProfileGenerationSource :
        IAgentExecutionProfileGenerationSource
    {
        private long generation;

        public DatabaseProfileGeneration GetGeneration()
        {
            return new DatabaseProfileGeneration(
                Interlocked.Read(ref generation));
        }

        public void SetGeneration(long value)
        {
            Interlocked.Exchange(ref generation, value);
        }
    }

    private sealed class SwitchingDatabaseProfileRuntimeAccessor(
        Guid initialProfileId) : IDatabaseProfileRuntimeAccessor
    {
        private readonly Lock gate = new();
        private ResolvedDatabaseProfile currentProfile =
            CreateResolvedProfile(initialProfileId);

        public ResolvedDatabaseProfile ResolveCurrentProfile()
        {
            lock (gate)
            {
                return currentProfile;
            }
        }

        public ResolvedDatabaseProfile ResolveProfile(Guid profileId)
        {
            lock (gate)
            {
                return currentProfile.Profile.Id == profileId
                    ? currentProfile
                    : CreateResolvedProfile(profileId);
            }
        }

        public void SwitchTo(Guid profileId)
        {
            lock (gate)
            {
                currentProfile = CreateResolvedProfile(profileId);
            }
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
            EventHandler<DatabaseProfileChangedNotification>? snapshot;
            lock (gate)
            {
                snapshot = handlers;
            }

            snapshot?.Invoke(this, notification);
        }
    }
}
