namespace CanDoItAll.Modules.Processes;

public partial class ProcessWorkspace
{
    protected override async Task OnParametersSetAsync()
    {
        if (hasLoadedParameters &&
            loadedProjectId == ProjectId &&
            loadedProcessQueryId == ProcessIdQuery &&
            loadedRunQueryId == RunIdQuery &&
            loadedLaunchPlanQueryId == LaunchPlanIdQuery)
        {
            return;
        }

        hasLoadedParameters = true;
        loadedProjectId = ProjectId;
        loadedProcessQueryId = ProcessIdQuery;
        loadedRunQueryId = RunIdQuery;
        loadedLaunchPlanQueryId = LaunchPlanIdQuery;
        await LoadWorkspaceAsync();
    }

    public void Dispose()
    {
        StopRuntimeRefreshLoop();
        CancelPendingDefinitionCanvasPersistence();
    }

    public async ValueTask DisposeAsync()
    {
        StopRuntimeRefreshLoop();
        await QuiesceDefinitionCanvasPersistenceAsync(DefinitionCanvasPersistenceQuiescenceMode.CancelPendingChanges);
    }

    private async Task LoadWorkspaceAsync()
    {
        if (ProjectId.HasValue)
        {
            var project = await ProjectsService.GetAsync(ProjectId.Value);
            projectName = project.Name;
        }
        else
        {
            projectName = string.Empty;
        }

        definitions = await ProcessesService.ListDefinitionsAsync(ProjectId);
        var nextSelectedProcessId = ResolveSelectedProcessId();
        if (nextSelectedProcessId != selectedProcessId)
        {
            await QuiesceDefinitionCanvasPersistenceAsync(DefinitionCanvasPersistenceQuiescenceMode.FlushPendingChanges);
            selectedCanvasNodeId = null;
            ResetDefinitionCanvasState();
            ResetRuntimeCanvasState();
        }

        selectedProcessId = nextSelectedProcessId;
        editor = await ProcessesService.GetEditorAsync(selectedProcessId, ProjectId);
        executorOptions = await ProcessesService.ListExecutorOptionsAsync();
        analytics = await ProcessesService.GetAnalyticsAsync(selectedProcessId, ProjectId);
        improvements = await ProcessesService.ListImprovementsAsync(selectedProcessId);

        if (ProjectId.HasValue)
        {
            partyOptions = await ProcessesService.ListPartyOptionsAsync(ProjectId.Value);
        }
        else
        {
            partyOptions = [];
        }

        if (selectedProcessId.HasValue)
        {
            launchPlans = await ProcessesService.ListLaunchPlansAsync(selectedProcessId, ProjectId);
            selectedLaunchPlanId = ResolveSelectedLaunchPlanId();
            await LoadLaunchPlanDetailsAsync();
            runs = await ProcessesService.ListRunsAsync(selectedProcessId, ProjectId);
            activeRunSummaries = await RunDetailsLoader.LoadActiveRunSummariesAsync(runs);
        }
        else
        {
            launchPlans = [];
            selectedLaunchPlanId = null;
            selectedLaunchPlan = null;
            runs = [];
            activeRunSummaries = [];
        }

        var nextSelectedRunId = ResolveSelectedRunId();
        if (nextSelectedRunId != selectedRunId)
        {
            selectedCanvasNodeId = null;
            ResetRuntimeCanvasState();
        }

        selectedRunId = nextSelectedRunId;
        if ((RunIdQuery.HasValue && selectedRunId.HasValue) ||
            (LaunchPlanIdQuery.HasValue && selectedLaunchPlanId.HasValue))
        {
            detailTab = "runs";
        }

        await LoadRunDetailsAsync();
        RefreshCanvasSurface();
        UpdateRuntimeRefreshLoop();
        StateHasChanged();
    }

