using CanDoItAll.Components.CanvasLib;

namespace CanDoItAll.Modules.Processes;

public partial class ProcessWorkspace
{
    private async Task DeleteDefinitionCanvasNodeAsync(string? nodeId)
    {
        if (!string.IsNullOrWhiteSpace(nodeId) &&
            artifactCloneDrafts.Remove(nodeId))
        {
            var uiState = CloneCanvasUiState(ResolveStoredCanvasUiState());
            uiState.ManualPositions.Remove(nodeId);
            uiState.SelectedNodeIds = uiState.SelectedNodeIds
                .Where(selectedNodeId => !string.Equals(selectedNodeId, nodeId, StringComparison.Ordinal))
                .ToList();
            StoreCanvasUiState(uiState);
            SelectFallbackDefinitionNode();
            RefreshCanvasSurface();
            SetMessage("Artifact clone draft was removed from the canvas.");
            await InvokeAsync(StateHasChanged);
            return;
        }

        if (ResolveDefinitionRole(nodeId) is { } role)
        {
            var roleLabel = ResolveRoleLabel(role);
            RemoveRole(role, refreshSurface: false);
            SelectFallbackDefinitionNode();
            RefreshCanvasSurface();
            await PersistDefinitionCanvasChangesAsync($"{roleLabel} was removed from the process definition.", refreshSurface: false);
            return;
        }

        if (ResolveDefinitionStep(nodeId) is { } step)
        {
            var stepLabel = ResolveStepLabel(step);
            RemoveStep(step, refreshSurface: false);
            SelectFallbackDefinitionNode();
            RefreshCanvasSurface();
            await PersistDefinitionCanvasChangesAsync($"{stepLabel} was removed from the process definition.", refreshSurface: false);
            return;
        }

        SetError("The selected canvas node could not be resolved.");
    }

    private async Task DeleteDefinitionCanvasLinkAsync(CanvasWorkbenchContextActionRequest request)
    {
        if (string.Equals(request.LinkTargetPortId, ProcessCanvasCatalog.DefinitionPorts.BranchStepInput, StringComparison.Ordinal))
        {
            SetError("The step-to-branch structural connection is managed by the canvas and cannot be removed manually.");
            return;
        }

        if (TryResolveProtectedDependencyDeletionMessage(request, out var protectedDeletionMessage))
        {
            SetError(protectedDeletionMessage);
            return;
        }

        if (TryDeleteDecisionAuthorityLink(request, out var message) ||
            TryDeleteMessagingPolicyLink(request, out message) ||
            TryDeleteRoleParticipationLink(request, out message) ||
            TryDeleteArtifactInputLink(request, out message) ||
            TryDeleteStepDependencyLink(request, out message))
        {
            RefreshCanvasSurface();
            await PersistDefinitionCanvasChangesAsync(message, refreshSurface: false);
            return;
        }

        SetError("The selected canvas connection could not be removed.");
    }

