namespace CanDoItAll.Modules.Processes;

public partial class ProcessWorkspace
{
    private enum DefinitionCanvasPersistenceQuiescenceMode
    {
        FlushPendingChanges,
        CancelPendingChanges
    }

    private async Task PersistDefinitionCanvasChangesAsync(
        string? successMessage = null,
        bool refreshSurface = true,
        bool cancelPendingPersistence = true)
    {
        if (cancelPendingPersistence)
        {
            CancelPendingDefinitionCanvasPersistence();
            await definitionCanvasPersistDrainTask;
        }

        await definitionCanvasPersistGate.WaitAsync();
        try
        {
            if (refreshSurface)
            {
                RefreshCanvasSurface();
            }

            if (!selectedProcessId.HasValue)
            {
                if (!string.IsNullOrWhiteSpace(successMessage))
                {
                    SetMessage(successMessage);
                }

                return;
            }

            NormalizeEditorForAuthoring();
            var result = await ProcessesService.SaveAsync(editor);
            if (result.IsFailure)
            {
                SetError(result.Errors);
                return;
            }

            selectedProcessId = result.Value;
            editor = await ProcessesService.GetEditorAsync(selectedProcessId, ProjectId);
            definitions = await ProcessesService.ListDefinitionsAsync(ProjectId);
            RefreshCanvasSurface();
            if (!string.IsNullOrWhiteSpace(successMessage))
            {
                SetMessage(successMessage);
            }
        }
        finally
        {
            definitionCanvasPersistGate.Release();
        }
    }

    private void ScheduleDefinitionCanvasPersistence()
    {
        if (!selectedProcessId.HasValue)
        {
            CancelPendingDefinitionCanvasPersistence();
            return;
        }

        CancelPendingDefinitionCanvasPersistence();
        var persistCts = new CancellationTokenSource();
        pendingDefinitionCanvasPersistCts = persistCts;
        pendingDefinitionCanvasPersistTask = PersistDefinitionCanvasChangesWhenIdleAsync(persistCts);
        definitionCanvasPersistDrainTask = TrackDefinitionCanvasPersistenceAsync(definitionCanvasPersistDrainTask, pendingDefinitionCanvasPersistTask);
    }

    private async Task PersistDefinitionCanvasChangesWhenIdleAsync(CancellationTokenSource persistCts)
    {
        try
        {
            await Task.Delay(DefinitionCanvasPersistDelayMs, persistCts.Token);
            if (persistCts.IsCancellationRequested || !selectedProcessId.HasValue)
            {
                return;
            }

            await PersistDefinitionCanvasChangesAsync(refreshSurface: false, cancelPendingPersistence: false);
            await InvokeAsync(StateHasChanged);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            await InvokeAsync(() => SetError($"Process canvas changes could not be saved: {ex.Message}"));
        }
        finally
        {
            if (ReferenceEquals(pendingDefinitionCanvasPersistCts, persistCts))
            {
                pendingDefinitionCanvasPersistCts = null;
            }

            persistCts.Dispose();
        }
    }

    private async Task FlushPendingDefinitionCanvasPersistenceAsync()
        => await QuiesceDefinitionCanvasPersistenceAsync(DefinitionCanvasPersistenceQuiescenceMode.FlushPendingChanges);

    private async Task QuiesceDefinitionCanvasPersistenceAsync(DefinitionCanvasPersistenceQuiescenceMode mode)
    {
        CancelPendingDefinitionCanvasPersistence();
        await definitionCanvasPersistDrainTask;

        if (mode == DefinitionCanvasPersistenceQuiescenceMode.FlushPendingChanges && selectedProcessId.HasValue)
        {
            await PersistDefinitionCanvasChangesAsync(refreshSurface: false, cancelPendingPersistence: false);
        }

        await WaitForDefinitionCanvasPersistenceIdleAsync();
    }

    private async Task WaitForDefinitionCanvasPersistenceIdleAsync()
    {
        await definitionCanvasPersistGate.WaitAsync();
        definitionCanvasPersistGate.Release();
    }

