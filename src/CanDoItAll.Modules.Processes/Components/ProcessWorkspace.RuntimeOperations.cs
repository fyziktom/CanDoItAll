namespace CanDoItAll.Modules.Processes;

public partial class ProcessWorkspace
{
    private async Task StartRunAsync()
    {
        if (!selectedProcessId.HasValue)
        {
            SetError("Select a process definition before starting a run.");
            return;
        }

        var result = await ProcessesService.StartRunAsync(
            new ProcessRunStartRequest
            {
                ProcessDefinitionId = selectedProcessId.Value,
                ProjectId = ProjectId,
                RunName = string.IsNullOrWhiteSpace(runNameDraft) ? string.Empty : runNameDraft,
                OperatingMode = runOperatingMode,
                TriggerReason = "Started from process workspace."
            });
        if (result.IsFailure)
        {
            SetError(result.Errors);
            return;
        }

        selectedRunId = result.Value;
        selectedCanvasNodeId = null;
        ResetRuntimeCanvasState();
        detailTab = "runs";
        runNameDraft = string.Empty;
        await LoadWorkspaceAsync();
        SetMessage("Process run started.");
    }

    private async Task SelectRunAsync(Guid runId)
    {
        selectedRunId = runId;
        selectedCanvasNodeId = null;
        ResetRuntimeCanvasState();
        await LoadRunDetailsAsync();
        RefreshCanvasSurface();
    }

    private async Task ApplyStepStatusAsync(Guid stepRunId, ProcessStepRunStatus targetStatus)
    {
        var currentStepRun = stepRuns.FirstOrDefault(item => item.Id == stepRunId);
        if (currentStepRun is null)
        {
            SetError("Reload the run before updating this step.");
            return;
        }

        var selectedBranchOutcomeId = targetStatus == ProcessStepRunStatus.Completed
            ? ResolveSelectedBranchOutcomeId(stepRunId)
            : null;
        var result = await ProcessesService.TransitionStepAsync(
            new ProcessStepTransitionRequest
            {
                StepRunId = stepRunId,
                StepRunConcurrencyToken = currentStepRun.StepRunConcurrencyToken,
                TargetStatus = targetStatus,
                Reason = BuildTransitionReason(targetStatus, stepRunId, selectedBranchOutcomeId),
                SelectedBranchOutcomeId = selectedBranchOutcomeId,
                DecidedBy = "process-workspace"
            });
        if (result.IsFailure)
        {
            SetError(result.Errors);
            return;
        }

        await LoadWorkspaceAsync();
        detailTab = "runs";
        SetMessage($"Step updated to {targetStatus}.");
    }

    private void SelectAssignment(Guid assignmentId)
    {
        selectedAssignmentId = assignmentId;
        ApplyAssignmentSelection();
    }

    private void ApplyAssignmentSelection()
    {
        var assignment = SelectedAssignment;
        if (assignment is null)
        {
            assignmentPartyId = null;
            assignmentDisplayName = string.Empty;
            assignmentExecutorKind = "person";
            assignmentBindingReason = string.Empty;
            assignmentIsFallback = false;
            assignmentAllowsDirectMessaging = true;
            SynchronizeDirectMessagingComposer();
            return;
        }

        assignmentPartyId = assignment.PartyId;
        assignmentDisplayName = assignment.DisplayName;
        assignmentExecutorKind = string.IsNullOrWhiteSpace(assignment.ExecutorKind) ? "person" : assignment.ExecutorKind;
        assignmentBindingReason = assignment.BindingReason;
        assignmentIsFallback = assignment.IsFallback;
        assignmentAllowsDirectMessaging = assignment.AllowsDirectMessaging;
        SynchronizeDirectMessagingComposer();
    }

    private async Task ResolveSelectedAssignmentAsync()
    {
        var assignment = SelectedAssignment;
        if (assignment is null || !selectedRunId.HasValue)
        {
            SetError("Select a run assignment before resolving it.");
            return;
        }

        var result = await ProcessesService.ResolveAssignmentAsync(
            new ProcessAssignmentResolutionRequest
            {
                ProcessRunId = selectedRunId.Value,
                RoleRequirementId = assignment.RoleRequirementId,
                StepDefinitionId = assignment.StepDefinitionId,
                PartyId = assignmentPartyId,
                DisplayName = assignmentDisplayName,
                ExecutorKind = assignmentExecutorKind,
                BindingReason = string.IsNullOrWhiteSpace(assignmentBindingReason)
                    ? "Resolved from the process workspace."
                    : assignmentBindingReason,
                IsFallback = assignmentIsFallback,
                AllowsDirectMessaging = assignmentAllowsDirectMessaging
            });
        if (result.IsFailure)
        {
            SetError(result.Errors);
            return;
        }

        await LoadWorkspaceAsync();
        detailTab = "runs";
        SetMessage("Run assignment resolved.");
    }

