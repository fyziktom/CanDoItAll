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
    public ProcessExecutionAdapterDescriptor Descriptor => StandardProcessAdapterDescriptors.WorkflowAdapter;

    ProcessStepExecutionDriverDescriptor IProcessStepExecutionDriver.Descriptor => new(
        StandardProcessAdapterDriverIds.Workflow,
        Descriptor,
        Descriptor.Strategy);

    public ValueTask<ProcessExecutionAdapterResult> ExecuteStepAsync(
        ProcessExecutionAdapterRequest request,
        CancellationToken cancellationToken = default)
        => executor.ExecuteAsync(request, cancellationToken);

    public ValueTask<ProcessExecutionAdapterResult> ExecuteAsync(
        ProcessExecutionAdapterRequest request,
        CancellationToken cancellationToken = default)
        => executor.ExecuteAsync(request, cancellationToken);
}
