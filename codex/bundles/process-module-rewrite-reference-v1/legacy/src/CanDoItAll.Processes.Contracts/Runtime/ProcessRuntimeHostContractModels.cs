namespace CanDoItAll.Processes.Contracts;

public enum ProcessRuntimeHostContractSurface {
    VerificationHost = 1,
    DryRunExecution = 2,
    ManagerReadback = 3,
    OperatorStatus = 4,
    SchedulerWorkflowReadOnlyJob = 5
}

public enum ProcessRuntimeHostEffectSurface {
    ProcessState = 1,
    Transition = 2,
    Finalizer = 3,
    Claim = 4,
    Retry = 5,
    WorkspaceStorage = 6,
    ManagedStorage = 7,
    LocalCommand = 8,
    PackageRestore = 9,
    Network = 10,
    ExternalService = 11,
    BusinessRecord = 12,
    ProviderRepair = 13
}

public enum ProcessRuntimeHostOperationCategory {
    ReadOnlyInspection = 1,
    DiagnosticReadback = 2,
    DryRunPlanning = 3,
    ExecutionCapableSideEffect = 4
}

public enum ProcessRuntimeHostSandboxDecisionKind {
    Denied = 1,
    DryRunPlanAccepted = 2,
    FutureExecutionPrerequisitesSatisfied = 3
}

public enum ProcessRuntimeHostDenialCategory {
    Governance = 1,
    Authorization = 2,
    Sandbox = 3,
    SideEffect = 4,
    UnsupportedOperation = 5
}

public enum ProcessRuntimeHostContractViolationKind {
    ProductionExecutionAllowed = 1,
    MutationNotRecordedAsDenied = 2,
    ProcessMutationAllowed = 3,
    TransitionMutationAllowed = 4,
    FinalizerMutationAllowed = 5,
    SandboxExecutionAllowed = 6
}

public readonly record struct ProcessRuntimeHostContractVersion(int Major, int Minor, int Patch) {
    public static ProcessRuntimeHostContractVersion Current { get; } = new(1, 2, 0);

    public override string ToString() {
        return $"{Major}.{Minor}.{Patch}";
    }
}

public sealed record ProcessRuntimeHostContractViolation(
    ProcessRuntimeHostContractViolationKind Kind,
    string Message);

public sealed record ProcessRuntimeHostRequestIdentity {
    public ProcessRuntimeHostRequestIdentity(
        Guid requestId,
        Guid processRunId,
        Guid? stepRunId,
        string requestedBy,
        DateTimeOffset requestedAt) {
        if (requestId == Guid.Empty) {
            throw new ArgumentException("Runtime host request id is required.", nameof(requestId));
        }

        if (processRunId == Guid.Empty) {
            throw new ArgumentException("Runtime host process run id is required.", nameof(processRunId));
        }

        if (stepRunId == Guid.Empty) {
            throw new ArgumentException("Runtime host step run id cannot be empty.", nameof(stepRunId));
        }

        if (string.IsNullOrWhiteSpace(requestedBy)) {
            throw new ArgumentException("Runtime host requester identity is required.", nameof(requestedBy));
        }

        RequestId = requestId;
        ProcessRunId = processRunId;
        StepRunId = stepRunId;
        RequestedBy = requestedBy.Trim();
        RequestedAt = requestedAt;
    }

    public Guid RequestId { get; }

    public Guid ProcessRunId { get; }

    public Guid? StepRunId { get; }

    public string RequestedBy { get; }

    public DateTimeOffset RequestedAt { get; }
}

public sealed record ProcessRuntimeHostDenial {
    public ProcessRuntimeHostDenial(
        ProcessRuntimeHostDenialCategory category,
        string code,
        string message,
        IReadOnlyList<ProcessRuntimeHostEffectSurface> surfaces) {
        if (!Enum.IsDefined(category)) {
            throw new ArgumentOutOfRangeException(nameof(category), "Runtime host denial category is not supported.");
        }

        if (string.IsNullOrWhiteSpace(code)) {
            throw new ArgumentException("Runtime host denial code is required.", nameof(code));
        }

        if (string.IsNullOrWhiteSpace(message)) {
            throw new ArgumentException("Runtime host denial message is required.", nameof(message));
        }

        ArgumentNullException.ThrowIfNull(surfaces);
        if (surfaces.Any(surface => !Enum.IsDefined(surface))) {
            throw new ArgumentOutOfRangeException(nameof(surfaces), "Runtime host denial contains an unsupported effect surface.");
        }

        Category = category;
        Code = code.Trim();
        Message = message.Trim();
        Surfaces = surfaces.Distinct().ToArray();
    }

    public ProcessRuntimeHostDenialCategory Category { get; }

    public string Code { get; }

    public string Message { get; }

    public IReadOnlyList<ProcessRuntimeHostEffectSurface> Surfaces { get; }
}

