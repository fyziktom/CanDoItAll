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
        AgentWorkspaceService.ExecutionUpdated -= HandleManagerChatExecutionUpdated;
        StopRuntimeRefreshLoop();
        CancelPendingDefinitionCanvasPersistence();
    }

    public async ValueTask DisposeAsync()
    {
        AgentWorkspaceService.ExecutionUpdated -= HandleManagerChatExecutionUpdated;
        StopRuntimeRefreshLoop();
        await QuiesceDefinitionCanvasPersistenceAsync(DefinitionCanvasPersistenceQuiescenceMode.CancelPendingChanges);
    }

    private async Task LoadWorkspaceAsync()
    {
        if (RunIdQuery.HasValue || LaunchPlanIdQuery.HasValue)
        {
            detailTab = DetailTabRuns;
        }

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
        runtimeStateOverview = await RuntimeStateOverviewService.GetOverviewAsync(
            definitions.Select(definition => definition.Id).ToList(),
            ProjectId);
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
        managerAgentOptions = await ProcessesService.ListManagerAgentOptionsAsync();
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
            if (ShouldLoadRuntimePaneData())
            {
                await LoadRuntimePaneDataAsync();
            }
            else
            {
                ClearRuntimePaneData();
            }
        }
        else
        {
            ClearRuntimePaneData();
        }

        RefreshCanvasSurface();
        if (string.Equals(detailTab, DetailTabManagerChat, StringComparison.Ordinal))
        {
            await LoadManagerChatAsync();
        }

        UpdateRuntimeRefreshLoop();
        StateHasChanged();
    }

    private async Task LoadRuntimePaneDataAsync(CancellationToken cancellationToken = default)
    {
        if (!selectedProcessId.HasValue)
        {
            ClearRuntimePaneData();
            return;
        }

        if (ShouldLoadLaunchPlanData())
        {
            launchPlans = await ProcessesService.ListLaunchPlansAsync(selectedProcessId, ProjectId, cancellationToken);
            selectedLaunchPlanId = ResolveSelectedLaunchPlanId();
            await LoadLaunchPlanDetailsAsync();
        }
        else
        {
            launchPlans = [];
            selectedLaunchPlanId = null;
            selectedLaunchPlan = null;
        }

        runs = await ProcessesService.ListRunsAsync(selectedProcessId, ProjectId, cancellationToken);
        activeRunSummaries = string.Equals(detailTab, DetailTabRuns, StringComparison.Ordinal)
            ? await RunDetailsLoader.LoadActiveRunSummariesAsync(runs, cancellationToken)
            : [];

        var nextSelectedRunId = ResolveSelectedRunId();
        if (nextSelectedRunId != selectedRunId)
        {
            selectedCanvasNodeId = null;
            ResetRuntimeCanvasState();
        }

        selectedRunId = nextSelectedRunId;
        if (ShouldLoadSelectedRunDetails())
        {
            await LoadRunDetailsAsync(cancellationToken);
            return;
        }

        ClearRunDetails();
    }

    private void ClearRuntimePaneData()
    {
        launchPlans = [];
        selectedLaunchPlanId = null;
        selectedLaunchPlan = null;
        runs = [];
        activeRunSummaries = [];
        selectedRunId = null;
        ClearRunDetails();
    }

    private bool ShouldLoadRuntimePaneData()
    {
        return selectedProcessId.HasValue &&
            (string.Equals(detailTab, DetailTabRuns, StringComparison.Ordinal) ||
                string.Equals(detailTab, DetailTabAnalytics, StringComparison.Ordinal) ||
                RunIdQuery.HasValue ||
                LaunchPlanIdQuery.HasValue);
    }

    private bool ShouldLoadLaunchPlanData()
    {
        return selectedProcessId.HasValue &&
            (string.Equals(detailTab, DetailTabRuns, StringComparison.Ordinal) ||
                LaunchPlanIdQuery.HasValue);
    }

    private bool ShouldLoadSelectedRunDetails()
    {
        return selectedRunId.HasValue &&
            (string.Equals(detailTab, DetailTabRuns, StringComparison.Ordinal) ||
                RunIdQuery.HasValue ||
                LaunchPlanIdQuery.HasValue);
    }

    private async Task LoadRunDetailsAsync(CancellationToken cancellationToken = default)
    {
        if (!selectedRunId.HasValue)
        {
            ClearRunDetails();
            return;
        }

        var runDetails = await RunDetailsLoader.LoadAsync(selectedRunId.Value, cancellationToken);
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

    private void ClearRunDetails()
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
        operatorManagerDirective = string.Empty;
        operatorEscalationResolution = string.Empty;
        operatorApprovalDecisionSummary = string.Empty;
        assignmentAllowsDirectMessaging = true;
        directMessageSourceRoleRequirementId = null;
        directMessageTargetRoleRequirementId = null;
        directMessageBody = string.Empty;
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

        if (string.Equals(detailTab, DetailTabAnalytics, StringComparison.Ordinal) &&
            selectedRunId.HasValue &&
            runs.Any(run => run.Id == selectedRunId.Value))
        {
            return selectedRunId.Value;
        }

        if (!string.Equals(detailTab, DetailTabRuns, StringComparison.Ordinal))
        {
            return null;
        }

        if (selectedRunId.HasValue && runs.Any(run => run.Id == selectedRunId.Value))
        {
            return selectedRunId.Value;
        }

        return runs.FirstOrDefault()?.Id;
    }
}
