using System.Text.Json;

namespace CanDoItAll.Modules.Processes;

public partial class ProcessWorkspace
{
    private ProcessRoleEditorModel? roleDialogModel;
    private ProcessRoleEditorModel? roleDialogTarget;
    private bool roleDialogIsNew;
    private string roleDialogError = string.Empty;
    private string selectedRoleTemplateItemId = string.Empty;

    private IReadOnlyList<ProcessTemplateLibraryListItem> RoleTemplateOptions
        => ProcessTemplateLibraryService.ListItems(ProcessTemplateLibraryCategory.Roles);

    private string RoleDialogTitle
        => roleDialogIsNew
            ? "Add role"
            : $"Edit {ResolveRoleDisplayName(roleDialogTarget)}";

    private string RoleDialogSubtitle
        => roleDialogIsNew
            ? "Create the role draft here first. It appears as a role card after you save the dialog."
            : "Edit the role contract details, then save the draft back to the process definition.";

    private async Task CreateNewAsync()
    {
        await QuiesceDefinitionCanvasPersistenceAsync(DefinitionCanvasPersistenceQuiescenceMode.FlushPendingChanges);
        selectedProcessId = null;
        selectedRunId = null;
        selectedCanvasNodeId = null;
        ResetDefinitionCanvasState();
        ResetRuntimeCanvasState();
        editor = await ProcessesService.GetEditorAsync(null, ProjectId);
        detailTab = DetailTabDefinition;
        ClearRuntimePaneData();
        improvements = [];
        analytics = await ProcessesService.GetAnalyticsAsync(null, ProjectId);
        CloseCanvasEditor();
        canvasActionDialog = null;
        RefreshCanvasSurface();
        ClearMessage();
    }

    private async Task SelectDefinitionAsync(Guid definitionId)
    {
        await QuiesceDefinitionCanvasPersistenceAsync(DefinitionCanvasPersistenceQuiescenceMode.FlushPendingChanges);
        selectedProcessId = definitionId;
        detailTab = DetailTabDefinition;
        selectedCanvasNodeId = null;
        ResetDefinitionCanvasState();
        ResetRuntimeCanvasState();
        await LoadWorkspaceAsync();
    }

    private async Task HandleProcessTreeSelectAsync(string nodeId)
    {
        if (!ProcessDefinitionTreeNodeBuilder.TryReadDefinitionId(nodeId, out var definitionId))
        {
            return;
        }

        await SelectDefinitionAsync(definitionId);
    }

    private Task HandleProcessTreeToggleAsync(string nodeId)
    {
        if (!expandedProcessTreeNodeIds.Add(nodeId))
        {
            expandedProcessTreeNodeIds.Remove(nodeId);
        }

        return Task.CompletedTask;
    }

    private async Task SaveAsync()
    {
        await QuiesceDefinitionCanvasPersistenceAsync(DefinitionCanvasPersistenceQuiescenceMode.CancelPendingChanges);
        NormalizeEditorForAuthoring();
        var result = await ProcessesService.SaveAsync(editor);
        if (result.IsFailure)
        {
            SetError(result.Errors);
            return;
        }

        selectedProcessId = result.Value;
        InvalidateObservationState(selectedProcessId);
        await LoadWorkspaceAsync();
        SetMessage("Process definition saved.");
    }

    private async Task PublishAsync()
    {
        if (!selectedProcessId.HasValue)
        {
            SetError("Save the process definition before publishing it.");
            return;
        }

        await QuiesceDefinitionCanvasPersistenceAsync(DefinitionCanvasPersistenceQuiescenceMode.FlushPendingChanges);
        var result = await ProcessesService.PublishAsync(
            new ProcessDefinitionPublishRequest
            {
                DefinitionId = selectedProcessId.Value,
                DefinitionConcurrencyToken = editor.DefinitionConcurrencyToken,
                DraftVersionConcurrencyToken = editor.WorkingVersionConcurrencyToken
            });
        if (result.IsFailure)
        {
            SetError(result.Errors);
            return;
        }

        await LoadWorkspaceAsync();
        SetMessage("Process definition published.");
    }

    private async Task DeleteAsync()
    {
        if (!selectedProcessId.HasValue)
        {
            return;
        }

        await QuiesceDefinitionCanvasPersistenceAsync(DefinitionCanvasPersistenceQuiescenceMode.CancelPendingChanges);
        await ProcessesService.DeleteAsync(selectedProcessId.Value);
        selectedProcessId = null;
        selectedRunId = null;
        selectedCanvasNodeId = null;
        ResetDefinitionCanvasState();
        ResetRuntimeCanvasState();
        InvalidateObservationState();
        await LoadWorkspaceAsync();
        SetMessage("Process definition deleted.");
    }

