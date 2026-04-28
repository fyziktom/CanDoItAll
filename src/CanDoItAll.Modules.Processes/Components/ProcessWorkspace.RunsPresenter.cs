using CanDoItAll.Components.CanvasLib;
using CanDoItAll.Modules.Projects;

namespace CanDoItAll.Modules.Processes;

public partial class ProcessWorkspace
{
    public sealed class ProcessWorkspaceRunsTabPresenter
    {
        private readonly ProcessWorkspace workspace;

        internal ProcessWorkspaceRunsTabPresenter(ProcessWorkspace workspace)
        {
            this.workspace = workspace;
        }

        public CanvasWorkbenchSurface? CanvasSurface => workspace.canvasSurface;

        public CanvasWorkbenchWindowState CanvasSelectionWindowState => workspace.canvasSelectionWindowState;

        public string CanvasSelectionWindowId => ProcessWorkspace.CanvasSelectionWindowId;

        public string CanvasSelectionWindowTitle => workspace.CanvasSelectionWindowTitle;

        public string CanvasSelectionWindowSummary => workspace.CanvasSelectionWindowSummary;

        public ProcessStepRunViewModel? SelectedCanvasRuntimeStep => workspace.SelectedCanvasRuntimeStep;

        public bool HasSelectedProcess => workspace.selectedProcessId.HasValue;

        public IReadOnlyList<ProcessRunListItem> Runs => workspace.runs;

        public ProcessRunListItem? SelectedRun => workspace.SelectedRun;

        public IReadOnlyList<ProcessLaunchPlanListItem> LaunchPlans => workspace.launchPlans;

        public ProcessLaunchPlanDetails? SelectedLaunchPlan => workspace.selectedLaunchPlan;

        public ProcessLaunchPlanListItem? SelectedLaunchPlanSummary => workspace.SelectedLaunchPlanSummary;

        public IReadOnlyList<ProcessStepRunViewModel> StepRuns => workspace.stepRuns;

        public IReadOnlyList<ProcessDecisionViewModel> Decisions => workspace.decisions;

        public IReadOnlyList<ProcessArtifactViewModel> Artifacts => workspace.artifacts;

        public IReadOnlyList<ProcessOutboxRecordViewModel> OutboxRecords => workspace.outboxRecords;

        public ProcessRunHealthSummaryViewModel SelectedRunHealth => workspace.selectedRunHealth;

        public IReadOnlyList<ProcessRunAssignmentViewModel> Assignments => workspace.assignments;

        public ProcessRunAssignmentViewModel? SelectedAssignment => workspace.SelectedAssignment;

        public IReadOnlyList<ProcessWorkBriefViewModel> WorkBriefs => workspace.workBriefs;

        public IReadOnlyList<ProcessConformanceObservationViewModel> ConformanceObservations => workspace.conformanceObservations;

        public IReadOnlyList<ProcessExecutionRunViewModel> ExecutionRuns => workspace.executionRuns;

        public IReadOnlyList<ProcessEscalationViewModel> ProcessEscalations => workspace.processEscalations;

        public IReadOnlyList<ProcessOperatorApprovalViewModel> OperatorApprovals => workspace.operatorApprovals;

        public IReadOnlyList<ProcessAttemptTimelineEntryViewModel> AttemptTimeline => workspace.attemptTimeline;

        public IReadOnlyList<ProcessActiveRunSummaryViewModel> ActiveRunSummaries => workspace.activeRunSummaries;

        public IReadOnlyList<ProjectPartyOption> PartyOptions => workspace.partyOptions;

        public string RunNameDraft
        {
            get => workspace.runNameDraft;
            set => workspace.runNameDraft = value ?? string.Empty;
        }

        public string LaunchNameDraft
        {
            get => workspace.launchNameDraft;
            set => workspace.launchNameDraft = value ?? string.Empty;
        }

        public ProcessOperatingMode RunOperatingMode
        {
            get => workspace.runOperatingMode;
            set => workspace.runOperatingMode = value;
        }

        public string LaunchDecisionSummary
        {
            get => workspace.launchDecisionSummary;
            set => workspace.launchDecisionSummary = value ?? string.Empty;
        }

