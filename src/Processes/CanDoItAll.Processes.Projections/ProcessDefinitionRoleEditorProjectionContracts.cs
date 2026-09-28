using System.ComponentModel;
namespace CanDoItAll.Processes.Projections;

[Description("Classification of role executor kind. Numeric JSON values are listed by the OpenAPI schema.")]
public enum ProcessDefinitionRoleExecutorKind
{
    Unspecified,
    Person,
    Agent,
    PersonOrAgent,
    AiAgent,
    Workflow
}

[Description("Classification of role workflow preference kind. Numeric JSON values are listed by the OpenAPI schema.")]
public enum ProcessDefinitionRoleWorkflowPreferenceKind
{
    SpecificWorkflow
}

[Description("Classification of role project assignment kind. Numeric JSON values are listed by the OpenAPI schema.")]
public enum ProcessDefinitionRoleProjectAssignmentKind
{
    Unspecified,
    Customer,
    CustomerContact,
    Stakeholder,
    DeliveryUnit,
    Manager,
    TechnicalContact,
    Reviewer,
    TeamMember,
    AiAgent,
    Developer,
    Architect,
    WorkItemAssignee,
    BillingContact,
    Partner,
    MeetingParticipant
}

[Description("Classification of role template override status. Numeric JSON values are listed by the OpenAPI schema.")]
public enum ProcessDefinitionRoleTemplateOverrideStatus
{
    None,
    AppliedFromTemplate,
    LocallyCustomized,
    ConflictMetadataAvailable
}

[Description("Classification of role command kind. Numeric JSON values are listed by the OpenAPI schema.")]
public enum ProcessDefinitionRoleCommandKind
{
    AddRole,
    SaveRole,
    ApplyTemplate,
    DeleteRole
}

[Description("Classification of role command status. Numeric JSON values are listed by the OpenAPI schema.")]
public enum ProcessDefinitionRoleCommandStatus
{
    Accepted,
    Rejected
}

[Description("Classification of role lint severity. Numeric JSON values are listed by the OpenAPI schema.")]
public enum ProcessDefinitionRoleLintSeverity
{
    Info,
    Warning,
    Error
}

[Description("Classification of role lint section. Numeric JSON values are listed by the OpenAPI schema.")]
public enum ProcessDefinitionRoleLintSection
{
    Identity,
    Execution,
    Template,
    Binding
}

[Description("Classification of step role responsibility kind. Numeric JSON values are listed by the OpenAPI schema.")]
public enum ProcessStepRoleResponsibilityKind
{
    Responsible,
    Reviewer,
    Approver,
    Observer,
    Contributor
}

[Description("Opaque role key represented by its value member. Preserve the value; do not derive it from display text.")]
public readonly record struct ProcessDefinitionRoleKey
{
    public ProcessDefinitionRoleKey(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Process definition role key is required.", nameof(value));
        }

        Value = value.Trim();
    }

    [Description("Opaque identity or authoring-version text carried by this wrapper; preserve exactly.")]
    public string Value { get; }

    public override string ToString() => Value;
}

[Description("Opaque role template action key represented by its value member. Preserve the value; do not derive it from display text.")]
public readonly record struct ProcessDefinitionRoleTemplateActionKey
{
    public ProcessDefinitionRoleTemplateActionKey(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Process definition role template action key is required.", nameof(value));
        }

        Value = value.Trim();
    }

    [Description("Opaque identity or authoring-version text carried by this wrapper; preserve exactly.")]
    public string Value { get; }

    public override string ToString() => Value;
}

[Description("Opaque version of the role editor projection. This is an authoring concurrency token, not an authentication credential.")]
public readonly record struct ProcessDefinitionRoleEditorVersionToken
{
    public ProcessDefinitionRoleEditorVersionToken(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Process definition role editor version token is required.", nameof(value));
        }

        Value = value.Trim();
    }

    [Description("Opaque identity or authoring-version text carried by this wrapper; preserve exactly.")]
    public string Value { get; }

    public override string ToString() => Value;
}

[Description("Opaque step key represented by its value member. Preserve the value; do not derive it from display text.")]
public readonly record struct ProcessDefinitionStepKey
{
    public ProcessDefinitionStepKey(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Process definition step key is required.", nameof(value));
        }

        Value = value.Trim();
    }

    [Description("Opaque identity or authoring-version text carried by this wrapper; preserve exactly.")]
    public string Value { get; }

    public override string ToString() => Value;
}

[Description("Preferred workflow definition and optional pinned version for a role.")]
public sealed record ProcessDefinitionWorkflowPreferenceProjection(
    [property: Description("Classification of this projected authoring item.")]
    ProcessDefinitionRoleWorkflowPreferenceKind Kind,
    [property: Description("Preferred workflow definition identity, or null when no workflow is selected.")]
    Guid? WorkflowDefinitionId,
    [property: Description("Pinned preferred workflow version identity, or null when unpinned.")]
    Guid? WorkflowVersionId,
    [property: Description("Display name of the role, workflow preference or subprocess option.")]
    string DisplayName);

