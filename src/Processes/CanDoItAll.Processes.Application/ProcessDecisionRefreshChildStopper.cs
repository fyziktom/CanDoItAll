using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Processes.Application;

public interface IProcessDecisionRefreshChildStopper {
    ProcessDecisionExecutionContract Contract { get; }
    Task<ProcessDecisionChildStopResult> StopAsync(ProcessDecisionChildMaterialBinding original,
        RuntimeCommandId admissionId, CancellationToken cancellationToken = default);
}

public interface IProcessDecisionRefreshAuthorityReader {
    Task RequireCurrentAsync(ProcessDecisionChildMaterialBinding original, RuntimeCommandId admissionId,
        CancellationToken cancellationToken = default);
}
