using CanDoItAll.Modules.Processes;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Drivers.Abstractions;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Projections;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace CanDoItAll.Tests.Unit.Processes;

[Trait("Category", "UnixRuntimePortability")]
public sealed class ProcessesModuleHostedWorkerRegistrationTests
{
    [Fact]
    public void Add_processes_module_binds_runtime_dispatch_options()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{ProcessRuntimeDispatchOptions.ConfigurationSectionName}:DispatchLease"] = "02:30:00",
                [$"{ProcessRuntimeDispatchOptions.ConfigurationSectionName}:StepExecutionTimeout"] = "02:00:00",
                [$"{ProcessRuntimeDispatchOptions.ConfigurationSectionName}:PreRunningClaimStaleAfter"] = "00:03:00"
            })
            .Build();

        services.AddProcessesModule(configuration);

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<ProcessRuntimeDispatchOptions>();

        Assert.Equal(TimeSpan.FromMinutes(150), options.DispatchLease);
        Assert.Equal(TimeSpan.FromHours(2), options.StepExecutionTimeout);
        Assert.Equal(TimeSpan.FromMinutes(3), options.PreRunningClaimStaleAfter);
    }

    [Fact]
    public void Add_processes_module_rejects_invalid_runtime_dispatch_options()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{ProcessRuntimeDispatchOptions.ConfigurationSectionName}:DispatchLease"] = "00:30:00",
                [$"{ProcessRuntimeDispatchOptions.ConfigurationSectionName}:StepExecutionTimeout"] = "01:00:00"
            })
            .Build();

        services.AddProcessesModule(configuration);

        using var provider = services.BuildServiceProvider();
        var exception = Assert.Throws<OptionsValidationException>(
            provider.GetRequiredService<ProcessRuntimeDispatchOptions>);

        Assert.Contains("Process runtime dispatch options are invalid.", exception.Message);
    }

    [Fact]
    public void Add_processes_module_registers_run_record_backfill_services()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();

        services.AddProcessesModule(configuration);

        Assert.Contains(
            services,
            descriptor =>
                descriptor.ServiceType == typeof(IProcessRunRecordBackfillSource) &&
                descriptor.ImplementationType == typeof(EfProcessRunRecordBackfillSource) &&
                descriptor.Lifetime == ServiceLifetime.Scoped);
        Assert.Contains(
            services,
            descriptor =>
                descriptor.ServiceType == typeof(ProcessRunRecordBackfillProcessor) &&
                descriptor.ImplementationType == typeof(ProcessRunRecordBackfillProcessor) &&
                descriptor.Lifetime == ServiceLifetime.Scoped);
    }

    [Fact]
    public void Add_processes_module_registers_blocked_run_recovery_pipeline()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();

        services.AddProcessesModule(configuration);

        Assert.Contains(
            services,
            descriptor =>
                descriptor.ServiceType == typeof(IProcessBlockedRunRecoveryCommandExecutor) &&
                descriptor.ImplementationType == typeof(ProcessBlockedRunRecoveryCommandExecutor) &&
                descriptor.Lifetime == ServiceLifetime.Scoped);
        Assert.Contains(
            services,
            descriptor =>
                descriptor.ServiceType == typeof(IProcessBlockedRunRecoveryPolicyCatalog) &&
                descriptor.ImplementationType == typeof(ProcessBlockedRunRecoveryPolicyCatalog) &&
                descriptor.Lifetime == ServiceLifetime.Scoped);
        Assert.Contains(
            services,
            descriptor =>
                descriptor.ServiceType == typeof(IProcessBlockedRunRecoveryCoordinator) &&
                descriptor.ImplementationType == typeof(ProcessBlockedRunRecoveryCoordinator) &&
                descriptor.Lifetime == ServiceLifetime.Scoped);
    }

    [Fact]
    public void Add_processes_module_resolves_dispatch_and_blocked_recovery_pipeline()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();
        services.AddSingleton<ICanonicalRuntimeDatabase>(new TestCanonicalRuntimeDatabase());
        services.AddScoped<IProcessRuntimeStrategyFactoryResolver, UnusableStrategyFactoryResolver>();
        services.AddProcessesModule(configuration);
        services.RemoveAll<IProcessExecutionAdapter>();
        services.RemoveAll<IProcessStepExecutionDriver>();
        services.RemoveAll<IProcessRuntimeStepAssignmentRepairService>();
        services.RemoveAll<IProcessRuntimeRunCancellationObserver>();

        using var provider = services.BuildServiceProvider(
            new ServiceProviderOptions
            {
                ValidateScopes = true
            });
        using var scope = provider.CreateScope();

        Assert.IsType<ProcessRuntimeDispatchApplicationService>(
            scope.ServiceProvider.GetRequiredService<ProcessRuntimeDispatchApplicationService>());
        Assert.IsType<ProcessBlockedRunRecoveryCoordinator>(
            scope.ServiceProvider.GetRequiredService<IProcessBlockedRunRecoveryCoordinator>());
    }

    [Fact]
    public void Add_processes_module_registers_projection_replay_worker_for_background_worker_lane()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();

        services.AddProcessesModule(configuration);

        Assert.Contains(
            services,
            descriptor =>
                descriptor.ServiceType == typeof(IHostedService) &&
                descriptor.ImplementationType == typeof(ProcessRuntimeProjectionReplayBackgroundWorker));
    }

    [Fact]
    public void Add_processes_module_suppresses_projection_replay_worker_for_mcp_tool_host_lane()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [LocalRuntimeHostedWorkerPolicy.LaneKindConfigurationKey] =
                    LocalRuntimeHostedWorkerPolicy.McpToolHostLaneKind
            })
            .Build();

        services.AddProcessesModule(configuration);

        Assert.DoesNotContain(
            services,
            descriptor =>
                descriptor.ServiceType == typeof(IHostedService) &&
                descriptor.ImplementationType == typeof(ProcessRuntimeProjectionReplayBackgroundWorker));
    }

    private sealed class TestCanonicalRuntimeDatabase : ICanonicalRuntimeDatabase
    {
        public ResolvedDatabaseProfile Profile { get; } = new(
            new DatabaseProfileRecord
            {
                Id = Guid.NewGuid(),
                DisplayName = "Processes module DI test",
                ProviderKind = DatabaseProviderKind.InMemory,
                SourceKind = DatabaseProfileSourceKind.InMemory
            },
            DatabaseProfileResolutionSource.ExplicitOverride,
            $"processes-module-di-{Guid.NewGuid():N}");

        public long Generation => 0;
    }

    private sealed class UnusableStrategyFactoryResolver : IProcessRuntimeStrategyFactoryResolver
    {
        public ValueTask<IProcessStrategyFactory> ResolveAsync(
            ProcessStrategyBindingSnapshot binding,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("The DI resolution test does not dispatch a strategy.");
        }
    }
}
