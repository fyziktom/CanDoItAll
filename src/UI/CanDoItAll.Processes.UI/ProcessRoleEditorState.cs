using System.Globalization;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Processes.Projections;
using Microsoft.AspNetCore.Components;

namespace CanDoItAll.Processes.UI;

public sealed class ProcessRoleEditorState {

    public ProcessDefinitionRoleEditorProjection RoleEditor { get; set; } = default!;

    public ProcessWorkspaceShellScope Scope { get; set; } = default!;

    public bool Disabled { get; set; }

    public EventCallback<ProcessDefinitionRoleEditorCommand> ExecuteCommand { get; set; }

    public ProcessDefinitionRoleEditorVersionToken? SyncedVersionToken { get; set; }
    public ProcessDefinitionRoleKey? SelectedRoleKey { get; set; }
    public bool RoleDetailDialogOpen { get; set; }
    public string? CommandNotice { get; set; }
    public string SelectedTemplateActionKey { get; set; } = string.Empty;
    public string RoleDisplayName { get; set; } = string.Empty;
    public string RolePurpose { get; set; } = string.Empty;
    public string RoleStaffingIntent { get; set; } = string.Empty;
    public string RoleTemplateSourceKey { get; set; } = string.Empty;
    public string RoleTemplateSnapshotName { get; set; } = string.Empty;
    public string RoleSnapshotSummary { get; set; } = string.Empty;
    public ProcessDefinitionRoleExecutorKind RoleExecutorKind { get; set; } = ProcessDefinitionRoleExecutorKind.Person;
    public ProcessDefinitionRoleProjectAssignmentKind RoleProjectAssignment { get; set; } = ProcessDefinitionRoleProjectAssignmentKind.Unspecified;
    public ProcessDefinitionRoleTemplateOverrideStatus RoleOverrideStatus { get; set; } = ProcessDefinitionRoleTemplateOverrideStatus.None;
    public Guid? RoleWorkflowDefinitionId { get; set; }
    public Guid? RoleWorkflowVersionId { get; set; }
    public string RoleWorkflowDefinitionIdInput { get; set; } = string.Empty;
    public string RoleWorkflowVersionIdInput { get; set; } = string.Empty;
    public string? RoleWorkflowDefinitionIdError { get; set; }
    public string? RoleWorkflowVersionIdError { get; set; }
    public bool RoleIsRequired { get; set; }
    public bool RoleAllowsFallback { get; set; }
    public bool RoleRequiresApproval { get; set; }
    public string RoleDefaultAllocationPercentInput { get; set; } = "0";
    public int RoleDefaultAllocationPercent => int.Parse(RoleDefaultAllocationPercentInput, NumberStyles.Integer, CultureInfo.InvariantCulture);

    public static readonly ProcessDefinitionRoleExecutorKind[] ExecutorKindOptions =
    [
        ProcessDefinitionRoleExecutorKind.Person,
        ProcessDefinitionRoleExecutorKind.Agent,
        ProcessDefinitionRoleExecutorKind.PersonOrAgent,
        ProcessDefinitionRoleExecutorKind.AiAgent,
        ProcessDefinitionRoleExecutorKind.Workflow
    ];

    public static readonly ProcessDefinitionRoleProjectAssignmentKind[] ProjectAssignmentOptions =
    [
        ProcessDefinitionRoleProjectAssignmentKind.Unspecified,
        ProcessDefinitionRoleProjectAssignmentKind.Manager,
        ProcessDefinitionRoleProjectAssignmentKind.TechnicalContact,
        ProcessDefinitionRoleProjectAssignmentKind.Reviewer,
        ProcessDefinitionRoleProjectAssignmentKind.TeamMember,
        ProcessDefinitionRoleProjectAssignmentKind.CustomerContact,
        ProcessDefinitionRoleProjectAssignmentKind.AiAgent,
        ProcessDefinitionRoleProjectAssignmentKind.Developer,
        ProcessDefinitionRoleProjectAssignmentKind.Architect
    ];

