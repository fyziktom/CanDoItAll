using System.ComponentModel;
namespace CanDoItAll.Processes.Projections;

[Description("Classification of step kind. Numeric JSON values are listed by the OpenAPI schema.")]
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

[Description("Classification of step operation kind. Numeric JSON values are listed by the OpenAPI schema.")]
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
    StartProjectNodeProcess,
    RecoverArtifactsOnly,
    EscalateOrDecide
}

[Description("Classification of step target scope kind. Numeric JSON values are listed by the OpenAPI schema.")]
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

[Description("Classification of route target kind. Numeric JSON values are listed by the OpenAPI schema.")]
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

[Description("Classification of artifact kind. Numeric JSON values are listed by the OpenAPI schema.")]
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

[Description("Classification of artifact trust requirement. Numeric JSON values are listed by the OpenAPI schema.")]
public enum ProcessDefinitionArtifactTrustRequirement
{
    Unspecified,
    ReviewRequired,
    ApprovalRequired,
    HumanApproved
}

[Description("Classification of artifact sensitivity level. Numeric JSON values are listed by the OpenAPI schema.")]
public enum ProcessDefinitionArtifactSensitivityLevel
{
    Unspecified,
    Internal,
    Confidential,
    Restricted
}

[Description("Classification of workflow output kind. Numeric JSON values are listed by the OpenAPI schema.")]
public enum ProcessDefinitionWorkflowOutputKind
{
    Unspecified,
    Artifact,
    File,
    Json,
    Markdown,
    Text
}

[Description("Classification of step command kind. Numeric JSON values are listed by the OpenAPI schema.")]
public enum ProcessDefinitionStepCommandKind
{
    SaveStep,
    AddBranchOutcome,
    AddArtifactExpectation,
    MapSubprocess
}

[Description("Classification of step command status. Numeric JSON values are listed by the OpenAPI schema.")]
public enum ProcessDefinitionStepCommandStatus
{
    Accepted,
    Rejected
}

[Description("Classification of step lint severity. Numeric JSON values are listed by the OpenAPI schema.")]
public enum ProcessDefinitionStepLintSeverity
{
    Info,
    Warning,
    Error
}

[Description("Classification of step lint section. Numeric JSON values are listed by the OpenAPI schema.")]
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

[Description("Opaque version of the step editor projection. This is an authoring concurrency token, not an authentication credential.")]
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

    [Description("Opaque identity or authoring-version text carried by this wrapper; preserve exactly.")]
    public string Value { get; }

    public override string ToString() => Value;
}

[Description("Opaque branch outcome key represented by its value member. Preserve the value; do not derive it from display text.")]
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

    [Description("Opaque identity or authoring-version text carried by this wrapper; preserve exactly.")]
    public string Value { get; }

    public override string ToString() => Value;
}

[Description("Opaque artifact expectation key represented by its value member. Preserve the value; do not derive it from display text.")]
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

    [Description("Opaque identity or authoring-version text carried by this wrapper; preserve exactly.")]
    public string Value { get; }

    public override string ToString() => Value;
}

[Description("A step in display order, with its type and current selection state.")]
public sealed record ProcessDefinitionStepListItemProjection(
    [property: Description("Opaque identity of the definition step referenced by this item, when applicable.")]
    ProcessDefinitionStepKey StepKey,
    [property: Description("Human-readable title of this authoring item.")]
    string Title,
    [property: Description("Secondary display text beneath the item title.")]
    string Subtitle,
    [property: Description("Behavior category of the definition step.")]
    ProcessDefinitionStepKind StepKind,
    [property: Description("Presentation ordering value within the definition step list.")]
    int Order,
    [property: Description("Whether this item is selected in the returned projection.")]
    bool IsSelected);

