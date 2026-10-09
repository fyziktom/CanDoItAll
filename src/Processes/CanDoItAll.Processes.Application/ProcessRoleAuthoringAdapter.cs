using CanDoItAll.Processes.Contracts;
using CanDoItAll.Processes.Projections;
using CanDoItAll.Processes.Templates;

namespace CanDoItAll.Processes.Application;

public sealed class ProcessRoleAuthoringAdapter(ProcessAuthoringWorkspace workspace, ProcessTemplatePackLoader templates, IProcessProjectionClock clock) {
    public async Task<ProcessDefinitionRoleEditorProjection> ReadAsync(ProcessWorkspaceShellScope scope, ProcessDefinitionCatalogItemKey key, CancellationToken cancellationToken)
        => await ProjectAsync(await workspace.ReadAsync(scope, key, cancellationToken), null, cancellationToken);

    public async Task<ProcessDefinitionRoleEditorCommandResult> ExecuteAsync(ProcessDefinitionRoleEditorCommand submitted, CancellationToken cancellationToken) {
        var (command, fingerprint) = ProcessAuthoringRequests.Capture(submitted);
        if (await workspace.RecoverAsync(command.Scope, command.DefinitionKey, command.ExpectedVersionToken?.Value,
                command.OperationId, fingerprint, cancellationToken) is { } replay) {
            return await ResultAsync(command, workspace.FromReceipt(command.Scope, replay), replay.Outcome, replay.Selection?.RoleKey, null, cancellationToken);
        }
        var baseline = await workspace.ReadAsync(command.Scope, command.DefinitionKey, cancellationToken);
        if (!workspace.Matches(baseline, command.ExpectedVersionToken?.Value)) {
            return await ResultAsync(command, baseline, ProcessAuthoringOutcome.Conflict, command.Draft.RoleKey.Value, null, cancellationToken);
        }
        var existing = baseline.Content.Definition.RoleUsages.SingleOrDefault(role => role.Key == command.Draft.RoleKey.Value);
        if (command.CommandKind != ProcessDefinitionRoleCommandKind.AddRole && existing is null) {
            return await ResultAsync(command, baseline, ProcessAuthoringOutcome.Conflict, null, "The role no longer exists. Role keys cannot be renamed by a save.", cancellationToken);
        }
        if (command.CommandKind == ProcessDefinitionRoleCommandKind.DeleteRole && baseline.Content.Definition.Steps.Any(step =>
                step.DecisionRoleKey == existing!.Key || step.RoleAssignments.Any(binding => binding.RoleKey == existing.Key))) {
            return await ResultAsync(command, baseline, ProcessAuthoringOutcome.Conflict, existing!.Key,
                "Remove this role's step assignments and decision bindings before deleting it.", cancellationToken);
        }
        var normalized = await Engine(baseline).ExecuteCommandAsync(command, cancellationToken);
        if (normalized.Receipt.Status != ProcessDefinitionRoleCommandStatus.Accepted) {
            return normalized with { Projection = normalized.Projection with { Observation = baseline.Observation } };
        }
        var content = ProcessAuthoringCodec.Read(ProcessAuthoringCodec.Write(baseline.Content));
        if (command.CommandKind == ProcessDefinitionRoleCommandKind.DeleteRole) {
            content.Definition.RoleUsages.RemoveAll(role => role.Key == command.Draft.RoleKey.Value);
        } else {
            var patch = normalized.Projection.SelectedRole!.Draft;
            var target = content.Definition.RoleUsages.SingleOrDefault(role => role.Key == patch.RoleKey.Value);
            if (target is null) {
                target = new() { Key = patch.RoleKey.Value };
                content.Definition.RoleUsages.Add(target);
            }
            Apply(target, patch);
            if (content.RoleResources.TryGetValue(target.Key, out var resource)) {
                resource.DisplayName = target.DisplayName;
                resource.Purpose = target.Purpose;
                resource.StaffingIntent = target.StaffingIntent;
                resource.PreferredExecutorKind = target.PreferredExecutorKind;
                resource.PreferredProjectAssignmentRole = target.PreferredProjectAssignmentRole;
                resource.RoleTemplateSourceKey = target.RoleTemplateSourceKey;
                resource.RoleTemplateSnapshotName = target.RoleTemplateSnapshotName;
                resource.SnapshotSummary = target.SnapshotSummary;
            }
        }
        var selected = normalized.Projection.SelectedRoleKey?.Value;
        var saved = await workspace.CommitAsync(baseline, command.OperationId, fingerprint, content, ProcessAuthoringLifecycle.Draft,
            false, new(RoleKey: selected), cancellationToken);
        return await ResultAsync(command, workspace.FromReceipt(command.Scope, saved), saved.Outcome, saved.Selection?.RoleKey, null, cancellationToken);
    }