    public void Observe() {
        if (definitionKey != RoleEditor.DefinitionKey || observedScope != Scope) {
            definitionKey = RoleEditor.DefinitionKey;
            observedScope = Scope;
            SelectedRoleKey = RoleEditor.SelectedRoleKey;
            baseline = null;
            submission = null;
            SyncedVersionToken = null;
            HasConflict = false;
        }
        CommandNotice = RoleEditor.LastCommandReceipt?.Summary;
        if (SyncedVersionToken == RoleEditor.VersionToken) {
            return;
        }
        if (IsDirty) {
            HasConflict = true;
            return;
        }
        SyncedVersionToken = RoleEditor.VersionToken;
        SelectedRoleKey ??= RoleEditor.SelectedRoleKey;
        SelectedTemplateActionKey = RoleEditor.TemplateActions.FirstOrDefault()?.ActionKey.Value ?? string.Empty;
        SyncSelectedRole();
    }

    public ProcessDefinitionRoleProjection? ActiveDialogRole
        => SelectedRoleKey is { } roleKey
            ? RoleEditor.Roles.FirstOrDefault(candidate => candidate.RoleKey == roleKey)
            : RoleEditor.SelectedRole;

    public Task OpenRoleDetailsAsync(ProcessDefinitionRoleKey roleKey) {
        if (SelectedRoleKey == roleKey && baseline is not null) {
            RoleDetailDialogOpen = true;
            return Task.CompletedTask;
        }
        submission = null;
        SelectedRoleKey = roleKey;
        SyncedVersionToken = RoleEditor.VersionToken;
        HasConflict = false;
        SelectedTemplateActionKey = RoleEditor.TemplateActions.FirstOrDefault()?.ActionKey.Value ?? string.Empty;
        SyncSelectedRole();
        RoleDetailDialogOpen = true;
        return Task.CompletedTask;
    }

    public Task CloseRoleDetailsAsync() {
        RoleDetailDialogOpen = false;
        return Task.CompletedTask;
    }

    public void SyncSelectedRole() {
        var role = SelectedRoleKey is { } roleKey
            ? RoleEditor.Roles.FirstOrDefault(candidate => candidate.RoleKey == roleKey)
            : RoleEditor.SelectedRole;
        if (role is null) {
            return;
        }

        var draft = role.Draft;
        RoleDisplayName = draft.DisplayName;
        RolePurpose = draft.Purpose;
        RoleStaffingIntent = draft.StaffingIntent;
        RoleExecutorKind = draft.PreferredExecutorKind == ProcessDefinitionRoleExecutorKind.Unspecified
            ? ProcessDefinitionRoleExecutorKind.Person
            : draft.PreferredExecutorKind;
        RoleWorkflowDefinitionId = draft.WorkflowPreference.WorkflowDefinitionId;
        RoleWorkflowVersionId = draft.WorkflowPreference.WorkflowVersionId;
        RoleWorkflowDefinitionIdInput = FormatGuid(RoleWorkflowDefinitionId);
        RoleWorkflowVersionIdInput = FormatGuid(RoleWorkflowVersionId);
        ValidateWorkflowInputs();
        RoleProjectAssignment = draft.PreferredProjectAssignmentRole;
        RoleIsRequired = draft.IsRequired;
        RoleAllowsFallback = draft.AllowsFallback;
        RoleRequiresApproval = draft.RequiresExplicitApproval;
        RoleDefaultAllocationPercentInput = draft.DefaultAllocationPercent.ToString(CultureInfo.InvariantCulture);
        RoleTemplateSourceKey = draft.RoleTemplateSourceKey;
        RoleTemplateSnapshotName = draft.RoleTemplateSnapshotName;
        RoleSnapshotSummary = draft.SnapshotSummary;
        RoleOverrideStatus = draft.OverrideStatus;
        baseline = CaptureInputs();
    }

