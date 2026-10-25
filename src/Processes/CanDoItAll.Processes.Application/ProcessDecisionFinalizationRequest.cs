using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Processes.Application;

public sealed record ProcessDecisionFinalizationRequest(ProcessStepInstanceId StepInstanceId,
    ProcessDecisionExpectation Decision, ProcessDecisionBudgetExpectation Budget,
    ProcessRuntimeStepStatus StepStatus, RuntimeCommandId ActionId) {
    public void Validate() {
        if (ActionId.Value == Guid.Empty) {
            throw new InvalidDataException("Terminal decision finalization requires its retained original action identity.");
        }
        Command.Validate();
    }

    internal ProcessDecisionFinalization Command => new(StepInstanceId, Decision, Budget, StepStatus);
}