        public Guid? ArtifactStepRunId
        {
            get => workspace.artifactStepRunId;
            set => workspace.artifactStepRunId = value;
        }

        public string ArtifactTitle
        {
            get => workspace.artifactTitle;
            set => workspace.artifactTitle = value ?? string.Empty;
        }

        public ProcessArtifactKind ArtifactKind
        {
            get => workspace.artifactKind;
            set => workspace.artifactKind = value;
        }

        public Guid? AssignmentPartyId
        {
            get => workspace.assignmentPartyId;
            set => workspace.assignmentPartyId = value;
        }

        public string AssignmentDisplayName
        {
            get => workspace.assignmentDisplayName;
            set => workspace.assignmentDisplayName = value ?? string.Empty;
        }

        public string AssignmentExecutorKind
        {
            get => workspace.assignmentExecutorKind;
            set => workspace.assignmentExecutorKind = value ?? string.Empty;
        }

        public string AssignmentBindingReason
        {
            get => workspace.assignmentBindingReason;
            set => workspace.assignmentBindingReason = value ?? string.Empty;
        }

        public bool AssignmentIsFallback
        {
            get => workspace.assignmentIsFallback;
            set => workspace.assignmentIsFallback = value;
        }

        public bool AssignmentAllowsDirectMessaging
        {
            get => workspace.assignmentAllowsDirectMessaging;
            set => workspace.assignmentAllowsDirectMessaging = value;
        }

        public Guid? DirectMessageSourceRoleRequirementId
        {
            get => workspace.directMessageSourceRoleRequirementId;
            set => workspace.directMessageSourceRoleRequirementId = value;
        }

        public Guid? DirectMessageTargetRoleRequirementId
        {
            get => workspace.directMessageTargetRoleRequirementId;
            set => workspace.directMessageTargetRoleRequirementId = value;
        }

        public string DirectMessageBody
        {
            get => workspace.directMessageBody;
            set => workspace.directMessageBody = value ?? string.Empty;
        }

        public Guid? OperatorReworkStepRunId
        {
            get => workspace.operatorReworkStepRunId;
            set => workspace.operatorReworkStepRunId = value;
        }

        public string OperatorReworkDirective
        {
            get => workspace.operatorReworkDirective;
            set => workspace.operatorReworkDirective = value ?? string.Empty;
        }

        public string OperatorEscalationOwner
        {
            get => workspace.operatorEscalationOwner;
            set => workspace.operatorEscalationOwner = value ?? string.Empty;
        }

        public string OperatorEscalationResolution
        {
            get => workspace.operatorEscalationResolution;
            set => workspace.operatorEscalationResolution = value ?? string.Empty;
        }

        public string OperatorApprovalDecisionSummary
        {
            get => workspace.operatorApprovalDecisionSummary;
            set => workspace.operatorApprovalDecisionSummary = value ?? string.Empty;
        }

        public IReadOnlyList<ProcessRunAssignmentViewModel> DirectMessageAssignments => workspace.DirectMessageAssignments;

        public IReadOnlyList<ProcessDirectMessageThreadViewModel> DirectMessageThreads => workspace.directMessageThreads;

        public bool CanSendDirectMessage
            => workspace.selectedRunId.HasValue &&
               DirectMessageAssignments.Count >= 2 &&
               DirectMessageSourceRoleRequirementId.HasValue &&
               DirectMessageTargetRoleRequirementId.HasValue &&
               DirectMessageSourceRoleRequirementId != DirectMessageTargetRoleRequirementId &&
               !string.IsNullOrWhiteSpace(DirectMessageBody);

        public void CaptureWorkbench(CanvasWorkbench? workbench)
        {
            workspace.workbenchRef = workbench;
        }

        public Task HandleCanvasStateChangedAsync(string stateJson)
        {
            return workspace.HandleCanvasStateChangedAsync(stateJson);
        }

        public Task HandleCanvasSelectionChangedAsync(CanvasWorkbenchSelectionChangedEventArgs args)
        {
            return workspace.HandleCanvasSelectionChangedAsync(args);
        }