    public async Task ExecuteAsync(ProcessDefinitionRoleCommandKind commandKind) {
        if (executing || Disabled || NumericInputError is not null || commandKind == ProcessDefinitionRoleCommandKind.SaveRole && HasWorkflowInputErrors) {
            return;
        }
        var draft = CreateDraft();
        ProcessDefinitionRoleTemplateActionKey? templateActionKey = string.IsNullOrWhiteSpace(SelectedTemplateActionKey)
            ? null
            : new ProcessDefinitionRoleTemplateActionKey(SelectedTemplateActionKey);
        var command = new ProcessDefinitionRoleEditorCommand(
            Scope,
            RoleEditor.DefinitionKey,
            commandKind,
            SyncedVersionToken ?? RoleEditor.VersionToken,
            draft,
            templateActionKey);
        var submitted = CaptureInputs();
        submission = submitted;
        executing = true;
        try {
            await ExecuteCommand.InvokeAsync(command);
        } finally {
            executing = false;
            if (ReferenceEquals(submission, submitted)) {
                submission = null;
            }
        }
    }

    public ProcessDefinitionRoleDraftProjection CreateDraft() {
        var roleKey = SelectedRoleKey ?? RoleEditor.SelectedRole?.RoleKey ?? new ProcessDefinitionRoleKey("role-draft");
        return new ProcessDefinitionRoleDraftProjection(
            roleKey,
            RoleDisplayName,
            RolePurpose,
            RoleStaffingIntent,
            RoleExecutorKind,
            new ProcessDefinitionWorkflowPreferenceProjection(
                ProcessDefinitionRoleWorkflowPreferenceKind.SpecificWorkflow,
                RoleExecutorKind == ProcessDefinitionRoleExecutorKind.Workflow
                    ? RoleWorkflowDefinitionId
                    : null,
                RoleExecutorKind == ProcessDefinitionRoleExecutorKind.Workflow
                    ? RoleWorkflowVersionId
                    : null,
                FormatWorkflowPreference()),
            RoleProjectAssignment,
            RoleIsRequired,
            RoleAllowsFallback,
            RoleRequiresApproval,
            RoleDefaultAllocationPercent,
            RoleTemplateSourceKey,
            RoleTemplateSnapshotName,
            RoleSnapshotSummary,
            RoleOverrideStatus,
            string.IsNullOrWhiteSpace(RoleTemplateSourceKey)
                ? "Local role override without a global template source."
                : $"Local override tracks {RoleTemplateSourceKey}.");
    }

    public static string ReadChangeValue(ChangeEventArgs args)
        => args.Value?.ToString() ?? string.Empty;

    public static bool ParseBool(ChangeEventArgs args)
        => args.Value is bool value && value;

    public void UpdateRoleExecutorKind(ChangeEventArgs args) {
        RoleExecutorKind = Enum.TryParse<ProcessDefinitionRoleExecutorKind>(ReadChangeValue(args), out var parsed)
            ? parsed
            : ProcessDefinitionRoleExecutorKind.Person;
        ValidateWorkflowInputs();
    }

    public void UpdateWorkflowDefinitionId(string value) {
        RoleWorkflowDefinitionIdInput = value;
        ValidateWorkflowInputs();
    }

    public void UpdateWorkflowVersionId(string value) {
        RoleWorkflowVersionIdInput = value;
        ValidateWorkflowInputs();
    }

    public void ValidateWorkflowInputs() {
        RoleWorkflowDefinitionId = ParseWorkflowIdentifier(
            RoleWorkflowDefinitionIdInput,
            isRequired: RoleExecutorKind == ProcessDefinitionRoleExecutorKind.Workflow,
            "Workflow definition id",
            out var definitionError);
        RoleWorkflowVersionId = ParseWorkflowIdentifier(
            RoleWorkflowVersionIdInput,
            isRequired: false,
            "Workflow version id",
            out var versionError);
        RoleWorkflowDefinitionIdError = definitionError;
        RoleWorkflowVersionIdError = versionError;
    }

    public static Guid? ParseWorkflowIdentifier(
        string value,
        bool isRequired,
        string displayName,
        out string? error) {
        if (string.IsNullOrWhiteSpace(value)) {
            error = isRequired ? $"{displayName} is required." : null;
            return null;
        }

        if (!Guid.TryParse(value, out var parsed) || parsed == Guid.Empty) {
            error = $"{displayName} must be a non-empty GUID.";
            return null;
        }

        error = null;
        return parsed;
    }

