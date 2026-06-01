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
        _ = CancelDeferredWorkspaceDetailLoad();
        AgentWorkspaceService.ExecutionUpdated -= HandleManagerChatExecutionUpdated;
        StopRuntimeRefreshLoop();
        CancelPendingDefinitionCanvasPersistence();
    }

    public async ValueTask DisposeAsync()
    {
        componentLifetimeCts.Cancel();
        var deferredWorkspaceLoad = CancelDeferredWorkspaceDetailLoad();
        AgentWorkspaceService.ExecutionUpdated -= HandleManagerChatExecutionUpdated;
        await StopRuntimeRefreshLoopAsync();
        if (deferredWorkspaceLoad is not null)
        {
            try
            {
                await deferredWorkspaceLoad;
            }
            catch (OperationCanceledException)
            {
            }
        }

        await QuiesceDefinitionCanvasPersistenceAsync(DefinitionCanvasPersistenceQuiescenceMode.CancelPendingChanges);
        componentLifetimeCts.Dispose();
    }

    private async Task LoadWorkspaceAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _ = CancelDeferredWorkspaceDetailLoad();
        if (RunIdQuery.HasValue || LaunchPlanIdQuery.HasValue)
        {
            detailTab = DetailTabRuns;
        }

        var projectNameTask = LoadProjectNameAsync(cancellationToken);
        var definitionsTask = ProcessesService.ListDefinitionsAsync(ProjectId, cancellationToken);
        await Task.WhenAll(projectNameTask, definitionsTask);
        projectName = await projectNameTask;
        definitions = await definitionsTask;
        var nextSelectedProcessId = ResolveSelectedProcessId();
        if (nextSelectedProcessId != selectedProcessId)
        {
            await QuiesceDefinitionCanvasPersistenceAsync(DefinitionCanvasPersistenceQuiescenceMode.FlushPendingChanges);
            cancellationToken.ThrowIfCancellationRequested();
            selectedCanvasNodeId = null;
            ResetDefinitionCanvasState();
            ResetRuntimeCanvasState();
            ResetAnalyticsPaneData();
            ResetGraphPaneData();
        }

        selectedProcessId = nextSelectedProcessId;
        if (analyticsLoaded && (analyticsLoadedProcessId != selectedProcessId || analyticsLoadedProjectId != ProjectId))
        {
            ResetAnalyticsPaneData();
        }

        isEditorLoading = selectedProcessId.HasValue;
        ApplySelectedDefinitionEditorShell();
        RefreshCanvasSurface();
        StartDeferredWorkspaceDetailLoad(selectedProcessId, ProjectId, cancellationToken);

        UpdateRuntimeRefreshLoop();
        StateHasChanged();
    }

    private async Task<string> LoadProjectNameAsync(CancellationToken cancellationToken)
    {
        if (!ProjectId.HasValue)
        {
            return string.Empty;
        }

        var project = await ProjectsService.GetAsync(ProjectId.Value, cancellationToken);
        return project.Name;
    }

    private async Task LoadSelectedEditorAsync(
        Guid? processId,
        Guid? projectId,
        CancellationToken cancellationToken)
    {
        try
        {
            var loadedEditor = await ProcessesService.GetEditorAsync(processId, projectId, cancellationToken);
            if (selectedProcessId != processId || ProjectId != projectId)
            {
                return;
            }

            editor = loadedEditor;
        }
        finally
        {
            if (!cancellationToken.IsCancellationRequested &&
                selectedProcessId == processId &&
                ProjectId == projectId)
            {
                isEditorLoading = false;
            }
        }
    }

    private void StartDeferredWorkspaceDetailLoad(
        Guid? processId,
        Guid? projectId,
        CancellationToken cancellationToken)
    {
        var loadCts = CancellationTokenSource.CreateLinkedTokenSource(componentLifetimeCts.Token, cancellationToken);
        deferredWorkspaceLoadCts = loadCts;
        deferredWorkspaceLoadTask = InvokeAsync(async () =>
        {
            try
            {
                await LoadWorkspaceDetailsAsync(processId, projectId, loadCts.Token);
            }
            catch (OperationCanceledException) when (loadCts.IsCancellationRequested || componentLifetimeCts.IsCancellationRequested)
            {
            }
            catch (Exception exception)
            {
                if (!componentLifetimeCts.IsCancellationRequested)
                {
                    SetError($"Failed to load process workspace details. {exception.Message}");
                    StateHasChanged();
                }
            }
            finally
            {
                if (ReferenceEquals(deferredWorkspaceLoadCts, loadCts))
                {
                    deferredWorkspaceLoadCts = null;
                    deferredWorkspaceLoadTask = null;
                }

                loadCts.Dispose();
            }
        });
    }

    private Task? CancelDeferredWorkspaceDetailLoad()
    {
        var loadTask = deferredWorkspaceLoadTask;
        deferredWorkspaceLoadCts?.Cancel();
        deferredWorkspaceLoadCts = null;
        deferredWorkspaceLoadTask = null;
        return loadTask;
    }

    private async Task LoadWorkspaceDetailsAsync(
        Guid? processId,
        Guid? projectId,
        CancellationToken cancellationToken)
    {
        var definitionIds = definitions.Select(definition => definition.Id).ToList();
        await LoadSelectedEditorAsync(processId, projectId, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (!IsCurrentWorkspaceLoad(processId, projectId))
        {
            return;
        }

        RefreshCanvasSurface();
        StateHasChanged();

        await LoadRuntimeOverviewAsync(
            processId,
            projectId,
            definitionIds,
            cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (!IsCurrentWorkspaceLoad(processId, projectId))
        {
            return;
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
            await EnsureManagerAgentOptionsLoadedAsync(cancellationToken);
            await LoadManagerChatAsync(cancellationToken);
        }
        else
        {
            await EnsureManagerAgentOptionsLoadedAsync(cancellationToken);
        }

        cancellationToken.ThrowIfCancellationRequested();
        if (!IsCurrentWorkspaceLoad(processId, projectId))
        {
            return;
        }

        UpdateRuntimeRefreshLoop();
        StateHasChanged();
    }

    private bool IsCurrentWorkspaceLoad(Guid? processId, Guid? projectId)
    {
        return selectedProcessId == processId && ProjectId == projectId;
    }

    private void ApplySelectedDefinitionEditorShell()
    {
        if (SelectedDefinitionSummary is not { } definition)
        {
            editor = new ProcessDefinitionEditorModel
            {
                ProjectId = ProjectId
            };
            return;
        }

        editor = new ProcessDefinitionEditorModel
        {
            Id = definition.Id,
            ProjectId = definition.ProjectId,
            Name = definition.Name,
            Summary = definition.Summary,
            ValueStatement = definition.ValueStatement,
            Status = definition.Status
        };
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

        if (string.Equals(detailTab, DetailTabRuns, StringComparison.Ordinal) || RunIdQuery.HasValue || LaunchPlanIdQuery.HasValue)
        {
            await EnsureRuntimeOptionsLoadedAsync(cancellationToken);
        }

        if (ShouldLoadLaunchPlanData())
        {
            await LoadLaunchAgentTeamsAsync(cancellationToken);
            launchPlans = await ProcessesService.ListLaunchPlansAsync(selectedProcessId, ProjectId, cancellationToken);
            selectedLaunchPlanId = ResolveSelectedLaunchPlanId();
            await LoadLaunchPlanDetailsAsync(cancellationToken);
        }
        else
        {
            launchPlans = [];
            agentTeams = [];
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
            analyticsLoaded = true;
            analyticsLoadedProcessId = selectedProcessId;
            analyticsLoadedProjectId = ProjectId;
        }

        if (string.Equals(detailTab, DetailTabAnalytics, StringComparison.Ordinal))
        {
            if (!analyticsLoaded)
            {
                await EnsureAnalyticsLoadedAsync(cancellationToken: cancellationToken);
            }

            await EnsureImprovementsLoadedAsync(cancellationToken);
        }

        var nextSelectedRunId = ResolveSelectedRunId();
        if (nextSelectedRunId != selectedRunId)
        {
            selectedCanvasNodeId = null;
            ResetRuntimeCanvasState();
            ResetSelectedRunGraphData();
        }

        selectedRunId = nextSelectedRunId;
        if (ShouldLoadSelectedRunDetails())
        {
            await LoadRunDetailsAsync(cancellationToken);
            return;
        }

        ClearRunDetails();
    }

    private async Task EnsureRuntimeOptionsLoadedAsync(CancellationToken cancellationToken = default)
    {
        await EnsureExecutorOptionsLoadedAsync(cancellationToken);
        await EnsureWorkflowOptionsLoadedAsync(cancellationToken);
        await EnsurePartyOptionsLoadedAsync(cancellationToken);
    }

    private async Task EnsureExecutorOptionsLoadedAsync(CancellationToken cancellationToken = default)
    {
        if (executorOptionsLoaded)
        {
            return;
        }

        executorOptions = await ProcessesService.ListExecutorOptionsAsync(cancellationToken);
        executorOptionsLoaded = true;
    }

    private async Task EnsureWorkflowOptionsLoadedAsync(CancellationToken cancellationToken = default)
    {
        if (workflowOptionsLoaded)
        {
            return;
        }

        workflowOptions = await ProcessesService.ListWorkflowDefinitionOptionsAsync(cancellationToken);
        workflowOptionsLoaded = true;
    }

    private async Task EnsureManagerAgentOptionsLoadedAsync(CancellationToken cancellationToken = default)
    {
        if (managerAgentOptionsLoaded)
        {
            return;
        }

        managerAgentOptions = await ProcessesService.ListManagerAgentOptionsAsync(cancellationToken);
        managerAgentOptionsLoaded = true;
    }

    private async Task EnsurePartyOptionsLoadedAsync(CancellationToken cancellationToken = default)
    {
        if (!ProjectId.HasValue)
        {
            partyOptions = [];
            partyOptionsLoadedProjectId = null;
            return;
        }

        if (partyOptionsLoadedProjectId == ProjectId.Value)
        {
            return;
        }

        partyOptions = await ProcessesService.ListPartyOptionsAsync(ProjectId.Value, cancellationToken);
        partyOptionsLoadedProjectId = ProjectId.Value;
    }

    private async Task EnsureAnalyticsLoadedAsync(
        bool forceRefresh = false,
        CancellationToken cancellationToken = default)
    {
        if (!forceRefresh &&
            analyticsLoaded &&
            analyticsLoadedProcessId == selectedProcessId &&
            analyticsLoadedProjectId == ProjectId)
        {
            return;
        }

        analytics = await ProcessesService.GetAnalyticsAsync(selectedProcessId, ProjectId, cancellationToken);
        analyticsLoaded = true;
        analyticsLoadedProcessId = selectedProcessId;
        analyticsLoadedProjectId = ProjectId;
    }

    private async Task EnsureImprovementsLoadedAsync(CancellationToken cancellationToken = default)
    {
        if (improvementsLoaded && improvementsLoadedProcessId == selectedProcessId)
        {
            return;
        }

        improvements = await ProcessesService.ListImprovementsAsync(selectedProcessId, cancellationToken);
        improvementsLoaded = true;
        improvementsLoadedProcessId = selectedProcessId;
    }

    private void ResetAnalyticsPaneData()
    {
        analytics = new ProcessAnalyticsSummary(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
        improvements = [];
        analyticsLoaded = false;
        analyticsLoadedProcessId = null;
        analyticsLoadedProjectId = null;
        improvementsLoaded = false;
        improvementsLoadedProcessId = null;
    }

    private async Task LoadRuntimeOverviewAsync(
        Guid? processId,
        Guid? projectId,
        IReadOnlyCollection<Guid> definitionIds,
        CancellationToken cancellationToken = default,
        bool forceRefresh = false)
    {
        var observation = await ProcessObservationService.GetDashboardSnapshotAsync(
            new ProcessObservationDashboardQuery(
                projectId,
                definitionIds,
                processId,
                ForceRefresh: forceRefresh),
            cancellationToken);
        if (!IsCurrentWorkspaceLoad(processId, projectId))
        {
            return;
        }

        runtimeStateOverview = observation.RuntimeStateOverview;
        ObservationDashboardState.SetDashboardSnapshot(observation);
    }

    private void ClearRuntimePaneData()
    {
        launchPlans = [];
        agentTeams = [];
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
        workflowRuns = runDetails.WorkflowRuns;
        processEscalations = runDetails.Escalations;
        operatorApprovals = runDetails.OperatorApprovals;
        attemptTimeline = runDetails.AttemptTimeline;
        invariantDiagnostics = runDetails.InvariantDiagnostics;
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
        workflowRuns = [];
        processEscalations = [];
        operatorApprovals = [];
        attemptTimeline = [];
        invariantDiagnostics = [];
        selectedRunHealth = ProcessRunHealthSummaryViewModel.Empty;
        directMessageThreads = [];
        ResetSelectedRunGraphData();
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
