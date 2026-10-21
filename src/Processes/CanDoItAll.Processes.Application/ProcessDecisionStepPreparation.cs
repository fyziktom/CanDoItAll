using CanDoItAll.Processes.Runtime;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Processes.Application;

public sealed record ProcessDecisionStepSelection(ProcessLaunchAuthority Source, ProcessDecisionDataPolicyAdmission Policy,
    Guid ExecutorId, string StepKey, DecisionBudgetBalance Budget, DecisionBudgetBalance RootBudget,
    ProcessDecisionGeneration? Head, ProcessDecisionRunScopeState? SharedScope, bool HasUnclosedWork,
    ProcessDecisionStepMaterial? Material);

public sealed record ProcessDecisionStepMaterial(ProcessDecisionMaterialScope Scope, ProcessDecisionMaterialStamp Stamp,
    DateTimeOffset CapturedAtUtc);

public interface IProcessDecisionStepQuery {
    Task<ProcessDecisionStepSelection> ReadAsync(ProcessRuntimeStateSnapshot state,
        ProcessRuntimeDispatchPrecondition claim, CancellationToken cancellationToken = default);
}

public enum ProcessDecisionStepPreparationStatus { Ready, Pending, ReviewRequired, Stopped, SourceProjectionRequired }

public sealed record ProcessDecisionStepPreparationResult(ProcessDecisionStepPreparationStatus Status,
    ProcessRuntimeStateSnapshot State, ProcessDecisionChildMaterialBinding? Child = null, bool EnrolledNow = false);
