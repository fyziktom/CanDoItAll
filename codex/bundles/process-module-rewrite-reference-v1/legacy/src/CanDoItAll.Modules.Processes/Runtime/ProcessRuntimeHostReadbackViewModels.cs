namespace CanDoItAll.Modules.Processes;

public sealed record ProcessRuntimeHostReadbackPanelViewModel(
    string HostReadiness,
    string HostAuditStoreKind,
    bool HostEnabled,
    bool HostEmergencyDisabled,
    bool HostUsesDurableAuditStore,
    bool HostSupportsAuditRetentionQuery,
    int RegisteredLaneCount,
    int EnabledLaneCount,
    int CapabilityCount,
    string VerificationStatus,
    string CapabilityKey,
    string Lane,
    Guid ProcessRunId,
    Guid StepRunId,
    string CallerContext,
    string ProjectionMode,
    string ProjectionSource,
    bool ProjectionAttached,
    Guid? AuditRecordId,
    int ResponseCount,
    int DiagnosticCount,
    int EvidenceReferenceCount,
    string AuditRecordObservationHash,
    string DenialCategory,
    string DenialCode,
    string DenialMessage,
    bool NoMutationPerformed,
    bool AllowsProcessMutation,
    bool AllowsTransitionMutation,
    bool AllowsFinalizerMutation,
    DateTimeOffset RequestedAt,
    DateTimeOffset ObservedAt,
    IReadOnlyList<ProcessRuntimeHostReadbackDiagnosticViewModel> Diagnostics,
    IReadOnlyList<ProcessRuntimeHostReadbackAuditRecordViewModel> AuditRecords)
{
    public bool HasDenial => !string.IsNullOrWhiteSpace(DenialCode) ||
        !string.IsNullOrWhiteSpace(DenialMessage);

    public bool IsMutationGuarded => NoMutationPerformed &&
        !AllowsProcessMutation &&
        !AllowsTransitionMutation &&
        !AllowsFinalizerMutation;
}

public sealed record ProcessRuntimeHostReadbackDiagnosticViewModel(
    string Lane,
    string Severity,
    string Category,
    string Message,
    int EvidenceReferenceCount,
    string ContractVersion);

public sealed record ProcessRuntimeHostReadbackAuditRecordViewModel(
    Guid Id,
    DateTimeOffset RecordedAt,
    string Lane,
    int ResponseCount,
    int AcceptedCount,
    int DeniedCount,
    bool NoMutationPerformed,
    bool AllowsProcessMutation,
    bool AllowsTransitionMutation,
    bool AllowsFinalizerMutation,
    string ObservationHash);
