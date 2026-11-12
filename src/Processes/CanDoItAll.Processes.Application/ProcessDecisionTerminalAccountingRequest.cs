using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Processes.Application;

public sealed record ProcessDecisionTerminalAccountingRequest(ProcessDecisionChildAddress Child,
    ProcessDecisionExpectation Decision, ProcessDecisionBudgetExpectation Budget,
    ProcessRuntimeStepStatus StepStatus, RuntimeCommandId ActionId) {
    public void Validate() {
        ArgumentNullException.ThrowIfNull(Child);
        ArgumentNullException.ThrowIfNull(Decision);
        ArgumentNullException.ThrowIfNull(Budget);
        Child.Validate();
        Decision.Validate();
        if (ActionId.Value == Guid.Empty || Decision.Reference != Child.Reference ||
                Budget.StepInstanceId != Child.StepInstanceId || Budget.BudgetId.Value == Guid.Empty || Budget.Revision < 1 ||
                StepStatus is not (ProcessRuntimeStepStatus.Completed or ProcessRuntimeStepStatus.Failed or ProcessRuntimeStepStatus.Cancelled)) {
            throw new InvalidDataException("Terminal accounting requires an exact child, displayed decision/budget, terminal outcome and retained action.");
        }
    }
}
