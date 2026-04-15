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
        CancelPendingDefinitionCanvasPersistence();
    }

    public async ValueTask DisposeAsync()
    {
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
        }
        else
        {
            launchPlans = [];
            selectedLaunchPlanId = null;
            selectedLaunchPlan = null;
            runs = [];
        }

        var nextSelectedRunId = ResolveSelectedRunId();
        if (nextSelectedRunId != selectedRunId)
        {
            selectedCanvasNodeId = null;
            ResetRuntimeCanvasState();
        }

        selectedRunId = nextSelectedRunId;
        await LoadRunDetailsAsync();
        RefreshCanvasSurface();
        StateHasChanged();
    }

    private async Task LoadRunDetailsAsync()
    {
        if (!selectedRunId.HasValue)
        {
            stepRuns = [];
            decisions = [];
            artifacts = [];
            assignments = [];
            workBriefs = [];
            conformanceObservations = [];
            executionRuns = [];
            directMessageThreads = [];
            selectedAssignmentId = null;
            artifactStepRunId = null;
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
        assignments = runDetails.Assignments;
        workBriefs = runDetails.WorkBriefs;
        conformanceObservations = runDetails.ConformanceObservations;
        executionRuns = runDetails.ExecutionRuns;
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