    private async Task SeedBaselineAsync()
    {
        var result = await SeedService.SeedBaselineAsync(ProjectId);
        if (result.IsFailure)
        {
            SetError(result.Errors);
            return;
        }

        selectedProcessId = result.Value!.PrimaryDefinitionId;
        selectedRunId = result.Value.SeededRunIds.FirstOrDefault();
        selectedCanvasNodeId = null;
        ResetDefinitionCanvasState();
        ResetRuntimeCanvasState();
        detailTab = DetailTabRuns;
        InvalidateObservationState(selectedProcessId);
        await LoadWorkspaceAsync();
        SetMessage("Development seed baseline prepared.");
    }

    private async Task ExportAsync()
    {
        if (!selectedProcessId.HasValue)
        {
            return;
        }

        await QuiesceDefinitionCanvasPersistenceAsync(DefinitionCanvasPersistenceQuiescenceMode.FlushPendingChanges);
        var envelope = await ProcessesService.ExportAsync(selectedProcessId.Value);
        exportJson = JsonSerializer.Serialize(envelope, JsonOptions);
        detailTab = DetailTabExchange;
        SetMessage("Process definition exported.");
    }

    private async Task ImportAsync()
    {
        if (string.IsNullOrWhiteSpace(importJson))
        {
            SetError("Paste an import envelope before running import.");
            return;
        }

        ProcessImportExportEnvelope? envelope;
        try
        {
            envelope = JsonSerializer.Deserialize<ProcessImportExportEnvelope>(importJson, JsonOptions);
        }
        catch (JsonException exception)
        {
            SetError($"Import envelope is not valid JSON. {exception.Message}");
            return;
        }

        if (envelope is null)
        {
            SetError("Import envelope could not be parsed.");
            return;
        }

        var result = await ProcessesService.ImportAsync(envelope);
        if (result.IsFailure)
        {
            SetError(result.Errors);
            return;
        }

        selectedProcessId = result.Value;
        selectedCanvasNodeId = null;
        ResetDefinitionCanvasState();
        ResetRuntimeCanvasState();
        await LoadWorkspaceAsync();
        SetMessage("Process definition imported.");
    }

    private void AddRole()
    {
        OpenRoleDialog(
            new ProcessRoleEditorModel
            {
                Id = Guid.NewGuid(),
                DefaultAllocationPercent = 100,
                IsRequired = true,
                AllowsFallback = true
            },
            target: null,
            isNew: true);
    }

    private void OpenRoleDetails(ProcessRoleEditorModel role)
    {
        OpenRoleDialog(CloneRole(role), role, isNew: false);
    }

    private void OpenRoleDialog(ProcessRoleEditorModel draft, ProcessRoleEditorModel? target, bool isNew)
    {
        roleDialogModel = draft;
        roleDialogTarget = target;
        roleDialogIsNew = isNew;
        roleDialogError = string.Empty;
        selectedRoleTemplateItemId = string.Empty;
    }

    private Task CloseRoleDialogAsync()
    {
        roleDialogModel = null;
        roleDialogTarget = null;
        roleDialogIsNew = false;
        roleDialogError = string.Empty;
        selectedRoleTemplateItemId = string.Empty;
        return Task.CompletedTask;
    }

    private void ApplySelectedRoleTemplate()
    {
        if (roleDialogModel is null || string.IsNullOrWhiteSpace(selectedRoleTemplateItemId))
        {
            return;
        }

        var currentId = roleDialogModel.Id;
        var currentCanvasX = roleDialogModel.CanvasX;
        var currentCanvasY = roleDialogModel.CanvasY;
        var draft = CreateUniqueRoleDraftFromTemplate(selectedRoleTemplateItemId, roleDialogTarget);

        if (!roleDialogIsNew && currentId.HasValue)
        {
            draft.Id = currentId;
            draft.CanvasX = currentCanvasX;
            draft.CanvasY = currentCanvasY;
        }

        roleDialogModel = draft;
        roleDialogError = string.Empty;
    }

