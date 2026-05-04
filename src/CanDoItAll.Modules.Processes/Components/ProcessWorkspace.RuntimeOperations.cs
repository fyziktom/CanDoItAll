using CanDoItAll.Components.BaseLib;

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
        detailTab = DetailTabRuns;
        runNameDraft = string.Empty;
        RuntimeStateOverviewService.Invalidate();
        await LoadWorkspaceAsync();
        SetMessage("Process run started.");
    }

    private async Task SelectRunAsync(Guid runId)
    {
        selectedRunId = runId;
        detailTab = DetailTabRuns;
        selectedCanvasNodeId = null;
        ResetRuntimeCanvasState();
        await LoadRunDetailsAsync();
        RefreshCanvasSurface();
        UpdateRuntimeRefreshLoop();
    }

    private async Task OpenRunStepsDialogAsync(Guid runId)
    {
        await SelectRunAsync(runId);

        var selectedRun = SelectedRun;
        if (selectedRun is null)
        {
            SetError("Reload the process before opening this run.");
            return;
        }

        _ = DialogService.OpenAsync<ProcessWorkspaceRunStepsDialog>(
            selectedRun.Name,
            new Dictionary<string, object?>
            {
                [nameof(ProcessWorkspaceRunStepsDialog.Presenter)] = RunsTabPresenter
            },
            new DialogOptions
            {
                Eyebrow = "Process run",
                Subtitle = BuildRunSummary(selectedRun),
                Size = ModalSize.Full,
                DenseChrome = true,
                TestId = "processes-run-steps-dialog",
                AriaLabel = $"Run steps for {selectedRun.Name}",
                Style = "max-width:calc(100vw - 1.5rem);width:calc(100vw - 1.5rem);height:calc(100vh - 1.5rem);max-height:calc(100vh - 1.5rem);"
            });
    }

    private async Task StopBlockedRunAsync(Guid runId)
    {
        var run = runs.FirstOrDefault(item => item.Id == runId);
        if (run is null)
        {
            SetError("Reload the process before stopping this run.");
            return;
        }

        if (run.Status != ProcessRunStatus.Blocked)
        {
            SetError("Only blocked process runs can be stopped from this action.");
            return;
        }

        stoppingRunId = runId;
        try
        {
            var result = await ProcessesService.StopBlockedRunAsync(
                new ProcessRunStopRequest
                {
                    ProcessRunId = runId,
                    Reason = "Stopped from the process workspace Runs tab.",
                    StoppedBy = "process-workspace"
                });
            if (result.IsFailure)
            {
                SetError(result.Errors);
                return;
            }

            selectedRunId = runId;
            detailTab = DetailTabRuns;
            RuntimeStateOverviewService.Invalidate();
            await LoadWorkspaceAsync();
            SetMessage("Blocked process run stopped.");
        }
        finally
        {
            stoppingRunId = null;
        }
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

        detailTab = DetailTabRuns;
        RuntimeStateOverviewService.Invalidate();
        await LoadWorkspaceAsync();
        SetMessage($"Step updated to {targetStatus}.");
    }

    private async Task RerunAgentStepAsync(Guid stepRunId)
    {
        var currentStepRun = stepRuns.FirstOrDefault(item => item.Id == stepRunId);
        if (currentStepRun is null)
        {
            SetError("Reload the run before rerunning this agent step.");
            return;
        }

        var operatorReason = string.IsNullOrWhiteSpace(operatorReworkDirective)
            ? "Operator requested a governed agent rerun from Process Workspace."
            : operatorReworkDirective.Trim();
        var result = await ProcessesService.RerunAgentStepAsync(
            new ProcessAgentStepRerunRequest
            {
                StepRunId = stepRunId,
                StepRunConcurrencyToken = currentStepRun.StepRunConcurrencyToken,
                OperatorReason = operatorReason
            });
        if (result.IsFailure)
        {
            SetError(result.Errors);
            return;
        }

        operatorReworkStepRunId = null;
        operatorReworkDirective = string.Empty;
        detailTab = DetailTabRuns;
        RuntimeStateOverviewService.Invalidate();
        await LoadWorkspaceAsync();
        SetMessage("Agent step rerun requested with a recovery directive.");
    }

    private async Task AssignEscalationAsync(Guid escalationId)
    {
        var result = await EscalationService.AssignAsync(
            new ProcessEscalationAssignmentRequest
            {
                EscalationId = escalationId,
                Owner = string.IsNullOrWhiteSpace(operatorEscalationOwner)
                    ? "process-workspace"
                    : operatorEscalationOwner,
                AssignedBy = "process-workspace"
            });
        if (result.IsFailure)
        {
            SetError(result.Errors);
            return;
        }

        detailTab = DetailTabRuns;
        await LoadWorkspaceAsync();
        SetMessage("Escalation assigned.");
    }

    private async Task ResolveEscalationAsync(Guid escalationId)
    {
        var result = await EscalationService.ResolveAsync(
            new ProcessEscalationResolutionRequest
            {
                EscalationId = escalationId,
                Resolution = string.IsNullOrWhiteSpace(operatorEscalationResolution)
                    ? "Operator resolved this escalation from Process Workspace."
                    : operatorEscalationResolution,
                ResolvedBy = "process-workspace"
            });
        if (result.IsFailure)
        {
            SetError(result.Errors);
            return;
        }

        operatorEscalationResolution = string.Empty;
        detailTab = DetailTabRuns;
        await LoadWorkspaceAsync();
        SetMessage("Escalation resolved.");
    }

    private async Task ReopenEscalationAsync(Guid escalationId)
    {
        var result = await EscalationService.ReopenAsync(
            new ProcessEscalationReopenRequest
            {
                EscalationId = escalationId,
                Reason = string.IsNullOrWhiteSpace(operatorEscalationResolution)
                    ? "Operator reopened this escalation from Process Workspace."
                    : operatorEscalationResolution,
                ReopenedBy = "process-workspace"
            });
        if (result.IsFailure)
        {
            SetError(result.Errors);
            return;
        }

        operatorEscalationResolution = string.Empty;
        detailTab = DetailTabRuns;
        await LoadWorkspaceAsync();
        SetMessage("Escalation reopened.");
    }

    private async Task RequestEscalationReworkAsync(Guid escalationId)
    {
        var escalation = processEscalations.FirstOrDefault(item => item.Id == escalationId);
        if (escalation is null || !escalation.StepRunId.HasValue)
        {
            SetError("Select a step-scoped escalation before requesting rework.");
            return;
        }

        var currentStepRun = stepRuns.FirstOrDefault(item => item.Id == escalation.StepRunId.Value);
        if (currentStepRun is null)
        {
            SetError("Reload the run before requesting rework for this escalation.");
            return;
        }

        var result = await EscalationService.RequestReworkAsync(
            new ProcessEscalationReworkRequest
            {
                EscalationId = escalationId,
                StepRunConcurrencyToken = currentStepRun.StepRunConcurrencyToken,
                Directive = string.IsNullOrWhiteSpace(operatorReworkDirective)
                    ? escalation.Reason
                    : operatorReworkDirective,
                RequestedBy = "process-workspace"
            });
        if (result.IsFailure)
        {
            SetError(result.Errors);
            return;
        }

        operatorReworkStepRunId = null;
        operatorReworkDirective = string.Empty;
        detailTab = DetailTabRuns;
        RuntimeStateOverviewService.Invalidate();
        await LoadWorkspaceAsync();
        SetMessage("Escalation rework requested.");
    }

    private async Task RequestManualReworkAsync()
    {
        if (!operatorReworkStepRunId.HasValue)
        {
            SetError("Select a blocked or failed agent step before requesting rework.");
            return;
        }

        await RerunAgentStepAsync(operatorReworkStepRunId.Value);
    }

    private async Task SendManagerDirectiveAsync()
    {
        if (!selectedRunId.HasValue)
        {
            SetError("Select a run before instructing its manager.");
            return;
        }

        var result = await ProcessesService.RecordManagerDirectiveAsync(
            new ProcessManagerDirectiveRequest
            {
                ProcessRunId = selectedRunId.Value,
                Directive = operatorManagerDirective,
                InstructedBy = "process-workspace"
            });
        if (result.IsFailure)
        {
            SetError(result.Errors);
            return;
        }

        operatorManagerDirective = string.Empty;
        detailTab = DetailTabRuns;
        await LoadWorkspaceAsync();
        SetMessage("Manager directive recorded.");
    }

    private async Task DecideOperatorApprovalAsync(
        ProcessOperatorApprovalViewModel approval,
        ProcessOperatorApprovalStatus status)
    {
        if (!approval.ExecutionRunId.HasValue)
        {
            SetError("Only execution-run approvals can be continued from this console.");
            return;
        }

        if (!selectedRunId.HasValue)
        {
            SetError("Select a run before deciding an approval.");
            return;
        }

        try
        {
            await AgentWorkspaceService.ContinueExecutionRunAsync(
                approval.ExecutionRunId.Value,
                approved: status == ProcessOperatorApprovalStatus.Approved,
                autoApprovePendingToolCalls: false);
        }
        catch (InvalidOperationException exception)
        {
            SetError(exception.Message);
            return;
        }

        var recordResult = await EscalationService.RecordApprovalDecisionAsync(
            new ProcessOperatorApprovalDecisionRequest
            {
                ProcessRunId = selectedRunId.Value,
                StepRunId = approval.StepRunId,
                ExecutionRunId = approval.ExecutionRunId,
                ExternalApprovalId = approval.ExternalApprovalId,
                Status = status,
                Summary = string.IsNullOrWhiteSpace(operatorApprovalDecisionSummary)
                    ? $"{status} from Process Workspace."
                    : operatorApprovalDecisionSummary,
                DecidedBy = "process-workspace"
            });
        if (recordResult.IsFailure)
        {
            SetError(recordResult.Errors);
            return;
        }

        operatorApprovalDecisionSummary = string.Empty;
        detailTab = DetailTabRuns;
        await LoadWorkspaceAsync();
        SetMessage($"Approval {status}.");
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

        detailTab = DetailTabRuns;
        await LoadWorkspaceAsync();
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
            detailTab = DetailTabRuns;
            SetError(result.Errors);
            return;
        }

        directMessageBody = string.Empty;
        await LoadRunDetailsAsync();
        detailTab = DetailTabRuns;
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
        detailTab = DetailTabRuns;
        await LoadWorkspaceAsync();
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

        var runtimeRoleName = assignments
            .FirstOrDefault(item => item.RoleRequirementId == roleId.Value && !string.IsNullOrWhiteSpace(item.RoleDisplayName))
            ?.RoleDisplayName;
        if (!string.IsNullOrWhiteSpace(runtimeRoleName))
        {
            return runtimeRoleName;
        }

        var launchPlanRoleName = selectedLaunchPlan?.Roles
            .FirstOrDefault(item => item.RoleRequirementId == roleId.Value)
            ?.DisplayName;
        if (!string.IsNullOrWhiteSpace(launchPlanRoleName))
        {
            return launchPlanRoleName;
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

    private static string ResolveEscalationStatusTone(ProcessEscalationViewModel escalation)
    {
        if (escalation.Status == ProcessEscalationStatus.Resolved)
        {
            return "mint";
        }

        return ResolveEscalationSeverityTone(escalation.Severity);
    }

    private static string ResolveEscalationSeverityTone(ProcessEscalationSeverity severity)
    {
        return severity switch
        {
            ProcessEscalationSeverity.Critical => "danger",
            ProcessEscalationSeverity.High => "danger",
            ProcessEscalationSeverity.Moderate => "warning",
            _ => "info"
        };
    }

    private static string ResolveOperatorApprovalTone(ProcessOperatorApprovalStatus status)
    {
        return status switch
        {
            ProcessOperatorApprovalStatus.Approved => "mint",
            ProcessOperatorApprovalStatus.Rejected => "danger",
            ProcessOperatorApprovalStatus.ChangesRequested => "warning",
            _ => "warning"
        };
    }
}
