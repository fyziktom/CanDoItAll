using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Drivers.Abstractions;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Processes.Application;

public sealed record ProcessDecisionResultChild(ProcessDecisionChildMaterialBinding Original, ProcessDecisionChildObservation Observation);

public sealed record ProcessDecisionResultSelection(ProcessRunId RootRunId, ProcessRunId RunId, ProcessStepInstanceId StepInstanceId,
    DecisionBudgetBalance Budget, ProcessDecisionGeneration? Head, ProcessDecisionResultChild? Child,
    bool OtherWorkPending, bool SharedScopePending) {
    public void RequireOriginal(ProcessRuntimeStateSnapshot state, ProcessStepInstanceId stepId, ProcessExecutionRunId? execution) {
        ArgumentNullException.ThrowIfNull(Budget);
        Budget.Validate();
        Head?.Validate();
        if (RootRunId != state.RootRunId || RunId != state.RunId || StepInstanceId != stepId ||
                Head is not null && Head.BudgetId != Budget.BudgetId) {
            throw new InvalidDataException("The Process result selection differs from its original run, step or budget.");
        }
        if (Child is not { } child) {
            return;
        }
        child.Original.Validate();
        child.Observation.Generation.Validate();
        child.Observation.Closure?.Validate();
        child.Observation.Settlement?.Validate();
        var address = child.Original.Child.Address;
        if (Head is null || address.RootRunId != RootRunId || address.RunId != RunId || address.StepInstanceId != StepInstanceId ||
                execution is not { } actual || address.ChildId.Value != actual.Value || child.Original.Child.BudgetId != Budget.BudgetId ||
                child.Original.Child != child.Observation.Admission || address.Reference != child.Observation.Generation.Reference ||
                Head.Reference.DecisionId != address.Reference.DecisionId || Head.Reference.Generation < address.Reference.Generation ||
                child.Observation.Generation.MaterialId != child.Original.Child.MaterialId ||
                (child.Observation.Closure is null) != (child.Observation.Settlement is null) ||
                child.Observation.Closure?.OwnerRevision != child.Observation.Settlement?.OwnerWorkRevision) {
            throw new InvalidDataException("The Process result selection does not retain the exact original child and work.");
        }
    }
}

public interface IProcessDecisionResultQuery {
    Task<ProcessDecisionResultSelection?> ReadAsync(ProcessRunId runId, ProcessStepInstanceId stepId,
        ProcessExecutionRunId? executionRunId, CancellationToken cancellationToken = default);
}
