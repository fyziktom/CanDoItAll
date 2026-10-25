using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Core;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Processes.Application;

public sealed record ProcessDecisionPendingReview(ProcessDecisionChildAddress Child, StrategyId StrategyId,
    StrategyResultIdempotencyKey WaitingResultKey, ProcessDecisionExpectation Decision, ProcessDecisionBudgetExpectation Budget) {
    public ProcessDecisionReviewRequest ForAction(Guid actionId) {
        ProcessDecisionReviewRequest request = new(Child, StrategyId, WaitingResultKey, new(actionId), Decision, Budget);
        request.Validate();
        return request;
    }
}

public sealed record ProcessDecisionReviewObservation(ProcessDecisionResultChild Child,
    ProcessDecisionPendingReview? Pending, ProcessValidationFailure? Refusal, bool RuntimeCompletionObserved = false) {
    public bool IsSettled => RuntimeCompletionObserved && Child.Observation.Closure is not null && Child.Observation.Settlement is not null;
}
