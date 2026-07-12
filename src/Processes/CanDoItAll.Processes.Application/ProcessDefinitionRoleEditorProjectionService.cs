using System.Globalization;
using CanDoItAll.Processes.Contracts;
using CanDoItAll.Processes.Projections;
using CanDoItAll.Processes.Templates;

namespace CanDoItAll.Processes.Application;

public sealed class ProcessDefinitionRoleEditorProjectionService
{
    private const string RoleKeyRequiredCode = "processes.definition.role.identity.key-required";
    private const string RoleNameRequiredCode = "processes.definition.role.identity.name-required";
    private const string RoleExecutorRequiredCode = "processes.definition.role.execution.executor-required";
    private const string AllocationOutOfRangeCode = "processes.definition.role.execution.allocation-out-of-range";
    private const string MissingTemplateSourceCode = "processes.definition.role.template.source-recommended";
    private const string VersionConflictCode = "processes.definition.role.version-conflict";
    private const string MissingRoleCode = "processes.definition.role.missing";
    private const string MissingTemplateActionCode = "processes.definition.role.template.action-missing";
    private const string WorkflowSelectionRequiredCode = "process.role.workflow_selection_required";
    private const string WorkflowVersionInvalidCode = "process.role.workflow_version_invalid";
    private const string WorkflowBindingExecutorMismatchCode = "process.role.workflow_binding_executor_mismatch";

    private readonly ProcessTemplatePackLoader templatePackLoader;
    private readonly IProcessProjectionClock clock;
    private readonly Dictionary<ProcessDefinitionRoleEditorStateKey, ProcessDefinitionRoleEditorSnapshot> snapshots = [];

    public ProcessDefinitionRoleEditorProjectionService(IProcessProjectionClock clock)
        : this(new ProcessTemplatePackLoader(), clock)
    {
    }

