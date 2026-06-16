namespace CanDoItAll.Processes.Projections;

public enum ProcessDefinitionStepKind
{
    Unspecified,
    Start,
    Work,
    Decision,
    Review,
    Approval,
    Delivery,
    Subprocess,
    End
}

public enum ProcessDefinitionStepOperationKind
{
    Unspecified,
    ReadProcessContext,
    ReadProjectStructure,
    ReadUpstreamArtifacts,
    WriteManagedProcessArtifacts,
    WriteExternalArtifactDestination,
    MutateProductTarget,
    RunValidation,
    LaunchRuntime,
    CaptureRuntimeProof,
    ExecuteExternalAction,
    RecoverArtifactsOnly,
    EscalateOrDecide
}

public enum ProcessDefinitionStepTargetScopeKind
{
    Unspecified,
    ManagedProcessArtifactsOnly,
    ManagedOutputProduct,
    ExternalArtifactDestination,
    ExternalProductTargetReadOnly,
    ExternalProductTargetMutable,
    ExternalActionControlled
}

public enum ProcessDefinitionRouteTargetKind
{
    NextStep,
    SpecificStep,
    PreviousStep,
    SubprocessStart,
    SubprocessResume,
    WaitForArtifact,
    WaitForUser,
    Escalate,
    CompleteRun,
    FailRun,
    CancelRun
}

public enum ProcessDefinitionArtifactKind
{
    Unspecified,
    Brief,
    Checklist,
    Dataset,
    Decision,
    DecisionRecord,
    Deliverable,
    Evidence,
    Prompt,
    Report,
    Transcript
}

public enum ProcessDefinitionArtifactTrustRequirement
{
    Unspecified,
    ReviewRequired,
    ApprovalRequired,
    HumanApproved
}

public enum ProcessDefinitionArtifactSensitivityLevel
{
    Unspecified,
    Internal,
    Confidential,
    Restricted
}

public enum ProcessDefinitionWorkflowOutputKind
{
    Unspecified,
    Artifact,
    File,
    Json,
    Markdown,
    Text
}

public enum ProcessDefinitionStepCommandKind
{
    SaveStep,
    AddBranchOutcome,
    AddArtifactExpectation,
    MapSubprocess
}

public enum ProcessDefinitionStepCommandStatus
{
    Accepted,
    Rejected
}

public enum ProcessDefinitionStepLintSeverity
{
    Info,
    Warning,
    Error
}

public enum ProcessDefinitionStepLintSection
{
    Basic,
    OperationContract,
    Contracts,
    Routing,
    Roles,
    Artifacts,
    Subprocess
}

public readonly record struct ProcessDefinitionStepEditorVersionToken
{
    public ProcessDefinitionStepEditorVersionToken(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Process definition step editor version token is required.", nameof(value));
        }

        Value = value.Trim();
    }

    public string Value { get; }

    public override string ToString() => Value;
}

public readonly record struct ProcessDefinitionBranchOutcomeKey
{
    public ProcessDefinitionBranchOutcomeKey(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Process definition branch outcome key is required.", nameof(value));
        }

        Value = value.Trim();
    }

    public string Value { get; }

    public override string ToString() => Value;
}

public readonly record struct ProcessDefinitionArtifactExpectationKey
{
    public ProcessDefinitionArtifactExpectationKey(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Process definition artifact expectation key is required.", nameof(value));
        }

        Value = value.Trim();
    }

    public string Value { get; }

    public override string ToString() => Value;
}

public sealed record ProcessDefinitionStepListItemProjection(
    ProcessDefinitionStepKey StepKey,
    string Title,
    string Subtitle,
    ProcessDefinitionStepKind StepKind,
    int Order,
    bool IsSelected);

public sealed record ProcessDefinitionStepBasicDraftProjection(
    ProcessDefinitionStepKey StepKey,
    string Title,
    string Subtitle,
    string Notes,
    ProcessDefinitionStepKind StepKind,
    int TargetLeadHours,
    bool AllowsManualSkip,
    bool AllowsSafeRefusal,
    bool RequiresApproval,
    bool RequiresDecisionRecord,
    ProcessDefinitionRoleKey? DecisionRoleKey);

public sealed record ProcessDefinitionStepOperationContractProjection(
    ProcessDefinitionStepTargetScopeKind TargetScope,
    IReadOnlyList<ProcessDefinitionStepOperationKind> AllowedOperations);

public sealed record ProcessDefinitionStepContractsProjection(
    string InputContractSummary,
    string OutputContractSummary,
    string EvidenceContractSummary,
    string DecisionRightsSummary,
    string ExceptionPolicySummary);

public sealed record ProcessDefinitionRouteTargetProjection(
    ProcessDefinitionRouteTargetKind Kind,
    ProcessDefinitionStepKey? StepKey,
    ProcessDefinitionArtifactExpectationKey? ArtifactExpectationKey,
    string Summary);

public sealed record ProcessDefinitionLoopBudgetProjection(
    bool IsRequired,
    int MaximumRepeats,
    string FingerprintPolicyKey,
    ProcessDefinitionRouteTargetKind EscalationTargetKind);

