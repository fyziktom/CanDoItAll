using CanDoItAll.Processes.Drivers.Abstractions;
using CanDoItAll.Processes.Drivers.Standard;

namespace CanDoItAll.Modules.Processes;

internal interface IAgentFrameworkProcessStepExecutor
{
    ValueTask<ProcessExecutionAdapterResult> ExecuteAsync(
        ProcessExecutionAdapterRequest request,
        CancellationToken cancellationToken = default);
}

internal sealed class AgentFrameworkProcessExecutionAdapter(IAgentFrameworkProcessStepExecutor executor) :
    IProcessExecutionAdapter,
    IProcessStepExecutionDriver
{
    internal static ProcessStepExecutionDriverDescriptor DriverDescriptor { get; } = new(
        StandardProcessAdapterDriverIds.Workflow,
        StandardProcessAdapterDescriptors.WorkflowAdapter,
        StandardProcessAdapterDescriptors.WorkflowAdapter.Strategy);

    public ProcessExecutionAdapterDescriptor Descriptor => StandardProcessAdapterDescriptors.WorkflowAdapter;

    ProcessStepExecutionDriverDescriptor IProcessStepExecutionDriver.Descriptor => DriverDescriptor;

    public ValueTask<ProcessExecutionAdapterResult> ExecuteStepAsync(
        ProcessExecutionAdapterRequest request,
        CancellationToken cancellationToken = default)
        => executor.ExecuteAsync(request, cancellationToken);

    public ValueTask<ProcessExecutionAdapterResult> ExecuteAsync(
        ProcessExecutionAdapterRequest request,
        CancellationToken cancellationToken = default)
        => executor.ExecuteAsync(request, cancellationToken);
}
