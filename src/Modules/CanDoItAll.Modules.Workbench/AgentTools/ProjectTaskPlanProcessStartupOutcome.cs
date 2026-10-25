using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Drivers.Abstractions;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.Workbench;

internal static class ProjectTaskPlanProcessStartupOutcome {
    internal static ProcessDecisionChildResult.Closed Read(ProcessDecisionResultChild selected, ProcessExecutionOwnerEvidence evidence,
        AgentToolProfileBinding profile) {
        var original = selected.Original;
        original.Validate();
        var child = original.Child;
        var parent = AgentToolProtocolEnvelope.Create(ProcessDecisionChildMaterialBindingCodec.Format,
            ProcessDecisionChildMaterialBindingCodec.Version, ProcessDecisionChildMaterialBindingCodec.Serialize(original));
        if (evidence is not ProcessExecutionOwnerEvidence<AgentInheritedPlanningPreparationFailure> { Value: { } failure } ||
                original.ExecutionContract != ProjectTaskPlanProcessContract.Execution ||
                failure.ExecutionRunId != child.Address.ChildId.Value || failure.AgentId == Guid.Empty ||
                failure.AgentId != selected.OriginalAgentId || failure.Owner != AgentPlanningOwner.ProjectTaskPlan ||
                failure.Profile != profile || original.Scope.DatabaseProfileId != profile.ProfileId ||
                selected.OriginalProject is not { } project || project.DatabaseProfileId != profile.ProfileId ||
                failure.Scope != WorkspaceScopeDescriptor.Project(project.ProjectId.ToString("D")) ||
                failure.ParentBinding != parent || failure.Allocation != new DecisionBudgetAllocation(child.BudgetId, child.AllocationId, child.Grant) ||
                failure.StartedAtUtc == default || failure.StartedAtUtc.Offset != TimeSpan.Zero || failure.ClosedAtUtc.Offset != TimeSpan.Zero ||
                failure.ClosedAtUtc < failure.StartedAtUtc || failure.WorkTicks < (failure.ClosedAtUtc - failure.StartedAtUtc).Ticks ||
                failure.Outcome is not (RunOutcome.Failed or RunOutcome.Cancelled)) {
            throw new InvalidDataException("The closed startup failure does not match its original Process child, Agent, scope and allocation.");
        }
        return new(failure.Outcome == RunOutcome.Cancelled ? ProcessDecisionChildTerminalStatus.Cancelled : ProcessDecisionChildTerminalStatus.Failed,
            new(1, ProcessDecisionChildOutcome.NoEffect, null), new(1, new(failure.WorkTicks, 0)));
    }
}
