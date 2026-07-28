using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.AgentFramework;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace CanDoItAll.Tests.Unit;

public sealed class ProviderRuntimeProfileSnapshotServiceTests
{
    private static readonly Guid ProfileA =
        Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid ProfileB =
        Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly ProviderConfigurationRevision RevisionA =
        new(Guid.Parse("AAAAAAAA-AAAA-AAAA-AAAA-AAAAAAAAAAAA"));
    private static readonly ProviderConfigurationRevision RevisionB =
        new(Guid.Parse("BBBBBBBB-BBBB-BBBB-BBBB-BBBBBBBBBBBB"));
    private static readonly SandboxWorkspaceCatalogSnapshot CatalogSnapshot =
        new(SandboxWorkspaceCatalog.Empty, default);

    [Fact]
    public async Task Warm_reads_probe_revisions_without_full_profile_reload()
    {
        var provider = CreateProvider(Guid.NewGuid(), "primary");
        var loader = new RecordingLoader(Canonical(provider, RevisionA));
        var runtimeState = new MutableRuntimeState(
            new DatabaseRuntimeSnapshot(ProfileA, "profile-a", 0));
        var notifications = new DatabaseSwitchNotificationService();
        using var fixture = CreateService(
            loader,
            runtimeState,
            notifications);

        await fixture.Service.InitializeAsync();

        Assert.Equal(
            provider.Id,
            (await fixture.Service.GetProviderAsync(provider.Id))?.Id);
        Assert.Single(await fixture.Service.ListProvidersAsync());
        Assert.NotNull(
            fixture.Service.CaptureProvider(
                provider.Id,
                CatalogSnapshot));
        Assert.Equal(1, loader.LoadAllCallCount);
        Assert.Equal(0, loader.LoadOneCallCount);
        Assert.Equal(1, loader.LoadRevisionCallCount);
        Assert.Equal(1, loader.LoadRevisionsCallCount);
    }

    [Fact]
    public async Task Same_process_commit_observer_publishes_new_revision_before_acquisition()
    {
        var providerId = Guid.NewGuid();
        var original = Canonical(
            CreateProvider(providerId, "original"),
            RevisionA);
        var updated = Canonical(
            CreateProvider(providerId, "updated"),
            RevisionB);
        var loader = new RecordingLoader(original);
        var runtimeState = new MutableRuntimeState(
            new DatabaseRuntimeSnapshot(ProfileA, "profile-a", 0));
        var notifications = new DatabaseSwitchNotificationService();
        using var fixture = CreateService(
            loader,
            runtimeState,
            notifications);
        await fixture.Service.InitializeAsync();
        var observer =
            new AgentFrameworkProviderRuntimeSnapshotCommitObserver(
                loader,
                fixture.Service,
                NullLogger<
                    AgentFrameworkProviderRuntimeSnapshotCommitObserver>
                    .Instance);

        loader.Upsert(updated);
        await observer.ProviderSavedAsync(providerId);
        var loadCountAfterObserver = loader.LoadOneCallCount;

        var acquired = await fixture.Service.AcquireProviderAsync(
            providerId,
            CatalogSnapshot);

        Assert.NotNull(acquired);
        Assert.Equal("updated", acquired.Profile.Name);
        Assert.Equal(RevisionB, acquired.ConfigurationRevision);
        Assert.Equal(1, loadCountAfterObserver);
        Assert.Equal(
            loadCountAfterObserver,
            loader.LoadOneCallCount);
        Assert.Equal(1, loader.LoadRevisionCallCount);
    }

    [Fact]
    public async Task Cross_host_revision_change_refreshes_from_database_and_ignores_catalog()
    {
        var providerId = Guid.NewGuid();
        var original = Canonical(
            CreateProvider(providerId, "database-original"),
            RevisionA);
        var updated = Canonical(
            CreateProvider(providerId, "database-updated"),
            RevisionB);
        var staleCatalogProvider =
            CreateProvider(providerId, "catalog-stale");
        var divergentCatalogSnapshot =
            new SandboxWorkspaceCatalogSnapshot(
                SandboxWorkspaceCatalog.Empty with
                {
                    Providers = [staleCatalogProvider]
                },
                default);
        var loader = new RecordingLoader(original);
        var runtimeState = new MutableRuntimeState(
            new DatabaseRuntimeSnapshot(ProfileA, "profile-a", 0));
        var notifications = new DatabaseSwitchNotificationService();
        using var fixture = CreateService(
            loader,
            runtimeState,
            notifications);
        await fixture.Service.InitializeAsync();

        loader.Upsert(updated);

        var acquired = await fixture.Service.AcquireProviderAsync(
            providerId,
            divergentCatalogSnapshot);

        Assert.NotNull(acquired);
        Assert.Equal("database-updated", acquired.Profile.Name);
        Assert.Equal(RevisionB, acquired.ConfigurationRevision);
        Assert.Equal(1, loader.LoadOneCallCount);
        Assert.Equal(2, loader.LoadRevisionCallCount);
    }

