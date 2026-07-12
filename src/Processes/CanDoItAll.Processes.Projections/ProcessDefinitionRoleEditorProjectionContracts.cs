namespace CanDoItAll.Processes.Projections;

public enum ProcessDefinitionRoleExecutorKind
{
    Unspecified,
    Person,
    Agent,
    PersonOrAgent,
    AiAgent,
    Workflow
}

public enum ProcessDefinitionRoleWorkflowPreferenceKind
{
    SpecificWorkflow
}

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

public enum ProcessDefinitionRoleTemplateOverrideStatus
{
    None,
    AppliedFromTemplate,
    LocallyCustomized,
    ConflictMetadataAvailable
}

public enum ProcessDefinitionRoleCommandKind
{
    AddRole,
    SaveRole,
    ApplyTemplate,
    DeleteRole
}

public enum ProcessDefinitionRoleCommandStatus
{
    Accepted,
    Rejected
}

public enum ProcessDefinitionRoleLintSeverity
{
    Info,
    Warning,
    Error
}

public enum ProcessDefinitionRoleLintSection
{
    Identity,
    Execution,
    Template,
    Binding
}

public enum ProcessStepRoleResponsibilityKind
{
    Responsible,
    Reviewer,
    Approver,
    Observer,
    Contributor
}

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

    public string Value { get; }

    public override string ToString() => Value;
}

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

    public string Value { get; }

    public override string ToString() => Value;
}

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

    public string Value { get; }

    public override string ToString() => Value;
}

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

    public string Value { get; }

    public override string ToString() => Value;
}

public sealed record ProcessDefinitionWorkflowPreferenceProjection(
    ProcessDefinitionRoleWorkflowPreferenceKind Kind,
    Guid? WorkflowDefinitionId,
    Guid? WorkflowVersionId,
    string DisplayName);

public sealed record ProcessDefinitionRoleDraftProjection(
    ProcessDefinitionRoleKey RoleKey,
    string DisplayName,
    string Purpose,
    string StaffingIntent,
    ProcessDefinitionRoleExecutorKind PreferredExecutorKind,
    ProcessDefinitionWorkflowPreferenceProjection WorkflowPreference,
    ProcessDefinitionRoleProjectAssignmentKind PreferredProjectAssignmentRole,
    bool IsRequired,
    bool AllowsFallback,
    bool RequiresExplicitApproval,
    int DefaultAllocationPercent,
    string RoleTemplateSourceKey,
    string RoleTemplateSnapshotName,
    string SnapshotSummary,
    ProcessDefinitionRoleTemplateOverrideStatus OverrideStatus,
    string OverrideSummary);

public sealed record ProcessDefinitionRoleProjection(
    ProcessDefinitionRoleKey RoleKey,
    string DisplayName,
    string Summary,
    ProcessDefinitionRoleDraftProjection Draft,
    int StepBindingCount);

public sealed record ProcessDefinitionRoleTemplateActionProjection(
    ProcessDefinitionRoleTemplateActionKey ActionKey,
    string Label,
    string Summary,
    ProcessDefinitionRoleKey? TemplateRoleKey,
    string KeyPrefix,
    string DisplayNamePreview,
    ProcessDefinitionRoleExecutorKind PreferredExecutorKind,
    int DefaultAllocationPercent);

public sealed record ProcessDefinitionStepRoleBindingProjection(
    ProcessDefinitionStepKey StepKey,
    string StepTitle,
    ProcessDefinitionRoleKey RoleKey,
    string RoleDisplayName,
    ProcessStepRoleResponsibilityKind ResponsibilityKind,
    bool IsRequired,
    int FallbackOrder,
    string RebindPolicySummary);

public sealed record ProcessDefinitionRoleLintIssueProjection(
    string Code,
    ProcessDefinitionRoleLintSeverity Severity,
    ProcessDefinitionRoleLintSection Section,
    string Message,
    string Suggestion);

public sealed record ProcessDefinitionRoleLintProjection(
    IReadOnlyList<ProcessDefinitionRoleLintIssueProjection> Issues)
{
    public bool HasWarningsOrErrors => Issues.Any(issue => issue.Severity is ProcessDefinitionRoleLintSeverity.Warning or ProcessDefinitionRoleLintSeverity.Error);

    public bool HasBlockingIssues => Issues.Any(issue => issue.Severity == ProcessDefinitionRoleLintSeverity.Error);
}

public sealed record ProcessDefinitionRoleCommandProjection(
    ProcessDefinitionRoleCommandKind Kind,
    string Text,
    string Icon,
    bool IsEnabled,
    string? DisabledReason);

public sealed record ProcessDefinitionRoleCommandReceipt(
    Guid ReceiptId,
    ProcessDefinitionRoleCommandKind CommandKind,
    ProcessDefinitionRoleCommandStatus Status,
    ProcessDefinitionRoleEditorVersionToken VersionToken,
    DateTimeOffset ObservedAtUtc,
    string Summary,
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

public sealed record ProcessDefinitionRoleEditorProjection(
    ProcessDefinitionCatalogItemKey DefinitionKey,
    ProcessDefinitionRoleEditorVersionToken VersionToken,
    ProcessDefinitionRoleKey? SelectedRoleKey,
    IReadOnlyList<ProcessDefinitionRoleProjection> Roles,
    ProcessDefinitionRoleProjection? SelectedRole,
    IReadOnlyList<ProcessDefinitionRoleTemplateActionProjection> TemplateActions,
    IReadOnlyList<ProcessDefinitionStepRoleBindingProjection> StepRoleBindings,
    ProcessDefinitionRoleLintProjection Lint,
    IReadOnlyList<ProcessDefinitionRoleCommandProjection> Commands,
    ProcessDefinitionRoleCommandReceipt? LastCommandReceipt);