    private async Task CreateDefinitionCanvasConnectionAsync(CanvasWorkbenchContextActionRequest request)
    {
        var sourceStep = ResolveDefinitionStep(request.LinkSourceId);
        var targetStep = ResolveDefinitionStep(request.LinkTargetId);
        var sourceRole = ResolveDefinitionRole(request.LinkSourceId);
        var targetRole = ResolveDefinitionRole(request.LinkTargetId);
        if (sourceStep?.Id.HasValue == true &&
            targetStep?.Id.HasValue == true &&
            sourceStep.Id == targetStep.Id)
        {
            SetError("A process step cannot connect to itself.");
            return;
        }

        if (sourceRole?.Id.HasValue == true &&
            targetRole?.Id.HasValue == true &&
            sourceRole.Id == targetRole.Id &&
            ProcessCanvasCatalog.DefinitionPorts.IsRoleMessagingOutputPortId(request.LinkSourcePortId) &&
            ProcessCanvasCatalog.DefinitionPorts.IsRoleMessagingInputPortId(request.LinkTargetPortId))
        {
            SetError("A process role cannot create a Messaging link to itself.");
            return;
        }

        if (string.Equals(request.LinkTargetPortId, ProcessCanvasCatalog.DefinitionPorts.BranchStepInput, StringComparison.Ordinal))
        {
            SetError("The step-to-branch structural connection is created automatically. Connect downstream work from branch outputs instead.");
            return;
        }

        if (IsDefinitionStepNodeId(request.LinkSourceId) &&
            IsDefinitionStepNodeId(request.LinkTargetId) &&
            IsStandardOutputPortId(request.LinkSourcePortId) &&
            IsStandardInputPortId(request.LinkTargetPortId) &&
            sourceStep is not null &&
            ProcessCanvasBranching.ShouldRenderBranchRouter(sourceStep))
        {
            SetError("This step already routes through a branch node. Connect downstream work from the branch outputs instead of the step body.");
            return;
        }

        if (TryAssignDecisionAuthorityConnection(request, out var message) ||
            TryCreateMessagingPolicyConnection(request, out message) ||
            TryCreateRoleParticipationConnection(request, out message) ||
            TryCreateArtifactInputConnection(request, out message) ||
            TryCreateRoutedStepDependencyConnection(request, out message) ||
            TryCreateDirectStepDependencyConnection(request, out message))
        {
            RefreshCanvasSurface();
            await PersistDefinitionCanvasChangesAsync(message, refreshSurface: false);
            return;
        }

        SetError("That connection is not valid for the selected process nodes.");
    }

    private double ResolveCanvasStepEditorX(ProcessStepEditorModel? sourceStep, Guid? branchOutcomeId, double requestedX)
    {
        if (requestedX > 0)
        {
            return requestedX;
        }

        if (sourceStep is null)
        {
            return 140 + (editor.Steps.Count * 280);
        }

        var sourceIndex = editor.Steps.IndexOf(sourceStep);
        var sourceX = sourceStep.CanvasX != 0
            ? sourceStep.CanvasX
            : 140 + (Math.Max(0, sourceIndex) * 280);
        return branchOutcomeId.HasValue || ProcessCanvasBranching.ShouldRenderBranchRouter(sourceStep)
            ? sourceX + 640d
            : sourceX + 300d;
    }

    private static double ResolveCanvasStepEditorY(ProcessStepEditorModel? sourceStep, Guid? branchOutcomeId, double requestedY)
    {
        if (requestedY > 0)
        {
            return requestedY;
        }

        if (sourceStep is null)
        {
            return 180;
        }

        if (!branchOutcomeId.HasValue || sourceStep.BranchOutcomes.Count == 0)
        {
            return sourceStep.CanvasY;
        }

        var branchIndex = sourceStep.BranchOutcomes.FindIndex(outcome => outcome.Id == branchOutcomeId.Value);
        if (branchIndex < 0)
        {
            return sourceStep.CanvasY;
        }

        var midpoint = (sourceStep.BranchOutcomes.Count - 1) / 2d;
        return sourceStep.CanvasY + ((branchIndex - midpoint) * 220d);
    }

    private bool TryDeleteDecisionAuthorityLink(CanvasWorkbenchContextActionRequest request, out string message)
    {
        message = string.Empty;
        var isBranchDecisionTarget = string.Equals(request.LinkTargetPortId, ProcessCanvasCatalog.DefinitionPorts.BranchDecisionRoleInput, StringComparison.Ordinal);
        var isStepDecisionTarget = string.Equals(request.LinkTargetPortId, ProcessCanvasCatalog.DefinitionPorts.StepDecisionAuthorityInput, StringComparison.Ordinal);
        if (!isBranchDecisionTarget && !isStepDecisionTarget)
        {
            return false;
        }

        if (ResolveDefinitionRole(request.LinkSourceId) is not { Id: { } roleId } role ||
            ResolveDefinitionStep(request.LinkTargetId) is not { } step ||
            step.DecisionRoleRequirementId != roleId)
        {
            return false;
        }

        step.DecisionRoleRequirementId = null;
        message = $"{ResolveRoleLabel(role)} no longer provides decision authority for {ResolveStepLabel(step)}.";
        return true;
    }

