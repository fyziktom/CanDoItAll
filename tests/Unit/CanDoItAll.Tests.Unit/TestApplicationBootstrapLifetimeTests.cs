using System.Runtime.CompilerServices;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Unit;

public sealed class TestApplicationBootstrapLifetimeTests {
    [Fact]
    public async Task Disposed_fixture_provider_is_collectible_after_resolving_storage_drivers() {
        await using var environment = CanDoItAllTestEnvironment.Create("fixture-provider-lifetime");
        var reference = CreateDisposedProvider(environment);

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        Assert.False(reference.TryGetTarget(out _));
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference<ServiceProvider> CreateDisposedProvider(CanDoItAllTestEnvironment environment) {
        var profile = environment.CreateInMemoryProfile("fixture");
        var services = new ServiceCollection();
        TestApplicationBootstrap.ConfigureDefaultServices(
            services,
            TestApplicationBootstrap.BuildConfiguration(profile),
            environment.CreateHostEnvironment(nameof(TestApplicationBootstrapLifetimeTests)));
        using var provider = services.BuildServiceProvider();
        _ = provider.GetRequiredService<IStorageDriverRegistry>();
        return new WeakReference<ServiceProvider>(provider);
    }
}