    private async Task LoadRunDetailsAsync()
    {
        if (!selectedRunId.HasValue)
        {
            stepRuns = [];
            decisions = [];
            artifacts = [];
            outboxRecords = [];
            assignments = [];
            workBriefs = [];
            conformanceObservations = [];
            executionRuns = [];
            processEscalations = [];
            operatorApprovals = [];
            attemptTimeline = [];
            selectedRunHealth = ProcessRunHealthSummaryViewModel.Empty;
            directMessageThreads = [];
            selectedAssignmentId = null;
            artifactStepRunId = null;
            operatorReworkStepRunId = null;
            operatorReworkDirective = string.Empty;
            operatorEscalationResolution = string.Empty;
            operatorApprovalDecisionSummary = string.Empty;
            assignmentAllowsDirectMessaging = true;
            directMessageSourceRoleRequirementId = null;
            directMessageTargetRoleRequirementId = null;
            directMessageBody = string.Empty;
            return;
        }

        var runDetails = await RunDetailsLoader.LoadAsync(selectedRunId.Value);
        stepRuns = runDetails.StepRuns;
        decisions = runDetails.Decisions;
        artifacts = runDetails.Artifacts;
        outboxRecords = runDetails.OutboxRecords;
        assignments = runDetails.Assignments;
        workBriefs = runDetails.WorkBriefs;
        conformanceObservations = runDetails.ConformanceObservations;
        executionRuns = runDetails.ExecutionRuns;
        processEscalations = runDetails.Escalations;
        operatorApprovals = runDetails.OperatorApprovals;
        attemptTimeline = runDetails.AttemptTimeline;
        selectedRunHealth = runDetails.Health;
        directMessageThreads = runDetails.DirectMessageThreads;
        var refreshedRuntimeBranchSelections = new Dictionary<Guid, Guid?>();
        foreach (var stepRun in stepRuns)
        {
            runtimeBranchOutcomeSelections.TryGetValue(stepRun.Id, out var selectedBranchOutcomeId);
            if (selectedBranchOutcomeId.HasValue &&
                stepRun.AvailableBranchOutcomes.All(item => item.Id != selectedBranchOutcomeId.Value))
            {
                selectedBranchOutcomeId = null;
            }

            refreshedRuntimeBranchSelections[stepRun.Id] = selectedBranchOutcomeId ?? stepRun.SelectedBranchOutcomeId;
        }

        runtimeBranchOutcomeSelections = refreshedRuntimeBranchSelections;

        if (!selectedAssignmentId.HasValue || assignments.All(item => item.Id != selectedAssignmentId.Value))
        {
            selectedAssignmentId = assignments.FirstOrDefault()?.Id;
        }

        ApplyAssignmentSelection();
        SynchronizeDirectMessagingComposer();

        if (!artifactStepRunId.HasValue || stepRuns.All(item => item.Id != artifactStepRunId.Value))
        {
            artifactStepRunId = stepRuns.FirstOrDefault()?.Id;
        }

        if (!operatorReworkStepRunId.HasValue || stepRuns.All(item => item.Id != operatorReworkStepRunId.Value))
        {
            operatorReworkStepRunId = stepRuns.FirstOrDefault(item => item.Health.CanManualRerun)?.Id;
        }
    }

    private Guid? ResolveSelectedProcessId()
    {
        if (ProcessIdQuery.HasValue && definitions.Any(definition => definition.Id == ProcessIdQuery.Value))
        {
            return ProcessIdQuery.Value;
        }

        if (selectedProcessId.HasValue && definitions.Any(definition => definition.Id == selectedProcessId.Value))
        {
            return selectedProcessId.Value;
        }

        return definitions.FirstOrDefault()?.Id;
    }

    private Guid? ResolveSelectedRunId()
    {
        if (RunIdQuery.HasValue && runs.Any(run => run.Id == RunIdQuery.Value))
        {
            return RunIdQuery.Value;
        }

        if (selectedRunId.HasValue && runs.Any(run => run.Id == selectedRunId.Value))
        {
            return selectedRunId.Value;
        }

        return runs.FirstOrDefault()?.Id;
    }
}
