namespace CanDoItAll.Processes.Contracts;

public enum ProcessRuntimeHostContractSurface {
    VerificationHost = 1,
    DryRunExecution = 2,
    ManagerReadback = 3,
    OperatorStatus = 4,
    SchedulerWorkflowReadOnlyJob = 5
}

public enum ProcessRuntimeHostContractViolationKind {
    ProductionExecutionAllowed = 1,
    MutationNotRecordedAsDenied = 2,
    ProcessMutationAllowed = 3,
    TransitionMutationAllowed = 4,
    FinalizerMutationAllowed = 5
}

public readonly record struct ProcessRuntimeHostContractVersion(int Major, int Minor, int Patch) {
    public static ProcessRuntimeHostContractVersion Current { get; } = new(1, 1, 0);

    public override string ToString() {
        return $"{Major}.{Minor}.{Patch}";
    }
}

public sealed record ProcessRuntimeHostContractViolation(
    ProcessRuntimeHostContractViolationKind Kind,
    string Message);

public sealed record ProcessRuntimeHostContractSnapshot(
    ProcessRuntimeHostContractVersion Version,
    ProcessRuntimeHostContractSurface Surface,
    bool DryRunOnly,
    bool NoMutationPerformed,
    bool AllowsProcessMutation,
    bool AllowsTransitionMutation,
    bool AllowsFinalizerMutation) {
    public static ProcessRuntimeHostContractSnapshot Create(
        ProcessRuntimeHostContractSurface surface,
        bool dryRunOnly = true) {
        return new ProcessRuntimeHostContractSnapshot(
            ProcessRuntimeHostContractVersion.Current,
            surface,
            dryRunOnly,
            NoMutationPerformed: true,
            AllowsProcessMutation: false,
            AllowsTransitionMutation: false,
            AllowsFinalizerMutation: false);
    }

    public bool IsReadOnlySafe =>
        DryRunOnly &&
        NoMutationPerformed &&
        !AllowsProcessMutation &&
        !AllowsTransitionMutation &&
        !AllowsFinalizerMutation;

    public IReadOnlyList<ProcessRuntimeHostContractViolation> ValidateReadOnlySafety() {
        var violations = new List<ProcessRuntimeHostContractViolation>();
        AddIf(DryRunOnly, ProcessRuntimeHostContractViolationKind.ProductionExecutionAllowed, "Runtime host contract must remain dry-run/read-only only.");
        AddIf(NoMutationPerformed, ProcessRuntimeHostContractViolationKind.MutationNotRecordedAsDenied, "Runtime host contract must record that no mutation was performed.");
        AddIf(!AllowsProcessMutation, ProcessRuntimeHostContractViolationKind.ProcessMutationAllowed, "Runtime host contract must not allow process mutation.");
        AddIf(!AllowsTransitionMutation, ProcessRuntimeHostContractViolationKind.TransitionMutationAllowed, "Runtime host contract must not allow transition mutation.");
        AddIf(!AllowsFinalizerMutation, ProcessRuntimeHostContractViolationKind.FinalizerMutationAllowed, "Runtime host contract must not allow finalizer mutation.");
        return violations;

        void AddIf(bool valid, ProcessRuntimeHostContractViolationKind kind, string message) {
            if (!valid) {
                violations.Add(new ProcessRuntimeHostContractViolation(kind, message));
            }
        }
    }
}
