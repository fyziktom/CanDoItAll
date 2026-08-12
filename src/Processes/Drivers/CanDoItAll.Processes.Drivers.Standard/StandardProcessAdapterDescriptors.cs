using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Drivers.Abstractions;

namespace CanDoItAll.Processes.Drivers.Standard;

public static class StandardProcessAdapterDriverIds
{
    public static DriverId Foundation { get; } = new("process.driver.adapters.foundation");

    public static DriverId Workflow { get; } = new("process.driver.adapters.workflow");
}

public static class StandardProcessAdapterCapabilities
{
    public static CapabilityTag AdapterExecution { get; } = new("process.adapter.execution");

    public static CapabilityTag WorkflowExecution { get; } = new("process.adapter.workflow");
}

public static class StandardProcessAdapterDescriptors
{
    public static ProcessExecutionAdapterDescriptor WorkflowAdapter { get; } = new(
        new ProcessExecutionAdapterId("adapter.workflow.standard"),
        ProcessExecutionAdapterKind.Workflow,
        "1.0.0",
        new ProcessStrategyDescriptor(
            new StrategyId("strategy.adapter.workflow.execute"),
            "1.0.0",
            ProcessStrategyKind.StepExecution,
            new HashSet<CapabilityTag>
            {
                StandardProcessAdapterCapabilities.AdapterExecution,
                StandardProcessAdapterCapabilities.WorkflowExecution
            })
        {
            RequiredHostCapabilities = new HashSet<ProcessHostCapabilityId>
            {
                ProcessHostCapabilityIds.ManagedProcessAdapter
            }
        },
        new HashSet<CapabilityTag>
        {
            StandardProcessAdapterCapabilities.AdapterExecution,
            StandardProcessAdapterCapabilities.WorkflowExecution
        });
}
