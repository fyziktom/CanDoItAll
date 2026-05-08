using CanDoItAll.Composition;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.Automation;
using CanDoItAll.Modules.Processes;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace CanDoItAll.Tests.Integration;

public sealed class RuntimeHostedWorkerPolicyIntegrationTests
{
    [Fact]
    public void AddCanDoItAllRuntimeModules_SuppressesBackgroundWorkers_ForPublishedActiveLane()
    {
        var services = new ServiceCollection();

        services.AddCanDoItAllRuntimeModules(BuildConfiguration("PublishedActive"));

        Assert.DoesNotContain(services, descriptor => MatchesHostedService(descriptor, nameof(ProcessOutboxDrainWorker)));
        Assert.DoesNotContain(services, descriptor => MatchesHostedService(descriptor, nameof(ProcessRunRecoveryWorker)));
        Assert.DoesNotContain(services, descriptor => MatchesHostedService(descriptor, nameof(AutomationMessagePumpWorker)));
        Assert.DoesNotContain(services, descriptor => MatchesHostedService(descriptor, nameof(ConnectorOutboxDrainWorker)));
        Assert.DoesNotContain(services, descriptor => MatchesHostedService(descriptor, "AgentFrameworkExecutionRecoveryWorker"));
    }

    [Fact]
    public void AddCanDoItAllRuntimeModules_RegistersNonRecoveringBackgroundWorkers_ForSourceWatchLane()
    {
        var services = new ServiceCollection();

        services.AddCanDoItAllRuntimeModules(BuildConfiguration("SourceWatch"));

        Assert.Contains(services, descriptor => MatchesHostedService(descriptor, nameof(ProcessOutboxDrainWorker)));
        Assert.DoesNotContain(services, descriptor => MatchesHostedService(descriptor, nameof(ProcessRunRecoveryWorker)));
        Assert.Contains(services, descriptor => MatchesHostedService(descriptor, nameof(AutomationMessagePumpWorker)));
        Assert.Contains(services, descriptor => MatchesHostedService(descriptor, nameof(ConnectorOutboxDrainWorker)));
        Assert.Contains(services, descriptor => MatchesHostedService(descriptor, "AgentFrameworkExecutionRecoveryWorker"));
    }

    [Fact]
    public void AddCanDoItAllRuntimeModules_RegistersProcessRunRecoveryWorker_WhenStartupRecoveryIsEnabled()
    {
        var services = new ServiceCollection();

        services.AddCanDoItAllRuntimeModules(BuildConfiguration(
            "SourceWatch",
            new Dictionary<string, string?>
            {
                ["Processes:Runtime:RecoverActiveRunsOnStartup"] = "true"
            }));

        Assert.Contains(services, descriptor => MatchesHostedService(descriptor, nameof(ProcessOutboxDrainWorker)));
        Assert.Contains(services, descriptor => MatchesHostedService(descriptor, nameof(ProcessRunRecoveryWorker)));
    }

    [Fact]
    public void AddCanDoItAllRuntimeModules_UsesPrefixedEnvironmentProjectionKey()
    {
        var services = new ServiceCollection();

        services.AddCanDoItAllRuntimeModules(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["LaneKind"] = "PublishedCandidate"
            })
            .Build());

        Assert.DoesNotContain(services, descriptor => MatchesHostedService(descriptor, nameof(ProcessOutboxDrainWorker)));
        Assert.DoesNotContain(services, descriptor => MatchesHostedService(descriptor, nameof(AutomationMessagePumpWorker)));
        Assert.DoesNotContain(services, descriptor => MatchesHostedService(descriptor, "AgentFrameworkExecutionRecoveryWorker"));
    }

    private static IConfiguration BuildConfiguration(
        string laneKind,
        IReadOnlyDictionary<string, string?>? additionalValues = null)
    {
        var values = new Dictionary<string, string?>
        {
            ["CanDoItAllMcpLaneKind"] = laneKind
        };
        if (additionalValues is not null)
        {
            foreach (var value in additionalValues)
            {
                values[value.Key] = value.Value;
            }
        }

        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }

    private static bool MatchesHostedService(ServiceDescriptor descriptor, string implementationTypeName)
    {
        return descriptor.ServiceType == typeof(IHostedService) &&
               string.Equals(descriptor.ImplementationType?.Name, implementationTypeName, StringComparison.Ordinal);
    }
}