    private void SynchronizeDirectMessagingComposer()
    {
        var runScopedAssignments = DirectMessageAssignments;
        if (runScopedAssignments.Count == 0)
        {
            directMessageSourceRoleRequirementId = null;
            directMessageTargetRoleRequirementId = null;
            directMessageBody = string.Empty;
            return;
        }

        var availableRoleIds = runScopedAssignments
            .Select(item => item.RoleRequirementId)
            .Distinct()
            .ToList();
        var preferredSourceRoleId = SelectedAssignment is { StepDefinitionId: null }
            ? SelectedAssignment.RoleRequirementId
            : directMessageSourceRoleRequirementId;
        if (!preferredSourceRoleId.HasValue || !availableRoleIds.Contains(preferredSourceRoleId.Value))
        {
            preferredSourceRoleId = availableRoleIds[0];
        }

        directMessageSourceRoleRequirementId = preferredSourceRoleId;

        if (!preferredSourceRoleId.HasValue)
        {
            directMessageTargetRoleRequirementId = null;
            directMessageBody = string.Empty;
            return;
        }

        var availableTargetRoleIds = availableRoleIds
            .Where(item => item != preferredSourceRoleId.Value)
            .ToList();
        if (availableTargetRoleIds.Count == 0)
        {
            directMessageTargetRoleRequirementId = null;
            directMessageBody = string.Empty;
            return;
        }

        if (!directMessageTargetRoleRequirementId.HasValue ||
            directMessageTargetRoleRequirementId.Value == preferredSourceRoleId.Value ||
            !availableTargetRoleIds.Contains(directMessageTargetRoleRequirementId.Value))
        {
            directMessageTargetRoleRequirementId = availableTargetRoleIds[0];
        }
    }

    private async Task SendDirectMessageAsync()
    {
        if (!selectedRunId.HasValue)
        {
            SetError("Select a run before sending direct messages.");
            return;
        }

        if (!directMessageSourceRoleRequirementId.HasValue || !directMessageTargetRoleRequirementId.HasValue)
        {
            SetError("Select both source and target process roles before sending a direct message.");
            return;
        }

        if (string.IsNullOrWhiteSpace(directMessageBody))
        {
            SetError("Write a direct message before sending it.");
            return;
        }

        var result = await ProcessesService.SendDirectMessageAsync(
            new ProcessDirectMessageRequest
            {
                ProcessRunId = selectedRunId.Value,
                SourceRoleRequirementId = directMessageSourceRoleRequirementId.Value,
                TargetRoleRequirementId = directMessageTargetRoleRequirementId.Value,
                MessageBody = directMessageBody
            });
        if (result.IsFailure)
        {
            await LoadRunDetailsAsync();
            detailTab = "runs";
            SetError(result.Errors);
            return;
        }

        directMessageBody = string.Empty;
        await LoadRunDetailsAsync();
        detailTab = "runs";
        SetMessage("Direct message recorded.");
    }

    private async Task RecordArtifactAsync()
    {
        if (!selectedRunId.HasValue)
        {
            SetError("Start or select a run before recording artifacts.");
            return;
        }

        var result = await ProcessesService.RecordArtifactAsync(
            new ProcessArtifactRecordRequest
            {
                ProcessRunId = selectedRunId.Value,
                StepRunId = artifactStepRunId,
                ArtifactKind = artifactKind,
                Title = artifactTitle,
                TrustStatus = artifactTrustStatus,
                SensitivityLevel = artifactSensitivityLevel,
                ProvenanceSummary = artifactProvenance,
                AllowedFutureUsageSummary = artifactAllowedUsage,
                ReviewSummary = artifactReview
            });
        if (result.IsFailure)
        {
            SetError(result.Errors);
            return;
        }

        artifactTitle = string.Empty;
        artifactProvenance = string.Empty;
        artifactAllowedUsage = string.Empty;
        artifactReview = string.Empty;
        await LoadWorkspaceAsync();
        detailTab = "runs";
        SetMessage("Artifact recorded.");
    }

    private string BuildDefinitionSummary(ProcessDefinitionListItem definition)
    {
        var scope = string.IsNullOrWhiteSpace(definition.ProjectName) ? "Global" : definition.ProjectName;
        return $"{scope} / v{definition.LatestVersionNumber} / {definition.RoleCount} roles / {definition.StepCount} steps";
    }

    private static string BuildRunSummary(ProcessRunListItem run)
    {
        return $"{run.Status} / {run.CompletedStepCount} of {run.TotalStepCount} steps / {run.CapabilityGapCount} gaps";
    }

