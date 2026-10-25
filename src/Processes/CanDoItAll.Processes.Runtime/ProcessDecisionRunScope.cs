using CanDoItAll.Processes.Abstractions;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Processes.Runtime;

public sealed record ProcessDecisionRunScopeAdmission(ProcessDecisionMaterialScopeId ScopeId, ProcessDecisionFundingReference Funding,
    Guid DatabaseProfileId, Guid WorkspaceNamespaceId, ProcessDecisionContentContract ContentContract) {
    public ProcessDecisionMaterialScope MaterialScope => new(ScopeId, DatabaseProfileId, WorkspaceNamespaceId,
        Funding.RootRunId, Funding.RootRunId, null);

    public void Validate() {
        ArgumentNullException.ThrowIfNull(Funding);
        ArgumentNullException.ThrowIfNull(ContentContract);
        Funding.Validate();
        ContentContract.Validate();
        MaterialScope.Validate();
    }

    public void RequireRoot(ProcessRuntimeStateSnapshot state, ProcessDefinitionVersionId definitionVersionId, ProcessDecisionRunAdmission root) {
        Validate();
        ArgumentNullException.ThrowIfNull(root);
        root.RequireRoot(state, definitionVersionId);
        if (Funding != root.Funding || DatabaseProfileId != root.DatabaseProfileId || WorkspaceNamespaceId != root.WorkspaceNamespaceId ||
                root.Policy.ReadBasisMode != ReadBasisMode.RunSnapshot) {
            throw new InvalidDataException("A shared material scope requires its original RunSnapshot root admission and funding.");
        }
    }
}

public enum ProcessDecisionRunScopePhase { AwaitingCapture, Capturing, Active, Reconciling, Completed, Cancelled }
public enum ProcessDecisionRunCaptureKind { Initial, ManagerRefresh, AutomaticReplan }

public sealed record ProcessDecisionRunScopeExpectation(ProcessDecisionMaterialScopeId ScopeId, long Revision) {
    public void Validate() {
        if (ScopeId.Value == Guid.Empty || Revision < 1) {
            throw new InvalidDataException("A shared material expectation requires its original scope and positive revision.");
        }
    }
}

public sealed record ProcessDecisionRunScopeMaterial(ProcessDecisionMaterialStamp Stamp, DateTimeOffset CapturedAtUtc) {
    public void Validate() {
        ArgumentNullException.ThrowIfNull(Stamp);
        Stamp.Validate();
        if (CapturedAtUtc == default || CapturedAtUtc.Offset != TimeSpan.Zero) {
            throw new InvalidDataException("Shared material must retain its original UTC capture time.");
        }
    }

    public static ProcessDecisionRunScopeMaterial From(ProcessDecisionMaterial material) => new(material.Stamp, material.CapturedAtUtc);
}

public sealed record ProcessDecisionRunScopeCapture(DecisionBudgetAllocationId AllocationId, ProcessDecisionMaterialId MaterialId,
    ProcessDecisionRunCaptureKind Kind) {
    public void Validate() {
        if (AllocationId.Value == Guid.Empty || MaterialId.Value == Guid.Empty || !Enum.IsDefined(Kind)) {
            throw new InvalidDataException("A shared capture requires its original allocation, material identity and capture kind.");
        }
    }
}

public sealed record ProcessDecisionRunScopeState(ProcessDecisionRunScopeAdmission Admission, long Revision,
    ProcessDecisionRunScopePhase Phase, ProcessDecisionRunScopeMaterial? Material, int TotalMemberships, int CurrentMemberships,
    ProcessDecisionRunScopeCapture? Capture = null) {
    public const int MaximumMemberships = DecisionBudgetBalance.MaximumAllocations;
    public ProcessDecisionRunScopeExpectation Expectation => new(Admission.ScopeId, Revision);

    public void Validate() {
        ArgumentNullException.ThrowIfNull(Admission);
        Admission.Validate();
        Expectation.Validate();
        Material?.Validate();
        Capture?.Validate();
        var generation = Material?.Stamp.Reference.Generation ?? 0;
        if (!Enum.IsDefined(Phase) || TotalMemberships is < 0 or > MaximumMemberships ||
                CurrentMemberships < 0 || CurrentMemberships > TotalMemberships ||
                Material is not null && Material.Stamp.Reference.ScopeId != Admission.ScopeId ||
                Material is null && (TotalMemberships != 0 || Phase is ProcessDecisionRunScopePhase.Active or ProcessDecisionRunScopePhase.Completed) ||
                Phase == ProcessDecisionRunScopePhase.AwaitingCapture && Material is not null ||
                (Phase == ProcessDecisionRunScopePhase.Capturing) != (Capture is not null) ||
                Capture is not null && ((Material is null) != (Capture.Kind == ProcessDecisionRunCaptureKind.Initial) ||
                    Capture.MaterialId == Material?.Stamp.Reference.MaterialId || generation == ProcessDecisionGeneration.MaximumGenerations) ||
                Revision < 1L + TotalMemberships + 2L * generation + (Capture is null ? 0 : 1)) {
            throw new InvalidDataException("The retained shared material head has an invalid phase, capture or membership balance.");
        }
    }
}

public sealed record ProcessDecisionRunScopeMembership(ProcessDecisionMaterialReference Material, ProcessRunId RunId,
    ProcessStepInstanceId StepInstanceId, ProcessDecisionReference Decision) {
    public void Validate() {
        ArgumentNullException.ThrowIfNull(Material);
        ArgumentNullException.ThrowIfNull(Decision);
        Material.Validate();
        Decision.Validate();
        if (RunId.Value == Guid.Empty || StepInstanceId.Value == Guid.Empty) {
            throw new InvalidDataException("A shared material member requires its original real run, step and decision generation.");
        }
    }
}

public sealed record ProcessDecisionRunScopeMemberObservation(ProcessDecisionRunScopeMembership Membership,
    ProcessDecisionGeneration Decision, bool HasActiveClaim) {
    public void Validate() {
        ArgumentNullException.ThrowIfNull(Membership);
        ArgumentNullException.ThrowIfNull(Decision);
        Membership.Validate();
        Decision.Validate();
        if (Decision.Reference != Membership.Decision || Decision.MaterialId != Membership.Material.MaterialId) {
            throw new InvalidDataException("The shared material member has different original decision evidence.");
        }
    }
}

public enum ProcessDecisionRunScopeRefusal { StaleExpectation, InvalidPhase, MembershipCapacity, GenerationCapacity,
    OriginalMembershipChanged, IncompleteMembership, UnclosedWork, OriginalCaptureChanged }

public sealed record ProcessDecisionRunScopeJoin(bool Changed, ProcessDecisionRunScopeState State,
    ProcessDecisionRunScopeMembership Membership);

public sealed class ProcessDecisionRunScopeException(ProcessDecisionRunScopeRefusal reason)
    : InvalidOperationException($"The shared Process material scope refused the transition: {reason}.") {
    public ProcessDecisionRunScopeRefusal Reason { get; } = reason;
}
