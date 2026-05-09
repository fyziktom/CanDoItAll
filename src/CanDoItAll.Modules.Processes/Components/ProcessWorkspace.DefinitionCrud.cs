using System.Text.Json;

namespace CanDoItAll.Modules.Processes;

public partial class ProcessWorkspace
{
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
        editor.Roles.Add(new ProcessRoleEditorModel
        {
            Id = Guid.NewGuid(),
            DisplayName = $"Role {editor.Roles.Count + 1}",
            DefaultAllocationPercent = 100
        });
        RefreshCanvasSurface();
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
