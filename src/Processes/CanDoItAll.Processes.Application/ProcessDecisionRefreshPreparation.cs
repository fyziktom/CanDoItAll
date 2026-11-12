using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Core;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Processes.Application;

public static class ProcessDecisionRefreshPreparation {
    public static ProcessDecisionBeginRefresh Create(ProcessRuntimeStateSnapshot state, ProcessAcceptedExecutionInput input,
        ProcessDecisionResultSelection selection, ProcessDecisionRefreshRequest request, ProcessEventActor actor) {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(selection);
        request.Validate();
        if (state.Steps.Count(step => step.IsExecutable) != 1) {
            throw new NotSupportedException("Refresh admission currently requires the original single-activity Process.");
        }
        selection.RequireOriginal(state, request.Child.StepInstanceId, new ProcessExecutionRunId(request.Child.ChildId.Value));
        if (selection.Child is not { Observation.IsCurrent: true } child || selection.Head is not { } head ||
                child.Original.Child.Address != request.Child || child.Observation.Generation != head ||
                child.Observation.Closure is not null || child.Observation.Settlement is not null ||
                selection.OtherWorkPending || selection.SharedScopePending || selection.SharedScope != request.SharedScope ||
                selection.Budget.BudgetId != request.Budget.BudgetId || selection.Budget.Revision != request.Budget.Revision) {
            throw new ProcessDecisionException(ProcessDecisionRefusal.StaleExpectation);
        }
        ProcessDecisionSingleActivityAdmission.Require(state, input, child.Original.ExecutionContract);
        var admission = input.Assignment.DecisionDataPolicy!;
        if (input.Assignment.StepInstanceId != request.Child.StepInstanceId || admission.SharedRunScopeId != request.SharedScope?.ScopeId ||
                input.Source.DatabaseProfileId != child.Original.Scope.DatabaseProfileId ||
                admission.WorkspaceNamespaceId != child.Original.Scope.WorkspaceNamespaceId) {
            throw new InvalidDataException("Refresh must retain its original accepted source, workspace and shared material scope.");
        }
        var digest = ProcessDecisionChildMaterialBindingCodec.Fingerprint(child.Original);
        ProcessDecisionBeginRefresh command = new(new(request, actor, child.Original.Child.MaterialId, input.InputFingerprint, digest));
        _ = ProcessDecisionRefreshAdmission.Begin(state, head, command, admission.Policy);
        return command;
    }
}
