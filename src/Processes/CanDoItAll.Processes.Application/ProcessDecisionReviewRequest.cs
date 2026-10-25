using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.Processes.Drivers.Abstractions;
using CanDoItAll.Processes.Core;

namespace CanDoItAll.Processes.Application;

public sealed record ProcessDecisionReviewRequest(ProcessDecisionChildAddress Child, StrategyId StrategyId,
    StrategyResultIdempotencyKey WaitingResultKey, StrategyResultIdempotencyKey CompletionKey,
    ProcessDecisionExpectation Decision, ProcessDecisionBudgetExpectation Budget) {
    public void Validate() {
        ArgumentNullException.ThrowIfNull(Child);
        ArgumentNullException.ThrowIfNull(Decision);
        ArgumentNullException.ThrowIfNull(Budget);
        Child.Validate();
        Decision.Validate();
        if (!ProcessStrategyReceiptValuePolicy.IsStableIdentifier(StrategyId.Value) ||
                WaitingResultKey.Value == Guid.Empty || CompletionKey.Value == Guid.Empty || WaitingResultKey == CompletionKey ||
                Decision.Reference != Child.Reference || Budget.StepInstanceId != Child.StepInstanceId ||
                Budget.BudgetId.Value == Guid.Empty || Budget.Revision < 1) {
            throw new InvalidDataException("Review completion requires the original child, waiting result and displayed decision/budget expectations.");
        }
    }
}

public sealed class ProcessDecisionReviewAdmissionException(ProcessValidationFailure refusal) : InvalidOperationException(refusal.Message) {
    public ProcessValidationFailure Refusal { get; } = refusal;
}
