using CanDoItAll.Processes.Runtime;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Processes.Application;

public sealed record ProcessDecisionRootSelection(ProcessDecisionRunAdmission Root, DecisionBudgetBalance Budget,
    ProcessDecisionRunScopeState Scope, ProcessRuntimeStatus RootStatus, ProcessLaunchAuthority Source);

public interface IProcessDecisionRootQuery {
    Task<ProcessDecisionRootSelection?> ReadAsync(ProcessRuntimeStateSnapshot state, CancellationToken cancellationToken = default);
}

public sealed class ProcessDecisionSourceProjectionRequiredException()
    : InvalidOperationException("Process preparation requires the bounded original accepted source projection.");

public enum ProcessDecisionRootPreparationStatus { Ready, CapturePending, ReviewRequired, Stopped, SourceProjectionRequired, NotRequired }

public sealed record ProcessDecisionRootPreparationResult(ProcessDecisionRootPreparationStatus Status, ProcessRuntimeStateSnapshot State);