    private void SaveRoleDialog()
    {
        if (roleDialogModel is null)
        {
            return;
        }

        roleDialogError = ValidateRoleDialog(roleDialogModel);
        if (!string.IsNullOrWhiteSpace(roleDialogError))
        {
            return;
        }

        roleDialogModel.Id ??= Guid.NewGuid();
        if (roleDialogIsNew || roleDialogTarget is null || !editor.Roles.Contains(roleDialogTarget))
        {
            editor.Roles.Add(CloneRole(roleDialogModel));
        }
        else
        {
            CopyRole(roleDialogModel, roleDialogTarget);
        }

        RefreshCanvasSurface();
        SetMessage(roleDialogIsNew ? "Role added to the process draft." : "Role details updated.");
        _ = CloseRoleDialogAsync();
    }

    private void RemoveRoleFromDialog()
    {
        if (roleDialogTarget is null)
        {
            _ = CloseRoleDialogAsync();
            return;
        }

        RemoveRole(roleDialogTarget);
        SetMessage("Role removed from the process draft.");
        _ = CloseRoleDialogAsync();
    }

    private void RemoveRole(ProcessRoleEditorModel role, bool refreshSurface = true)
    {
        editor.Roles.Remove(role);
        editor.MessagingPolicies.RemoveAll(item =>
            item.SourceRoleRequirementId == role.Id ||
            item.TargetRoleRequirementId == role.Id);
        foreach (var step in editor.Steps)
        {
            step.RoleAssignments.RemoveAll(item => item.RoleRequirementId == role.Id);
            if (step.DecisionRoleRequirementId == role.Id)
            {
                step.DecisionRoleRequirementId = null;
            }
        }

        if (refreshSurface)
        {
            RefreshCanvasSurface();
        }
    }

    private ProcessRoleEditorModel CreateUniqueRoleDraftFromTemplate(string itemId, ProcessRoleEditorModel? excludedRole)
    {
        var ordinal = 1;
        ProcessRoleEditorModel draft;
        do
        {
            draft = ProcessTemplateLibraryService.CreateRoleDraft(itemId, ordinal);
            ordinal++;
        }
        while (editor.Roles.Any(role =>
            !ReferenceEquals(role, excludedRole) &&
            string.Equals(role.Key, draft.Key, StringComparison.OrdinalIgnoreCase)));

        return draft;
    }

    private static string ValidateRoleDialog(ProcessRoleEditorModel role)
    {
        if (string.IsNullOrWhiteSpace(role.DisplayName))
        {
            return "Display name is required before the role can be added.";
        }

        if (role.DefaultAllocationPercent is < 0 or > 100)
        {
            return "Default allocation percent must be between 0 and 100.";
        }

        return string.Empty;
    }

    private static string ResolveRoleDisplayName(ProcessRoleEditorModel? role)
    {
        if (role is null || string.IsNullOrWhiteSpace(role.DisplayName))
        {
            return "Unnamed role";
        }

        return role.DisplayName.Trim();
    }

    private static string ResolveRoleExecutorKind(ProcessRoleEditorModel role)
    {
        return string.IsNullOrWhiteSpace(role.PreferredExecutorKind)
            ? "Executor kind not set"
            : role.PreferredExecutorKind.Trim();
    }

    private static string ResolveRoleSummary(ProcessRoleEditorModel role)
    {
        if (!string.IsNullOrWhiteSpace(role.Purpose))
        {
            return role.Purpose.Trim();
        }

        if (!string.IsNullOrWhiteSpace(role.StaffingIntent))
        {
            return role.StaffingIntent.Trim();
        }

        return "No purpose is configured yet.";
    }

    private static string ResolveRoleKey(ProcessRoleEditorModel role)
    {
        return string.IsNullOrWhiteSpace(role.Key)
            ? "Key pending"
            : role.Key.Trim();
    }

    private static string ResolveRoleTemplateLabel(ProcessRoleEditorModel role)
    {
        if (!string.IsNullOrWhiteSpace(role.RoleTemplateSnapshotName))
        {
            return role.RoleTemplateSnapshotName.Trim();
        }

        if (!string.IsNullOrWhiteSpace(role.RoleTemplateSourceKey))
        {
            return role.RoleTemplateSourceKey.Trim();
        }

        return "Manual role";
    }

    private static string BuildRoleInitials(ProcessRoleEditorModel role)
    {
        var source = ResolveRoleDisplayName(role);
        var segments = source
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Take(2)
            .ToList();

        if (segments.Count == 0)
        {
            return "RL";
        }

        return string.Concat(segments.Select(segment => char.ToUpperInvariant(segment[0])));
    }