[Description("Step identity, category, lead-time target and manual/approval controls.")]
public sealed record ProcessDefinitionStepBasicDraftProjection(
    [property: Description("Opaque identity of the definition step referenced by this item, when applicable.")]
    ProcessDefinitionStepKey StepKey,
    [property: Description("Human-readable title of this authoring item.")]
    string Title,
    [property: Description("Secondary display text beneath the item title.")]
    string Subtitle,
    [property: Description("Operator-authored notes about this step.")]
    string Notes,
    [property: Description("Behavior category of the definition step.")]
    ProcessDefinitionStepKind StepKind,
    [property: Description("Target lead time for the step, expressed in hours.")]
    int TargetLeadHours,
    [property: Description("Whether the definition permits an operator to skip the step manually.")]
    bool AllowsManualSkip,
    [property: Description("Whether the definition permits the step to refuse unsafe work.")]
    bool AllowsSafeRefusal,
    [property: Description("Whether the step requires approval before its governed action.")]
    bool RequiresApproval,
    [property: Description("Whether the step must produce a durable decision record.")]
    bool RequiresDecisionRecord,
    [property: Description("Role authorized to make the step decision, when one is assigned.")]
    ProcessDefinitionRoleKey? DecisionRoleKey);

[Description("Allowed operation categories and the target boundary within which a step may act.")]
public sealed record ProcessDefinitionStepOperationContractProjection(
    [property: Description("Boundary within which the step may perform its allowed operations.")]
    ProcessDefinitionStepTargetScopeKind TargetScope,
    [property: Description("Operation categories permitted by this step contract.")]
    IReadOnlyList<ProcessDefinitionStepOperationKind> AllowedOperations);

[Description("Human-readable input, output, evidence, decision and exception contracts for one step.")]
public sealed record ProcessDefinitionStepContractsProjection(
    [property: Description("Human-readable description of required step inputs.")]
    string InputContractSummary,
    [property: Description("Human-readable description of expected step outputs.")]
    string OutputContractSummary,
    [property: Description("Human-readable evidence requirements for the step.")]
    string EvidenceContractSummary,
    [property: Description("Human-readable allocation of decision authority for the step.")]
    string DecisionRightsSummary,
    [property: Description("Human-readable handling rules for exceptional step outcomes.")]
    string ExceptionPolicySummary);

[Description("Destination of a branch, which may select a step, artifact wait or terminal action.")]
public sealed record ProcessDefinitionRouteTargetProjection(
    [property: Description("Classification of this projected authoring item.")]
    ProcessDefinitionRouteTargetKind Kind,
    [property: Description("Opaque identity of the definition step referenced by this item, when applicable.")]
    ProcessDefinitionStepKey? StepKey,
    [property: Description("Expected artifact whose availability controls this route target, when applicable.")]
    ProcessDefinitionArtifactExpectationKey? ArtifactExpectationKey,
    [property: Description("Human-readable explanation of this item; display text is not an executable contract.")]
    string Summary);

[Description("Explicit repeat limit and escalation policy for a backward route.")]
public sealed record ProcessDefinitionLoopBudgetProjection(
    [property: Description("Whether the projected artifact, role, binding or loop constraint is mandatory.")]
    bool IsRequired,
    [property: Description("Maximum number of backward-route repetitions permitted by this loop budget.")]
    int MaximumRepeats,
    [property: Description("Identifier of the policy used to recognize repeated process work; not credential material.")]
    string FingerprintPolicyKey,
    [property: Description("Destination category used when the repeat budget is exhausted.")]
    ProcessDefinitionRouteTargetKind EscalationTargetKind);

[Description("A named decision outcome and the route taken when that outcome is selected.")]
public sealed record ProcessDefinitionBranchOutcomeProjection(
    [property: Description("Opaque identity of the branch outcome within its definition.")]
    ProcessDefinitionBranchOutcomeKey OutcomeKey,
    [property: Description("Human-readable title of this authoring item.")]
    string Title,
    [property: Description("Human-readable explanation of this branch outcome or template category.")]
    string Description,
    [property: Description("Destination selected when this branch outcome is taken.")]
    ProcessDefinitionRouteTargetProjection RouteTarget,
    [property: Description("True when the route returns to an earlier step and may require a loop budget.")]
    bool IsBackwardRoute,
    [property: Description("Repeat and escalation bounds for this branch outcome.")]
    ProcessDefinitionLoopBudgetProjection LoopBudget);

