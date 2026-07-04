using CanDoItAll.Processes.Abstractions;

namespace CanDoItAll.Processes.Drivers.Abstractions;

public interface IProcessStepExecutionDriver
{
    ProcessStepExecutionDriverDescriptor Descriptor { get; }

    ValueTask<ProcessExecutionAdapterResult> ExecuteStepAsync(
        ProcessExecutionAdapterRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ProcessStepExecutionDriverDescriptor(
    DriverId DriverId,
    ProcessExecutionAdapterDescriptor Adapter,
    ProcessStrategyDescriptor Strategy);