        public Task HandleCanvasContextActionAsync(CanvasWorkbenchContextActionRequest request)
        {
            return workspace.HandleCanvasContextActionAsync(request);
        }

        public Task HandleCanvasNodeOpenedAsync(string nodeId)
        {
            return workspace.HandleCanvasNodeOpenedAsync(nodeId);
        }

        public Task ToggleCanvasSelectionWindowAsync()
        {
            return workspace.ToggleCanvasSelectionWindowAsync();
        }

        public Task HandleCanvasSelectionWindowStateChangedAsync(CanvasWorkbenchWindowState state)
        {
            return workspace.HandleCanvasSelectionWindowStateChangedAsync(state);
        }

        public Guid? SelectedCanvasRuntimeBranchOutcomeId
            => workspace.SelectedCanvasRuntimeStep is null
                ? null
                : workspace.ResolveSelectedBranchOutcomeId(workspace.SelectedCanvasRuntimeStep.Id);

        public Task UpdateSelectedCanvasRuntimeBranchOutcomeAsync(Guid? branchOutcomeId)
        {
            return workspace.SelectedCanvasRuntimeStep is null
                ? Task.CompletedTask
                : workspace.UpdateRuntimeBranchOutcomeSelectionAsync(workspace.SelectedCanvasRuntimeStep.Id, branchOutcomeId);
        }

        public Task ClearCanvasSelectionAsync()
        {
            return workspace.ClearCanvasSelectionAsync();
        }

        public Task OpenCanvasActionDialogAsync()
        {
            return workspace.OpenCanvasActionDialogAsync();
        }

        public Task ApplySelectedRuntimeStatusAsync(ProcessStepRunStatus status)
        {
            return workspace.ApplySelectedRuntimeStatusAsync(status);
        }

        public Task PrepareSelectedRuntimeArtifactCaptureAsync()
        {
            workspace.PrepareSelectedRuntimeArtifactCapture();
            return Task.CompletedTask;
        }

        public Task StartRunAsync()
        {
            return workspace.StartRunAsync();
        }

        public Task CreateLaunchPlanAsync()
        {
            return workspace.CreateLaunchPlanAsync();
        }

        public Task SelectLaunchPlanAsync(Guid launchPlanId)
        {
            return workspace.SelectLaunchPlanAsync(launchPlanId);
        }

        public Task SelectLaunchCandidateAsync(Guid launchPlanRoleId, Guid candidateId)
        {
            return workspace.SelectLaunchCandidateAsync(launchPlanRoleId, candidateId);
        }

        public Task SubmitLaunchPlanForApprovalAsync()
        {
            return workspace.SubmitLaunchPlanForApprovalAsync();
        }

        public Task ApproveLaunchPlanAsync()
        {
            return workspace.DecideLaunchPlanAsync(ProcessLaunchApprovalStatus.Approved);
        }

        public Task RequestLaunchChangesAsync()
        {
            return workspace.DecideLaunchPlanAsync(ProcessLaunchApprovalStatus.ChangesRequested);
        }

        public Task RejectLaunchPlanAsync()
        {
            return workspace.DecideLaunchPlanAsync(ProcessLaunchApprovalStatus.Rejected);
        }

        public Task ProvisionLaunchPlanAsync()
        {
            return workspace.ProvisionLaunchPlanAsync();
        }

        public Task ExecuteLaunchPlanAsync()
        {
            return workspace.ExecuteLaunchPlanAsync();
        }

        public Task SelectRunAsync(Guid runId)
        {
            return workspace.SelectRunAsync(runId);
        }

        public string BuildLaunchPlanSummary(ProcessLaunchPlanListItem plan)
        {
            return ProcessWorkspace.BuildLaunchPlanSummary(plan);
        }

        public string ResolveLaunchPlanTone(ProcessLaunchPlanStatus status)
        {
            return ProcessWorkspace.ResolveLaunchPlanTone(status);
        }

        public string ResolveLaunchCandidateTone(ProcessLaunchCandidateViewModel candidate)
        {
            return ProcessWorkspace.ResolveLaunchCandidateTone(candidate);
        }