    private bool TryDeleteStepDependencyLink(CanvasWorkbenchContextActionRequest request, out string message)
    {
        message = string.Empty;
        if (ResolveDefinitionStep(request.LinkSourceId) is not { Id: { } sourceStepId } sourceStep ||
            ResolveDefinitionStep(request.LinkTargetId) is not { } targetStep)
        {
            return false;
        }

        if (IsDefinitionBranchNodeId(request.LinkSourceId))
        {
            if (!TryResolveDefinitionBranchOutcomeByPortId(sourceStep, request.LinkSourcePortId, out var branchOutcome) ||
                !TryRemoveStepDependency(targetStep, sourceStepId, branchOutcome.Id))
            {
                return false;
            }

            message = $"{ResolveStepLabel(targetStep)} no longer waits on the '{ResolveBranchOutcomeLabel(branchOutcome)}' branch from {ResolveStepLabel(sourceStep)}.";
            return true;
        }

        if (!IsDefinitionStepNodeId(request.LinkSourceId) ||
            !TryRemoveStepDependency(targetStep, sourceStepId, null))
        {
            return false;
        }

        message = $"{ResolveStepLabel(targetStep)} no longer depends on {ResolveStepLabel(sourceStep)}.";
        return true;
    }

    private bool TryDeleteRoleParticipationLink(CanvasWorkbenchContextActionRequest request, out string message)
    {
        message = string.Empty;
        if (!IsDefinitionRoleNodeId(request.LinkSourceId) ||
            !IsDefinitionStepNodeId(request.LinkTargetId) ||
            !ProcessCanvasCatalog.DefinitionPorts.TryGetRoleResponsibilityKind(request.LinkSourcePortId, out var sourceResponsibilityKind) ||
            !ProcessCanvasCatalog.DefinitionPorts.TryGetStepResponsibilityKind(request.LinkTargetPortId, out var targetResponsibilityKind) ||
            sourceResponsibilityKind != targetResponsibilityKind)
        {
            return false;
        }

        if (ResolveDefinitionRole(request.LinkSourceId) is not { Id: { } roleId } role ||
            ResolveDefinitionStep(request.LinkTargetId) is not { } step)
        {
            return false;
        }

        var removed = step.RoleAssignments.RemoveAll(assignment =>
            assignment.RoleRequirementId == roleId &&
            assignment.ResponsibilityKind == sourceResponsibilityKind);
        if (removed == 0)
        {
            return false;
        }

        message = $"{ResolveRoleLabel(role)} no longer participates as {ProcessCanvasCatalog.DefinitionPorts.GetResponsibilityLabel(sourceResponsibilityKind).ToLowerInvariant()} on {ResolveStepLabel(step)}.";
        return true;
    }

    private bool TryDeleteMessagingPolicyLink(CanvasWorkbenchContextActionRequest request, out string message)
    {
        message = string.Empty;
        if (!IsDefinitionRoleNodeId(request.LinkSourceId) ||
            !IsDefinitionRoleNodeId(request.LinkTargetId) ||
            !ProcessCanvasCatalog.DefinitionPorts.IsRoleMessagingOutputPortId(request.LinkSourcePortId) ||
            !ProcessCanvasCatalog.DefinitionPorts.IsRoleMessagingInputPortId(request.LinkTargetPortId))
        {
            return false;
        }

        if (ResolveDefinitionRole(request.LinkSourceId) is not { Id: { } sourceRoleId } sourceRole ||
            ResolveDefinitionRole(request.LinkTargetId) is not { Id: { } targetRoleId } targetRole)
        {
            return false;
        }

        var removed = editor.MessagingPolicies.RemoveAll(item =>
            item.SourceRoleRequirementId == sourceRoleId &&
            item.TargetRoleRequirementId == targetRoleId);
        if (removed == 0)
        {
            return false;
        }

        message = $"{ResolveRoleLabel(sourceRole)} can no longer send direct messages to {ResolveRoleLabel(targetRole)}.";
        return true;
    }

