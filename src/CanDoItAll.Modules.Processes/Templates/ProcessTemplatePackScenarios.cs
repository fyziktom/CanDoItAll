namespace CanDoItAll.Modules.Processes;

public sealed class ProcessTemplateBaselineScenario
{
    public string Key { get; set; } = string.Empty;

    public string ProcessTemplateKey { get; set; } = string.Empty;

    public string RunName { get; set; } = string.Empty;

    public string Summary { get; set; } = string.Empty;

    public string OperatingMode { get; set; } = string.Empty;

    public string TriggerReason { get; set; } = string.Empty;

    public List<ProcessTemplateBaselineAssignment> Assignments { get; set; } = [];

    public List<ProcessTemplateBaselineTransition> Transitions { get; set; } = [];

    public List<ProcessTemplateBaselineArtifactRecord> Artifacts { get; set; } = [];

    public List<ProcessTemplateBaselineContractExercise> ContractExercises { get; set; } = [];

    public List<ProcessTemplateBaselineRecoveryExercise> RecoveryExercises { get; set; } = [];
}

public sealed class ProcessTemplateBaselineAssignment
{
    public string StepKey { get; set; } = string.Empty;

    public string RoleKey { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string ExecutorKind { get; set; } = string.Empty;

    public string BindingReason { get; set; } = string.Empty;

    public bool IsFallback { get; set; }
}

public sealed class ProcessTemplateBaselineTransition
{
    public string StepKey { get; set; } = string.Empty;

    public string TargetStatus { get; set; } = string.Empty;

    public string SelectedBranchOutcomeKey { get; set; } = string.Empty;

    public ProcessStepBlockCause? BlockCause { get; set; }

    public string Reason { get; set; } = string.Empty;

    public string DecidedBy { get; set; } = string.Empty;
}

public sealed class ProcessTemplateBaselineArtifactRecord
{
    public string StepKey { get; set; } = string.Empty;

    public string ArtifactKind { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string TrustStatus { get; set; } = string.Empty;

    public string SensitivityLevel { get; set; } = string.Empty;

    public string ProvenanceSummary { get; set; } = string.Empty;

    public string AllowedFutureUsageSummary { get; set; } = string.Empty;

    public string ReviewSummary { get; set; } = string.Empty;
}

public sealed class ProcessTemplateBaselineContractExercise
{
    public string StepKey { get; set; } = string.Empty;

    public ProcessStepTargetScope? ExpectedTargetScope { get; set; }

    public List<ProcessStepOperation> ExpectedAllowedOperations { get; set; } = [];

    public string Purpose { get; set; } = string.Empty;
}

public sealed class ProcessTemplateBaselineRecoveryExercise
{
    public string StepKey { get; set; } = string.Empty;

    public ProcessStepBlockCause BlockCause { get; set; }

    public List<ProcessStepRecoveryOption> ExpectedRecoveryOptions { get; set; } = [];

    public string Diagnostic { get; set; } = string.Empty;
}

public sealed record ProcessTemplateCatalogItem(
    string Key,
    string DisplayName,
    string Summary,
    string Criticality,
    string AutonomyLevel,
    int StepCount,
    int SharedRoleCount,
    int LocalRoleCount,
    string RelativePath);

public sealed record ProcessTemplateMermaidDocument(
    string ProcessKey,
    string ProcessName,
    string Flowchart,
    string Sequence,
    IReadOnlyList<string> SupportingFiles);

public sealed record ProcessTemplateImportResult(
    string ProcessKey,
    Guid DefinitionId,
    IReadOnlyList<string> Warnings);

public sealed record ProcessTemplateBaselineScenarioSummary(
    string Key,
    string ProcessTemplateKey,
    string RunName,
    string OperatingMode,
    int AssignmentCount,
    int TransitionCount,
    int ArtifactCount,
    int BranchSelectionCount,
    int BlockedTransitionCount,
    int ContractExerciseCount,
    int RecoveryExerciseCount);
