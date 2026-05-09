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
        try
        {
            await LoadWorkspaceAsync(componentLifetimeCts.Token);
        }
        catch (OperationCanceledException) when (componentLifetimeCts.IsCancellationRequested)
        {
        }
    }

    public void Dispose()
    {
        componentLifetimeCts.Cancel();
        AgentWorkspaceService.ExecutionUpdated -= HandleManagerChatExecutionUpdated;
        StopRuntimeRefreshLoop();
        CancelPendingDefinitionCanvasPersistence();
    }

    public async ValueTask DisposeAsync()
    {
        componentLifetimeCts.Cancel();
        AgentWorkspaceService.ExecutionUpdated -= HandleManagerChatExecutionUpdated;
        await StopRuntimeRefreshLoopAsync();
        await QuiesceDefinitionCanvasPersistenceAsync(DefinitionCanvasPersistenceQuiescenceMode.CancelPendingChanges);
        componentLifetimeCts.Dispose();
    }

    private async Task LoadWorkspaceAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (RunIdQuery.HasValue || LaunchPlanIdQuery.HasValue)
        {
            detailTab = DetailTabRuns;
        }

        if (ProjectId.HasValue)
        {
            var project = await ProjectsService.GetAsync(ProjectId.Value, cancellationToken);
            projectName = project.Name;
        }
        else
        {
            projectName = string.Empty;
        }

        definitions = await ProcessesService.ListDefinitionsAsync(ProjectId, cancellationToken);
        await LoadRuntimeOverviewAsync(cancellationToken);
        var nextSelectedProcessId = ResolveSelectedProcessId();
        if (nextSelectedProcessId != selectedProcessId)
        {
            await QuiesceDefinitionCanvasPersistenceAsync(DefinitionCanvasPersistenceQuiescenceMode.FlushPendingChanges);
            cancellationToken.ThrowIfCancellationRequested();
            selectedCanvasNodeId = null;
            ResetDefinitionCanvasState();
            ResetRuntimeCanvasState();
        }

        selectedProcessId = nextSelectedProcessId;
        editor = await ProcessesService.GetEditorAsync(selectedProcessId, ProjectId, cancellationToken);
        executorOptions = await ProcessesService.ListExecutorOptionsAsync(cancellationToken);
        managerAgentOptions = await ProcessesService.ListManagerAgentOptionsAsync(cancellationToken);
        analytics = await ProcessesService.GetAnalyticsAsync(selectedProcessId, ProjectId, cancellationToken);
        improvements = await ProcessesService.ListImprovementsAsync(selectedProcessId, cancellationToken);

        if (ProjectId.HasValue)
        {
            partyOptions = await ProcessesService.ListPartyOptionsAsync(ProjectId.Value, cancellationToken);
        }
        else
        {
            partyOptions = [];
        }

        if (selectedProcessId.HasValue)
        {
            if (ShouldLoadRuntimePaneData())
            {
                await LoadRuntimePaneDataAsync(cancellationToken);
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
            await LoadManagerChatAsync(cancellationToken);
        }

        cancellationToken.ThrowIfCancellationRequested();
        UpdateRuntimeRefreshLoop();
        StateHasChanged();
    }

    private async Task LoadRuntimePaneDataAsync(CancellationToken cancellationToken = default)
    {
        await LoadRuntimePaneDataAsync(forceRefresh: false, cancellationToken);
    }

    private async Task LoadRuntimePaneDataAsync(bool forceRefresh, CancellationToken cancellationToken = default)
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
            await LoadLaunchPlanDetailsAsync(cancellationToken);
        }
        else
        {
            launchPlans = [];
            selectedLaunchPlanId = null;
            selectedLaunchPlan = null;
        }

        var observation = await ProcessObservationService.GetDashboardSnapshotAsync(
            new ProcessObservationDashboardQuery(
                ProjectId,
                definitions.Select(definition => definition.Id).ToList(),
                selectedProcessId,
                IncludeRuns: true,
                IncludeActiveRunSummaries: string.Equals(detailTab, DetailTabRuns, StringComparison.Ordinal),
                IncludeAnalytics: string.Equals(detailTab, DetailTabAnalytics, StringComparison.Ordinal),
                ForceRefresh: forceRefresh),
            cancellationToken);
        runtimeStateOverview = observation.RuntimeStateOverview;
        runs = observation.Runs;
        activeRunSummaries = observation.ActiveRunSummaries;
        ObservationDashboardState.SetDashboardSnapshot(observation);
        if (observation.Analytics is not null)
        {
            analytics = observation.Analytics;
        }

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

    private async Task LoadRuntimeOverviewAsync(
        CancellationToken cancellationToken = default,
        bool forceRefresh = false)
    {
        var observation = await ProcessObservationService.GetDashboardSnapshotAsync(
            new ProcessObservationDashboardQuery(
                ProjectId,
                definitions.Select(definition => definition.Id).ToList(),
                selectedProcessId,
                ForceRefresh: forceRefresh),
            cancellationToken);
        runtimeStateOverview = observation.RuntimeStateOverview;
        ObservationDashboardState.SetDashboardSnapshot(observation);
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

        var runSnapshot = await ProcessObservationService.GetRunSnapshotAsync(
            new ProcessRunObservationQuery(selectedRunId.Value, ProjectId),
            cancellationToken);
        ApplyRunDetails(runSnapshot.Details);
    }

    private void ApplyRunDetails(ProcessWorkspaceRunDetails runDetails)
    {
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