    [Fact]
    public async Task Cross_host_delete_removes_provider_instead_of_serving_stale_lease()
    {
        var providerId = Guid.NewGuid();
        var loader = new RecordingLoader(
            Canonical(
                CreateProvider(providerId, "deleted-remotely"),
                RevisionA));
        var runtimeState = new MutableRuntimeState(
            new DatabaseRuntimeSnapshot(ProfileA, "profile-a", 0));
        var notifications = new DatabaseSwitchNotificationService();
        using var fixture = CreateService(
            loader,
            runtimeState,
            notifications);
        await fixture.Service.InitializeAsync();

        loader.Remove(providerId);

        var acquired = await fixture.Service.AcquireProviderAsync(
            providerId,
            CatalogSnapshot);

        Assert.Null(acquired);
        Assert.Null(
            fixture.Service.CaptureProvider(
                providerId,
                CatalogSnapshot));
        Assert.Equal(1, loader.LoadOneCallCount);
        Assert.Equal(2, loader.LoadRevisionCallCount);
    }

    [Fact]
    public async Task Revision_probe_failure_faults_snapshot_and_never_serves_stale_lease()
    {
        var providerId = Guid.NewGuid();
        var loader = new RecordingLoader(
            Canonical(
                CreateProvider(providerId, "primary"),
                RevisionA));
        var runtimeState = new MutableRuntimeState(
            new DatabaseRuntimeSnapshot(ProfileA, "profile-a", 0));
        var notifications = new DatabaseSwitchNotificationService();
        using var fixture = CreateService(
            loader,
            runtimeState,
            notifications);
        await fixture.Service.InitializeAsync();
        var cause = new InvalidOperationException(
            "revision probe failed");
        loader.RevisionProbeException = cause;

        var exception = await Assert.ThrowsAsync<
            ProviderRuntimeProfileSnapshotUnavailableException>(
            () => fixture.Service.AcquireProviderAsync(
                providerId,
                CatalogSnapshot));

        Assert.Equal(
            ProviderRuntimeProfileSnapshotStatus.Faulted,
            exception.Status);
        Assert.Same(cause, exception.InnerException);
        Assert.Throws<ProviderRuntimeProfileSnapshotUnavailableException>(
            () => fixture.Service.CaptureProvider(
                providerId,
                CatalogSnapshot));
        Assert.Equal(0, loader.LoadOneCallCount);
        Assert.Equal(1, loader.LoadRevisionCallCount);
    }

