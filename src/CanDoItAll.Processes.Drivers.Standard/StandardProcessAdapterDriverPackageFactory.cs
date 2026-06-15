using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Drivers.Abstractions;

namespace CanDoItAll.Processes.Drivers.Standard;

public static class StandardProcessAdapterDriverPackageFactory
{
    public static IReadOnlyList<ProcessDriverPackage> CreateLayeredPackages(IProcessExecutionAdapter adapter)
    {
        ArgumentNullException.ThrowIfNull(adapter);

        return [CreateFoundationPackage(), CreateAdapterPackage(adapter, ProcessDriverLayer.Platform)];
    }

    public static ProcessDriverPackage CreateAdapterPackage(
        IProcessExecutionAdapter adapter,
        ProcessDriverLayer layer)
    {
        ArgumentNullException.ThrowIfNull(adapter);

        var descriptor = new ProcessDriverDescriptor(
            ResolveDriverId(adapter.Descriptor.Kind),
            $"{adapter.Descriptor.Kind} Adapter Driver",
            "1.0.0",
            "runtime/1.0",
            "runtime/2.x",
            layer,
            adapter.Descriptor.CapabilityTags,
            [new ProcessDriverDependency(StandardProcessAdapterDriverIds.Foundation, ">=1.0")],
            [],
            [
                new ProcessDriverFacetDescriptor(
                    new DriverFacetKey("adapter." + GetAdapterKindToken(adapter.Descriptor.Kind)),
                    "1.0",
                    "Adapter execution facet.")
            ],
            [adapter.Descriptor.Strategy]);

        return new ProcessDriverPackage(
            descriptor,
            [new StandardProcessAdapterStrategyFactory(adapter)],
            [],
            [],
            [],
            [],
            []);
    }

    private static ProcessDriverPackage CreateFoundationPackage()
    {
        var descriptor = new ProcessDriverDescriptor(
            StandardProcessAdapterDriverIds.Foundation,
            "Process Adapter Foundation",
            "1.0.0",
            "runtime/1.0",
            "runtime/2.x",
            ProcessDriverLayer.BroadBase,
            new HashSet<CapabilityTag>
            {
                StandardProcessAdapterCapabilities.AdapterExecution
            },
            [],
            [],
            [
                new ProcessDriverFacetDescriptor(
                    new DriverFacetKey("adapter.foundation"),
                    "1.0",
                    "Common adapter envelope normalization.")
            ],
            []);

        return new ProcessDriverPackage(
            descriptor,
            [],
            [],
            [],
            [],
            [],
            []);
    }

    private static DriverId ResolveDriverId(ProcessExecutionAdapterKind kind)
    {
        return kind switch
        {
            ProcessExecutionAdapterKind.Workflow => StandardProcessAdapterDriverIds.Workflow,
            _ => new DriverId("process.driver.adapters." + GetAdapterKindToken(kind))
        };
    }

    private static string GetAdapterKindToken(ProcessExecutionAdapterKind kind)
    {
        return kind switch
        {
            ProcessExecutionAdapterKind.Workflow => "workflow",
            ProcessExecutionAdapterKind.SingleAgent => "single-agent",
            ProcessExecutionAdapterKind.AgentGroup => "agent-group",
            ProcessExecutionAdapterKind.Handoff => "handoff",
            ProcessExecutionAdapterKind.SchedulerTrigger => "scheduler-trigger",
            ProcessExecutionAdapterKind.ProjectContext => "project-context",
            ProcessExecutionAdapterKind.Plugin => "plugin",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unsupported adapter kind.")
        };
    }
}