    private void CancelPendingDefinitionCanvasPersistence()
    {
        var persistCts = pendingDefinitionCanvasPersistCts;
        pendingDefinitionCanvasPersistCts = null;
        pendingDefinitionCanvasPersistTask = Task.CompletedTask;
        if (persistCts is null)
        {
            return;
        }

        persistCts.Cancel();
        persistCts.Dispose();
    }

    private static async Task TrackDefinitionCanvasPersistenceAsync(Task priorTask, Task currentTask)
    {
        try
        {
            await priorTask;
        }
        catch
        {
        }

        try
        {
            await currentTask;
        }
        catch
        {
        }
    }

    private static bool TryResolveDefinitionArtifactByOutputPortId(
        ProcessStepEditorModel step,
        string? portId,
        out ProcessArtifactExpectationEditorModel artifact)
    {
        ArgumentNullException.ThrowIfNull(step);

        artifact = default!;
        if (string.IsNullOrWhiteSpace(portId))
        {
            return false;
        }

        var match = step.ArtifactExpectations.FirstOrDefault(candidate =>
            string.Equals(ProcessCanvasCatalog.DefinitionPorts.BuildStepArtifactOutputPortId(candidate), portId, StringComparison.Ordinal));
        if (match is null)
        {
            return false;
        }

        artifact = match;
        return true;
    }

    private static Guid? ResolveArtifactDependencyBranchOutcomeId(ProcessStepEditorModel sourceStep, ProcessStepEditorModel targetStep)
    {
        var existingDependency = ProcessCanvasBranching.GetOrderedDependencies(targetStep)
            .FirstOrDefault(dependency => dependency.DependsOnStepId == sourceStep.Id);
        if (existingDependency?.DependsOnBranchOutcomeId.HasValue == true)
        {
            return existingDependency.DependsOnBranchOutcomeId;
        }

        return ProcessCanvasBranching.ShouldRenderBranchRouter(sourceStep)
            ? ProcessCanvasBranching.GetDefaultOutcomeId(sourceStep)
            : null;
    }

    private static bool HasArtifactInputsFromSourceStep(ProcessStepEditorModel targetStep, ProcessStepEditorModel sourceStep)
    {
        var sourceArtifactIds = sourceStep.ArtifactExpectations
            .Where(artifact => artifact.Id.HasValue)
            .Select(artifact => artifact.Id!.Value)
            .ToHashSet();
        if (sourceArtifactIds.Count == 0)
        {
            return false;
        }

        return targetStep.ArtifactInputs.Any(input =>
            input.ArtifactExpectationId.HasValue &&
            sourceArtifactIds.Contains(input.ArtifactExpectationId.Value));
    }

