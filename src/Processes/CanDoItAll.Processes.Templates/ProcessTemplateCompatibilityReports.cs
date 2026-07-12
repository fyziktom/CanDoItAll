namespace CanDoItAll.Processes.Templates;

public sealed record ProcessTemplateCompatibilityScanRequest(
    string TemplatePackRoot,
    string TargetSchemaVersion,
    ProcessTemplateMigrationRegistry MigrationRegistry,
    DateTimeOffset ObservedAtUtc)
{
    public bool StrictExecutionContractValidation { get; init; }
}

public sealed record ProcessTemplateCompatibilityReport(
    string TemplatePackRoot,
    DateTimeOffset ObservedAtUtc,
    ProcessTemplateMigrationDryRunReport MigrationDryRun,
    ProcessTemplateSidecarDriftReport SidecarDrift,
    ProcessBranchMigrationDiagnosticReport BranchDiagnostics)
{
    public bool RequiresManualReview =>
        MigrationDryRun.Items.Any(item => item.Status is ProcessTemplateMigrationDryRunStatus.ManualReviewRequired or ProcessTemplateMigrationDryRunStatus.MigrationPlanFailed) ||
        SidecarDrift.Sidecars.Any(sidecar => sidecar.Status != ProcessTemplateSidecarDriftStatus.Aligned) ||
        BranchDiagnostics.Diagnostics.Count > 0 ||
        TemplateContractDiagnostics.Diagnostics.Count > 0 ||
        ArtifactContractDiagnostics.Diagnostics.Count > 0;

    public ProcessTemplateContractDiagnosticReport TemplateContractDiagnostics { get; init; } = ProcessTemplateContractDiagnosticReport.Empty;

    public ProcessArtifactContractDiagnosticReport ArtifactContractDiagnostics { get; init; } = ProcessArtifactContractDiagnosticReport.Empty;
}

public sealed record ProcessTemplateMigrationDryRunReport(
    int ProcessCount,
    int CanonicalJsonCount,
    int GeneratedSidecarCount,
    bool WouldMutateFiles,
    IReadOnlyList<ProcessTemplateMigrationDryRunItem> Items);

public sealed record ProcessTemplateMigrationDryRunItem(
    string ProcessKey,
    string RelativeDefinitionPath,
    string SourceSchemaVersion,
    string TargetSchemaVersion,
    ProcessTemplateMigrationDryRunStatus Status,
    IReadOnlyList<string> MigrationIds,
    string? ErrorCode,
    string? ErrorMessage);

public enum ProcessTemplateMigrationDryRunStatus
{
    Compatible,
    MigrationPlanned,
    ManualReviewRequired,
    MigrationPlanFailed
}

public sealed record ProcessTemplateSidecarDriftReport(
    int SidecarCount,
    IReadOnlyList<ProcessTemplateSidecarDrift> Sidecars);

public sealed record ProcessTemplateSidecarDrift(
    string ProcessKey,
    string RelativeSidecarPath,
    ProcessTemplateProjectionKind ProjectionKind,
    ProcessTemplateSidecarDriftStatus Status,
    string? ExpectedSourceJsonHash,
    string? ActualSourceJsonHash,
    string? Message);

public enum ProcessTemplateSidecarDriftStatus
{
    Aligned,
    MissingSourceHash,
    SourceHashMismatch,
    Unreadable,
    MissingCanonicalJson
}

public sealed record ProcessBranchMigrationDiagnosticReport(
    int OutcomeCount,
    IReadOnlyList<ProcessBranchMigrationDiagnostic> Diagnostics);

public sealed record ProcessBranchMigrationDiagnostic(
    string ProcessKey,
    string StepKey,
    string OutcomeKey,
    ProcessBranchMigrationDiagnosticKind Kind,
    string Message);

public enum ProcessBranchMigrationDiagnosticKind
{
    MissingStableOutcomeKey,
    AmbiguousRouteTarget
}

public sealed record ProcessTemplateContractDiagnosticReport(
    int DiagnosticCount,
    IReadOnlyList<ProcessTemplateContractDiagnostic> Diagnostics)
{
    public static ProcessTemplateContractDiagnosticReport Empty { get; } = new(0, []);
}

public sealed record ProcessTemplateContractDiagnostic(
    string ProcessKey,
    string StepKey,
    ProcessTemplateContractDiagnosticKind Kind,
    string Message);

public enum ProcessTemplateContractDiagnosticKind
{
    ProseOnlyHardGate,
    MissingExecutionContract,
    InvalidExecutionClass,
    MissingRuntimeOwnedExecutorKey,
    MissingDeterministicToolPlan,
    InvalidDeterministicToolPlan,
    MissingRequiredReceiptMetadata,
    MissingReadbackChecks,
    UnknownSubprocessDefinition,
    UnknownSubprocessChildOutputStep,
    UnknownSubprocessChildArtifactExpectation,
    InvalidBranchOutcomeKey,
    MissingProducedArtifactSlot
}

public sealed record ProcessArtifactContractDiagnosticReport(
    int DiagnosticCount,
    IReadOnlyList<ProcessArtifactContractDiagnostic> Diagnostics)
{
    public static ProcessArtifactContractDiagnosticReport Empty { get; } = new(0, []);
}

public sealed record ProcessArtifactContractDiagnostic(
    string ProcessKey,
    string ArtifactKey,
    ProcessArtifactContractDiagnosticKind Kind,
    string Message);

public enum ProcessArtifactContractDiagnosticKind
{
    MissingSemanticAcceptanceContract,
    FileOnlyAcceptanceAllowed,
    MissingArtifactSlot,
    MissingEvidenceKinds,
    InvalidSemanticAcceptanceContract
}
