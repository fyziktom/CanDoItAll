using CanDoItAll.Processes.Abstractions;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Processes.Runtime;

public sealed record ProcessDecisionBudgetExpectation(ProcessStepInstanceId? StepInstanceId, DecisionBudgetId BudgetId, long Revision);

public abstract record ProcessDecisionBudgetCommand(ProcessDecisionBudgetExpectation Expectation, DecisionBudgetAllocationId AllocationId) {
    public void Validate() {
        if (Expectation is null || Expectation.StepInstanceId is { Value: var step } && step == Guid.Empty || Expectation.BudgetId.Value == Guid.Empty ||
                Expectation.Revision < 1 || AllocationId.Value == Guid.Empty) {
            throw new InvalidDataException("Process accounting requires its real run/step owner, budget revision and allocation identity.");
        }
        switch (this) {
            case ProcessDecisionBudgetReservation reservation when reservation.Grant is not null:
                new DecisionBudgetAllocation(Expectation.BudgetId, AllocationId, reservation.Grant).Validate();
                break;
            case ProcessDecisionBudgetSettlement settlement when settlement.Settlement is not null:
                settlement.Settlement.Validate();
                break;
            default:
                throw new InvalidDataException("The Process accounting operation is unsupported or incomplete.");
        }
    }
}

public sealed record ProcessDecisionBudgetReservation(ProcessDecisionBudgetExpectation Expectation,
    DecisionBudgetAllocationId AllocationId, DecisionBudgetAmount Grant) : ProcessDecisionBudgetCommand(Expectation, AllocationId);

public sealed record ProcessDecisionBudgetSettlement(ProcessDecisionBudgetExpectation Expectation,
    DecisionBudgetAllocationId AllocationId, DecisionBudgetSettlement Settlement) : ProcessDecisionBudgetCommand(Expectation, AllocationId);

public sealed record ProcessDecisionBudgetReceipt(ProcessDecisionBudgetCommand Command, DecisionBudgetChange Change) {
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public ProcessDecisionBudgetFundingReceipt? Funding { get; init; }
}

public sealed record ProcessDecisionBudgetFundingReceipt(ProcessRunId RootRunId, ProcessDecisionBudgetReceipt Receipt);