    private bool TryDeleteArtifactInputLink(CanvasWorkbenchContextActionRequest request, out string message)
    {
        message = string.Empty;
        if (!IsDefinitionStepNodeId(request.LinkTargetId) ||
            !string.Equals(request.LinkTargetPortId, ProcessCanvasCatalog.DefinitionPorts.StepArtifactInputs, StringComparison.Ordinal))
        {
            return false;
        }

        if (ResolveDefinitionStep(request.LinkTargetId) is not { } targetStep ||
            !TryResolveDefinitionArtifactConnectionSource(request.LinkSourceId, request.LinkSourcePortId, out var sourceStep, out var artifact))
        {
            return false;
        }

        var removed = targetStep.ArtifactInputs.RemoveAll(input => input.ArtifactExpectationId == artifact.Id);
        if (removed == 0)
        {
            return false;
        }

        message = $"{ResolveStepLabel(targetStep)} no longer consumes the '{ResolveArtifactLabel(artifact)}' artifact from {ResolveStepLabel(sourceStep)}.";
        return true;
    }

    private bool TryAssignDecisionAuthorityConnection(CanvasWorkbenchContextActionRequest request, out string message)
    {
        message = string.Empty;
        if (!IsDefinitionRoleNodeId(request.LinkSourceId) ||
            !string.Equals(request.LinkSourcePortId, ProcessCanvasCatalog.DefinitionPorts.RoleDecisionAuthorityOutput, StringComparison.Ordinal))
        {
            return false;
        }

        var canAssignToBranchRouter = IsDefinitionBranchNodeId(request.LinkTargetId) &&
            string.Equals(request.LinkTargetPortId, ProcessCanvasCatalog.DefinitionPorts.BranchDecisionRoleInput, StringComparison.Ordinal);
        var canAssignToStep = IsDefinitionStepNodeId(request.LinkTargetId) &&
            string.Equals(request.LinkTargetPortId, ProcessCanvasCatalog.DefinitionPorts.StepDecisionAuthorityInput, StringComparison.Ordinal);
        if (!canAssignToBranchRouter && !canAssignToStep)
        {
            return false;
        }

        if (ResolveDefinitionRole(request.LinkSourceId) is not { Id: { } roleId } role ||
            ResolveDefinitionStep(request.LinkTargetId) is not { } step)
        {
            return false;
        }

        step.DecisionRoleRequirementId = roleId;
        message = $"{ResolveRoleLabel(role)} now provides decision authority for {ResolveStepLabel(step)}.";
        return true;
    }

    private bool TryCreateRoleParticipationConnection(CanvasWorkbenchContextActionRequest request, out string message)
    {
        message = string.Empty;
        if (!IsDefinitionRoleNodeId(request.LinkSourceId) ||
            !IsDefinitionStepNodeId(request.LinkTargetId) ||
            !ProcessCanvasCatalog.DefinitionPorts.TryGetRoleResponsibilityKind(request.LinkSourcePortId, out var sourceResponsibilityKind) ||
            !ProcessCanvasCatalog.DefinitionPorts.TryGetStepResponsibilityKind(request.LinkTargetPortId, out var targetResponsibilityKind) ||
            sourceResponsibilityKind != targetResponsibilityKind)
        {
            return false;
        }

        if (ResolveDefinitionRole(request.LinkSourceId) is not { Id: { } roleId } role ||
            ResolveDefinitionStep(request.LinkTargetId) is not { } step)
        {
            return false;
        }

        var existingAssignment = step.RoleAssignments.FirstOrDefault(assignment =>
            assignment.RoleRequirementId == roleId &&
            assignment.ResponsibilityKind == sourceResponsibilityKind);
        if (existingAssignment is null)
        {
            step.RoleAssignments.Add(new ProcessStepRoleRequirementEditorModel
            {
                Id = Guid.NewGuid(),
                RoleRequirementId = roleId,
                ResponsibilityKind = sourceResponsibilityKind,
                IsRequired = true
            });
            message = $"{ResolveRoleLabel(role)} now participates as {ProcessCanvasCatalog.DefinitionPorts.GetResponsibilityLabel(sourceResponsibilityKind).ToLowerInvariant()} on {ResolveStepLabel(step)}.";
            return true;
        }

        message = $"{ResolveRoleLabel(role)} already participates as {ProcessCanvasCatalog.DefinitionPorts.GetResponsibilityLabel(sourceResponsibilityKind).ToLowerInvariant()} on {ResolveStepLabel(step)}.";
        return true;
    }