[Description("Role staffing intent, executor preference, required approvals and template customization state.")]
public sealed record ProcessDefinitionRoleDraftProjection(
    [property: Description("Opaque role identity within the process definition.")]
    ProcessDefinitionRoleKey RoleKey,
    [property: Description("Display name of the role, workflow preference or subprocess option.")]
    string DisplayName,
    [property: Description("Business responsibility this role is intended to fulfill.")]
    string Purpose,
    [property: Description("Description of the intended staffing arrangement for the role.")]
    string StaffingIntent,
    [property: Description("Kind of executor preferred by this role.")]
    ProcessDefinitionRoleExecutorKind PreferredExecutorKind,
    [property: Description("Preferred workflow executor and optional pinned version for this role.")]
    ProcessDefinitionWorkflowPreferenceProjection WorkflowPreference,
    [property: Description("Project assignment classification preferred when staffing this role.")]
    ProcessDefinitionRoleProjectAssignmentKind PreferredProjectAssignmentRole,
    [property: Description("Whether the projected artifact, role, binding or loop constraint is mandatory.")]
    bool IsRequired,
    [property: Description("Whether staffing may select a fallback executor for the role.")]
    bool AllowsFallback,
    [property: Description("Whether assigning or using this role requires explicit approval.")]
    bool RequiresExplicitApproval,
    [property: Description("Default staffing allocation expressed as a percentage.")]
    int DefaultAllocationPercent,
    [property: Description("Stable key of the source role template.")]
    string RoleTemplateSourceKey,
    [property: Description("Name of the source role template snapshot.")]
    string RoleTemplateSnapshotName,
    [property: Description("Human-readable explanation of the source role snapshot.")]
    string SnapshotSummary,
    [property: Description("Template customization state of the role.")]
    ProcessDefinitionRoleTemplateOverrideStatus OverrideStatus,
    [property: Description("Explanation of differences from the source role template.")]
    string OverrideSummary);

[Description("Role catalog entry with its editable projection and number of step bindings.")]
public sealed record ProcessDefinitionRoleProjection(
    [property: Description("Opaque role identity within the process definition.")]
    ProcessDefinitionRoleKey RoleKey,
    [property: Description("Display name of the role, workflow preference or subprocess option.")]
    string DisplayName,
    [property: Description("Human-readable explanation of this item; display text is not an executable contract.")]
    string Summary,
    [property: Description("Complete editable role projection for this catalog entry.")]
    ProcessDefinitionRoleDraftProjection Draft,
    [property: Description("Number of definition step bindings that reference this role.")]
    int StepBindingCount);

[Description("Role-template application preview; reading it does not apply the template.")]
public sealed record ProcessDefinitionRoleTemplateActionProjection(
    [property: Description("Opaque key identifying this template or toolbox action.")]
    ProcessDefinitionRoleTemplateActionKey ActionKey,
    [property: Description("Human-readable label for this projected authoring item.")]
    string Label,
    [property: Description("Human-readable explanation of this item; display text is not an executable contract.")]
    string Summary,
    [property: Description("Source role key used by this role template action, when available.")]
    ProcessDefinitionRoleKey? TemplateRoleKey,
    [property: Description("Prefix used when materializing role identities from this template action.")]
    string KeyPrefix,
    [property: Description("Preview of the role display name after applying the template.")]
    string DisplayNamePreview,
    [property: Description("Kind of executor preferred by this role.")]
    ProcessDefinitionRoleExecutorKind PreferredExecutorKind,
    [property: Description("Default staffing allocation expressed as a percentage.")]
    int DefaultAllocationPercent);

[Description("Assignment of a definition role to a step responsibility, with required/fallback rules.")]
public sealed record ProcessDefinitionStepRoleBindingProjection(
    [property: Description("Opaque identity of the definition step referenced by this item, when applicable.")]
    ProcessDefinitionStepKey StepKey,
    [property: Description("Display title of the step referenced by the role binding.")]
    string StepTitle,
    [property: Description("Opaque role identity within the process definition.")]
    ProcessDefinitionRoleKey RoleKey,
    [property: Description("Display name of the role referenced by the step binding.")]
    string RoleDisplayName,
    [property: Description("Responsibility this role holds for the referenced step.")]
    ProcessStepRoleResponsibilityKind ResponsibilityKind,
    [property: Description("Whether the projected artifact, role, binding or loop constraint is mandatory.")]
    bool IsRequired,
    [property: Description("Priority order of a fallback role binding.")]
    int FallbackOrder,
    [property: Description("Human-readable rules for changing the executor assigned to this role binding.")]
    string RebindPolicySummary);

