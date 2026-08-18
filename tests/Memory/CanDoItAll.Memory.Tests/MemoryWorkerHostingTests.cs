using CanDoItAll.Composition;
using CanDoItAll.Memory.Application;
using CanDoItAll.Memory.Persistence;
using CanDoItAll.Memory.Persistence.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace CanDoItAll.Memory.Tests.Runtime;

public sealed class MemoryWorkerHostingTests
{
    [Fact]
    public void WH001_Default_registration_does_not_host_durable_workers()
    {
        var services = new ServiceCollection();

        services.AddGenericMemoryModule();

        Assert.DoesNotContain(services, IsMemoryHostedService);
    }

    [Fact]
    public void WH002_Explicit_composition_configuration_registers_validated_hosting()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Memory:BackgroundWorkers:Enabled"] = "true",
                ["Memory:BackgroundWorkers:CycleInterval"] = "00:00:02"
            })
            .Build();

        services.AddCanDoItAllRuntimeModules(configuration, MemoryTestHostEnvironment.Instance);

        var options = Assert.IsType<MemoryWorkerHostingOptions>(services
            .Single(descriptor => descriptor.ServiceType == typeof(MemoryWorkerHostingOptions))
            .ImplementationInstance);
        Assert.True(options.Enabled);
        Assert.Equal(TimeSpan.FromSeconds(2), options.CycleInterval);
        Assert.Equal(MemoryWorkerHostingOptions.DefaultLeaseDuration, options.LeaseDuration);
        Assert.Equal(MemoryWorkerHostingOptions.DefaultLeaseRenewalInterval, options.LeaseRenewalInterval);
        Assert.Contains(services, IsMemoryHostedService);
    }

    [Fact]
    public void WH003_Enabled_hosting_rejects_a_spin_interval()
    {
        var services = new ServiceCollection();

        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            services.AddGenericMemoryModule(options =>
            {
                options.WorkerHosting = MemoryWorkerHostingOptions.EnabledWithInterval(TimeSpan.Zero);
            }));

        Assert.Equal(nameof(MemoryWorkerHostingOptions.CycleInterval), exception.ParamName);
    }

    [Fact]
    public async Task WH004_Cycle_invokes_every_durable_worker_phase_once()
    {
        var workers = new RecordingMemoryWorkers();
        var cycle = CreateCycle(workers, NullLoggerFactory.Instance);

        await cycle.RunAsync();

        Assert.Equal(
            [
                MemoryWorkerInvocation.OperationPolling,
                MemoryWorkerInvocation.FeedbackDelivery,
                MemoryWorkerInvocation.ProviderEventPolling,
                MemoryWorkerInvocation.ProviderEventInbox,
                MemoryWorkerInvocation.ProviderEventOutbox,
                MemoryWorkerInvocation.Retention
            ],
            workers.Invocations);
    }

    [Fact]
    public async Task WH005_Phase_failure_is_logged_and_does_not_skip_later_phases()
    {
        var workers = new RecordingMemoryWorkers
        {
            Failure = MemoryWorkerInvocation.OperationPolling
        };
        var loggerFactory = new RecordingMemoryLoggerFactory();
        var cycle = CreateCycle(workers, loggerFactory);

        await cycle.RunAsync();

        Assert.Equal(6, workers.Invocations.Count);
        Assert.Contains(MemoryWorkerInvocation.Retention, workers.Invocations);
        Assert.Contains(
            loggerFactory.Entries,
            entry => entry.Level == LogLevel.Error &&
                     entry.Message.Contains(
                         nameof(MemoryWorkerInvocation.OperationPolling),
                         StringComparison.Ordinal));
    }

    [Fact]
    public async Task WH006_Host_stop_cancels_the_active_scoped_cycle()
    {
        var blockingOperationWorker = new BlockingMemoryOperationWorker();
        var otherWorkers = new RecordingMemoryWorkers();
        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddScoped<IMemoryAsyncOperationWorker>(_ => blockingOperationWorker);
        services.AddScoped<IMemoryFeedbackWorker>(_ => otherWorkers);
        services.AddScoped<IMemoryProviderEventWorker>(_ => otherWorkers);
        services.AddScoped<IMemoryRetentionWorker>(_ => otherWorkers);
        services.AddScoped<IMemoryWorkerLeaseRunner, PassThroughMemoryWorkerLeaseRunner>();
        services.AddScoped<IMemoryBackgroundWorkerCycle, MemoryBackgroundWorkerCycle>();
        await using var serviceProvider = services.BuildServiceProvider(validateScopes: true);
        var hostedService = new MemoryBackgroundWorkerHostedService(
            serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            MemoryWorkerHostingOptions.EnabledWithInterval(TimeSpan.FromHours(1)),
            TimeProvider.System,
            NullLoggerFactory.Instance);

        await hostedService.StartAsync(CancellationToken.None);
        await blockingOperationWorker.Started.WaitAsync(TimeSpan.FromSeconds(2));
        using var stopTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        await hostedService.StopAsync(stopTimeout.Token);
        await blockingOperationWorker.CancellationObserved.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Empty(otherWorkers.Invocations);
    }

    [Fact]
    public async Task WH007_Cycle_skips_a_phase_owned_by_another_replica()
    {
        var workers = new RecordingMemoryWorkers();
        var leaseRunner = new PassThroughMemoryWorkerLeaseRunner
        {
            UnavailablePhase = MemoryBackgroundWorkerPhase.FeedbackDelivery
        };
        var cycle = new MemoryBackgroundWorkerCycle(
            workers,
            workers,
            workers,
            workers,
            leaseRunner,
            NullLoggerFactory.Instance);

        await cycle.RunAsync();

        Assert.DoesNotContain(MemoryWorkerInvocation.FeedbackDelivery, workers.Invocations);
        Assert.Equal(5, workers.Invocations.Count);
    }

    [Fact]
    public void WH008_Lease_duration_must_exceed_two_renewal_intervals()
    {
        var options = new MemoryWorkerHostingOptions(
            enabled: true,
            TimeSpan.FromSeconds(1),
            leaseDuration: TimeSpan.FromSeconds(10),
            leaseRenewalInterval: TimeSpan.FromSeconds(5));

        var exception = Assert.Throws<ArgumentOutOfRangeException>(options.Validate);

        Assert.Equal(nameof(MemoryWorkerHostingOptions.LeaseDuration), exception.ParamName);
    }

    private static MemoryBackgroundWorkerCycle CreateCycle(
        RecordingMemoryWorkers workers,
        ILoggerFactory loggerFactory) =>
        new(
            workers,
            workers,
            workers,
            workers,
            new PassThroughMemoryWorkerLeaseRunner(),
            loggerFactory);

    private static bool IsMemoryHostedService(ServiceDescriptor descriptor) =>
        descriptor.ServiceType == typeof(IHostedService) &&
        descriptor.ImplementationType == typeof(MemoryBackgroundWorkerHostedService);
}