    public bool IsCommandDisabled(ProcessDefinitionRoleCommandProjection command)
        => !command.IsEnabled ||
           Disabled || NumericInputError is not null ||
           command.Kind == ProcessDefinitionRoleCommandKind.SaveRole && HasWorkflowInputErrors;

    public string? ResolveCommandDisabledReason(ProcessDefinitionRoleCommandProjection command)
        => command.Kind == ProcessDefinitionRoleCommandKind.SaveRole && HasWorkflowInputErrors
            ? RoleWorkflowDefinitionIdError ?? RoleWorkflowVersionIdError
            : command.DisabledReason;

    public bool HasWorkflowInputErrors
        => RoleExecutorKind == ProcessDefinitionRoleExecutorKind.Workflow &&
           (RoleWorkflowDefinitionIdError is not null || RoleWorkflowVersionIdError is not null);

    public string FormatWorkflowPreference() {
        if (RoleExecutorKind != ProcessDefinitionRoleExecutorKind.Workflow ||
            RoleWorkflowDefinitionId is not { } workflowDefinitionId) {
            return "Select a workflow";
        }

        return RoleWorkflowVersionId is { } workflowVersionId
            ? $"Workflow {workflowDefinitionId:D}, version {workflowVersionId:D}"
            : $"Workflow {workflowDefinitionId:D}, latest active version";
    }

    public static string FormatGuid(Guid? value)
        => value?.ToString("D") ?? string.Empty;

    public static ProcessDefinitionRoleProjectAssignmentKind ParseProjectAssignment(ChangeEventArgs args)
        => Enum.TryParse<ProcessDefinitionRoleProjectAssignmentKind>(ReadChangeValue(args), out var parsed)
            ? parsed
            : ProcessDefinitionRoleProjectAssignmentKind.Unspecified;

    public static string ResolveLintTone(ProcessDefinitionRoleLintProjection lint) {
        if (lint.HasBlockingIssues) {
            return "danger";
        }

        return lint.HasWarningsOrErrors ? "warning" : "success";
    }

    public static string ResolveLintText(ProcessDefinitionRoleLintProjection lint) {
        if (lint.HasBlockingIssues) {
            return $"{lint.Issues.Count(issue => issue.Severity == ProcessDefinitionRoleLintSeverity.Error)} blocking";
        }

        return lint.HasWarningsOrErrors ? $"{lint.Issues.Count} role lint issue(s)" : "Role lint clear";
    }

    public static string ResolveLintIssueTone(ProcessDefinitionRoleLintSeverity severity)
        => severity switch {
            ProcessDefinitionRoleLintSeverity.Error => "danger",
            ProcessDefinitionRoleLintSeverity.Warning => "warning",
            ProcessDefinitionRoleLintSeverity.Info => "info",
            _ => "neutral"
        };

    public static string ResolveOverrideTone(ProcessDefinitionRoleTemplateOverrideStatus status)
        => status switch {
            ProcessDefinitionRoleTemplateOverrideStatus.AppliedFromTemplate => "info",
            ProcessDefinitionRoleTemplateOverrideStatus.LocallyCustomized => "warning",
            ProcessDefinitionRoleTemplateOverrideStatus.ConflictMetadataAvailable => "danger",
            _ => "neutral"
        };

    public static ButtonStyle ResolveCommandButtonStyle(ProcessDefinitionRoleCommandKind kind)
        => kind switch {
            ProcessDefinitionRoleCommandKind.AddRole => ButtonStyle.Primary,
            ProcessDefinitionRoleCommandKind.SaveRole => ButtonStyle.Success,
            ProcessDefinitionRoleCommandKind.ApplyTemplate => ButtonStyle.Info,
            ProcessDefinitionRoleCommandKind.DeleteRole => ButtonStyle.Danger,
            _ => ButtonStyle.Light
        };

    public static string BuildRoleCommandTestId(ProcessDefinitionRoleCommandKind kind)
        => kind switch {
            ProcessDefinitionRoleCommandKind.AddRole => "processes-role-add",
            ProcessDefinitionRoleCommandKind.SaveRole => "processes-role-save",
            ProcessDefinitionRoleCommandKind.ApplyTemplate => "processes-role-apply-template",
            ProcessDefinitionRoleCommandKind.DeleteRole => "processes-role-delete",
            _ => $"processes-role-command-{kind.ToString().ToLowerInvariant()}"
        };