[Description("One role validation finding with a stable reason code and suggested correction.")]
public sealed record ProcessDefinitionRoleLintIssueProjection(
    [property: Description("Stable machine-readable reason code of this validation finding.")]
    string Code,
    [property: Description("Severity of this validation finding.")]
    ProcessDefinitionRoleLintSeverity Severity,
    [property: Description("Authoring section to which this validation finding belongs.")]
    ProcessDefinitionRoleLintSection Section,
    [property: Description("Human-readable explanation of this validation finding.")]
    string Message,
    [property: Description("Suggested authoring correction for this validation finding.")]
    string Suggestion);

[Description("Role validation findings and derived warning/blocking indicators.")]
public sealed record ProcessDefinitionRoleLintProjection(
    [property: Description("Validation findings for the current projection.")]
    IReadOnlyList<ProcessDefinitionRoleLintIssueProjection> Issues)
{
    [Description("True when at least one finding has warning or error severity.")]
    public bool HasWarningsOrErrors => Issues.Any(issue => issue.Severity is ProcessDefinitionRoleLintSeverity.Warning or ProcessDefinitionRoleLintSeverity.Error);

    [Description("True when at least one error finding blocks the authoring action.")]
    public bool HasBlockingIssues => Issues.Any(issue => issue.Severity == ProcessDefinitionRoleLintSeverity.Error);
}

[Description("Availability of a role-authoring command; this read API does not execute it.")]
public sealed record ProcessDefinitionRoleCommandProjection(
    [property: Description("Classification of this projected authoring item.")]
    ProcessDefinitionRoleCommandKind Kind,
    [property: Description("User-facing command label.")]
    string Text,
    [property: Description("Presentation icon name for the command or action.")]
    string Icon,
    [property: Description("Whether the authoring application currently allows this command; this flag does not grant HTTP authority.")]
    bool IsEnabled,
    [property: Description("Human-readable reason a command is unavailable, or null when it is available.")]
    string? DisabledReason);

[Description("Most recent role command result and validation findings at its observed version.")]
public sealed record ProcessDefinitionRoleCommandReceipt(
    [property: Description("Unique identifier of the recorded authoring command receipt.")]
    Guid ReceiptId,
    [property: Description("Authoring command whose outcome this receipt records.")]
    ProcessDefinitionRoleCommandKind CommandKind,
    [property: Description("Current lifecycle or command outcome of this projection.")]
    ProcessDefinitionRoleCommandStatus Status,
    [property: Description("Opaque version of this authoring projection; it is not an authentication token.")]
    ProcessDefinitionRoleEditorVersionToken VersionToken,
    [property: Description("UTC instant when this command result was observed.")]
    DateTimeOffset ObservedAtUtc,
    [property: Description("Human-readable explanation of this item; display text is not an executable contract.")]
    string Summary,
    [property: Description("Validation findings associated with the recorded command.")]
    IReadOnlyList<ProcessDefinitionRoleLintIssueProjection> LintIssues);

public sealed record ProcessDefinitionRoleEditorCommand(
    ProcessWorkspaceShellScope Scope,
    ProcessDefinitionCatalogItemKey DefinitionKey,
    ProcessDefinitionRoleCommandKind CommandKind,
    ProcessDefinitionRoleEditorVersionToken? ExpectedVersionToken,
    ProcessDefinitionRoleDraftProjection Draft,
    ProcessDefinitionRoleTemplateActionKey? TemplateActionKey);

public sealed record ProcessDefinitionRoleEditorCommandResult(
    ProcessDefinitionRoleCommandReceipt Receipt,
    ProcessDefinitionRoleEditorProjection Projection);

[Description("Definition roles, selected role, step assignments and role validation findings.")]
public sealed record ProcessDefinitionRoleEditorProjection(
    [property: Description("Opaque catalog key of the process definition; preserve it exactly when constructing read URLs.")]
    ProcessDefinitionCatalogItemKey DefinitionKey,
    [property: Description("Opaque version of this authoring projection; it is not an authentication token.")]
    ProcessDefinitionRoleEditorVersionToken VersionToken,
    [property: Description("Key of the selected role, or null when none is selected.")]
    ProcessDefinitionRoleKey? SelectedRoleKey,
    [property: Description("Roles declared by this definition in the authoring projection.")]
    IReadOnlyList<ProcessDefinitionRoleProjection> Roles,
    [property: Description("Complete selected role projection, or null when none is selected.")]
    ProcessDefinitionRoleProjection? SelectedRole,
    [property: Description("Available role template application previews; no template is applied by a read.")]
    IReadOnlyList<ProcessDefinitionRoleTemplateActionProjection> TemplateActions,
    [property: Description("Role assignments to steps of this definition.")]
    IReadOnlyList<ProcessDefinitionStepRoleBindingProjection> StepRoleBindings,
    [property: Description("Validation findings for the current authoring snapshot.")]
    ProcessDefinitionRoleLintProjection Lint,
    [property: Description("Commands available in the authoring application; these read endpoints do not execute commands.")]
    IReadOnlyList<ProcessDefinitionRoleCommandProjection> Commands,
    [property: Description("Most recent command receipt, or null when this snapshot has no command receipt.")]
    ProcessDefinitionRoleCommandReceipt? LastCommandReceipt);
