using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Processes.Application;

public sealed record ProcessDecisionLaunchAdmissions(ProcessDecisionRunAdmission Root,
    IReadOnlyDictionary<ProcessStepInstanceId, ProcessDecisionDataPolicyAdmission> Steps);

public interface IProcessDecisionLaunchPolicyProvider {
    ProcessLaunchDecisionDescriptor Descriptor { get; }

    Task<ProcessDecisionLaunchAdmissions> PrepareAsync(ProcessLaunchRequest request,
        ProcessRuntimeCommitRequest initial, CancellationToken cancellationToken);
}
