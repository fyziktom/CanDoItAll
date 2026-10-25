using CanDoItAll.Processes.Drivers.Abstractions;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Processes.Application;

public interface IProcessDecisionStepExecutor {
    ProcessDecisionExecutionContract Contract { get; }
    ValueTask<ProcessExecutionAdapterResult> ExecuteAsync(ProcessExecutionAdapterRequest request,
        Func<ProcessRuntimeStepAssignment, string> encodeOriginalMetadata, CancellationToken cancellationToken = default);
}

public sealed class ProcessDecisionPreparationInProgressException(ProcessRuntimeDispatchPrecondition claim)
    : Exception($"The original decision preparation for step '{claim.StepInstanceId}' still owns its dispatch claim.") {
    public ProcessRuntimeDispatchPrecondition Claim { get; } = claim;
}