        public string ResolveLaunchApprovalTone(ProcessLaunchApprovalStatus status)
        {
            return ProcessWorkspace.ResolveLaunchApprovalTone(status);
        }

        public string ResolveProvisioningTone(ProcessLaunchProvisioningStatus status)
        {
            return ProcessWorkspace.ResolveProvisioningTone(status);
        }

        public string BuildRunSummary(ProcessRunListItem run)
        {
            return ProcessWorkspace.BuildRunSummary(run);
        }

        public string ResolveRunTone(ProcessRunStatus status)
        {
            return ProcessWorkspace.ResolveRunTone(status);
        }

        public Guid? ResolveSelectedBranchOutcomeId(Guid stepRunId)
        {
            return workspace.ResolveSelectedBranchOutcomeId(stepRunId);
        }

        public Task UpdateRuntimeBranchOutcomeSelectionAsync(Guid stepRunId, Guid? branchOutcomeId)
        {
            return workspace.UpdateRuntimeBranchOutcomeSelectionAsync(stepRunId, branchOutcomeId);
        }

        public bool CanApplyRuntimeStatus(ProcessStepRunViewModel? stepRun, ProcessStepRunStatus targetStatus)
        {
            return ProcessWorkspace.CanApplyRuntimeStatus(stepRun, targetStatus);
        }

        public Task ApplyStepStatusAsync(Guid stepRunId, ProcessStepRunStatus targetStatus)
        {
            return workspace.ApplyStepStatusAsync(stepRunId, targetStatus);
        }

        public Task RerunAgentStepAsync(Guid stepRunId)
        {
            return workspace.RerunAgentStepAsync(stepRunId);
        }

        public Task AssignEscalationAsync(Guid escalationId)
        {
            return workspace.AssignEscalationAsync(escalationId);
        }

        public Task ResolveEscalationAsync(Guid escalationId)
        {
            return workspace.ResolveEscalationAsync(escalationId);
        }

        public Task ReopenEscalationAsync(Guid escalationId)
        {
            return workspace.ReopenEscalationAsync(escalationId);
        }

        public Task RequestEscalationReworkAsync(Guid escalationId)
        {
            return workspace.RequestEscalationReworkAsync(escalationId);
        }

        public Task RequestManualReworkAsync()
        {
            return workspace.RequestManualReworkAsync();
        }

        public Task DecideOperatorApprovalAsync(
            ProcessOperatorApprovalViewModel approval,
            ProcessOperatorApprovalStatus status)
        {
            return workspace.DecideOperatorApprovalAsync(approval, status);
        }

        public void SelectAssignment(Guid assignmentId)
        {
            workspace.SelectAssignment(assignmentId);
        }

        public string ResolveRoleName(Guid? roleId)
        {
            return workspace.ResolveRoleName(roleId);
        }

        public Task ResolveSelectedAssignmentAsync()
        {
            return workspace.ResolveSelectedAssignmentAsync();
        }

        public string BuildDirectMessageAssignmentLabel(ProcessRunAssignmentViewModel assignment)
        {
            return workspace.BuildDirectMessageAssignmentLabel(assignment);
        }

        public Task SendDirectMessageAsync()
        {
            return workspace.SendDirectMessageAsync();
        }

        public Task RecordArtifactAsync()
        {
            return workspace.RecordArtifactAsync();
        }

        public string ResolveStepTone(ProcessStepRunStatus status)
        {
            return ProcessWorkspace.ResolveStepTone(status);
        }

        public string ResolveConformanceTone(ProcessConformanceSeverity severity)
        {
            return ProcessWorkspace.ResolveConformanceTone(severity);
        }

        public string ResolveEscalationStatusTone(ProcessEscalationViewModel escalation)
        {
            return ProcessWorkspace.ResolveEscalationStatusTone(escalation);
        }

        public string ResolveEscalationSeverityTone(ProcessEscalationSeverity severity)
        {
            return ProcessWorkspace.ResolveEscalationSeverityTone(severity);
        }

        public string ResolveOperatorApprovalTone(ProcessOperatorApprovalStatus status)
        {
            return ProcessWorkspace.ResolveOperatorApprovalTone(status);
        }
    }
}
