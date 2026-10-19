using CanDoItAll.Processes.Abstractions;

namespace CanDoItAll.Processes.Runtime;

public enum ProcessDecisionPhaseChange { WaitForReview, BeginReconciliation, Complete, Cancel }
public enum ProcessDecisionRefreshKind { ManagerRefresh, AutomaticReplan }

public abstract record ProcessDecisionCommand(ProcessStepInstanceId StepInstanceId) {
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public ProcessDecisionMaterialStamp? Material { get; init; }

    public void Validate() {
        if (StepInstanceId.Value == Guid.Empty) {
            throw new InvalidDataException("A Process decision command requires its original step.");
        }
        if (Material is { } material) {
            material.Validate();
            var materialId = this switch {
                ProcessDecisionStart start => start.MaterialId,
                ProcessDecisionRefresh refresh => refresh.MaterialId,
                _ => throw new InvalidDataException("Only a Process start or refresh can admit original material.")
            };
            if (material.Reference.MaterialId != materialId) {
                throw new InvalidDataException("The Process command and its original material identity disagree.");
            }
        }
        switch (this) {
            case ProcessDecisionStart start when start.DecisionId.Value != Guid.Empty && start.MaterialId.Value != Guid.Empty:
                return;
            case ProcessDecisionEnrollment enrollment when enrollment.Expectation is not null && enrollment.Child is not null:
                enrollment.Expectation.Validate();
                enrollment.Child.Validate();
                if (enrollment.Child.Reference != enrollment.Expectation.Reference || enrollment.Child.Closure is not null) {
                    throw new InvalidDataException("Enrollment requires an open child of the expected generation.");
                }
                return;
            case ProcessDecisionClosure closure when closure.Expectation is not null && closure.ChildId.Value != Guid.Empty && closure.Closure is not null:
                closure.Expectation.Validate();
                closure.Closure.Validate();
                return;
            case ProcessDecisionChangePhase phase when phase.Expectation is not null && Enum.IsDefined(phase.Change):
                phase.Expectation.Validate();
                return;
            case ProcessDecisionRefresh refresh when refresh.Expectation is not null && refresh.MaterialId.Value != Guid.Empty && Enum.IsDefined(refresh.Kind):
                refresh.Expectation.Validate();
                return;
            default:
                throw new InvalidDataException("The Process decision command is unsupported or incomplete.");
        }
    }

    public ProcessDecisionExpectation? Expected => this switch {
        ProcessDecisionStart => null,
        ProcessDecisionEnrollment enrollment => enrollment.Expectation,
        ProcessDecisionClosure closure => closure.Expectation,
        ProcessDecisionChangePhase phase => phase.Expectation,
        ProcessDecisionRefresh refresh => refresh.Expectation,
        _ => throw new InvalidDataException("The Process decision command is unsupported.")
    };
}

public sealed record ProcessDecisionStart(ProcessStepInstanceId StepInstanceId, ProcessDecisionId DecisionId,
    ProcessDecisionMaterialId MaterialId) : ProcessDecisionCommand(StepInstanceId);
public sealed record ProcessDecisionEnrollment(ProcessStepInstanceId StepInstanceId, ProcessDecisionExpectation Expectation,
    ProcessDecisionChild Child) : ProcessDecisionCommand(StepInstanceId);
public sealed record ProcessDecisionClosure(ProcessStepInstanceId StepInstanceId, ProcessDecisionExpectation Expectation,
    ProcessDecisionChildId ChildId, ProcessDecisionChildClosure Closure) : ProcessDecisionCommand(StepInstanceId);
public sealed record ProcessDecisionChangePhase(ProcessStepInstanceId StepInstanceId, ProcessDecisionExpectation Expectation,
    ProcessDecisionPhaseChange Change) : ProcessDecisionCommand(StepInstanceId);
public sealed record ProcessDecisionRefresh(ProcessStepInstanceId StepInstanceId, ProcessDecisionExpectation Expectation,
    ProcessDecisionMaterialId MaterialId, ProcessDecisionRefreshKind Kind) : ProcessDecisionCommand(StepInstanceId);

public sealed record ProcessDecisionReceipt(ProcessDecisionCommand Command, bool Changed, ProcessDecisionGeneration Current,
    ProcessDecisionGeneration? Previous = null, ProcessDecisionChild? Child = null);
public sealed record ProcessDecisionReconciliation(ProcessDecisionGeneration Generation, IReadOnlyList<ProcessDecisionChild> Children);
