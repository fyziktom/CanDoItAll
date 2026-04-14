using System.Text.Json;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Components.CanvasLib;
using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;
using Microsoft.AspNetCore.Components;

namespace CanDoItAll.Modules.Processes;

public partial class ProcessWorkspace : ComponentBase, IDisposable, IAsyncDisposable
{
    private const string DefinitionCanvasSelectTool = "authoring";
    private const string DefinitionCanvasDeleteTool = "delete";
    private const string CompactHelpPopoverRootClass = "pf-help-popover flex items-center";
    private const string CompactHelpPopoverToggleClass = "pf-help-popover__toggle pf-help-popover__toggle--compact";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private static readonly IReadOnlyList<SecondaryTabItem> DetailTabs =
    [
        new("definition", "Definition", Description: "Identity, governance, and publication controls."),
        new("roles", "Roles", Description: "Role-first staffing semantics and executor intent."),
        new("steps", "Steps", Description: "Typed workflow steps, bindings, artifacts, and authoring canvas."),
        new("runs", "Runs", Description: "Runtime state, assignments, work briefs, and evidence capture."),
        new("analytics", "Analytics", Description: "Economics, conformance, capability gaps, and improvement signals."),
        new("exchange", "Exchange", Description: "Import, export, and future executor-registry seam review.")
    ];

    [Inject]
    private ProcessesService ProcessesService { get; set; } = default!;

    [Inject]
    private ProcessDevelopmentSeedService SeedService { get; set; } = default!;

    [Inject]
    private ProcessCanvasSurfaceFactory CanvasSurfaceFactory { get; set; } = default!;

    [Inject]
    private ProcessTemplateCatalogService ProcessTemplateCatalogService { get; set; } = default!;

    [Inject]
    private ProjectsService ProjectsService { get; set; } = default!;

    [Inject]
    private ProcessWorkspaceRunDetailsLoader RunDetailsLoader { get; set; } = default!;

    [Parameter]
    public Guid? ProjectId { get; set; }

    [SupplyParameterFromQuery(Name = "processId")]
    private Guid? ProcessIdQuery { get; set; }

    [SupplyParameterFromQuery(Name = "runId")]
    private Guid? RunIdQuery { get; set; }

    private IReadOnlyList<ProcessDefinitionListItem> definitions = [];
    private IReadOnlyList<ProcessRunListItem> runs = [];
    private IReadOnlyList<ProcessStepRunViewModel> stepRuns = [];
    private IReadOnlyList<ProcessDecisionViewModel> decisions = [];
    private IReadOnlyList<ProcessArtifactViewModel> artifacts = [];
    private IReadOnlyList<ProcessRunAssignmentViewModel> assignments = [];
    private IReadOnlyList<ProcessWorkBriefViewModel> workBriefs = [];
    private IReadOnlyList<ProcessConformanceObservationViewModel> conformanceObservations = [];
    private IReadOnlyList<ProcessImprovementViewModel> improvements = [];
    private IReadOnlyList<ProcessExecutorRegistryOption> executorOptions = [];
    private IReadOnlyList<ProjectPartyOption> partyOptions = [];