[Description("Expected artifact, its retention and trust requirements, and optional workflow or child-process output binding.")]
public sealed record ProcessDefinitionArtifactExpectationProjection(
    [property: Description("Identity of the expected artifact associated with this projection.")]
    ProcessDefinitionArtifactExpectationKey ArtifactKey,
    [property: Description("Source artifact template key, when the expectation derives from a template.")]
    string TemplateKey,
    [property: Description("Human-readable title of this authoring item.")]
    string Title,
    [property: Description("Category of artifact the step is expected to produce.")]
    ProcessDefinitionArtifactKind ArtifactKind,
    [property: Description("Whether the projected artifact, role, binding or loop constraint is mandatory.")]
    bool IsRequired,
    [property: Description("Review or approval requirement before the artifact is trusted.")]
    ProcessDefinitionArtifactTrustRequirement TrustRequirement,
    [property: Description("Sensitivity classification applied to the expected artifact.")]
    ProcessDefinitionArtifactSensitivityLevel SensitivityLevel,
    [property: Description("Retention period for the expected artifact, expressed in days.")]
    int RetentionDays,
    [property: Description("Identifier of the workflow output bound to this artifact expectation.")]
    string WorkflowOutputId,
    [property: Description("Display name of the workflow output bound to the artifact expectation.")]
    string WorkflowOutputName,
    [property: Description("Representation of the workflow output used by the artifact mapping.")]
    ProcessDefinitionWorkflowOutputKind WorkflowOutputKind,
    [property: Description("Identity of the linked child-process artifact expectation, when mapped.")]
    Guid? SubprocessChildArtifactExpectationId,
    [property: Description("Step key in the child process used by this artifact mapping.")]
    string SubprocessChildStepKey,
    [property: Description("Display title of the linked child-process artifact expectation.")]
    string SubprocessChildArtifactTitle,
    [property: Description("Explanation of permitted reuse of the expected artifact.")]
    string AllowedFutureUsageSummary,
    [property: Description("Human-readable validation requirements for the expected artifact.")]
    string ValidationRequirementSummary);

[Description("Child-process definition snapshot and its artifact mappings to the parent step.")]
public sealed record ProcessDefinitionSubprocessMappingProjection(
    [property: Description("Stable key of the mapped child process definition.")]
    string ProcessKey,
    [property: Description("Display name of the child definition snapshot used by this mapping.")]
    string DefinitionSnapshotName,
    [property: Description("Expected artifacts mapped between the parent step and child process.")]
    IReadOnlyList<ProcessDefinitionArtifactExpectationProjection> ChildArtifactMappings);

[Description("Complete projected step draft, including operation bounds, routing, roles, artifacts and subprocess mapping.")]
public sealed record ProcessDefinitionStepDraftProjection(
    [property: Description("Step identity, category, timing and approval controls.")]
    ProcessDefinitionStepBasicDraftProjection Basic,
    [property: Description("Allowed operation categories and target boundary for this step.")]
    ProcessDefinitionStepOperationContractProjection OperationContract,
    [property: Description("Declared interface and behavior contracts for this authoring item.")]
    ProcessDefinitionStepContractsProjection Contracts,
    [property: Description("Named outcomes and routes available when this step completes.")]
    IReadOnlyList<ProcessDefinitionBranchOutcomeProjection> BranchOutcomes,
    [property: Description("Role responsibilities assigned to this step.")]
    IReadOnlyList<ProcessDefinitionStepRoleBindingProjection> RoleBindings,
    [property: Description("Artifacts this step is expected to provide and their trust/mapping requirements.")]
    IReadOnlyList<ProcessDefinitionArtifactExpectationProjection> ArtifactExpectations,
    [property: Description("Child definition and artifact mappings used when this step invokes a subprocess.")]
    ProcessDefinitionSubprocessMappingProjection SubprocessMapping);

[Description("One step validation finding with a stable reason code and suggested correction.")]
public sealed record ProcessDefinitionStepLintIssueProjection(
    [property: Description("Stable machine-readable reason code of this validation finding.")]
    string Code,
    [property: Description("Severity of this validation finding.")]
    ProcessDefinitionStepLintSeverity Severity,
    [property: Description("Authoring section to which this validation finding belongs.")]
    ProcessDefinitionStepLintSection Section,
    [property: Description("Human-readable explanation of this validation finding.")]
    string Message,
    [property: Description("Suggested authoring correction for this validation finding.")]
    string Suggestion);