    public static string BuildRoleItemTestId(ProcessDefinitionRoleKey roleKey) {
        var normalized = new string(roleKey.Value
            .Select(character => char.IsLetterOrDigit(character) ? char.ToLowerInvariant(character) : '-')
            .ToArray());
        return $"processes-role-{normalized}";
    }

    public string? NumericInputError => !int.TryParse(RoleDefaultAllocationPercentInput, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) || value < 0 || value > 100
        ? "Allocation must be a whole number from 0 to 100." : null;

    private ProcessWorkspaceShellScope? observedScope;
    private Inputs? baseline;
    private Inputs? submission;
    private bool executing;
    private ProcessDefinitionCatalogItemKey? definitionKey;
    public bool HasConflict { get; private set; }
    public bool IsDirty => baseline is not null && !CaptureInputs().Matches(baseline);

    private Inputs CaptureInputs() => new(
            RoleDisplayName,
            RolePurpose,
            RoleStaffingIntent,
            RoleTemplateSourceKey,
            RoleTemplateSnapshotName,
            RoleSnapshotSummary,
            RoleExecutorKind,
            RoleProjectAssignment,
            RoleOverrideStatus,
            RoleWorkflowDefinitionId,
            RoleWorkflowVersionId,
            RoleWorkflowDefinitionIdInput,
            RoleWorkflowVersionIdInput,
            RoleWorkflowDefinitionIdError,
            RoleWorkflowVersionIdError,
            RoleIsRequired,
            RoleAllowsFallback,
            RoleRequiresApproval,
            RoleDefaultAllocationPercentInput);

    private sealed record Inputs(
        string RoleDisplayName,
        string RolePurpose,
        string RoleStaffingIntent,
        string RoleTemplateSourceKey,
        string RoleTemplateSnapshotName,
        string RoleSnapshotSummary,
        ProcessDefinitionRoleExecutorKind RoleExecutorKind,
        ProcessDefinitionRoleProjectAssignmentKind RoleProjectAssignment,
        ProcessDefinitionRoleTemplateOverrideStatus RoleOverrideStatus,
        Guid? RoleWorkflowDefinitionId,
        Guid? RoleWorkflowVersionId,
        string RoleWorkflowDefinitionIdInput,
        string RoleWorkflowVersionIdInput,
        string? RoleWorkflowDefinitionIdError,
        string? RoleWorkflowVersionIdError,
        bool RoleIsRequired,
        bool RoleAllowsFallback,
        bool RoleRequiresApproval,
        string RoleDefaultAllocationPercentInput) {
        public bool Matches(Inputs other) => RoleDisplayName == other.RoleDisplayName &&
            RolePurpose == other.RolePurpose &&
            RoleStaffingIntent == other.RoleStaffingIntent &&
            RoleTemplateSourceKey == other.RoleTemplateSourceKey &&
            RoleTemplateSnapshotName == other.RoleTemplateSnapshotName &&
            RoleSnapshotSummary == other.RoleSnapshotSummary &&
            RoleExecutorKind == other.RoleExecutorKind &&
            RoleProjectAssignment == other.RoleProjectAssignment &&
            RoleOverrideStatus == other.RoleOverrideStatus &&
            RoleWorkflowDefinitionId == other.RoleWorkflowDefinitionId &&
            RoleWorkflowVersionId == other.RoleWorkflowVersionId &&
            RoleWorkflowDefinitionIdInput == other.RoleWorkflowDefinitionIdInput &&
            RoleWorkflowVersionIdInput == other.RoleWorkflowVersionIdInput &&
            RoleWorkflowDefinitionIdError == other.RoleWorkflowDefinitionIdError &&
            RoleWorkflowVersionIdError == other.RoleWorkflowVersionIdError &&
            RoleIsRequired == other.RoleIsRequired &&
            RoleAllowsFallback == other.RoleAllowsFallback &&
            RoleRequiresApproval == other.RoleRequiresApproval &&
            RoleDefaultAllocationPercentInput == other.RoleDefaultAllocationPercentInput;
    }

