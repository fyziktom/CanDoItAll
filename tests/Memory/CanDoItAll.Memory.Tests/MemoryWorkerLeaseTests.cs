using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Memory.Persistence;
using CanDoItAll.Memory.Persistence.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Memory.Tests;

public sealed class MemoryWorkerLeaseTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-07-12T18:00:00Z");
    private static readonly TimeSpan LeaseDuration = TimeSpan.FromSeconds(30);

    [Fact]
    public async Task WL001_Only_one_replica_acquires_an_active_phase()
    {
        await using var services = CreateServiceProvider();
        await using var scope = services.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IMemoryWorkerLeaseStore>();

        var attempts = await Task.WhenAll(
            store.TryAcquireAsync(
                MemoryBackgroundWorkerPhase.OperationPolling,
                MemoryWorkerLeaseOwnerId.Parse("replica-a"),
                Now,
                LeaseDuration),
            store.TryAcquireAsync(
                MemoryBackgroundWorkerPhase.OperationPolling,
                MemoryWorkerLeaseOwnerId.Parse("replica-b"),
                Now,
                LeaseDuration));

        Assert.Single(attempts, lease => lease is not null);
    }

    [Fact]
    public async Task WL002_Expired_lease_is_recovered_and_stale_owner_cannot_complete()
    {
        await using var services = CreateServiceProvider();
        await using var scope = services.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IMemoryWorkerLeaseStore>();
        var first = Assert.IsType<MemoryWorkerLease>(await store.TryAcquireAsync(
            MemoryBackgroundWorkerPhase.FeedbackDelivery,
            MemoryWorkerLeaseOwnerId.Parse("replica-a"),
            Now,
            LeaseDuration));

        var activeAttempt = await store.TryAcquireAsync(
            first.Phase,
            MemoryWorkerLeaseOwnerId.Parse("replica-b"),
            Now.AddSeconds(29),
            LeaseDuration);
        var recovered = Assert.IsType<MemoryWorkerLease>(await store.TryAcquireAsync(
            first.Phase,
            MemoryWorkerLeaseOwnerId.Parse("replica-b"),
            Now.AddSeconds(31),
            LeaseDuration));

        Assert.Null(activeAttempt);
        Assert.False(await store.CompleteAsync(first, Now.AddSeconds(32)));
        Assert.True(await store.CompleteAsync(recovered, Now.AddSeconds(32)));
    }

    [Fact]
    public async Task WL003_Completion_and_release_require_the_exact_owner_token()
    {
        await using var services = CreateServiceProvider();
        await using var scope = services.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IMemoryWorkerLeaseStore>();
        var lease = Assert.IsType<MemoryWorkerLease>(await store.TryAcquireAsync(
            MemoryBackgroundWorkerPhase.Retention,
            MemoryWorkerLeaseOwnerId.Parse("replica-a"),
            Now,
            LeaseDuration));
        var forged = lease with { Token = MemoryWorkerLeaseToken.New() };

        Assert.False(await store.CompleteAsync(forged, Now.AddSeconds(1)));
        Assert.False(await store.ReleaseAsync(forged, Now.AddSeconds(1)));
        Assert.True(await store.CompleteAsync(lease, Now.AddSeconds(1)));
    }

    [Fact]
    public async Task WL004_Renewal_extends_ownership_and_blocks_early_recovery()
    {
        await using var services = CreateServiceProvider();
        await using var scope = services.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IMemoryWorkerLeaseStore>();
        var lease = Assert.IsType<MemoryWorkerLease>(await store.TryAcquireAsync(
            MemoryBackgroundWorkerPhase.ProviderEventInbox,
            MemoryWorkerLeaseOwnerId.Parse("replica-a"),
            Now,
            LeaseDuration));

        Assert.True(await store.RenewAsync(lease, Now.AddSeconds(20), LeaseDuration));
        Assert.Null(await store.TryAcquireAsync(
            lease.Phase,
            MemoryWorkerLeaseOwnerId.Parse("replica-b"),
            Now.AddSeconds(40),
            LeaseDuration));
        Assert.NotNull(await store.TryAcquireAsync(
            lease.Phase,
            MemoryWorkerLeaseOwnerId.Parse("replica-b"),
            Now.AddSeconds(51),
            LeaseDuration));
    }

    private static ServiceProvider CreateServiceProvider()
    {
        var services = new ServiceCollection();
        var databaseName = $"memory-worker-leases-{Guid.NewGuid():N}";
        services.AddDbContextFactory<AppDbContext>(options =>
            options.UseInMemoryDatabase(databaseName));
        services.AddGenericMemoryModule();
        return services.BuildServiceProvider(validateScopes: true);
    }
}
