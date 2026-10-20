using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Processes.Application;

public interface IProcessDecisionDispatchAuthorityQuery {
    Task<ProcessExecutionDispatchAuthorityResult> ReadDecisionDispatchAsync(ProcessExecutionClaimEvidence evidence,
        ProcessDecisionChildMaterialBinding original, ProcessDecisionExecutionContract supportedExecution,
        ProcessDecisionContentContract? supportedContent, CancellationToken cancellationToken = default);
}
