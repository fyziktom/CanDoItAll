using CanDoItAll.Processes.Abstractions;

namespace CanDoItAll.Processes.Runtime;

public sealed record ProcessRuntimeDispatchPrecondition(ProcessStepInstanceId StepInstanceId,
    DispatchClaimToken ClaimToken, DispatcherOwnerId OwnerId) {
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public ProcessDecisionExecutionContract? DecisionContract { get; init; }

    public void Validate() {
        DecisionContract?.Validate();
        if (StepInstanceId.Value == Guid.Empty || ClaimToken.Value == Guid.Empty || string.IsNullOrWhiteSpace(OwnerId.Value)) {
            throw new InvalidDataException("Dispatch attachment requires its original step, claim and dispatcher owner.");
        }
    }
}

public enum ProcessRuntimeDispatchPreconditionRefusal { RuntimeInactive, ClaimChanged, LeaseExpired }

public sealed class ProcessRuntimeDispatchPreconditionException(ProcessRunId runId, ProcessStepInstanceId stepId,
    ProcessRuntimeDispatchPreconditionRefusal refusal)
    : Exception($"Process dispatch attachment for run '{runId}', step '{stepId}' was refused: {refusal}.") {
    public ProcessRunId RunId { get; } = runId;
    public ProcessStepInstanceId StepInstanceId { get; } = stepId;
    public ProcessRuntimeDispatchPreconditionRefusal Refusal { get; } = refusal;
}