    private async Task<ProcessDefinitionRoleEditorCommandResult> ResultAsync(ProcessDefinitionRoleEditorCommand command,
        ProcessAuthoringSession session, ProcessAuthoringOutcome outcome, string? selectedKey, string? reason, CancellationToken cancellationToken) {
        var projection = await ProjectAsync(session, selectedKey, cancellationToken);
        ProcessDefinitionRoleCommandReceipt receipt = new(command.OperationId, command.CommandKind,
            outcome == ProcessAuthoringOutcome.Accepted ? ProcessDefinitionRoleCommandStatus.Accepted : ProcessDefinitionRoleCommandStatus.Rejected,
            projection.VersionToken, session.CommittedAtUtc ?? clock.GetUtcNow(), reason ?? (outcome == ProcessAuthoringOutcome.Accepted ? "The role change was committed."
                : "The definition changed. Review the current revision before saving this role again."), []);
        return new(receipt, projection with { LastCommandReceipt = receipt });
    }

    private async Task<ProcessDefinitionRoleEditorProjection> ProjectAsync(ProcessAuthoringSession session, string? selectedKey, CancellationToken cancellationToken) {
        var projection = await Engine(session).GetEditorAsync(session.Scope, new(session.Address.DefinitionKey), cancellationToken);
        var selected = selectedKey is null ? projection.SelectedRole : projection.Roles.FirstOrDefault(role => role.RoleKey.Value == selectedKey);
        return projection with { Observation = session.Observation, SelectedRole = selected, SelectedRoleKey = selected?.RoleKey };
    }

    private ProcessDefinitionRoleEditorProjectionService Engine(ProcessAuthoringSession session) {
        var engine = new ProcessDefinitionRoleEditorProjectionService(templates, clock);
        engine.InitializeAuthored(session);
        return engine;
    }

    internal static void Apply(ProcessTemplateDefinitionRoleUsageDocument target, ProcessDefinitionRoleDraftProjection patch) {
        target.DisplayName = patch.DisplayName;
        target.Purpose = patch.Purpose;
        target.StaffingIntent = patch.StaffingIntent;
        target.PreferredExecutorKind = patch.PreferredExecutorKind switch {
            ProcessDefinitionRoleExecutorKind.PersonOrAgent => "person-or-agent",
            ProcessDefinitionRoleExecutorKind.AiAgent => "ai agent",
            _ => patch.PreferredExecutorKind.ToString().ToLowerInvariant()
        };
        target.PreferredProjectAssignmentRole = patch.PreferredProjectAssignmentRole.ToString();
        target.IsRequired = patch.IsRequired;
        target.AllowsFallback = patch.AllowsFallback;
        target.RequiresExplicitApproval = patch.RequiresExplicitApproval;
        target.DefaultAllocationPercent = patch.DefaultAllocationPercent;
        target.RoleTemplateSourceKey = patch.RoleTemplateSourceKey;
        target.RoleTemplateSnapshotName = patch.RoleTemplateSnapshotName;
        target.SnapshotSummary = patch.SnapshotSummary;
        target.WorkflowBinding = patch.WorkflowPreference.WorkflowDefinitionId is { } workflowId
            ? new(new(workflowId), patch.WorkflowPreference.WorkflowVersionId is { } versionId ? new ProcessWorkflowVersionId(versionId) : null,
                target.WorkflowBinding?.OutputMapping ?? ProcessWorkflowOutputMappingKind.ProcessStepOutcome) : null;
    }
}
