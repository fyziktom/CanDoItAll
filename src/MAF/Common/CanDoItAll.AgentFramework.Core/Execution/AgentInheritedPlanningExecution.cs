using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

public sealed class AgentInheritedPlanningExecution {
    private readonly AgentToolRunLease lease;
    private readonly Func<CancellationToken, Task<AgentPlanningExecutionResult>> execute;
    private int started;

    internal AgentInheritedPlanningExecution(AgentToolRunLease lease,
        Func<CancellationToken, Task<AgentPlanningExecutionResult>> execute) {
        this.lease = lease;
        this.execute = execute;
    }

    public ExecutionRunRecord Run => (lease.PlanningWork
        ?? throw new InvalidOperationException("The inherited execution has no live work owner.")).OriginalRun;

    public Task<AgentPlanningExecutionResult> RunAsync(CancellationToken cancellationToken = default) {
        _ = Run;
        if (Interlocked.Exchange(ref started, 1) != 0) {
            throw new AgentToolAdmissionException("planning.execution-already-started", "The live inherited execution can enter its provider loop only once.");
        }
        return execute(cancellationToken);
    }
}