    private bool TryCreateMessagingPolicyConnection(CanvasWorkbenchContextActionRequest request, out string message)
    {
        message = string.Empty;
        if (!IsDefinitionRoleNodeId(request.LinkSourceId) ||
            !IsDefinitionRoleNodeId(request.LinkTargetId) ||
            !ProcessCanvasCatalog.DefinitionPorts.IsRoleMessagingOutputPortId(request.LinkSourcePortId) ||
            !ProcessCanvasCatalog.DefinitionPorts.IsRoleMessagingInputPortId(request.LinkTargetPortId))
        {
            return false;
        }

        if (ResolveDefinitionRole(request.LinkSourceId) is not { Id: { } sourceRoleId } sourceRole ||
            ResolveDefinitionRole(request.LinkTargetId) is not { Id: { } targetRoleId } targetRole)
        {
            return false;
        }

        var existingPolicy = editor.MessagingPolicies.FirstOrDefault(item =>
            item.SourceRoleRequirementId == sourceRoleId &&
            item.TargetRoleRequirementId == targetRoleId);
        if (existingPolicy is not null)
        {
            message = $"{ResolveRoleLabel(sourceRole)} already can send direct messages to {ResolveRoleLabel(targetRole)}.";
            return true;
        }

        editor.MessagingPolicies.Add(new ProcessRoleMessagingPolicyEditorModel
        {
            Id = Guid.NewGuid(),
            SourceRoleRequirementId = sourceRoleId,
            TargetRoleRequirementId = targetRoleId
        });
        message = $"{ResolveRoleLabel(sourceRole)} can now send direct messages to {ResolveRoleLabel(targetRole)}.";
        return true;
    }

    private bool TryCreateArtifactInputConnection(CanvasWorkbenchContextActionRequest request, out string message)
    {
        message = string.Empty;
        if (!IsDefinitionStepNodeId(request.LinkTargetId) ||
            !string.Equals(request.LinkTargetPortId, ProcessCanvasCatalog.DefinitionPorts.StepArtifactInputs, StringComparison.Ordinal))
        {
            return false;
        }

        if (ResolveDefinitionStep(request.LinkTargetId) is not { } targetStep ||
            !TryResolveDefinitionArtifactConnectionSource(request.LinkSourceId, request.LinkSourcePortId, out var sourceStep, out var artifact) ||
            sourceStep.Id is not Guid sourceStepId)
        {
            return false;
        }

        var dependencyBranchOutcomeId = ResolveArtifactDependencyBranchOutcomeId(sourceStep, targetStep);
        AddStepDependency(targetStep, sourceStepId, dependencyBranchOutcomeId);
        var artifactInput = targetStep.ArtifactInputs.FirstOrDefault(input => input.ArtifactExpectationId == artifact.Id);
        if (artifactInput is null)
        {
            artifactInput = new ProcessStepArtifactInputEditorModel
            {
                Id = Guid.NewGuid(),
                ArtifactExpectationId = artifact.Id
            };
            targetStep.ArtifactInputs.Add(artifactInput);
            TryConvertDraftArtifactCloneToInputClone(request.LinkSourceId, artifact, artifactInput, targetStep);
            message = $"{ResolveStepLabel(targetStep)} now consumes the '{ResolveArtifactLabel(artifact)}' artifact from {ResolveStepLabel(sourceStep)}.";
            return true;
        }

        TryConvertDraftArtifactCloneToInputClone(request.LinkSourceId, artifact, artifactInput, targetStep);
        message = $"{ResolveStepLabel(targetStep)} already consumes the '{ResolveArtifactLabel(artifact)}' artifact from {ResolveStepLabel(sourceStep)}.";
        return true;
    }

