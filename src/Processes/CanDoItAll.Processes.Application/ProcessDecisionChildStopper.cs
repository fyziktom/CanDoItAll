using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Processes.Application;

public enum ProcessDecisionChildStopResult {
    ReadyForAccounting, ActiveWork, LeaseBusy, MissingJournal, UnresolvedEffects, CompletionPending
}

public interface IProcessDecisionChildStopper {
    ProcessDecisionExecutionContract Contract { get; }

    Task<ProcessDecisionChildStopResult> StopAsync(ProcessDecisionChildMaterialBinding original,
        CancellationToken cancellationToken = default);
}

public interface IProcessRootCancellationBarrierReader {
    Task<bool> IsStoppedAsync(ProcessRunId rootRunId, CancellationToken cancellationToken = default);
}