public sealed record ProcessDefinitionBranchOutcomeProjection(
    ProcessDefinitionBranchOutcomeKey OutcomeKey,
    string Title,
    string Description,
    ProcessDefinitionRouteTargetProjection RouteTarget,
    bool IsBackwardRoute,
    ProcessDefinitionLoopBudgetProjection LoopBudget);

public sealed record ProcessDefinitionArtifactExpectationProjection(
    ProcessDefinitionArtifactExpectationKey ArtifactKey,
    string TemplateKey,
    string Title,
    ProcessDefinitionArtifactKind ArtifactKind,
    bool IsRequired,
    ProcessDefinitionArtifactTrustRequirement TrustRequirement,
    ProcessDefinitionArtifactSensitivityLevel SensitivityLevel,
    int RetentionDays,
    string WorkflowOutputId,
    string WorkflowOutputName,
    ProcessDefinitionWorkflowOutputKind WorkflowOutputKind,
    Guid? SubprocessChildArtifactExpectationId,
    string SubprocessChildStepKey,
    string SubprocessChildArtifactTitle,
    string AllowedFutureUsageSummary,
    string ValidationRequirementSummary);

public sealed record ProcessDefinitionSubprocessMappingProjection(
    string ProcessKey,
    string DefinitionSnapshotName,
    IReadOnlyList<ProcessDefinitionArtifactExpectationProjection> ChildArtifactMappings);

public sealed record ProcessDefinitionStepDraftProjection(
    ProcessDefinitionStepBasicDraftProjection Basic,
    ProcessDefinitionStepOperationContractProjection OperationContract,
    ProcessDefinitionStepContractsProjection Contracts,
    IReadOnlyList<ProcessDefinitionBranchOutcomeProjection> BranchOutcomes,
    IReadOnlyList<ProcessDefinitionStepRoleBindingProjection> RoleBindings,
    IReadOnlyList<ProcessDefinitionArtifactExpectationProjection> ArtifactExpectations,
    ProcessDefinitionSubprocessMappingProjection SubprocessMapping);

public sealed record ProcessDefinitionStepLintIssueProjection(
    string Code,
    ProcessDefinitionStepLintSeverity Severity,
    ProcessDefinitionStepLintSection Section,
    string Message,
    string Suggestion);

public sealed record ProcessDefinitionStepLintProjection(
    IReadOnlyList<ProcessDefinitionStepLintIssueProjection> Issues)
{
    public bool HasWarningsOrErrors => Issues.Any(issue => issue.Severity is ProcessDefinitionStepLintSeverity.Warning or ProcessDefinitionStepLintSeverity.Error);

    public bool HasBlockingIssues => Issues.Any(issue => issue.Severity == ProcessDefinitionStepLintSeverity.Error);
}

public sealed record ProcessDefinitionStepCommandProjection(
    ProcessDefinitionStepCommandKind Kind,
    string Text,
    string Icon,
    bool IsEnabled,
    string? DisabledReason);

public sealed record ProcessDefinitionSubprocessOptionProjection(
    ProcessDefinitionCatalogItemKey DefinitionKey,
    string DisplayName,
    string Summary);

public sealed record ProcessDefinitionStepCommandReceipt(
    Guid ReceiptId,
    ProcessDefinitionStepCommandKind CommandKind,
    ProcessDefinitionStepCommandStatus Status,
    ProcessDefinitionStepEditorVersionToken VersionToken,
    DateTimeOffset ObservedAtUtc,
    string Summary,
    IReadOnlyList<ProcessDefinitionStepLintIssueProjection> LintIssues);

public sealed record ProcessDefinitionStepEditorCommand(
    ProcessWorkspaceShellScope Scope,
    ProcessDefinitionCatalogItemKey DefinitionKey,
    ProcessDefinitionStepCommandKind CommandKind,
    ProcessDefinitionStepEditorVersionToken? ExpectedVersionToken,
    ProcessDefinitionStepDraftProjection Draft);

public sealed record ProcessDefinitionStepEditorCommandResult(
    ProcessDefinitionStepCommandReceipt Receipt,
    ProcessDefinitionStepEditorProjection Projection);

public sealed record ProcessDefinitionStepEditorProjection(
    ProcessDefinitionCatalogItemKey DefinitionKey,
    ProcessDefinitionStepEditorVersionToken VersionToken,
    ProcessDefinitionStepKey? SelectedStepKey,
    IReadOnlyList<ProcessDefinitionStepListItemProjection> Steps,
    IReadOnlyList<ProcessDefinitionStepDraftProjection> StepDrafts,
    ProcessDefinitionStepDraftProjection? SelectedStep,
    IReadOnlyList<ProcessDefinitionSubprocessOptionProjection> SubprocessOptions,
    IReadOnlyList<ProcessDefinitionStepCommandProjection> Commands,
    ProcessDefinitionStepLintProjection Lint,
    ProcessDefinitionStepCommandReceipt? LastCommandReceipt);
