using CanDoItAll.Processes.Runtime;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Processes.Application;

public enum ProcessDecisionChildPendingReason { MissingJournal, ActiveWork, ApprovalRequired, BasisChanged, UnresolvedEffects, ExecutionIncomplete }
public enum ProcessDecisionChildTerminalStatus { Succeeded, Failed, Cancelled }

public abstract record ProcessDecisionChildResult {
    private protected ProcessDecisionChildResult() { }

    public sealed record Pending(ProcessDecisionChildPendingReason Reason) : ProcessDecisionChildResult;
    public sealed record Closed(ProcessDecisionChildTerminalStatus Status, ProcessDecisionChildClosure Closure,
        DecisionBudgetSettlement Work) : ProcessDecisionChildResult;
}

public interface IProcessDecisionChildOutcomeReader {
    ProcessDecisionExecutionContract Contract { get; }
    Task<ProcessDecisionChildResult> ReadAsync(ProcessDecisionChildMaterialBinding original, CancellationToken cancellationToken = default);
}