public sealed record ProcessRuntimeHostSandboxDecision {
    public ProcessRuntimeHostSandboxDecision(
        ProcessRuntimeHostSandboxDecisionKind kind,
        bool executionAllowed,
        bool dryRunOnly,
        IReadOnlyList<ProcessRuntimeHostEffectSurface> requestedSurfaces,
        IReadOnlyList<ProcessRuntimeHostEffectSurface> deniedSurfaces,
        IReadOnlyList<ProcessRuntimeHostDenial> denials) {
        if (!Enum.IsDefined(kind)) {
            throw new ArgumentOutOfRangeException(nameof(kind), "Runtime host sandbox decision is not supported.");
        }

        ArgumentNullException.ThrowIfNull(requestedSurfaces);
        ArgumentNullException.ThrowIfNull(deniedSurfaces);
        ArgumentNullException.ThrowIfNull(denials);

        if (requestedSurfaces.Any(surface => !Enum.IsDefined(surface))) {
            throw new ArgumentOutOfRangeException(nameof(requestedSurfaces), "Runtime host sandbox decision contains an unsupported requested surface.");
        }

        if (deniedSurfaces.Any(surface => !Enum.IsDefined(surface))) {
            throw new ArgumentOutOfRangeException(nameof(deniedSurfaces), "Runtime host sandbox decision contains an unsupported denied surface.");
        }

        Kind = kind;
        ExecutionAllowed = executionAllowed;
        DryRunOnly = dryRunOnly;
        RequestedSurfaces = requestedSurfaces.Distinct().ToArray();
        DeniedSurfaces = deniedSurfaces.Distinct().ToArray();
        Denials = denials.ToArray();
    }

    public ProcessRuntimeHostSandboxDecisionKind Kind { get; }

    public bool ExecutionAllowed { get; }

    public bool DryRunOnly { get; }

    public IReadOnlyList<ProcessRuntimeHostEffectSurface> RequestedSurfaces { get; }

    public IReadOnlyList<ProcessRuntimeHostEffectSurface> DeniedSurfaces { get; }

    public IReadOnlyList<ProcessRuntimeHostDenial> Denials { get; }
}

public sealed record ProcessRuntimeHostAuditReference {
    public ProcessRuntimeHostAuditReference(
        string auditId,
        string contentHash,
        DateTimeOffset recordedAt) {
        if (string.IsNullOrWhiteSpace(auditId)) {
            throw new ArgumentException("Runtime host audit id is required.", nameof(auditId));
        }

        if (string.IsNullOrWhiteSpace(contentHash)) {
            throw new ArgumentException("Runtime host audit content hash is required.", nameof(contentHash));
        }

        AuditId = auditId.Trim();
        ContentHash = contentHash.Trim();
        RecordedAt = recordedAt;
    }

    public string AuditId { get; }

    public string ContentHash { get; }

    public DateTimeOffset RecordedAt { get; }
}

public sealed record ProcessRuntimeHostCapabilityDescriptorReference {
    public ProcessRuntimeHostCapabilityDescriptorReference(
        string key,
        ProcessRuntimeHostContractSurface contractSurface,
        ProcessRuntimeHostOperationCategory operationCategory) {
        if (string.IsNullOrWhiteSpace(key)) {
            throw new ArgumentException("Runtime host capability descriptor key is required.", nameof(key));
        }

        if (!Enum.IsDefined(contractSurface)) {
            throw new ArgumentOutOfRangeException(nameof(contractSurface), "Runtime host capability descriptor surface is not supported.");
        }

        if (!Enum.IsDefined(operationCategory)) {
            throw new ArgumentOutOfRangeException(nameof(operationCategory), "Runtime host operation category is not supported.");
        }

        Key = key.Trim();
        ContractSurface = contractSurface;
        OperationCategory = operationCategory;
    }

    public string Key { get; }

    public ProcessRuntimeHostContractSurface ContractSurface { get; }

    public ProcessRuntimeHostOperationCategory OperationCategory { get; }
}

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

    public ProcessRuntimeHostRequestIdentity? RequestIdentity { get; init; }

    public ProcessRuntimeHostSandboxDecision? SandboxDecision { get; init; }

    public ProcessRuntimeHostAuditReference? AuditReference { get; init; }

    public ProcessRuntimeHostCapabilityDescriptorReference? CapabilityDescriptor { get; init; }

    public bool IsReadOnlySafe =>
        DryRunOnly &&
        NoMutationPerformed &&
        !AllowsProcessMutation &&
        !AllowsTransitionMutation &&
        !AllowsFinalizerMutation &&
        (SandboxDecision is null || (!SandboxDecision.ExecutionAllowed && SandboxDecision.DryRunOnly));

    public IReadOnlyList<ProcessRuntimeHostContractViolation> ValidateReadOnlySafety() {
        var violations = new List<ProcessRuntimeHostContractViolation>();
        AddIf(DryRunOnly, ProcessRuntimeHostContractViolationKind.ProductionExecutionAllowed, "Runtime host contract must remain dry-run/read-only only.");
        AddIf(NoMutationPerformed, ProcessRuntimeHostContractViolationKind.MutationNotRecordedAsDenied, "Runtime host contract must record that no mutation was performed.");
        AddIf(!AllowsProcessMutation, ProcessRuntimeHostContractViolationKind.ProcessMutationAllowed, "Runtime host contract must not allow process mutation.");
        AddIf(!AllowsTransitionMutation, ProcessRuntimeHostContractViolationKind.TransitionMutationAllowed, "Runtime host contract must not allow transition mutation.");
        AddIf(!AllowsFinalizerMutation, ProcessRuntimeHostContractViolationKind.FinalizerMutationAllowed, "Runtime host contract must not allow finalizer mutation.");
        AddIf(SandboxDecision is null || (!SandboxDecision.ExecutionAllowed && SandboxDecision.DryRunOnly), ProcessRuntimeHostContractViolationKind.SandboxExecutionAllowed, "Runtime host sandbox decision must not allow production execution.");
        return violations;

        void AddIf(bool valid, ProcessRuntimeHostContractViolationKind kind, string message) {
            if (!valid) {
                violations.Add(new ProcessRuntimeHostContractViolation(kind, message));
            }
        }
    }
}
