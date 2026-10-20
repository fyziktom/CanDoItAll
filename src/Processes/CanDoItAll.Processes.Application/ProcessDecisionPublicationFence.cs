using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Processes.Application;

public interface IProcessDecisionPublicationFence {
    Task RequireAsync(ProcessDecisionChildAdmission original, ProcessDecisionPublicationKind kind,
        CancellationToken cancellationToken = default);

    Task RequireSelectedAsync(ProcessDecisionChildMaterialBinding original, ProcessExecutionDispatchAuthority expectedSource,
        ProcessDecisionExecutionContract supportedExecution, ProcessDecisionContentContract? supportedContent,
        ProcessDecisionPublicationKind kind, CancellationToken cancellationToken = default);
}