    private ProcessAnalyticsSummary analytics = new(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
    private ProcessDefinitionEditorModel editor = new();
    private CanvasWorkbenchSurface? canvasSurface;
    private CanvasWorkbenchUiState definitionCanvasUiState = CreateDefaultDefinitionCanvasUiState();
    private CanvasWorkbenchUiState runtimeCanvasUiState = CreateDefaultRuntimeCanvasUiState();
    private string definitionCanvasTool = DefinitionCanvasSelectTool;

    private Guid? selectedProcessId;
    private Guid? selectedRunId;
    private Guid? selectedAssignmentId;
    private string detailTab = "definition";
    private string definitionSearch = string.Empty;
    private string runNameDraft = string.Empty;
    private ProcessOperatingMode runOperatingMode = ProcessOperatingMode.AssistedExecution;
    private Guid? artifactStepRunId;
    private string artifactTitle = string.Empty;
    private ProcessArtifactKind artifactKind = ProcessArtifactKind.Evidence;
    private ProcessArtifactTrustStatus artifactTrustStatus = ProcessArtifactTrustStatus.ReviewRequired;
    private ProcessSensitivityLevel artifactSensitivityLevel = ProcessSensitivityLevel.Internal;
    private string artifactProvenance = string.Empty;
    private string artifactAllowedUsage = string.Empty;
    private string artifactReview = string.Empty;
    private Guid? assignmentPartyId;
    private string assignmentDisplayName = string.Empty;
    private string assignmentExecutorKind = "person";
    private string assignmentBindingReason = string.Empty;
    private bool assignmentIsFallback;
    private Dictionary<Guid, Guid?> runtimeBranchOutcomeSelections = [];
    private string exportJson = string.Empty;
    private string importJson = string.Empty;
    private string projectName = string.Empty;
    private string message = string.Empty;
    private bool isError;
    private bool hasLoadedParameters;
    private Guid? loadedProjectId;
    private Guid? loadedProcessQueryId;
    private Guid? loadedRunQueryId;

    private IReadOnlyList<ProcessDefinitionListItem> FilteredDefinitions => definitions
        .Where(definition =>
            string.IsNullOrWhiteSpace(definitionSearch) ||
            definition.Name.Contains(definitionSearch, StringComparison.OrdinalIgnoreCase) ||
            definition.Summary.Contains(definitionSearch, StringComparison.OrdinalIgnoreCase) ||
            definition.ValueStatement.Contains(definitionSearch, StringComparison.OrdinalIgnoreCase))
        .OrderByDescending(definition => definition.UpdatedAtUtc)
        .ToList();

    private ProcessDefinitionListItem? SelectedDefinitionSummary
        => selectedProcessId.HasValue
            ? definitions.FirstOrDefault(definition => definition.Id == selectedProcessId.Value)
            : null;

    private ProcessRunListItem? SelectedRun
        => selectedRunId.HasValue
            ? runs.FirstOrDefault(run => run.Id == selectedRunId.Value)
            : null;

    private ProcessRunAssignmentViewModel? SelectedAssignment
        => selectedAssignmentId.HasValue
            ? assignments.FirstOrDefault(item => item.Id == selectedAssignmentId.Value)
            : null;

    private string PageEyebrow
        => ProjectId.HasValue
            ? "Project processes"
            : "Processes";

    private string PageTitleText
        => ProjectId.HasValue && !string.IsNullOrWhiteSpace(projectName)
            ? $"{projectName} processes"
            : "Process management";

    private string EditorTitle
        => string.IsNullOrWhiteSpace(editor.Name)
            ? "New process definition"
            : editor.Name;

    private string SelectedDefinitionTone
        => ResolveDefinitionStatusTone(editor.Status);

    private int SelectedDetailTabIndex
        => ResolveDetailTabIndex(detailTab);

    protected override async Task OnParametersSetAsync()
    {
        if (hasLoadedParameters &&
            loadedProjectId == ProjectId &&
            loadedProcessQueryId == ProcessIdQuery &&
            loadedRunQueryId == RunIdQuery)
        {
            return;
        }

        hasLoadedParameters = true;
        loadedProjectId = ProjectId;
        loadedProcessQueryId = ProcessIdQuery;
        loadedRunQueryId = RunIdQuery;
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
            runs = await ProcessesService.ListRunsAsync(selectedProcessId, ProjectId);
        }
        else
        {
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
            selectedAssignmentId = null;
            artifactStepRunId = null;
            return;
        }

        var runDetails = await RunDetailsLoader.LoadAsync(selectedRunId.Value);
        stepRuns = runDetails.StepRuns;
        decisions = runDetails.Decisions;
        artifacts = runDetails.Artifacts;
        assignments = runDetails.Assignments;
        workBriefs = runDetails.WorkBriefs;
        conformanceObservations = runDetails.ConformanceObservations;
        var refreshedRuntimeBranchSelections = new Dictionary<Guid, Guid?>();
        foreach (var stepRun in stepRuns) {
            runtimeBranchOutcomeSelections.TryGetValue(stepRun.Id, out var selectedBranchOutcomeId);
            if (selectedBranchOutcomeId.HasValue &&
                stepRun.AvailableBranchOutcomes.All(item => item.Id != selectedBranchOutcomeId.Value)) {
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

    private void RefreshCanvasSurface()
    {
        NormalizeEditorForAuthoring();
        canvasSurface = detailTab == "runs" && SelectedRun is not null
            ? CanvasSurfaceFactory.BuildRunSurface(SelectedRun, stepRuns, selectedCanvasNodeId)
            : CanvasSurfaceFactory.BuildDefinitionSurface(editor, selectedCanvasNodeId, definitionCanvasTool);

        var uiState = BuildCanvasUiState(canvasSurface, ResolveStoredCanvasUiState());
        canvasSurface.UiState = uiState;
        StoreCanvasUiState(uiState);

        if (string.Equals(selectedCanvasNodeId, NoCanvasSelection, StringComparison.Ordinal))
        {
            return;
        }

        var synchronizedSelection = uiState.SelectedNodeIds.FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(synchronizedSelection))
        {
            selectedCanvasNodeId = synchronizedSelection;
            return;
        }

        if (selectedCanvasNodeId is not null)
        {
            selectedCanvasNodeId = null;
        }
    }

    private void NormalizeEditorForAuthoring()
    {
        ProcessCanvasBranching.NormalizeDefinitionEditor(editor);
    }

    private static void NormalizeStepDraftForAuthoring(ProcessStepEditorModel step)
    {
        ProcessCanvasBranching.NormalizeStepDraft(step);
    }

    private CanvasWorkbenchUiState ResolveStoredCanvasUiState()
        => IsRuntimeCanvasActive
            ? runtimeCanvasUiState
            : definitionCanvasUiState;

    private void StoreCanvasUiState(CanvasWorkbenchUiState uiState)
    {
        var storedState = CloneCanvasUiState(uiState);
        if (IsRuntimeCanvasActive)
        {
            runtimeCanvasUiState = storedState;
        }
        else
        {
            definitionCanvasUiState = storedState;
        }
    }

    private CanvasWorkbenchUiState BuildCanvasUiState(CanvasWorkbenchSurface surface, CanvasWorkbenchUiState storedUiState)
    {
        ArgumentNullException.ThrowIfNull(surface);
        ArgumentNullException.ThrowIfNull(storedUiState);

        var uiState = CloneCanvasUiState(storedUiState);
        var availableNodeIds = surface.Nodes
            .Select(node => node.Id)
            .ToHashSet(StringComparer.Ordinal);

        if (string.Equals(selectedCanvasNodeId, NoCanvasSelection, StringComparison.Ordinal))
        {
            uiState.SelectedNodeIds = [];
        }
        else if (!string.IsNullOrWhiteSpace(selectedCanvasNodeId) && availableNodeIds.Contains(selectedCanvasNodeId))
        {
            uiState.SelectedNodeIds = [selectedCanvasNodeId];
        }
        else
        {
            uiState.SelectedNodeIds = uiState.SelectedNodeIds
                .Where(availableNodeIds.Contains)
                .Distinct(StringComparer.Ordinal)
                .ToList();

            if (uiState.SelectedNodeIds.Count == 0)
            {
                uiState.SelectedNodeIds = surface.UiState.SelectedNodeIds
                    .Where(availableNodeIds.Contains)
                    .Distinct(StringComparer.Ordinal)
                    .ToList();
            }
        }

        if (string.IsNullOrWhiteSpace(uiState.ActiveInspectorTab))
        {
            uiState.ActiveInspectorTab = surface.UiState.ActiveInspectorTab;
        }

        return uiState;
    }

    private void ResetDefinitionCanvasState()
    {
        definitionCanvasTool = DefinitionCanvasSelectTool;
        definitionCanvasUiState = CreateDefaultDefinitionCanvasUiState();
    }

    private void ResetRuntimeCanvasState()
    {
        runtimeCanvasUiState = CreateDefaultRuntimeCanvasUiState();
    }

    private static CanvasWorkbenchUiState CreateDefaultDefinitionCanvasUiState()
        => new()
        {
            ActiveInspectorTab = "definition"
        };

    private Task SelectDefinitionCanvasToolAsync()
    {
        SetDefinitionCanvasTool(DefinitionCanvasSelectTool);
        return Task.CompletedTask;
    }

    private Task DeleteDefinitionCanvasToolAsync()
    {
        SetDefinitionCanvasTool(DefinitionCanvasDeleteTool);
        return Task.CompletedTask;
    }

    private void SetDefinitionCanvasTool(string tool)
    {
        definitionCanvasTool = string.Equals(tool, DefinitionCanvasDeleteTool, StringComparison.Ordinal)
            ? DefinitionCanvasDeleteTool
            : DefinitionCanvasSelectTool;
        if (IsDefinitionCanvasActive)
        {
            RefreshCanvasSurface();
        }
    }

    private static CanvasWorkbenchUiState CreateDefaultRuntimeCanvasUiState()
        => new()
        {
            ActiveInspectorTab = "runtime"
        };

    private static CanvasWorkbenchUiState CloneCanvasUiState(CanvasWorkbenchUiState uiState)
        => CanvasWorkbenchUiState.Parse(uiState.ToJson());

    private static int ResolveDetailTabIndex(string key)
    {
        for (var index = 0; index < DetailTabs.Count; index++)
        {
            if (string.Equals(DetailTabs[index].Key, key, StringComparison.Ordinal))
            {
                return index;
            }
        }

        return 0;
    }

    private static string ResolveDetailTabKey(int index)
    {
        if (index >= 0 && index < DetailTabs.Count)
        {
            return DetailTabs[index].Key;
        }

        return DetailTabs[0].Key;
    }

    private void SetMessage(string value)
    {
        message = value;
        isError = false;
    }

    private void SetError(string value)
    {
        message = value;
        isError = true;
    }

    private void SetError(IEnumerable<Error> errors)
    {
        SetError(string.Join(" ", errors.Select(error => error.Message)));
    }

    private void ClearMessage()
    {
        message = string.Empty;
        isError = false;
    }
}