    private ProcessStepEditorModel? ResolveDefinitionStep(string? nodeId)
    {
        if (!ProcessCanvasBranching.TryResolveDefinitionStepToken(nodeId, out var rawId))
        {
            return null;
        }

        if (Guid.TryParse(rawId, out var stepId))
        {
            return editor.Steps.FirstOrDefault(step => step.Id == stepId);
        }

        return editor.Steps.FirstOrDefault(step =>
            string.Equals(step.Key, rawId, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(step.Title.Replace(' ', '-'), rawId, StringComparison.OrdinalIgnoreCase));
    }

    private ProcessRoleEditorModel? ResolveDefinitionRole(string? nodeId)
    {
        if (!ProcessCanvasBranching.TryResolveDefinitionRoleToken(nodeId, out var rawId))
        {
            return null;
        }

        if (Guid.TryParse(rawId, out var roleId))
        {
            return editor.Roles.FirstOrDefault(role => role.Id == roleId);
        }

        return editor.Roles.FirstOrDefault(role =>
            string.Equals(role.Key, rawId, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(role.DisplayName.Replace(' ', '-'), rawId, StringComparison.OrdinalIgnoreCase));
    }

    private ProcessStepEditorModel? ResolveDefinitionStep(Guid? stepId)
        => stepId.HasValue
            ? editor.Steps.FirstOrDefault(step => step.Id == stepId.Value)
            : null;

    private static bool IsDefinitionStepNodeId(string? nodeId)
        => !string.IsNullOrWhiteSpace(nodeId) &&
           nodeId.StartsWith(ProcessCanvasCatalog.NodePrefixes.DefinitionStep, StringComparison.Ordinal);

    private static bool IsDefinitionBranchNodeId(string? nodeId)
        => !string.IsNullOrWhiteSpace(nodeId) &&
           nodeId.StartsWith(ProcessCanvasCatalog.NodePrefixes.DefinitionBranchRouter, StringComparison.Ordinal);

    private static bool IsDefinitionRoleNodeId(string? nodeId)
        => !string.IsNullOrWhiteSpace(nodeId) &&
           nodeId.StartsWith(ProcessCanvasCatalog.NodePrefixes.DefinitionRole, StringComparison.Ordinal);

    private static bool IsStandardInputPortId(string? portId)
        => ProcessCanvasCatalog.DefinitionPorts.IsStepStructuralInputPortId(portId);

    private static bool IsStandardOutputPortId(string? portId)
        => ProcessCanvasCatalog.DefinitionPorts.IsStepStructuralOutputPortId(portId);

    private static bool TryResolveDefinitionBranchOutcomeByPortId(
        ProcessStepEditorModel step,
        string? portId,
        out ProcessStepBranchOutcomeEditorModel branchOutcome)
    {
        ArgumentNullException.ThrowIfNull(step);

        branchOutcome = default!;
        if (string.IsNullOrWhiteSpace(portId))
        {
            return false;
        }

        var match = step.BranchOutcomes.FirstOrDefault(candidate =>
            string.Equals(ProcessCanvasBranching.BuildOutcomePortId(candidate), portId, StringComparison.Ordinal));
        if (match is null)
        {
            return false;
        }

        branchOutcome = match;
        return true;
    }

    private ProcessStepRunViewModel? ResolveRuntimeStep(string? nodeId)
    {
        if (!ProcessCanvasBranching.TryResolveRuntimeStepId(nodeId, out var stepRunId))
        {
            return null;
        }

        return stepRuns.FirstOrDefault(stepRun => stepRun.Id == stepRunId);
    }

    private static string BuildDefinitionNodeId(ProcessStepEditorModel step)
        => ProcessCanvasBranching.BuildDefinitionStepNodeId(step);

    private static string BuildRunNodeId(Guid stepRunId)
        => ProcessCanvasBranching.BuildRuntimeStepNodeId(stepRunId);

    private static string ResolveRoleLabel(ProcessRoleEditorModel role)
        => string.IsNullOrWhiteSpace(role.DisplayName)
            ? "Role"
            : role.DisplayName;

    private static string ResolveStepLabel(ProcessStepEditorModel step)
        => string.IsNullOrWhiteSpace(step.Title)
            ? "Step"
            : step.Title;

    private static string ResolveBranchOutcomeLabel(ProcessStepBranchOutcomeEditorModel branchOutcome)
        => string.IsNullOrWhiteSpace(branchOutcome.Title)
            ? "Untitled branch"
            : branchOutcome.Title;

    private static void AddStepDependency(ProcessStepEditorModel step, Guid sourceStepId, Guid? branchOutcomeId)
    {
        var dependencies = ProcessCanvasBranching.GetOrderedDependencies(step)
            .Where(dependency => dependency.DependsOnStepId != sourceStepId || dependency.DependsOnBranchOutcomeId != branchOutcomeId)
            .Append(new ProcessStepDependencyEditorModel
            {
                Id = Guid.NewGuid(),
                DependsOnStepId = sourceStepId,
                DependsOnBranchOutcomeId = branchOutcomeId
            });
        SetStepDependencies(step, dependencies);
    }

    private static bool TryRemoveStepDependency(ProcessStepEditorModel step, Guid sourceStepId, Guid? branchOutcomeId)
    {
        var dependencies = ProcessCanvasBranching.GetOrderedDependencies(step).ToList();
        var removed = dependencies.RemoveAll(dependency =>
            dependency.DependsOnStepId == sourceStepId &&
            dependency.DependsOnBranchOutcomeId == branchOutcomeId);
        if (removed == 0)
        {
            return false;
        }

        SetStepDependencies(step, dependencies);
        return true;
    }

    private static ProcessRoleEditorModel CloneRole(ProcessRoleEditorModel source)
    {
        return new ProcessRoleEditorModel
        {
            Id = source.Id,
            Key = source.Key,
            DisplayName = source.DisplayName,
            Purpose = source.Purpose,
            StaffingIntent = source.StaffingIntent,
            PreferredExecutorKind = source.PreferredExecutorKind,
            PreferredProjectAssignmentRole = source.PreferredProjectAssignmentRole,
            IsRequired = source.IsRequired,
            AllowsFallback = source.AllowsFallback,
            RequiresExplicitApproval = source.RequiresExplicitApproval,
            DefaultAllocationPercent = source.DefaultAllocationPercent,
            RoleTemplateSourceKey = source.RoleTemplateSourceKey,
            RoleTemplateSnapshotName = source.RoleTemplateSnapshotName,
            SnapshotSummary = source.SnapshotSummary,
            RequiredSkillIds = source.RequiredSkillIds.ToList(),
            CanvasX = source.CanvasX,
            CanvasY = source.CanvasY
        };
    }

    private static void CopyRole(ProcessRoleEditorModel source, ProcessRoleEditorModel target)
    {
        target.Id = source.Id;
        target.Key = source.Key;
        target.DisplayName = source.DisplayName;
        target.Purpose = source.Purpose;
        target.StaffingIntent = source.StaffingIntent;
        target.PreferredExecutorKind = source.PreferredExecutorKind;
        target.PreferredProjectAssignmentRole = source.PreferredProjectAssignmentRole;
        target.IsRequired = source.IsRequired;
        target.AllowsFallback = source.AllowsFallback;
        target.RequiresExplicitApproval = source.RequiresExplicitApproval;
        target.DefaultAllocationPercent = source.DefaultAllocationPercent;
        target.RoleTemplateSourceKey = source.RoleTemplateSourceKey;
        target.RoleTemplateSnapshotName = source.RoleTemplateSnapshotName;
        target.SnapshotSummary = source.SnapshotSummary;
        target.RequiredSkillIds = source.RequiredSkillIds.ToList();
        target.CanvasX = source.CanvasX;
        target.CanvasY = source.CanvasY;
    }

    private static ProcessStepEditorModel CloneStep(ProcessStepEditorModel source)
    {
        var clone = new ProcessStepEditorModel
        {
            Id = source.Id,
            Key = source.Key,
            Title = source.Title,
            Subtitle = source.Subtitle,
            Notes = source.Notes,
            StepKind = source.StepKind,
            AllowsManualSkip = source.AllowsManualSkip,
            AllowsSafeRefusal = source.AllowsSafeRefusal,
            RequiresApproval = source.RequiresApproval,
            RequiresDecisionRecord = source.RequiresDecisionRecord,
            InputContractSummary = source.InputContractSummary,
            OutputContractSummary = source.OutputContractSummary,
            EvidenceContractSummary = source.EvidenceContractSummary,
            DecisionRightsSummary = source.DecisionRightsSummary,
            ExceptionPolicySummary = source.ExceptionPolicySummary,
            TargetLeadHours = source.TargetLeadHours,
            DecisionRoleRequirementId = source.DecisionRoleRequirementId,
            CanvasX = source.CanvasX,
            CanvasY = source.CanvasY,
            BranchCanvasX = source.BranchCanvasX,
            BranchCanvasY = source.BranchCanvasY,
            BranchOutcomes = source.BranchOutcomes
                .Select(outcome => new ProcessStepBranchOutcomeEditorModel
                {
                    Id = outcome.Id,
                    Key = outcome.Key,
                    Title = outcome.Title,
                    Description = outcome.Description
                })
                .ToList(),
            RoleAssignments = source.RoleAssignments
                .Select(assignment => new ProcessStepRoleRequirementEditorModel
                {
                    Id = assignment.Id,
                    RoleRequirementId = assignment.RoleRequirementId,
                    ResponsibilityKind = assignment.ResponsibilityKind,
                    IsRequired = assignment.IsRequired,
                    FallbackOrder = assignment.FallbackOrder,
                    RebindPolicySummary = assignment.RebindPolicySummary
                })
                .ToList(),
            ArtifactExpectations = source.ArtifactExpectations
                .Select(artifact => new ProcessArtifactExpectationEditorModel
                {
                    Id = artifact.Id,
                    ArtifactKind = artifact.ArtifactKind,
                    Title = artifact.Title,
                    IsRequired = artifact.IsRequired,
                    TrustRequirement = artifact.TrustRequirement,
                    SensitivityLevel = artifact.SensitivityLevel,
                    RetentionDays = artifact.RetentionDays,
                    AllowedFutureUsageSummary = artifact.AllowedFutureUsageSummary,
                    ValidationRequirementSummary = artifact.ValidationRequirementSummary
                })
                .ToList(),
            ArtifactInputs = source.ArtifactInputs
                .Select(artifactInput => new ProcessStepArtifactInputEditorModel
                {
                    Id = artifactInput.Id,
                    ArtifactExpectationId = artifactInput.ArtifactExpectationId
                })
                .ToList()
        };

        ProcessStepDependencyCollection.SetEditorDependencies(
            clone,
            ProcessCanvasBranching.GetOrderedDependencies(source)
                .Select(dependency => new ProcessStepDependencyEditorModel
                {
                    Id = dependency.Id,
                    DependsOnStepId = dependency.DependsOnStepId,
                    DependsOnBranchOutcomeId = dependency.DependsOnBranchOutcomeId
                }));

        return clone;
    }

    private static void CopyStep(ProcessStepEditorModel source, ProcessStepEditorModel target)
    {
        var clone = CloneStep(source);

        target.Id = clone.Id;
        target.Key = clone.Key;
        target.Title = clone.Title;
        target.Subtitle = clone.Subtitle;
        target.Notes = clone.Notes;
        target.StepKind = clone.StepKind;
        target.AllowsManualSkip = clone.AllowsManualSkip;
        target.AllowsSafeRefusal = clone.AllowsSafeRefusal;
        target.RequiresApproval = clone.RequiresApproval;
        target.RequiresDecisionRecord = clone.RequiresDecisionRecord;
        target.InputContractSummary = clone.InputContractSummary;
        target.OutputContractSummary = clone.OutputContractSummary;
        target.EvidenceContractSummary = clone.EvidenceContractSummary;
        target.DecisionRightsSummary = clone.DecisionRightsSummary;
        target.ExceptionPolicySummary = clone.ExceptionPolicySummary;
        target.TargetLeadHours = clone.TargetLeadHours;
        target.DecisionRoleRequirementId = clone.DecisionRoleRequirementId;
        target.CanvasX = clone.CanvasX;
        target.CanvasY = clone.CanvasY;
        target.BranchCanvasX = clone.BranchCanvasX;
        target.BranchCanvasY = clone.BranchCanvasY;
        ProcessStepDependencyCollection.SetEditorDependencies(target, clone.Dependencies);
        target.BranchOutcomes = clone.BranchOutcomes;
        target.RoleAssignments = clone.RoleAssignments;
        target.ArtifactExpectations = clone.ArtifactExpectations;
        target.ArtifactInputs = clone.ArtifactInputs;
    }

    private sealed record ProcessCanvasNodeActionDialogState(
        string Title,
        string Summary,
        bool IsRuntime);
}