[Description("Step validation findings and derived warning/blocking indicators.")]
public sealed record ProcessDefinitionStepLintProjection(
    [property: Description("Validation findings for the current projection.")]
    IReadOnlyList<ProcessDefinitionStepLintIssueProjection> Issues)
{
    [Description("True when at least one finding has warning or error severity.")]
    public bool HasWarningsOrErrors => Issues.Any(issue => issue.Severity is ProcessDefinitionStepLintSeverity.Warning or ProcessDefinitionStepLintSeverity.Error);

    [Description("True when at least one error finding blocks the authoring action.")]
    public bool HasBlockingIssues => Issues.Any(issue => issue.Severity == ProcessDefinitionStepLintSeverity.Error);
}

[Description("Availability of a step-authoring command; this read API does not execute it.")]
public sealed record ProcessDefinitionStepCommandProjection(
    [property: Description("Classification of this projected authoring item.")]
    ProcessDefinitionStepCommandKind Kind,
    [property: Description("User-facing command label.")]
    string Text,
    [property: Description("Presentation icon name for the command or action.")]
    string Icon,
    [property: Description("Whether the authoring application currently allows this command; this flag does not grant HTTP authority.")]
    bool IsEnabled,
    [property: Description("Human-readable reason a command is unavailable, or null when it is available.")]
    string? DisabledReason);

[Description("Definition available as a subprocess target in the current authoring context.")]
public sealed record ProcessDefinitionSubprocessOptionProjection(
    [property: Description("Opaque catalog key of the process definition; preserve it exactly when constructing read URLs.")]
    ProcessDefinitionCatalogItemKey DefinitionKey,
    [property: Description("Display name of the role, workflow preference or subprocess option.")]
    string DisplayName,
    [property: Description("Human-readable explanation of this item; display text is not an executable contract.")]
    string Summary);

[Description("Most recent step command result and validation findings at its observed version.")]
public sealed record ProcessDefinitionStepCommandReceipt(
    [property: Description("Unique identifier of the recorded authoring command receipt.")]
    Guid ReceiptId,
    [property: Description("Authoring command whose outcome this receipt records.")]
    ProcessDefinitionStepCommandKind CommandKind,
    [property: Description("Current lifecycle or command outcome of this projection.")]
    ProcessDefinitionStepCommandStatus Status,
    [property: Description("Opaque version of this authoring projection; it is not an authentication token.")]
    ProcessDefinitionStepEditorVersionToken VersionToken,
    [property: Description("UTC instant when this command result was observed.")]
    DateTimeOffset ObservedAtUtc,
    [property: Description("Human-readable explanation of this item; display text is not an executable contract.")]
    string Summary,
    [property: Description("Validation findings associated with the recorded command.")]
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

[Description("Definition step list, editable step snapshots, selected step and validation findings.")]
public sealed record ProcessDefinitionStepEditorProjection(
    [property: Description("Opaque catalog key of the process definition; preserve it exactly when constructing read URLs.")]
    ProcessDefinitionCatalogItemKey DefinitionKey,
    [property: Description("Opaque version of this authoring projection; it is not an authentication token.")]
    ProcessDefinitionStepEditorVersionToken VersionToken,
    [property: Description("Key of the selected step, or null when none is selected.")]
    ProcessDefinitionStepKey? SelectedStepKey,
    [property: Description("Step catalog in presentation order.")]
    IReadOnlyList<ProcessDefinitionStepListItemProjection> Steps,
    [property: Description("Complete projected drafts for the definition steps.")]
    IReadOnlyList<ProcessDefinitionStepDraftProjection> StepDrafts,
    [property: Description("Selected step draft, or null when no step is selected.")]
    ProcessDefinitionStepDraftProjection? SelectedStep,
    [property: Description("Definitions available as child-process targets in this context.")]
    IReadOnlyList<ProcessDefinitionSubprocessOptionProjection> SubprocessOptions,
    [property: Description("Commands available in the authoring application; these read endpoints do not execute commands.")]
    IReadOnlyList<ProcessDefinitionStepCommandProjection> Commands,
    [property: Description("Validation findings for the current authoring snapshot.")]
    ProcessDefinitionStepLintProjection Lint,
    [property: Description("Most recent command receipt, or null when this snapshot has no command receipt.")]
    ProcessDefinitionStepCommandReceipt? LastCommandReceipt);