    public ProcessDefinitionRoleEditorProjectionService(
        ProcessTemplatePackLoader templatePackLoader,
        IProcessProjectionClock clock)
    {
        this.templatePackLoader = templatePackLoader ?? throw new ArgumentNullException(nameof(templatePackLoader));
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public Task<ProcessDefinitionRoleEditorProjection> GetEditorAsync(
        ProcessWorkspaceShellScope scope,
        ProcessDefinitionCatalogItemKey definitionKey,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateScope(scope);

        var stateKey = ProcessDefinitionRoleEditorStateKey.From(scope, definitionKey);
        if (snapshots.TryGetValue(stateKey, out var snapshot))
        {
            return Task.FromResult(CreateProjection(snapshot, lastReceipt: null));
        }

        var template = FindTemplateDefinition(definitionKey);
        return Task.FromResult(CreateProjection(CreateTemplateSnapshot(scope, template), lastReceipt: null));
    }

    public Task<ProcessDefinitionRoleEditorCommandResult> ExecuteCommandAsync(
        ProcessDefinitionRoleEditorCommand command,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(command.Scope);
        ArgumentNullException.ThrowIfNull(command.Draft);
        ValidateScope(command.Scope);

        var stateKey = ProcessDefinitionRoleEditorStateKey.From(command.Scope, command.DefinitionKey);
        var baseline = snapshots.TryGetValue(stateKey, out var existing)
            ? existing
            : CreateTemplateSnapshot(command.Scope, FindTemplateDefinition(command.DefinitionKey));
        var observedAtUtc = clock.GetUtcNow();
        var result = command.CommandKind switch
        {
            ProcessDefinitionRoleCommandKind.AddRole => ExecuteAddRole(stateKey, baseline, command, observedAtUtc),
            ProcessDefinitionRoleCommandKind.SaveRole => ExecuteSaveRole(stateKey, baseline, command, observedAtUtc),
            ProcessDefinitionRoleCommandKind.ApplyTemplate => ExecuteApplyTemplate(stateKey, baseline, command, observedAtUtc),
            ProcessDefinitionRoleCommandKind.DeleteRole => ExecuteDeleteRole(stateKey, baseline, command, observedAtUtc),
            _ => throw new ArgumentOutOfRangeException(nameof(command), command.CommandKind, "Unknown role editor command.")
        };

        return Task.FromResult(result);
    }

    private ProcessDefinitionRoleEditorCommandResult ExecuteAddRole(
        ProcessDefinitionRoleEditorStateKey stateKey,
        ProcessDefinitionRoleEditorSnapshot baseline,
        ProcessDefinitionRoleEditorCommand command,
        DateTimeOffset observedAtUtc)
    {
        var versionLint = CreateVersionLint(command.ExpectedVersionToken, baseline.VersionToken);
        if (versionLint.HasBlockingIssues)
        {
            return CreateRejectedResult(
                baseline with { Lint = versionLint },
                command.CommandKind,
                versionLint,
                observedAtUtc,
                "Role was not added because the role editor projection changed before submission.");
        }

        var templateAction = ResolveTemplateAction(baseline, command.TemplateActionKey);
        if (templateAction is null)
        {
            return RejectWithIssue(
                baseline,
                command.CommandKind,
                observedAtUtc,
                MissingTemplateActionCode,
                ProcessDefinitionRoleLintSection.Template,
                "The selected role template action is not available.",
                "Refresh role templates and choose an available action.");
        }

        var draft = CreateDraftFromTemplateAction(templateAction, baseline.Roles);
        var lint = LintDraft(draft);
        var stored = baseline with
        {
            Roles = [.. baseline.Roles, draft],
            SelectedRoleKey = draft.RoleKey,
            VersionToken = CreateVersionToken(command.CommandKind),
            Lint = lint
        };
        snapshots[stateKey] = stored;
        return CreateAcceptedResult(
            stored,
            command.CommandKind,
            lint,
            observedAtUtc,
            $"Role '{draft.DisplayName}' added from {templateAction.Label}.");
    }

    private ProcessDefinitionRoleEditorCommandResult ExecuteSaveRole(
        ProcessDefinitionRoleEditorStateKey stateKey,
        ProcessDefinitionRoleEditorSnapshot baseline,
        ProcessDefinitionRoleEditorCommand command,
        DateTimeOffset observedAtUtc)
    {
        var versionLint = CreateVersionLint(command.ExpectedVersionToken, baseline.VersionToken);
        var draft = NormalizeDraft(command.Draft);
        var lint = MergeLint(LintDraft(draft), versionLint);
        if (lint.HasBlockingIssues)
        {
            return CreateRejectedResult(
                baseline with { SelectedRoleKey = draft.RoleKey, Lint = lint },
                command.CommandKind,
                lint,
                observedAtUtc,
                "Role was not saved because blocking role lint issues remain.");
        }

        var roles = UpsertRole(baseline.Roles, draft);
        var stored = baseline with
        {
            Roles = roles,
            SelectedRoleKey = draft.RoleKey,
            VersionToken = CreateVersionToken(command.CommandKind),
            StepRoleBindings = UpdateStepRoleBindingNames(baseline.StepRoleBindings, roles),
            Lint = lint
        };
        snapshots[stateKey] = stored;
        return CreateAcceptedResult(
            stored,
            command.CommandKind,
            lint,
            observedAtUtc,
            $"Role '{draft.DisplayName}' saved.");
    }

    private ProcessDefinitionRoleEditorCommandResult ExecuteApplyTemplate(
        ProcessDefinitionRoleEditorStateKey stateKey,
        ProcessDefinitionRoleEditorSnapshot baseline,
        ProcessDefinitionRoleEditorCommand command,
        DateTimeOffset observedAtUtc)
    {
        var versionLint = CreateVersionLint(command.ExpectedVersionToken, baseline.VersionToken);
        var templateAction = ResolveTemplateAction(baseline, command.TemplateActionKey);
        if (templateAction is null)
        {
            var missingActionLint = MergeLint(versionLint, new ProcessDefinitionRoleLintProjection(
            [
                new ProcessDefinitionRoleLintIssueProjection(
                    MissingTemplateActionCode,
                    ProcessDefinitionRoleLintSeverity.Error,
                    ProcessDefinitionRoleLintSection.Template,
                    "The selected role template action is not available.",
                    "Refresh role templates and choose an available action.")
            ]));
            return CreateRejectedResult(
                baseline with { Lint = missingActionLint },
                command.CommandKind,
                missingActionLint,
                observedAtUtc,
                "Role template was not applied.");
        }

        var draft = ApplyTemplateAction(command.Draft, templateAction);
        var lint = MergeLint(LintDraft(draft), versionLint);
        if (lint.HasBlockingIssues)
        {
            return CreateRejectedResult(
                baseline with { SelectedRoleKey = draft.RoleKey, Lint = lint },
                command.CommandKind,
                lint,
                observedAtUtc,
                "Role template was not applied because blocking role lint issues remain.");
        }

        var roles = UpsertRole(baseline.Roles, draft);
        var stored = baseline with
        {
            Roles = roles,
            SelectedRoleKey = draft.RoleKey,
            VersionToken = CreateVersionToken(command.CommandKind),
            StepRoleBindings = UpdateStepRoleBindingNames(baseline.StepRoleBindings, roles),
            Lint = lint
        };
        snapshots[stateKey] = stored;
        return CreateAcceptedResult(
            stored,
            command.CommandKind,
            lint,
            observedAtUtc,
            $"Role '{draft.DisplayName}' customized from {templateAction.Label}.");
    }

    private ProcessDefinitionRoleEditorCommandResult ExecuteDeleteRole(
        ProcessDefinitionRoleEditorStateKey stateKey,
        ProcessDefinitionRoleEditorSnapshot baseline,
        ProcessDefinitionRoleEditorCommand command,
        DateTimeOffset observedAtUtc)
    {
        var versionLint = CreateVersionLint(command.ExpectedVersionToken, baseline.VersionToken);
        if (versionLint.HasBlockingIssues)
        {
            return CreateRejectedResult(
                baseline with { Lint = versionLint },
                command.CommandKind,
                versionLint,
                observedAtUtc,
                "Role was not deleted because the role editor projection changed before submission.");
        }

        var roleKey = command.Draft.RoleKey;
        if (!baseline.Roles.Any(role => role.RoleKey == roleKey))
        {
            return RejectWithIssue(
                baseline,
                command.CommandKind,
                observedAtUtc,
                MissingRoleCode,
                ProcessDefinitionRoleLintSection.Identity,
                "The selected role no longer exists.",
                "Reload the role editor and choose an available role.");
        }

        var roles = baseline.Roles
            .Where(role => role.RoleKey != roleKey)
            .ToArray();
        var bindings = baseline.StepRoleBindings
            .Where(binding => binding.RoleKey != roleKey)
            .ToArray();
        var selectedRoleKey = roles.FirstOrDefault()?.RoleKey;
        var stored = baseline with
        {
            Roles = roles,
            StepRoleBindings = bindings,
            SelectedRoleKey = selectedRoleKey,
            VersionToken = CreateVersionToken(command.CommandKind),
            Lint = new ProcessDefinitionRoleLintProjection([])
        };
        snapshots[stateKey] = stored;
        return CreateAcceptedResult(
            stored,
            command.CommandKind,
            stored.Lint,
            observedAtUtc,
            $"Role '{command.Draft.DisplayName}' deleted from the authoring projection.");
    }

    private ProcessDefinitionRoleEditorCommandResult RejectWithIssue(
        ProcessDefinitionRoleEditorSnapshot baseline,
        ProcessDefinitionRoleCommandKind commandKind,
        DateTimeOffset observedAtUtc,
        string code,
        ProcessDefinitionRoleLintSection section,
        string message,
        string suggestion)
    {
        var lint = new ProcessDefinitionRoleLintProjection(
        [
            new ProcessDefinitionRoleLintIssueProjection(
                code,
                ProcessDefinitionRoleLintSeverity.Error,
                section,
                message,
                suggestion)
        ]);
        return CreateRejectedResult(
            baseline with { Lint = lint },
            commandKind,
            lint,
            observedAtUtc,
            message);
    }

    private ProcessDefinitionRoleEditorCommandResult CreateAcceptedResult(
        ProcessDefinitionRoleEditorSnapshot snapshot,
        ProcessDefinitionRoleCommandKind commandKind,
        ProcessDefinitionRoleLintProjection lint,
        DateTimeOffset observedAtUtc,
        string summary)
    {
        var receipt = new ProcessDefinitionRoleCommandReceipt(
            Guid.NewGuid(),
            commandKind,
            ProcessDefinitionRoleCommandStatus.Accepted,
            snapshot.VersionToken,
            observedAtUtc,
            summary,
            lint.Issues);
        return new ProcessDefinitionRoleEditorCommandResult(receipt, CreateProjection(snapshot, receipt));
    }

    private ProcessDefinitionRoleEditorCommandResult CreateRejectedResult(
        ProcessDefinitionRoleEditorSnapshot snapshot,
        ProcessDefinitionRoleCommandKind commandKind,
        ProcessDefinitionRoleLintProjection lint,
        DateTimeOffset observedAtUtc,
        string summary)
    {
        var receipt = new ProcessDefinitionRoleCommandReceipt(
            Guid.NewGuid(),
            commandKind,
            ProcessDefinitionRoleCommandStatus.Rejected,
            snapshot.VersionToken,
            observedAtUtc,
            summary,
            lint.Issues);
        return new ProcessDefinitionRoleEditorCommandResult(receipt, CreateProjection(snapshot with { Lint = lint }, receipt));
    }

    private ProcessDefinitionRoleEditorProjection CreateProjection(
        ProcessDefinitionRoleEditorSnapshot snapshot,
        ProcessDefinitionRoleCommandReceipt? lastReceipt)
    {
        var roleProjections = CreateRoleProjections(snapshot.Roles, snapshot.StepRoleBindings);
        var selectedRole = snapshot.SelectedRoleKey is { } selectedRoleKey
            ? roleProjections.FirstOrDefault(role => role.RoleKey == selectedRoleKey)
            : roleProjections.FirstOrDefault();
        return new ProcessDefinitionRoleEditorProjection(
            snapshot.DefinitionKey,
            snapshot.VersionToken,
            selectedRole?.RoleKey,
            roleProjections,
            selectedRole,
            snapshot.TemplateActions,
            snapshot.StepRoleBindings,
            snapshot.Lint,
            CreateCommands(selectedRole, snapshot.TemplateActions),
            lastReceipt);
    }

    private static IReadOnlyList<ProcessDefinitionRoleProjection> CreateRoleProjections(
        IReadOnlyList<ProcessDefinitionRoleDraftProjection> roles,
        IReadOnlyList<ProcessDefinitionStepRoleBindingProjection> stepRoleBindings)
        => roles
            .Select(role => new ProcessDefinitionRoleProjection(
                role.RoleKey,
                role.DisplayName,
                string.IsNullOrWhiteSpace(role.SnapshotSummary) ? role.Purpose : role.SnapshotSummary,
                role,
                stepRoleBindings.Count(binding => binding.RoleKey == role.RoleKey)))
            .ToArray();

    private ProcessDefinitionRoleEditorSnapshot CreateTemplateSnapshot(
        ProcessWorkspaceShellScope scope,
        ProcessTemplateDefinitionSummary template)
    {
        var roles = template.RoleAuthoringDefaults.Roles
            .Select(CreateDraftFromTemplateRole)
            .ToArray();
        var bindings = template.RoleAuthoringDefaults.StepRoleBindings
            .Select(CreateStepRoleBinding)
            .ToArray();
        var selectedRoleKey = roles.FirstOrDefault()?.RoleKey;
        return new ProcessDefinitionRoleEditorSnapshot(
            scope,
            new ProcessDefinitionCatalogItemKey(template.Key),
            new ProcessDefinitionRoleEditorVersionToken($"template:{template.Key}:roles:{template.UpdatedAtUtc.UtcTicks}"),
            roles,
            selectedRoleKey,
            template.RoleAuthoringDefaults.TemplateActions.Select(CreateTemplateAction).ToArray(),
            UpdateStepRoleBindingNames(bindings, roles),
            new ProcessDefinitionRoleLintProjection([]));
    }

    private static ProcessDefinitionRoleDraftProjection CreateDraftFromTemplateRole(
        ProcessTemplateDefinitionRoleSummary role)
        => new(
            new ProcessDefinitionRoleKey(role.Key),
            role.DisplayName,
            role.Purpose,
            role.StaffingIntent,
            ParseExecutorKind(role.PreferredExecutorKind),
            new ProcessDefinitionWorkflowPreferenceProjection(
                ProcessDefinitionRoleWorkflowPreferenceKind.SpecificWorkflow,
                role.WorkflowBinding?.WorkflowId.Value,
                role.WorkflowBinding?.WorkflowVersionId?.Value,
                FormatWorkflowPreference(role.WorkflowBinding)),
            ParseProjectAssignmentKind(role.PreferredProjectAssignmentRole),
            role.IsRequired,
            role.AllowsFallback,
            role.RequiresExplicitApproval,
            role.DefaultAllocationPercent,
            role.RoleTemplateSourceKey,
            role.RoleTemplateSnapshotName,
            role.SnapshotSummary,
            string.IsNullOrWhiteSpace(role.RoleTemplateSourceKey)
                ? ProcessDefinitionRoleTemplateOverrideStatus.None
                : ProcessDefinitionRoleTemplateOverrideStatus.AppliedFromTemplate,
            role.OverrideSummary);

    private static ProcessDefinitionRoleTemplateActionProjection CreateTemplateAction(
        ProcessTemplateRoleTemplateActionSummary action)
        => new(
            new ProcessDefinitionRoleTemplateActionKey(action.ActionId),
            action.Label,
            action.Summary,
            string.IsNullOrWhiteSpace(action.TemplateRoleKey) ? null : new ProcessDefinitionRoleKey(action.TemplateRoleKey),
            action.KeyPrefix,
            action.DisplayNameTemplate.Replace("{ordinal}", "next", StringComparison.Ordinal),
            ParseExecutorKind(action.PreferredExecutorKind),
            action.DefaultAllocationPercent);

    private static ProcessDefinitionStepRoleBindingProjection CreateStepRoleBinding(
        ProcessTemplateDefinitionStepRoleBindingSummary binding)
        => new(
            new ProcessDefinitionStepKey(binding.StepKey),
            binding.StepTitle,
            new ProcessDefinitionRoleKey(binding.RoleKey),
            binding.RoleDisplayName,
            ParseResponsibilityKind(binding.ResponsibilityKind),
            binding.IsRequired,
            binding.FallbackOrder,
            binding.RebindPolicySummary);

    private static ProcessDefinitionRoleDraftProjection NormalizeDraft(ProcessDefinitionRoleDraftProjection draft)
        => draft with
        {
            DisplayName = NormalizeText(draft.DisplayName),
            Purpose = NormalizeText(draft.Purpose),
            StaffingIntent = NormalizeText(draft.StaffingIntent),
            RoleTemplateSourceKey = NormalizeText(draft.RoleTemplateSourceKey),
            RoleTemplateSnapshotName = NormalizeText(draft.RoleTemplateSnapshotName),
            SnapshotSummary = NormalizeText(draft.SnapshotSummary),
            OverrideSummary = NormalizeText(draft.OverrideSummary),
            WorkflowPreference = draft.WorkflowPreference with
            {
                DisplayName = NormalizeText(draft.WorkflowPreference.DisplayName)
            }
        };

    private static ProcessDefinitionRoleDraftProjection CreateDraftFromTemplateAction(
        ProcessDefinitionRoleTemplateActionProjection templateAction,
        IReadOnlyList<ProcessDefinitionRoleDraftProjection> existingRoles)
    {
        var roleKey = BuildUniqueRoleKey(templateAction.KeyPrefix, existingRoles);
        var ordinal = existingRoles.Count(role => role.RoleKey.Value.StartsWith(templateAction.KeyPrefix, StringComparison.OrdinalIgnoreCase)) + 1;
        var displayName = templateAction.DisplayNamePreview.Replace("next", ordinal.ToString(CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase);
        var sourceKey = templateAction.TemplateRoleKey is { } templateRoleKey
            ? $"process-role-template/{templateRoleKey.Value}"
            : string.Empty;
        return new ProcessDefinitionRoleDraftProjection(
            roleKey,
            displayName,
            templateAction.Summary,
            "Select project staffing that satisfies this role before launch planning.",
            templateAction.PreferredExecutorKind,
            new ProcessDefinitionWorkflowPreferenceProjection(
                ProcessDefinitionRoleWorkflowPreferenceKind.SpecificWorkflow,
                WorkflowDefinitionId: null,
                WorkflowVersionId: null,
                "Select a workflow"),
            ProcessDefinitionRoleProjectAssignmentKind.Unspecified,
            IsRequired: true,
            AllowsFallback: true,
            RequiresExplicitApproval: false,
            templateAction.DefaultAllocationPercent,
            sourceKey,
            string.IsNullOrWhiteSpace(sourceKey) ? string.Empty : $"{templateAction.Label} / template action",
            templateAction.Summary,
            string.IsNullOrWhiteSpace(sourceKey)
                ? ProcessDefinitionRoleTemplateOverrideStatus.None
                : ProcessDefinitionRoleTemplateOverrideStatus.AppliedFromTemplate,
            string.IsNullOrWhiteSpace(sourceKey)
                ? "Blank local role; no global role template source is attached."
                : $"Applied from {sourceKey}.");
    }

    private static ProcessDefinitionRoleDraftProjection ApplyTemplateAction(
        ProcessDefinitionRoleDraftProjection draft,
        ProcessDefinitionRoleTemplateActionProjection templateAction)
    {
        var sourceKey = templateAction.TemplateRoleKey is { } templateRoleKey
            ? $"process-role-template/{templateRoleKey.Value}"
            : string.Empty;
        return NormalizeDraft(draft) with
        {
            Purpose = templateAction.Summary,
            PreferredExecutorKind = templateAction.PreferredExecutorKind,
            DefaultAllocationPercent = templateAction.DefaultAllocationPercent,
            RoleTemplateSourceKey = sourceKey,
            RoleTemplateSnapshotName = string.IsNullOrWhiteSpace(sourceKey) ? string.Empty : $"{templateAction.Label} / template action",
            SnapshotSummary = templateAction.Summary,
            OverrideStatus = string.IsNullOrWhiteSpace(sourceKey)
                ? ProcessDefinitionRoleTemplateOverrideStatus.LocallyCustomized
                : ProcessDefinitionRoleTemplateOverrideStatus.AppliedFromTemplate,
            OverrideSummary = string.IsNullOrWhiteSpace(sourceKey)
                ? "Customized as a local role without a global template source."
                : $"Customized from {sourceKey}."
        };
    }

    private static ProcessDefinitionRoleLintProjection LintDraft(ProcessDefinitionRoleDraftProjection draft)
    {
        var issues = new List<ProcessDefinitionRoleLintIssueProjection>();
        if (string.IsNullOrWhiteSpace(draft.RoleKey.Value))
        {
            issues.Add(new ProcessDefinitionRoleLintIssueProjection(
                RoleKeyRequiredCode,
                ProcessDefinitionRoleLintSeverity.Error,
                ProcessDefinitionRoleLintSection.Identity,
                "Role key is required.",
                "Use a stable role key so steps and launch plans can bind to the role."));
        }

        if (string.IsNullOrWhiteSpace(draft.DisplayName))
        {
            issues.Add(new ProcessDefinitionRoleLintIssueProjection(
                RoleNameRequiredCode,
                ProcessDefinitionRoleLintSeverity.Error,
                ProcessDefinitionRoleLintSection.Identity,
                "Role display name is required.",
                "Enter a user-facing role name."));
        }

        if (draft.PreferredExecutorKind == ProcessDefinitionRoleExecutorKind.Unspecified)
        {
            issues.Add(new ProcessDefinitionRoleLintIssueProjection(
                RoleExecutorRequiredCode,
                ProcessDefinitionRoleLintSeverity.Error,
                ProcessDefinitionRoleLintSection.Execution,
                "Preferred executor kind is required.",
                "Choose a typed executor kind instead of leaving the role executor ambiguous."));
        }

        if (draft.PreferredExecutorKind == ProcessDefinitionRoleExecutorKind.Workflow &&
            (!draft.WorkflowPreference.WorkflowDefinitionId.HasValue ||
             draft.WorkflowPreference.WorkflowDefinitionId.Value == Guid.Empty))
        {
            issues.Add(new ProcessDefinitionRoleLintIssueProjection(
                WorkflowSelectionRequiredCode,
                ProcessDefinitionRoleLintSeverity.Error,
                ProcessDefinitionRoleLintSection.Execution,
                "Workflow executor roles require one explicitly selected workflow.",
                "Select a workflow definition; an arbitrary active workflow is not a valid process binding."));
        }

        if (draft.WorkflowPreference.WorkflowVersionId == Guid.Empty)
        {
            issues.Add(new ProcessDefinitionRoleLintIssueProjection(
                WorkflowVersionInvalidCode,
                ProcessDefinitionRoleLintSeverity.Error,
                ProcessDefinitionRoleLintSection.Execution,
                "An exact workflow version must be a non-empty identifier.",
                "Choose an exact saved workflow version or leave the version empty to use the selected workflow's latest active version."));
        }

        if (draft.PreferredExecutorKind != ProcessDefinitionRoleExecutorKind.Workflow &&
            (draft.WorkflowPreference.WorkflowDefinitionId.HasValue ||
             draft.WorkflowPreference.WorkflowVersionId.HasValue))
        {
            issues.Add(new ProcessDefinitionRoleLintIssueProjection(
                WorkflowBindingExecutorMismatchCode,
                ProcessDefinitionRoleLintSeverity.Error,
                ProcessDefinitionRoleLintSection.Execution,
                "Only workflow executor roles can retain a workflow binding.",
                "Change the executor kind to Workflow or clear the workflow definition and version identifiers."));
        }

        if (draft.DefaultAllocationPercent is < 0 or > 100)
        {
            issues.Add(new ProcessDefinitionRoleLintIssueProjection(
                AllocationOutOfRangeCode,
                ProcessDefinitionRoleLintSeverity.Error,
                ProcessDefinitionRoleLintSection.Execution,
                "Default allocation percent must be between 0 and 100.",
                "Enter a bounded allocation percentage for launch planning."));
        }

        if (string.IsNullOrWhiteSpace(draft.RoleTemplateSourceKey))
        {
            issues.Add(new ProcessDefinitionRoleLintIssueProjection(
                MissingTemplateSourceCode,
                ProcessDefinitionRoleLintSeverity.Warning,
                ProcessDefinitionRoleLintSection.Template,
                "Role template source is empty.",
                "Apply a role template when this role should track global template updates."));
        }

        return new ProcessDefinitionRoleLintProjection(issues);
    }

    private static ProcessDefinitionRoleLintProjection CreateVersionLint(
        ProcessDefinitionRoleEditorVersionToken? expected,
        ProcessDefinitionRoleEditorVersionToken current)
    {
        if (expected is null || expected == current)
        {
            return new ProcessDefinitionRoleLintProjection([]);
        }

        return new ProcessDefinitionRoleLintProjection(
        [
            new ProcessDefinitionRoleLintIssueProjection(
                VersionConflictCode,
                ProcessDefinitionRoleLintSeverity.Error,
                ProcessDefinitionRoleLintSection.Identity,
                "The role editor projection changed before this command was submitted.",
                "Reload the role editor and apply the edit again.")
        ]);
    }

    private static ProcessDefinitionRoleLintProjection MergeLint(
        ProcessDefinitionRoleLintProjection left,
        ProcessDefinitionRoleLintProjection right)
        => new([.. left.Issues, .. right.Issues]);

    private static IReadOnlyList<ProcessDefinitionRoleDraftProjection> UpsertRole(
        IReadOnlyList<ProcessDefinitionRoleDraftProjection> roles,
        ProcessDefinitionRoleDraftProjection draft)
    {
        var found = false;
        var updated = new List<ProcessDefinitionRoleDraftProjection>(roles.Count + 1);
        foreach (var role in roles)
        {
            if (role.RoleKey == draft.RoleKey)
            {
                updated.Add(draft);
                found = true;
                continue;
            }

            updated.Add(role);
        }

        if (!found)
        {
            updated.Add(draft);
        }

        return updated;
    }

    private static IReadOnlyList<ProcessDefinitionStepRoleBindingProjection> UpdateStepRoleBindingNames(
        IReadOnlyList<ProcessDefinitionStepRoleBindingProjection> bindings,
        IReadOnlyList<ProcessDefinitionRoleDraftProjection> roles)
    {
        var roleNames = roles.ToDictionary(role => role.RoleKey, role => role.DisplayName);
        return bindings
            .Select(binding => roleNames.TryGetValue(binding.RoleKey, out var displayName)
                ? binding with { RoleDisplayName = displayName }
                : binding)
            .ToArray();
    }

    private static ProcessDefinitionRoleTemplateActionProjection? ResolveTemplateAction(
        ProcessDefinitionRoleEditorSnapshot snapshot,
        ProcessDefinitionRoleTemplateActionKey? templateActionKey)
        => templateActionKey is null
            ? snapshot.TemplateActions.FirstOrDefault()
            : snapshot.TemplateActions.FirstOrDefault(action => action.ActionKey == templateActionKey);

    private ProcessTemplateDefinitionSummary FindTemplateDefinition(ProcessDefinitionCatalogItemKey definitionKey)
    {
        var pack = templatePackLoader.Load();
        return pack.Definitions.FirstOrDefault(definition =>
            string.Equals(definition.Key, definitionKey.Value, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"Process definition '{definitionKey.Value}' is not available in the template pack.");
    }

    private ProcessDefinitionRoleEditorVersionToken CreateVersionToken(ProcessDefinitionRoleCommandKind commandKind)
        => new($"{commandKind.ToString().ToLowerInvariant()}:{clock.GetUtcNow():yyyyMMddHHmmss}:{Guid.NewGuid():N}");

    private static IReadOnlyList<ProcessDefinitionRoleCommandProjection> CreateCommands(
        ProcessDefinitionRoleProjection? selectedRole,
        IReadOnlyList<ProcessDefinitionRoleTemplateActionProjection> templateActions)
        =>
        [
            new(ProcessDefinitionRoleCommandKind.AddRole, "Add role", "add", templateActions.Count > 0, templateActions.Count > 0 ? null : "No role templates are available."),
            new(ProcessDefinitionRoleCommandKind.SaveRole, "Save role", "save", selectedRole is not null, selectedRole is not null ? null : "Select a role first."),
            new(ProcessDefinitionRoleCommandKind.ApplyTemplate, "Apply template", "content_copy", selectedRole is not null && templateActions.Count > 0, selectedRole is null ? "Select a role first." : templateActions.Count > 0 ? null : "No role templates are available."),
            new(ProcessDefinitionRoleCommandKind.DeleteRole, "Delete role", "delete", selectedRole is not null, selectedRole is not null ? null : "Select a role first.")
        ];

    private static ProcessDefinitionRoleKey BuildUniqueRoleKey(
        string keyPrefix,
        IReadOnlyList<ProcessDefinitionRoleDraftProjection> roles)
    {
        var prefix = string.IsNullOrWhiteSpace(keyPrefix) ? "role" : keyPrefix.Trim();
        var used = roles
            .Select(role => role.RoleKey.Value)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        for (var index = 1; index < int.MaxValue; index++)
        {
            var candidate = $"{prefix}-{index.ToString(CultureInfo.InvariantCulture)}";
            if (!used.Contains(candidate))
            {
                return new ProcessDefinitionRoleKey(candidate);
            }
        }

        throw new InvalidOperationException($"Unable to allocate a unique process role key with prefix '{prefix}'.");
    }

    private static ProcessDefinitionRoleExecutorKind ParseExecutorKind(string value)
        => NormalizeText(value).ToLowerInvariant() switch
        {
            "person" => ProcessDefinitionRoleExecutorKind.Person,
            "agent" => ProcessDefinitionRoleExecutorKind.Agent,
            "person-or-agent" => ProcessDefinitionRoleExecutorKind.PersonOrAgent,
            "ai agent" => ProcessDefinitionRoleExecutorKind.AiAgent,
            "workflow" => ProcessDefinitionRoleExecutorKind.Workflow,
            _ => ProcessDefinitionRoleExecutorKind.Unspecified
        };

    private static string FormatWorkflowPreference(ProcessWorkflowExecutorBinding? binding)
    {
        if (binding is null)
        {
            return "Select a workflow";
        }

        return binding.WorkflowVersionId is { } versionId
            ? $"Workflow {binding.WorkflowId.Value:D}, version {versionId.Value:D}"
            : $"Workflow {binding.WorkflowId.Value:D}, latest active version";
    }

    private static ProcessDefinitionRoleProjectAssignmentKind ParseProjectAssignmentKind(string value)
        => Enum.TryParse<ProcessDefinitionRoleProjectAssignmentKind>(NormalizeText(value), ignoreCase: true, out var parsed)
            ? parsed
            : ProcessDefinitionRoleProjectAssignmentKind.Unspecified;

    private static ProcessStepRoleResponsibilityKind ParseResponsibilityKind(string value)
        => Enum.TryParse<ProcessStepRoleResponsibilityKind>(NormalizeText(value), ignoreCase: true, out var parsed)
            ? parsed
            : ProcessStepRoleResponsibilityKind.Responsible;

    private static string NormalizeText(string? value)
        => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();

    private static void ValidateScope(ProcessWorkspaceShellScope scope)
    {
        if (scope.Kind == ProcessWorkspaceScopeKind.Project && scope.ProjectId is null)
        {
            throw new ArgumentException("Project-scoped role editor command requires a project id.", nameof(scope));
        }

        if (scope.Kind == ProcessWorkspaceScopeKind.Global && scope.ProjectId is not null)
        {
            throw new ArgumentException("Global role editor command cannot carry a project id.", nameof(scope));
        }
    }

    private readonly record struct ProcessDefinitionRoleEditorStateKey(
        ProcessWorkspaceScopeKind ScopeKind,
        Guid? ProjectId,
        ProcessDefinitionCatalogItemKey DefinitionKey)
    {
        public static ProcessDefinitionRoleEditorStateKey From(
            ProcessWorkspaceShellScope scope,
            ProcessDefinitionCatalogItemKey definitionKey)
            => new(scope.Kind, scope.ProjectId, definitionKey);
    }

    private sealed record ProcessDefinitionRoleEditorSnapshot(
        ProcessWorkspaceShellScope Scope,
        ProcessDefinitionCatalogItemKey DefinitionKey,
        ProcessDefinitionRoleEditorVersionToken VersionToken,
        IReadOnlyList<ProcessDefinitionRoleDraftProjection> Roles,
        ProcessDefinitionRoleKey? SelectedRoleKey,
        IReadOnlyList<ProcessDefinitionRoleTemplateActionProjection> TemplateActions,
        IReadOnlyList<ProcessDefinitionStepRoleBindingProjection> StepRoleBindings,
        ProcessDefinitionRoleLintProjection Lint);
}
