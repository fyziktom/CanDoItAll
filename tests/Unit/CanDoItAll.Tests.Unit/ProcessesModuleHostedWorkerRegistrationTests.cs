using CanDoItAll.Modules.Processes;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Projections;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace CanDoItAll.Tests.Unit;

public sealed class ProcessesModuleHostedWorkerRegistrationTests
{
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
}