    private void AddStep()
    {
        var previousStep = editor.Steps.LastOrDefault();
        editor.Steps.Add(new ProcessStepEditorModel
        {
            Id = Guid.NewGuid(),
            Title = $"Step {editor.Steps.Count + 1}",
            StepKind = ProcessStepKind.Work,
            TargetLeadHours = 1,
            CanvasX = 140 + (editor.Steps.Count * 280),
            CanvasY = 180,
            Dependencies = previousStep?.Id.HasValue == true
                ? [new ProcessStepDependencyEditorModel { Id = Guid.NewGuid(), DependsOnStepId = previousStep.Id }]
                : []
        });
        RefreshCanvasSurface();
    }

    private void RemoveStep(ProcessStepEditorModel step, bool refreshSurface = true)
    {
        editor.Steps.Remove(step);
        foreach (var candidate in editor.Steps)
        {
            SetStepDependencies(
                candidate,
                ProcessCanvasBranching.GetOrderedDependencies(candidate)
                    .Where(dependency => dependency.DependsOnStepId != step.Id));
        }

        if (refreshSurface)
        {
            RefreshCanvasSurface();
        }
    }

    private void AddBranchOutcome(ProcessStepEditorModel step)
    {
        if (editor.Steps.Contains(step))
        {
            NormalizeEditorForAuthoring();
        }
        else
        {
            NormalizeStepDraftForAuthoring(step);
        }

        var customOutcomeCount = ProcessCanvasBranching.GetCustomBranchOutcomes(step).Count;
        step.BranchOutcomes.Add(new ProcessStepBranchOutcomeEditorModel
        {
            Id = Guid.NewGuid(),
            Key = $"outcome-{customOutcomeCount + 1}",
            Title = $"Outcome {customOutcomeCount + 1}"
        });
        if (editor.Steps.Contains(step))
        {
            NormalizeEditorForAuthoring();
        }
        else
        {
            NormalizeStepDraftForAuthoring(step);
        }

        RefreshCanvasSurface();
    }

    private void RemoveBranchOutcome(ProcessStepEditorModel step, ProcessStepBranchOutcomeEditorModel branchOutcome)
    {
        if (ProcessCanvasBranching.IsSystemOutcome(branchOutcome))
        {
            return;
        }

        step.BranchOutcomes.Remove(branchOutcome);
        if (!branchOutcome.Id.HasValue)
        {
            NormalizeEditorForAuthoring();
            RefreshCanvasSurface();
            return;
        }

        foreach (var candidate in editor.Steps)
        {
            SetStepDependencies(
                candidate,
                ProcessCanvasBranching.GetOrderedDependencies(candidate)
                    .Where(dependency => dependency.DependsOnBranchOutcomeId != branchOutcome.Id.Value));
        }

        NormalizeEditorForAuthoring();
        RefreshCanvasSurface();
    }

    private void AddRoleAssignment(ProcessStepEditorModel step)
    {
        step.RoleAssignments.Add(new ProcessStepRoleRequirementEditorModel
        {
            RoleRequirementId = editor.Roles.FirstOrDefault()?.Id,
            ResponsibilityKind = ProcessResponsibilityKind.Responsible,
            IsRequired = true
        });
        RefreshCanvasSurface();
    }

    private void RemoveRoleAssignment(ProcessStepEditorModel step, ProcessStepRoleRequirementEditorModel assignment)
    {
        step.RoleAssignments.Remove(assignment);
        RefreshCanvasSurface();
    }

    private void AddArtifact(ProcessStepEditorModel step)
    {
        step.ArtifactExpectations.Add(new ProcessArtifactExpectationEditorModel
        {
            ArtifactKind = ProcessArtifactKind.Evidence,
            Title = "New artifact",
            IsRequired = true,
            RetentionDays = 90
        });
        RefreshCanvasSurface();
    }

    private void RemoveArtifact(ProcessStepEditorModel step, ProcessArtifactExpectationEditorModel artifact)
    {
        step.ArtifactExpectations.Remove(artifact);
        RefreshCanvasSurface();
    }

    private async Task HandleDetailTabChanged(int index)
    {
        detailTab = ResolveDetailTabKey(index);
        if (string.Equals(detailTab, DetailTabRuns, StringComparison.Ordinal) ||
            string.Equals(detailTab, DetailTabAnalytics, StringComparison.Ordinal))
        {
            await LoadRuntimePaneDataAsync();
        }
        else if (string.Equals(detailTab, DetailTabManagerChat, StringComparison.Ordinal))
        {
            ClearRunDetails();
            await LoadManagerChatAsync();
        }
        else
        {
            ClearRunDetails();
        }

        RefreshCanvasSurface();
        UpdateRuntimeRefreshLoop();
    }
}
