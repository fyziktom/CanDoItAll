using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Tests.Support;
using CanDoItAll.Web.Dashboard;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CanDoItAll.Tests.Components;

public sealed class DashboardSnapshotLoadRunnerTests
{
    [Fact]
    public async Task LoadAsync_creates_and_async_disposes_a_fresh_loader_scope_per_call()
    {
        using var lifetime = new TestHostApplicationLifetime();
        var factory = new ScopeTrackingLoaderFactory();
        var services = new ServiceCollection();
        services.AddSingleton<IHostApplicationLifetime>(lifetime);
        services.AddSingleton<IDashboardSnapshotLoadRunner, DashboardSnapshotLoadRunner>();
        services.AddScoped<IDashboardSnapshotLoader>(_ => factory.Create());

        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });
        var runner = provider.GetRequiredService<IDashboardSnapshotLoadRunner>();

        var first = await runner.LoadAsync();
        var second = await runner.LoadAsync();

        Assert.Equal(2, factory.Loaders.Count);
        Assert.NotSame(factory.Loaders[0], factory.Loaders[1]);
        Assert.All(factory.Loaders, loader => Assert.True(loader.IsDisposed));
        Assert.All(
            factory.Loaders,
            loader => Assert.Equal(lifetime.ApplicationStopping, loader.CapturedCancellationToken));
        Assert.Equal(1, first.Usage.ObservedTokens);
        Assert.Equal(2, second.Usage.ObservedTokens);
    }

    [Fact]
    public async Task LoadAsync_passes_application_stopping_cancellation_and_disposes_scope()
    {
        using var lifetime = new TestHostApplicationLifetime();
        var loader = new ApplicationStoppingLoader();
        var services = new ServiceCollection();
        services.AddSingleton<IHostApplicationLifetime>(lifetime);
        services.AddSingleton<IDashboardSnapshotLoadRunner, DashboardSnapshotLoadRunner>();
        services.AddScoped<IDashboardSnapshotLoader>(_ => loader);

        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });
        var runner = provider.GetRequiredService<IDashboardSnapshotLoadRunner>();

        var loadTask = runner.LoadAsync();
        await loader.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        lifetime.NotifyStopping();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            loadTask.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.Equal(lifetime.ApplicationStopping, loader.CapturedCancellationToken);
        Assert.True(loader.IsDisposed);
    }

    [Fact]
    public async Task Runtime_change_retry_disposes_old_scope_and_loads_new_profile_data()
    {
        var firstProfileId = Guid.NewGuid();
        var secondProfileId = Guid.NewGuid();
        var runtime = new MutableDatabaseRuntimeState
        {
            Snapshot = new DatabaseRuntimeSnapshot(
                firstProfileId,
                "first-fingerprint",
                Generation: 1)
        };
        using var lifetime = new TestHostApplicationLifetime();
        var factory = new ProfileTrackingLoaderFactory(runtime);
        var services = new ServiceCollection();
        services.AddSingleton<IHostApplicationLifetime>(lifetime);
        services.AddSingleton<IDashboardSnapshotLoadRunner, DashboardSnapshotLoadRunner>();
        services.AddScoped<IDashboardSnapshotLoader>(_ => factory.Create());

        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });
        var cache = new DashboardSnapshotCache(
            runtime,
            provider.GetRequiredService<IDashboardSnapshotLoadRunner>(),
            TimeProvider.System,
            Options.Create(new DashboardSnapshotOptions()),
            NullLogger<DashboardSnapshotCache>.Instance);

        var readTask = cache.GetAsync(DashboardSnapshotRefreshMode.UseCached);
        await factory.FirstLoadStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        runtime.Snapshot = new DatabaseRuntimeSnapshot(
            secondProfileId,
            "second-fingerprint",
            Generation: 2);
        factory.ReleaseFirstLoad();

        var read = await readTask.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(secondProfileId, Assert.Single(read.Snapshot.Data.Projects).ProjectId);
        Assert.Equal(2, factory.Loaders.Count);
        Assert.Equal(firstProfileId, factory.Loaders[0].ProfileId);
        Assert.Equal(secondProfileId, factory.Loaders[1].ProfileId);
        Assert.All(factory.Loaders, loader => Assert.Equal(1, loader.InvocationCount));
        Assert.All(factory.Loaders, loader => Assert.True(loader.IsDisposed));
        Assert.True(factory.FirstLoaderWasDisposedBeforeSecondWasCreated);
    }

    [Fact]
    public void AddCanDoItAllDashboard_registers_only_the_loader_as_scoped()
    {
        var services = new ServiceCollection();

        services.AddCanDoItAllDashboard();

        Assert.Equal(
            ServiceLifetime.Scoped,
            Assert.Single(services, descriptor => descriptor.ServiceType == typeof(IDashboardSnapshotLoader)).Lifetime);
        Assert.Equal(
            ServiceLifetime.Singleton,
            Assert.Single(services, descriptor => descriptor.ServiceType == typeof(IDashboardSnapshotLoadRunner)).Lifetime);
        Assert.Equal(
            ServiceLifetime.Singleton,
            Assert.Single(services, descriptor => descriptor.ServiceType == typeof(DashboardSnapshotCache)).Lifetime);
        Assert.Equal(
            ServiceLifetime.Singleton,
            Assert.Single(services, descriptor => descriptor.ServiceType == typeof(DashboardSnapshotService)).Lifetime);
    }

    private static DashboardSnapshotData CreateData(long observedTokens)
    {
        return new DashboardSnapshotData(
            Projects: [],
            WorkflowMode: DashboardActivityMode.RecentFallback,
            Workflows: [],
            ProcessMode: DashboardActivityMode.RecentFallback,
            Processes: [],
            Usage: new DashboardUsageTotals(
                observedTokens,
                KnownCostUsd: 0m,
                UnknownUsageObservationCount: 0,
                UpdatedAtUtc: DateTimeOffset.UnixEpoch));
    }

    private static DashboardSnapshotData CreateProfileData(Guid profileId)
    {
        return new DashboardSnapshotData(
            Projects:
            [
                new DashboardProjectItem(
                    profileId,
                    $"Profile {profileId:N}",
                    "Active",
                    new DashboardDisplayStatus("Active", DashboardStatusTone.Success),
                    DateTimeOffset.UnixEpoch)
            ],
            WorkflowMode: DashboardActivityMode.RecentFallback,
            Workflows: [],
            ProcessMode: DashboardActivityMode.RecentFallback,
            Processes: [],
            Usage: new DashboardUsageTotals(
                ObservedTokens: 0,
                KnownCostUsd: 0m,
                UnknownUsageObservationCount: 0,
                UpdatedAtUtc: DateTimeOffset.UnixEpoch));
    }

    private sealed class ScopeTrackingLoaderFactory
    {
        public List<ScopeTrackingLoader> Loaders { get; } = [];

        public ScopeTrackingLoader Create()
        {
            var loader = new ScopeTrackingLoader(Loaders.Count + 1);
            Loaders.Add(loader);
            return loader;
        }
    }

    private sealed class ScopeTrackingLoader(long observedTokens) : IDashboardSnapshotLoader, IAsyncDisposable
    {
        private int disposed;

        public CancellationToken CapturedCancellationToken { get; private set; }

        public bool IsDisposed => Volatile.Read(ref disposed) == 1;

        public Task<DashboardSnapshotData> LoadAsync(CancellationToken cancellationToken = default)
        {
            CapturedCancellationToken = cancellationToken;
            return Task.FromResult(CreateData(observedTokens));
        }

        public ValueTask DisposeAsync()
        {
            Interlocked.Exchange(ref disposed, 1);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class ApplicationStoppingLoader : IDashboardSnapshotLoader, IAsyncDisposable
    {
        private int disposed;

        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public CancellationToken CapturedCancellationToken { get; private set; }

        public bool IsDisposed => Volatile.Read(ref disposed) == 1;

        public async Task<DashboardSnapshotData> LoadAsync(CancellationToken cancellationToken = default)
        {
            CapturedCancellationToken = cancellationToken;
            Started.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            throw new InvalidOperationException("An infinite cancellation delay completed unexpectedly.");
        }

        public ValueTask DisposeAsync()
        {
            Interlocked.Exchange(ref disposed, 1);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class ProfileTrackingLoaderFactory(MutableDatabaseRuntimeState runtime)
    {
        private readonly TaskCompletionSource firstLoadRelease = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource FirstLoadStarted { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public List<ProfileTrackingLoader> Loaders { get; } = [];

        public bool FirstLoaderWasDisposedBeforeSecondWasCreated { get; private set; }

        public ProfileTrackingLoader Create()
        {
            var profileId = runtime.GetSnapshot().ActiveProfileId ?? throw new InvalidOperationException(
                "A profile must be active before the dashboard loader scope is created.");
            if (Loaders.Count == 1)
            {
                FirstLoaderWasDisposedBeforeSecondWasCreated = Loaders[0].IsDisposed;
            }

            var loader = new ProfileTrackingLoader(Loaders.Count + 1, profileId, this);
            Loaders.Add(loader);
            return loader;
        }

        public void ReleaseFirstLoad()
        {
            firstLoadRelease.TrySetResult();
        }

        public async Task WaitForFirstLoadReleaseAsync()
        {
            FirstLoadStarted.TrySetResult();
            await firstLoadRelease.Task;
        }
    }

    private sealed class ProfileTrackingLoader(
        int sequence,
        Guid profileId,
        ProfileTrackingLoaderFactory factory) : IDashboardSnapshotLoader, IAsyncDisposable
    {
        private int disposed;
        private int invocationCount;

        public Guid ProfileId { get; } = profileId;

        public int InvocationCount => Volatile.Read(ref invocationCount);

        public bool IsDisposed => Volatile.Read(ref disposed) == 1;

        public async Task<DashboardSnapshotData> LoadAsync(CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref invocationCount);
            if (sequence == 1)
            {
                await factory.WaitForFirstLoadReleaseAsync();
            }

            return CreateProfileData(ProfileId);
        }

        public ValueTask DisposeAsync()
        {
            Interlocked.Exchange(ref disposed, 1);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class MutableDatabaseRuntimeState : IDatabaseRuntimeState
    {
        public required DatabaseRuntimeSnapshot Snapshot { get; set; }

        public DatabaseRuntimeSnapshot GetSnapshot()
        {
            return Snapshot;
        }

        public void MarkCurrentProfile(ResolvedDatabaseProfile profile)
        {
            throw new NotSupportedException();
        }
    }
}
