using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Drivers.Abstractions;

namespace CanDoItAll.Processes.Drivers.Standard;

public static class StandardProcessAdapterDriverPackageFactory
{
    public static IReadOnlyList<ProcessDriverPackage> CreateLayeredPackages(IProcessStepExecutionDriver driver)
    {
        ArgumentNullException.ThrowIfNull(driver);

        return [CreateFoundationPackage(), CreateAdapterPackage(driver, ProcessDriverLayer.Platform)];
    }

    public static ProcessDriverPackage CreateAdapterPackage(
        IProcessStepExecutionDriver driver,
        ProcessDriverLayer layer)
    {
        ArgumentNullException.ThrowIfNull(driver);

        var adapter = driver.Descriptor.Adapter;
        var descriptor = new ProcessDriverDescriptor(
            driver.Descriptor.DriverId,
            $"{adapter.Kind} Adapter Driver",
            "1.0.0",
            "runtime/1.0",
            "runtime/2.x",
            layer,
            adapter.CapabilityTags,
            [new ProcessDriverDependency(StandardProcessAdapterDriverIds.Foundation, ">=1.0")],
            [],
            [
                new ProcessDriverFacetDescriptor(
                    new DriverFacetKey("adapter." + GetAdapterKindToken(adapter.Kind)),
                    "1.0",
                    "Adapter execution facet.")
            ],
            [adapter.Strategy]);

        return new ProcessDriverPackage(
            descriptor,
            [new StandardProcessAdapterStrategyFactory(driver)],
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

    private static string GetAdapterKindToken(ProcessExecutionAdapterKind kind)
    {
        return kind switch
        {
            ProcessExecutionAdapterKind.Workflow => "workflow",
            ProcessExecutionAdapterKind.SingleAgent => "single-agent",
            ProcessExecutionAdapterKind.AgentGroup => "agent-group",
            ProcessExecutionAdapterKind.Handoff => "handoff",
            ProcessExecutionAdapterKind.SchedulerTrigger => "scheduler-trigger",
            ProcessExecutionAdapterKind.ScopedContext => "scoped-context",
            ProcessExecutionAdapterKind.Plugin => "plugin",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unsupported adapter kind.")
        };
    }
}