    private bool TryResolveDefinitionArtifactConnectionSource(
        string? sourceNodeId,
        string? sourcePortId,
        out ProcessStepEditorModel sourceStep,
        out ProcessArtifactExpectationEditorModel artifact)
    {
        sourceStep = default!;
        artifact = default!;
        if (IsDefinitionStepNodeId(sourceNodeId))
        {
            if (ResolveDefinitionStep(sourceNodeId) is not { } resolvedSourceStep ||
                !TryResolveDefinitionArtifactByOutputPortId(resolvedSourceStep, sourcePortId, out var resolvedArtifact) ||
                !resolvedArtifact.Id.HasValue)
            {
                return false;
            }

            sourceStep = resolvedSourceStep;
            artifact = resolvedArtifact;
            return true;
        }

        if (!string.Equals(sourcePortId, ProcessCanvasCatalog.DefinitionPorts.ArtifactUsageOutput, StringComparison.Ordinal) ||
            (!ProcessCanvasBranching.IsDefinitionArtifactNodeId(sourceNodeId) &&
                !ProcessCanvasBranching.IsDefinitionArtifactCloneNodeId(sourceNodeId)))
        {
            return false;
        }

        if (!TryResolveDefinitionArtifactWithOwner(sourceNodeId, out var artifactFromNode, out var ownerStep) ||
            !artifactFromNode.Id.HasValue)
        {
            return false;
        }

        sourceStep = ownerStep;
        artifact = artifactFromNode;
        return true;
    }

    private bool TryCreateRoutedStepDependencyConnection(CanvasWorkbenchContextActionRequest request, out string message)
    {
        message = string.Empty;
        if (!IsDefinitionBranchNodeId(request.LinkSourceId) ||
            !IsDefinitionStepNodeId(request.LinkTargetId) ||
            !IsStandardInputPortId(request.LinkTargetPortId))
        {
            return false;
        }

        if (ResolveDefinitionStep(request.LinkSourceId) is not { Id: { } sourceStepId } sourceStep ||
            ResolveDefinitionStep(request.LinkTargetId) is not { } targetStep ||
            !TryResolveDefinitionBranchOutcomeByPortId(sourceStep, request.LinkSourcePortId, out var branchOutcome))
        {
            return false;
        }

        AddStepDependency(targetStep, sourceStepId, branchOutcome.Id);
        message = $"{ResolveStepLabel(targetStep)} now follows the '{ResolveBranchOutcomeLabel(branchOutcome)}' branch from {ResolveStepLabel(sourceStep)}.";
        return true;
    }

    private bool TryCreateDirectStepDependencyConnection(CanvasWorkbenchContextActionRequest request, out string message)
    {
        message = string.Empty;
        if (!IsDefinitionStepNodeId(request.LinkSourceId) ||
            !IsDefinitionStepNodeId(request.LinkTargetId) ||
            !IsStandardOutputPortId(request.LinkSourcePortId) ||
            !IsStandardInputPortId(request.LinkTargetPortId))
        {
            return false;
        }

        if (ResolveDefinitionStep(request.LinkSourceId) is not { Id: { } sourceStepId } sourceStep ||
            ResolveDefinitionStep(request.LinkTargetId) is not { } targetStep)
        {
            return false;
        }

        AddStepDependency(targetStep, sourceStepId, null);
        message = $"{ResolveStepLabel(targetStep)} now depends on {ResolveStepLabel(sourceStep)}.";
        return true;
    }

    private bool TryResolveProtectedDependencyDeletionMessage(CanvasWorkbenchContextActionRequest request, out string message)
    {
        message = string.Empty;
        if (!ProcessCanvasCatalog.DefinitionPorts.IsStepStructuralInputPortId(request.LinkTargetPortId))
        {
            return false;
        }

        if (ResolveDefinitionStep(request.LinkSourceId) is not { } sourceStep ||
            ResolveDefinitionStep(request.LinkTargetId) is not { } targetStep)
        {
            return false;
        }

        if (!IsDefinitionBranchNodeId(request.LinkSourceId) &&
            !IsDefinitionStepNodeId(request.LinkSourceId))
        {
            return false;
        }

        if (!HasArtifactInputsFromSourceStep(targetStep, sourceStep))
        {
            return false;
        }

        message = $"{ResolveStepLabel(targetStep)} still consumes artifact inputs from {ResolveStepLabel(sourceStep)}. Remove the artifact link first.";
        return true;
    }
}
