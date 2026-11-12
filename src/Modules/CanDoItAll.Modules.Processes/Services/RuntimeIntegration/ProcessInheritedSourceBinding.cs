using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.AgentFramework.Hosting;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Modules.Processes;

internal static class ProcessInheritedSourceBinding {
    internal static (ProcessDecisionChildMaterialBinding Parent, ProcessDecisionContentContract? Content) Read(
        ExecutionRunRecord execution, ProcessExecutionClaimEvidence evidence,
        IReadOnlyDictionary<ProcessDecisionExecutionContract, ProcessDecisionContentContract?> contracts) {
        var journal = execution.ToolAdmission ?? throw new AgentToolReceiptAccessDeniedException();
        journal.Validate();
        var original = journal.PlanningDecision?.InheritedOrigin ?? throw new AgentToolReceiptAccessDeniedException();
        var selected = ReadInitial(execution, journal.Session.Profile, evidence, original.ParentBinding, contracts);
        var parent = selected.Parent;
        if (original.AgentId != execution.AgentId || journal.Session.AgentId != execution.AgentId ||
            journal.Session.Reference.ExecutionRunId != execution.Id || original.Profile != journal.Session.Profile ||
            original.Source != journal.Session.Reference.BackgroundSource || original.Source.SourceKind != execution.SourceKind ||
            original.Source.SourceId != evidence.StepKey || original.Source.SourceId != execution.SourceId ||
            parent.Scope.WorkspaceNamespaceId != original.WorkspaceNamespaceId || parent.Scope.ReadBasisMode != original.ParentPolicy.ReadBasisMode ||
            parent.Child.MaterialId.Value != original.BasisId || parent.Child.BudgetId != original.Allocation.BudgetId ||
            parent.Child.AllocationId != original.Allocation.Id || parent.Child.Grant != original.Allocation.Grant) {
            throw new AgentToolReceiptAccessDeniedException();
        }
        return selected;
    }

    internal static (ProcessDecisionChildMaterialBinding Parent, ProcessDecisionContentContract? Content) ReadInitial(
        ExecutionRunRecord execution, AgentToolProfileBinding profile, ProcessExecutionClaimEvidence evidence,
        AgentToolProtocolEnvelope envelope, IReadOnlyDictionary<ProcessDecisionExecutionContract, ProcessDecisionContentContract?> contracts) {
        ArgumentNullException.ThrowIfNull(envelope);
        if (envelope.Format != ProcessDecisionChildMaterialBindingCodec.Format || envelope.Version != ProcessDecisionChildMaterialBindingCodec.Version) {
            throw new NotSupportedException("The original inherited Process material binding is unsupported.");
        }
        var parent = ProcessDecisionChildMaterialBindingCodec.Deserialize(envelope.PayloadJson);
        if (!contracts.TryGetValue(parent.ExecutionContract, out var content)) {
            throw new NotSupportedException("The original inherited Process execution contract has no registered interpretation.");
        }
        var address = parent.Child.Address;
        if (execution.SourceKind != ProcessMockAgentCatalog.ProcessSourceKind || execution.SourceId != evidence.StepKey ||
            evidence.ExecutionRunId != execution.Id || evidence.ExecutorAgentId != execution.AgentId ||
            parent.ExecutionContract.Executor != ProcessDecisionChildOwner.Agent ||
            address.ChildId.Value != execution.Id || address.RunId != evidence.RunId || address.StepInstanceId != evidence.StepInstanceId ||
            parent.Scope.DatabaseProfileId != profile.ProfileId) {
            throw new AgentToolReceiptAccessDeniedException();
        }
        return (parent, content);
    }

    internal static void RequireOriginalPolicy(AgentPlanningInheritedOrigin original, ProcessExecutionDispatchAuthorityResult result) {
        if (result.OriginalDecisionPolicy != original.ParentPolicy || result.OriginalChildOnBasisChange != original.ChildOnBasisChange) {
            throw new AgentToolReceiptAccessDeniedException();
        }
    }
}
