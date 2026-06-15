namespace CanDoItAll.Processes.Templates;

public sealed record ProcessTemplateCompatibilityScanRequest(
    string TemplatePackRoot,
    string TargetSchemaVersion,
    ProcessTemplateMigrationRegistry MigrationRegistry,
    DateTimeOffset ObservedAtUtc);

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
        BranchDiagnostics.Diagnostics.Count > 0;
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
