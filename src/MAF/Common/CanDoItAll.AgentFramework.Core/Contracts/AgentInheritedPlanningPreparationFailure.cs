using CanDoItAll.AgentFramework.Models;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.AgentFramework.Core;

public sealed class AgentInheritedPlanningPreparationFailure {
    internal AgentInheritedPlanningPreparationFailure(AgentInheritedPlanningWorkspaceRequest request,
        AgentToolProfileBinding profile, DateTimeOffset startedAtUtc, DateTimeOffset closedAtUtc, long workTicks, bool cancelled) {
        ExecutionRunId = request.ExecutionRunId;
        AgentId = request.AgentId;
        Owner = request.Owner;
        Scope = request.ContextScope;
        ParentBinding = request.ParentBinding;
        Allocation = request.Start.Allocation;
        Profile = profile;
        StartedAtUtc = startedAtUtc;
        ClosedAtUtc = closedAtUtc;
        WorkTicks = workTicks;
        Outcome = cancelled ? RunOutcome.Cancelled : RunOutcome.Failed;
    }

    public Guid ExecutionRunId { get; }
    public Guid AgentId { get; }
    public AgentPlanningOwner Owner { get; }
    public WorkspaceScopeDescriptor Scope { get; }
    public AgentToolProtocolEnvelope ParentBinding { get; }
    public DecisionBudgetAllocation Allocation { get; }
    public AgentToolProfileBinding Profile { get; }
    public DateTimeOffset StartedAtUtc { get; }
    public DateTimeOffset ClosedAtUtc { get; }
    public long WorkTicks { get; }
    public RunOutcome Outcome { get; }
}

public sealed class AgentInheritedPlanningPreparationException : Exception {
    internal AgentInheritedPlanningPreparationException(AgentInheritedPlanningPreparationFailure failure, Exception inner)
        : base("Inherited planning preparation ended before a run was saved.", inner) {
        Failure = failure;
    }

    public AgentInheritedPlanningPreparationFailure Failure { get; }
}