    [Fact]
    public async Task Superseded_profile_rebuild_cannot_publish_old_data()
    {
        var loadStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseLoad = new TaskCompletionSource<
            IReadOnlyList<CanonicalProviderRuntimeProfile>>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var loader = new RecordingLoader(
            async cancellationToken =>
            {
                loadStarted.TrySetResult();
                return await releaseLoad.Task.WaitAsync(cancellationToken);
            });
        var runtimeState = new MutableRuntimeState(
            new DatabaseRuntimeSnapshot(ProfileA, "profile-a", 0));
        var notifications = new DatabaseSwitchNotificationService();
        using var fixture = CreateService(
            loader,
            runtimeState,
            notifications);
        var initialization = fixture.Service.InitializeAsync();
        await loadStarted.Task;

        runtimeState.Set(
            new DatabaseRuntimeSnapshot(ProfileB, "profile-b", 1));
        notifications.Publish(
            new DatabaseProfileChangedNotification(
                ProfileA,
                "profile-a",
                ProfileB,
                "profile-b",
                1));
        releaseLoad.SetResult(
            [
                Canonical(
                    CreateProvider(
                        Guid.NewGuid(),
                        "old-profile-provider"),
                    RevisionA)
            ]);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => initialization);
        var exception = await Assert.ThrowsAsync<
            ProviderRuntimeProfileSnapshotUnavailableException>(
            () => fixture.Service.ListProvidersAsync());
        Assert.Equal(
            ProviderRuntimeProfileSnapshotStatus.NotReady,
            exception.Status);
        Assert.Equal(ProfileB, exception.DatabaseProfileId);
        Assert.Equal(1, exception.DatabaseProfileGeneration);
    }

    [Fact]
    public async Task Cancelled_initialization_remains_not_ready()
    {
        var loadStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var loader = new RecordingLoader(
            async cancellationToken =>
            {
                loadStarted.TrySetResult();
                await Task.Delay(
                    Timeout.InfiniteTimeSpan,
                    cancellationToken);
                return [];
            });
        var runtimeState = new MutableRuntimeState(
            new DatabaseRuntimeSnapshot(ProfileA, "profile-a", 0));
        var notifications = new DatabaseSwitchNotificationService();
        using var fixture = CreateService(
            loader,
            runtimeState,
            notifications);
        using var cancellation = new CancellationTokenSource();
        var initialization = fixture.Service.InitializeAsync(
            cancellation.Token);
        await loadStarted.Task;

        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => initialization);
        var exception = await Assert.ThrowsAsync<
            ProviderRuntimeProfileSnapshotUnavailableException>(
            () => fixture.Service.GetProviderAsync(Guid.NewGuid()));
        Assert.Equal(
            ProviderRuntimeProfileSnapshotStatus.NotReady,
            exception.Status);
    }

    [Fact]
    public async Task Committed_projection_failure_is_an_explicit_fault()
    {
        var provider = CreateProvider(Guid.NewGuid(), "primary");
        var loader = new RecordingLoader(
            Canonical(provider, RevisionA));
        var runtimeState = new MutableRuntimeState(
            new DatabaseRuntimeSnapshot(ProfileA, "profile-a", 0));
        var notifications = new DatabaseSwitchNotificationService();
        using var fixture = CreateService(
            loader,
            runtimeState,
            notifications);
        await fixture.Service.InitializeAsync();
        var cause = new InvalidOperationException("projection failed");

        fixture.Service.Invalidate(provider.Id, cause);

        var exception = await Assert.ThrowsAsync<
            ProviderRuntimeProfileSnapshotUnavailableException>(
            () => fixture.Service.GetProviderAsync(provider.Id));
        Assert.Equal(
            ProviderRuntimeProfileSnapshotStatus.Faulted,
            exception.Status);
        Assert.Same(cause, exception.InnerException);
    }

    private static SnapshotFixture CreateService(
        IProviderRuntimeProfileSnapshotLoader loader,
        IDatabaseRuntimeState runtimeState,
        IDatabaseSwitchNotificationService notifications)
    {
        var services = new ServiceCollection();
        services.AddScoped<IProviderRuntimeProfileSnapshotLoader>(
            _ => loader);
        var provider = services.BuildServiceProvider();
        var service =
            new CanonicalProviderRuntimeProfileSnapshotService(
                provider.GetRequiredService<IServiceScopeFactory>(),
                runtimeState,
                notifications,
                NullLogger<
                    CanonicalProviderRuntimeProfileSnapshotService>
                    .Instance);
        return new SnapshotFixture(provider, service);
    }

    private static CanonicalProviderRuntimeProfile Canonical(
        ProviderProfile provider,
        ProviderConfigurationRevision revision)
    {
        return new CanonicalProviderRuntimeProfile(
            provider,
            revision);
    }

    private static ProviderProfile CreateProvider(
        Guid providerId,
        string name)
    {
        return new ProviderProfile(
            providerId,
            name,
            ProviderKind.OpenAi,
            "https://api.openai.com/v1",
            "secret:aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
            "gpt-5.4-mini",
            ProviderTransportKind.Responses,
            true,
            true,
            true,
            false,
            true,
            "{}",
            string.Empty,
            "Not checked",
            null,
            ["gpt-5.4-mini"]);
    }

    private sealed class RecordingLoader :
        IProviderRuntimeProfileSnapshotLoader
    {
        private readonly object gate = new();
        private readonly Func<
            CancellationToken,
            Task<IReadOnlyList<CanonicalProviderRuntimeProfile>>>?
            loadAllOverride;
        private readonly Dictionary<Guid, CanonicalProviderRuntimeProfile>
            providers = [];
        private int loadAllCallCount;
        private int loadOneCallCount;
        private int loadRevisionCallCount;
        private int loadRevisionsCallCount;

        public RecordingLoader(
            params CanonicalProviderRuntimeProfile[] providers)
        {
            foreach (var provider in providers)
            {
                this.providers[provider.Profile.Id] = provider;
            }
        }

        public RecordingLoader(
            Func<
                CancellationToken,
                Task<IReadOnlyList<CanonicalProviderRuntimeProfile>>>
                loadAll)
        {
            loadAllOverride = loadAll;
        }

        public int LoadAllCallCount =>
            Volatile.Read(ref loadAllCallCount);

        public int LoadOneCallCount =>
            Volatile.Read(ref loadOneCallCount);

        public int LoadRevisionCallCount =>
            Volatile.Read(ref loadRevisionCallCount);

        public int LoadRevisionsCallCount =>
            Volatile.Read(ref loadRevisionsCallCount);

        public Exception? RevisionProbeException { get; set; }

        public async Task<
            IReadOnlyList<CanonicalProviderRuntimeProfile>> LoadAllAsync(
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref loadAllCallCount);
            if (loadAllOverride is not null)
            {
                return await loadAllOverride(cancellationToken);
            }

            lock (gate)
            {
                return providers.Values.ToArray();
            }
        }

        public Task<CanonicalProviderRuntimeProfile?> LoadAsync(
            Guid providerId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Interlocked.Increment(ref loadOneCallCount);
            lock (gate)
            {
                return Task.FromResult(
                    providers.GetValueOrDefault(providerId));
            }
        }

        public Task<ProviderConfigurationRevision?> LoadRevisionAsync(
            Guid providerId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Interlocked.Increment(ref loadRevisionCallCount);
            if (RevisionProbeException is not null)
            {
                throw RevisionProbeException;
            }

            lock (gate)
            {
                return Task.FromResult(
                    providers.GetValueOrDefault(providerId)
                        ?.ConfigurationRevision);
            }
        }

        public Task<IReadOnlyDictionary<
            Guid,
            ProviderConfigurationRevision>> LoadRevisionsAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Interlocked.Increment(ref loadRevisionsCallCount);
            if (RevisionProbeException is not null)
            {
                throw RevisionProbeException;
            }

            lock (gate)
            {
                IReadOnlyDictionary<
                    Guid,
                    ProviderConfigurationRevision> revisions = providers
                    .Where(item =>
                        item.Value.ConfigurationRevision.HasValue)
                    .ToDictionary(
                        item => item.Key,
                        item => item.Value.ConfigurationRevision
                            .GetValueOrDefault());
                return Task.FromResult(revisions);
            }
        }

        public void Upsert(CanonicalProviderRuntimeProfile provider)
        {
            lock (gate)
            {
                providers[provider.Profile.Id] = provider;
            }
        }

        public void Remove(Guid providerId)
        {
            lock (gate)
            {
                providers.Remove(providerId);
            }
        }
    }

    private sealed class MutableRuntimeState(
        DatabaseRuntimeSnapshot snapshot) :
        IDatabaseRuntimeState
    {
        private DatabaseRuntimeSnapshot snapshot = snapshot;

        public DatabaseRuntimeSnapshot GetSnapshot()
        {
            return Volatile.Read(ref snapshot);
        }

        public void Set(DatabaseRuntimeSnapshot replacement)
        {
            Volatile.Write(ref snapshot, replacement);
        }

        public void MarkCurrentProfile(
            CanDoItAll.Infrastructure.ControlPlane.ResolvedDatabaseProfile
                profile)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class SnapshotFixture(
        ServiceProvider provider,
        CanonicalProviderRuntimeProfileSnapshotService service) :
        IDisposable
    {
        public CanonicalProviderRuntimeProfileSnapshotService Service { get; } =
            service;

        public void Dispose()
        {
            Service.Dispose();
            provider.Dispose();
        }
    }
}