    private string BuildTransitionReason(ProcessStepRunStatus status, Guid stepRunId, Guid? selectedBranchOutcomeId)
    {
        var branchOutcomeTitle = selectedBranchOutcomeId.HasValue
            ? stepRuns
                .FirstOrDefault(item => item.Id == stepRunId)?
                .AvailableBranchOutcomes
                .FirstOrDefault(item => item.Id == selectedBranchOutcomeId.Value)?
                .Title
            : null;
        return status switch
        {
            ProcessStepRunStatus.InProgress => "Work started from the runtime workspace.",
            ProcessStepRunStatus.Completed when !string.IsNullOrWhiteSpace(branchOutcomeTitle) => $"Work completed from the runtime workspace with branch outcome '{branchOutcomeTitle}'.",
            ProcessStepRunStatus.Completed => "Work completed from the runtime workspace.",
            ProcessStepRunStatus.Blocked => "Blocked from the runtime workspace for review.",
            ProcessStepRunStatus.Refused => "Executor recorded a safe refusal from the runtime workspace.",
            ProcessStepRunStatus.WaitingApproval => "Approval was requested from the runtime workspace.",
            ProcessStepRunStatus.Failed => "Failure was captured from the runtime workspace.",
            ProcessStepRunStatus.Skipped => "Step was skipped from the runtime workspace.",
            _ => "State updated from the runtime workspace."
        };
    }

    private Guid? ResolveSelectedBranchOutcomeId(Guid stepRunId)
    {
        return runtimeBranchOutcomeSelections.TryGetValue(stepRunId, out var selectedBranchOutcomeId)
            ? selectedBranchOutcomeId
            : stepRuns.FirstOrDefault(item => item.Id == stepRunId)?.SelectedBranchOutcomeId;
    }

    private Task UpdateRuntimeBranchOutcomeSelectionAsync(Guid stepRunId, Guid? branchOutcomeId)
    {
        runtimeBranchOutcomeSelections[stepRunId] = branchOutcomeId;
        return Task.CompletedTask;
    }

    private static void SetStepDependencies(
        ProcessStepEditorModel step,
        IEnumerable<ProcessStepDependencyEditorModel> dependencies)
    {
        ProcessStepDependencyCollection.SetEditorDependencies(step, dependencies);
    }

    private string ResolveRoleName(Guid? roleId)
    {
        if (!roleId.HasValue)
        {
            return "Unbound";
        }

        return editor.Roles.FirstOrDefault(role => role.Id == roleId.Value)?.DisplayName ?? "Unknown role";
    }

    private string BuildDirectMessageAssignmentLabel(ProcessRunAssignmentViewModel assignment)
    {
        var roleName = ResolveRoleName(assignment.RoleRequirementId);
        var bindingName = string.IsNullOrWhiteSpace(assignment.DisplayName) || string.Equals(assignment.DisplayName, "Unassigned role", StringComparison.Ordinal)
            ? null
            : assignment.DisplayName;
        var status = assignment.IsCapabilityGap
            ? "gap"
            : assignment.AllowsDirectMessaging
                ? "messaging on"
                : "messaging off";
        return string.IsNullOrWhiteSpace(bindingName)
            ? $"{roleName} ({status})"
            : $"{roleName} / {bindingName} ({status})";
    }

    private string ResolveDefinitionStatusTone(ProcessDefinitionStatus status)
    {
        return status switch
        {
            ProcessDefinitionStatus.Published => "info",
            ProcessDefinitionStatus.Archived => "neutral",
            _ => "warning"
        };
    }

    private static string ResolveRunTone(ProcessRunStatus status)
    {
        return status switch
        {
            ProcessRunStatus.Completed => "mint",
            ProcessRunStatus.Active => "info",
            ProcessRunStatus.Blocked => "warning",
            ProcessRunStatus.Failed => "danger",
            ProcessRunStatus.Cancelled => "neutral",
            _ => "neutral"
        };
    }

    private static bool CanApplyRuntimeStatus(ProcessStepRunViewModel? stepRun, ProcessStepRunStatus targetStatus)
    {
        return stepRun is not null &&
            stepRun.Status != targetStatus &&
            ProcessStepRunTransitions.IsAllowed(stepRun.Status, targetStatus);
    }

    private static string ResolveStepTone(ProcessStepRunStatus status)
    {
        return status switch
        {
            ProcessStepRunStatus.Completed => "mint",
            ProcessStepRunStatus.InProgress => "info",
            ProcessStepRunStatus.Blocked => "danger",
            ProcessStepRunStatus.Refused => "warning",
            ProcessStepRunStatus.WaitingApproval => "accent",
            ProcessStepRunStatus.Failed => "danger",
            _ => "neutral"
        };
    }

    private static string ResolveConformanceTone(ProcessConformanceSeverity severity)
    {
        return severity switch
        {
            ProcessConformanceSeverity.Critical => "danger",
            ProcessConformanceSeverity.High => "warning",
            ProcessConformanceSeverity.Moderate => "info",
            _ => "neutral"
        };
    }
}