    public void Accept(ProcessDefinitionRoleEditorProjection projection) {
        if (submission is not { } submitted || definitionKey != projection.DefinitionKey) {
            return;
        }
        var current = CaptureInputs();
        submission = null;
        RoleEditor = projection;
        SyncedVersionToken = projection.VersionToken;
        SyncSelectedRole();
        RoleDisplayName = current.RoleDisplayName == submitted.RoleDisplayName ? RoleDisplayName : current.RoleDisplayName;
        RolePurpose = current.RolePurpose == submitted.RolePurpose ? RolePurpose : current.RolePurpose;
        RoleStaffingIntent = current.RoleStaffingIntent == submitted.RoleStaffingIntent ? RoleStaffingIntent : current.RoleStaffingIntent;
        RoleTemplateSourceKey = current.RoleTemplateSourceKey == submitted.RoleTemplateSourceKey ? RoleTemplateSourceKey : current.RoleTemplateSourceKey;
        RoleTemplateSnapshotName = current.RoleTemplateSnapshotName == submitted.RoleTemplateSnapshotName ? RoleTemplateSnapshotName : current.RoleTemplateSnapshotName;
        RoleSnapshotSummary = current.RoleSnapshotSummary == submitted.RoleSnapshotSummary ? RoleSnapshotSummary : current.RoleSnapshotSummary;
        RoleExecutorKind = current.RoleExecutorKind == submitted.RoleExecutorKind ? RoleExecutorKind : current.RoleExecutorKind;
        RoleProjectAssignment = current.RoleProjectAssignment == submitted.RoleProjectAssignment ? RoleProjectAssignment : current.RoleProjectAssignment;
        RoleOverrideStatus = current.RoleOverrideStatus == submitted.RoleOverrideStatus ? RoleOverrideStatus : current.RoleOverrideStatus;
        RoleWorkflowDefinitionId = current.RoleWorkflowDefinitionId == submitted.RoleWorkflowDefinitionId ? RoleWorkflowDefinitionId : current.RoleWorkflowDefinitionId;
        RoleWorkflowVersionId = current.RoleWorkflowVersionId == submitted.RoleWorkflowVersionId ? RoleWorkflowVersionId : current.RoleWorkflowVersionId;
        RoleWorkflowDefinitionIdInput = current.RoleWorkflowDefinitionIdInput == submitted.RoleWorkflowDefinitionIdInput ? RoleWorkflowDefinitionIdInput : current.RoleWorkflowDefinitionIdInput;
        RoleWorkflowVersionIdInput = current.RoleWorkflowVersionIdInput == submitted.RoleWorkflowVersionIdInput ? RoleWorkflowVersionIdInput : current.RoleWorkflowVersionIdInput;
        RoleWorkflowDefinitionIdError = current.RoleWorkflowDefinitionIdError == submitted.RoleWorkflowDefinitionIdError ? RoleWorkflowDefinitionIdError : current.RoleWorkflowDefinitionIdError;
        RoleWorkflowVersionIdError = current.RoleWorkflowVersionIdError == submitted.RoleWorkflowVersionIdError ? RoleWorkflowVersionIdError : current.RoleWorkflowVersionIdError;
        RoleIsRequired = current.RoleIsRequired == submitted.RoleIsRequired ? RoleIsRequired : current.RoleIsRequired;
        RoleAllowsFallback = current.RoleAllowsFallback == submitted.RoleAllowsFallback ? RoleAllowsFallback : current.RoleAllowsFallback;
        RoleRequiresApproval = current.RoleRequiresApproval == submitted.RoleRequiresApproval ? RoleRequiresApproval : current.RoleRequiresApproval;
        RoleDefaultAllocationPercentInput = current.RoleDefaultAllocationPercentInput == submitted.RoleDefaultAllocationPercentInput ? RoleDefaultAllocationPercentInput : current.RoleDefaultAllocationPercentInput;
        HasConflict = false;
    }

    public void Discard() {
        SyncedVersionToken = RoleEditor.VersionToken;
        SyncSelectedRole();
        HasConflict = false;
    }
}
